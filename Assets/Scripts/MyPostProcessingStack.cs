﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/My Post-Processing Stack")]
public class MyPostProcessingStack : ScriptableObject
{
    private enum MainPass
    {
        Copy = 0,
        Blur,
        DepthStripes,
        ToneMapping, //old don't use
        Luminance,
    }

    private enum ToneMappingEnum
    {
        Simple,
        Lerp,
    }

    private static Mesh fullScreenTriangle;

    private static Material mainMat, toneMappingMat;


    private static int mainTexID = Shader.PropertyToID("_MainTex");
    private static int tempTexID = Shader.PropertyToID("_MyPostProcessingStackTempTex");
    private static int temp1TexID = Shader.PropertyToID("_MyPostProcessingStackTemp1Tex");
    private static int depthID = Shader.PropertyToID("_DepthTex");
    private static int resolvedTexID = Shader.PropertyToID("_MyPostProcessingStackResolvedTex");

    private static int luminClampID = Shader.PropertyToID("luminClamp");
    private static int curveABCID = Shader.PropertyToID("curveABC");
    private static int curveDEFID = Shader.PropertyToID("curveDEF");
    private static int customDataID = Shader.PropertyToID("customData");
    private static int hdrColorTexID = Shader.PropertyToID("_HDRColorTex");
    private static int avgLuminanceTexID = Shader.PropertyToID("_AvgLuminanceTex");

    //模糊强度
    [SerializeField, Range(0, 10)] private int blurStrength;

    //深度处理
    [SerializeField] private bool depthStripes;

    //颜色映射
    [SerializeField] private bool toneMapping;

    //颜色映射范围
    //[SerializeField, Range(1f, 100f)] private float toneMappingRange = 100f;

    //暂时只有一个颜色 (不支持LERP)  luminance 的 允许的最小值/最大值亮度
    [Space(10f), Header("Tonemapping"), SerializeField]
    private Vector2 tmluminanceClamp = new Vector2(0f, 2f);

    //ToneMapU2Func曲线 ABC DEF 曲线参数
    [SerializeField] private Vector3 tmCurveABC = new Vector3(0.25f, 0.306f, 0.099f),
        tmcurveDEF = new Vector3(0.35f, 0.025f, 0.40f);

    //.x->某种“白标”或中间灰度  .y->u2分子乘数  .z->log/mul/exp指数
    [SerializeField] private Vector3 tmCustomData = new Vector3(0.245f,1.50f,0.5f);


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

        mainMat = new Material(Shader.Find("Hidden/My Pipeline/PostEffectStack"))
        {
            name = "My Post-Processing Stack Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        toneMappingMat = new Material(Shader.Find("Hidden/My Pipeline/Tonemapping"))
        {
            name = "My Tonemapping Material",
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
                    ToneMapping(cb, cameraColorID, resolvedTexID, width, height, format);
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
            ToneMapping(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget, width, height, format);
        }
        else
        {
            Blit(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget);
        }
    }

    private void Blit(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID,
        MainPass mainPass = MainPass.Copy)
    {
        cb.SetGlobalTexture(mainTexID, srcID);

        cb.SetRenderTarget(destID, RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);

        cb.DrawMesh(fullScreenTriangle, Matrix4x4.identity, mainMat, 0, (int) mainPass);
    }

    private void Blit(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID
        , Material mat, int pass = (int) MainPass.Copy)
    {
        cb.SetGlobalTexture(mainTexID, srcID);

        cb.SetRenderTarget(destID, RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);

        cb.DrawMesh(fullScreenTriangle, Matrix4x4.identity, mat, 0, (int) pass);
    }

    private void Blur(CommandBuffer cb, int cameraColorID, int width, int height)
    {
        cb.BeginSample("Blur");

        if (blurStrength == 1)
        {
            Blit(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget, MainPass.Blur);
            cb.EndSample("Blur");
            return;
        }

        cb.GetTemporaryRT(tempTexID, width, height, 0, FilterMode.Bilinear);
        int passesLeft;

        for (passesLeft = blurStrength; passesLeft > 2; passesLeft -= 2)
        {
            Blit(cb, cameraColorID, tempTexID, MainPass.Blur);
            Blit(cb, tempTexID, cameraColorID, MainPass.Blur);
        }

        if (passesLeft > 1)
        {
            Blit(cb, cameraColorID, tempTexID, MainPass.Blur);
            Blit(cb, tempTexID, BuiltinRenderTextureType.CameraTarget, MainPass.Blur);
        }
        else
        {
            Blit(cb, cameraColorID, BuiltinRenderTextureType.CameraTarget, MainPass.Blur);
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
        Blit(cb, cameraColorID, tempTexID, MainPass.DepthStripes);
        Blit(cb, tempTexID, cameraColorID);
        cb.ReleaseTemporaryRT(tempTexID);

        cb.EndSample("Depth Stripes");
    }

    private void ToneMapping(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID
        , int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Tone Mapping");

        int max = Mathf.Max(width, height);

        int iterator = (int) (Mathf.Log(max, 2));

        cb.GetTemporaryRT(avgLuminanceTexID, 1,1,0, FilterMode.Bilinear, format);

        if (iterator > 0)
        {
            for (int i = 0; i < iterator; i++)
            {
                width = Mathf.Max(1, width >> 1);
                height = Mathf.Max(1, height >> 1);

                if (i == 0)
                {
                    cb.GetTemporaryRT(tempTexID, width, height, 0, FilterMode.Bilinear, format);
                    Blit(cb, srcID, tempTexID);
                }
                else if ((i & 1) == 1)
                {
                    cb.GetTemporaryRT(temp1TexID, width, height, 0, FilterMode.Bilinear, format);
                    Blit(cb, tempTexID, temp1TexID);
                    cb.ReleaseTemporaryRT(tempTexID);
                }
                else
                {
                    cb.GetTemporaryRT(tempTexID, width, height, 0, FilterMode.Bilinear, format);
                    Blit(cb, temp1TexID, tempTexID);
                    cb.ReleaseTemporaryRT(temp1TexID);
                }
            }

            int endID = (iterator & 1) == 0 ? tempTexID : temp1TexID;
            Blit(cb, endID, avgLuminanceTexID, MainPass.Luminance);
            cb.ReleaseTemporaryRT(endID);
        }
        else
        {
            Blit(cb, srcID, avgLuminanceTexID, MainPass.Luminance);
        }



        cb.SetGlobalVector(luminClampID, tmluminanceClamp);
        cb.SetGlobalVector(curveABCID, tmCurveABC);
        cb.SetGlobalVector(curveDEFID, tmcurveDEF);
        cb.SetGlobalVector(customDataID, tmCustomData);
        cb.SetGlobalTexture(avgLuminanceTexID, avgLuminanceTexID);
        cb.SetGlobalTexture(hdrColorTexID, srcID);

        Blit(cb, srcID, destID, toneMappingMat, (int) ToneMappingEnum.Simple);

        cb.ReleaseTemporaryRT(avgLuminanceTexID);


        cb.EndSample("Tone Mapping");
    }
}