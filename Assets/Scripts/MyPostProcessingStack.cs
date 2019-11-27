using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/My Post-Processing Stack")]
public class MyPostProcessingStack : ScriptableObject
{
    private enum Pass
    {
        Copy = 0,
        Blur,
        DepthStripes,
        ToneMapping,
    }

    private static Mesh fullScreenTriangle;

    private static Material material;

    private static int mainTexID = Shader.PropertyToID("_MainTex");
    private static int tempTexID = Shader.PropertyToID("_MyPostProcessingStackTempTex");
    private static int depthID = Shader.PropertyToID("_DepthTex");
    private static int resolvedTexID = Shader.PropertyToID("_MyPostProcessingStackResolvedTex");

    //模糊强度
    [SerializeField, Range(0, 10)] private int blurStrength;

    //深度处理
    [SerializeField] private bool depthStripes;

    //颜色映射
    [SerializeField] private bool toneMapping;

    //颜色映射范围
    [SerializeField, Range(1f, 100f)] private float toneMappingRange = 100f;

    public bool NeedsDepth => depthStripes;

    private static void InitializeStatic()
    {
        if (fullScreenTriangle)
        {
            return;
        }

        fullScreenTriangle = new Mesh()
        {
            name = "My Post-Processing Stack Full-Screen Triangle",
            vertices = new Vector3[]
            {
                new Vector3(-1f, -1f, 0f),
                new Vector3(-1f, 3f, 0f),
                new Vector3(3f, -1f, 0f),
            },
            triangles = new int[] {0, 1, 2},
        };
        fullScreenTriangle.UploadMeshData(true);

        material = new Material(Shader.Find("Hidden/My Pipeline/PostEffectStack"))
        {
            name = "My Post-Processing Stack Material",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    public void RenderAfterOpaque(CommandBuffer cb, int cameraColorID, int cameraDepthID, int width, int height,
        int samples, RenderTextureFormat format)
    {
        InitializeStatic();

        if (depthStripes)
        {
            DepthStripes(cb, cameraColorID, cameraDepthID, width, height, format);
        }
    }

    public void RenderAfterTransparent(CommandBuffer cb, int cameraColorID, int cameraDepthID, int width, int height,
        int samples, RenderTextureFormat format)
    {
        if (blurStrength > 0)
        {
            if (toneMapping || samples > 1)
            {
                cb.GetTemporaryRT(resolvedTexID, width, height, 0, FilterMode.Bilinear, format);
                if (toneMapping)
                {
                    ToneMapping(cb, cameraColorID, resolvedTexID);
                }
                else
                {
                    Blit(cb, cameraColorID, resolvedTexID);
                }

                Blur(cb, resolvedTexID, width, height);
                cb.ReleaseTemporaryRT(resolvedTexID);
            }
            else
            {
                Blur(cb, cameraColorID, width, height);
            }
        }
        else if (toneMapping)
        {
            ToneMapping(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget);
        }
        else
        {
            Blit(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget);
        }
    }

    private void Blit(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID,
        Pass pass = Pass.Copy)
    {
        cb.SetGlobalTexture(mainTexID, srcID);

        cb.SetRenderTarget(destID, RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);

        cb.DrawMesh(fullScreenTriangle, Matrix4x4.identity, material, 0, (int) pass);
    }

    private void Blur(CommandBuffer cb, int cameraColorID, int width, int height)
    {
        cb.BeginSample("Blur");

        if (blurStrength == 1)
        {
            Blit(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget, Pass.Blur);
            cb.EndSample("Blur");
            return;
        }

        cb.GetTemporaryRT(tempTexID, width, height, 0, FilterMode.Bilinear);
        int passesLeft;

        for (passesLeft = blurStrength; passesLeft > 2; passesLeft -= 2)
        {
            Blit(cb, cameraColorID, tempTexID, Pass.Blur);
            Blit(cb, tempTexID, cameraColorID, Pass.Blur);
        }

        if (passesLeft > 1)
        {
            Blit(cb, cameraColorID, tempTexID, Pass.Blur);
            Blit(cb, tempTexID, BuiltinRenderTextureType.CameraTarget, Pass.Blur);
        }
        else
        {
            Blit(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget, Pass.Blur);
        }

        cb.ReleaseTemporaryRT(tempTexID);

        cb.EndSample("Blur");
    }

    private void DepthStripes(CommandBuffer cb, int cameraColorID, int cameraDepthID,
        int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Depth Stripes");

        cb.GetTemporaryRT(tempTexID, width, height, 0, FilterMode.Point, format);
        cb.SetGlobalTexture(depthID, cameraDepthID);
        Blit(cb, cameraColorID, tempTexID, Pass.DepthStripes);
        Blit(cb, tempTexID, cameraColorID);
        cb.ReleaseTemporaryRT(tempTexID);

        cb.EndSample("Depth Stripes");
    }

    private void ToneMapping(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID)
    {
        cb.BeginSample("Tone Mapping");

        cb.SetGlobalFloat("_ReinhardModifier", 1f / (toneMappingRange * toneMappingRange));
        Blit(cb, srcID, destID, Pass.ToneMapping);

        cb.EndSample("Tone Mapping");
    }
}