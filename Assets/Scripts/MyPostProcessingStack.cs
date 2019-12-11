using System.Collections;
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
        EyeAdaptation = 0,
        ToneMappingSimple,
        ToneMappingLerp,
    }

    private static Mesh fullScreenTriangle;

    private static Material mainMat, toneMappingMat, chromaticAberrationMat;

    private static int tempTexID = Shader.PropertyToID("tempTex");
    private static int temp1TexID = Shader.PropertyToID("temp1Tex");
    private static int resolved1TexID = Shader.PropertyToID("_MyPostProcessingStackResolved1Tex");
    private static int resolved2TexID = Shader.PropertyToID("_MyPostProcessingStackResolved2Tex");


    private static int mainTexID = Shader.PropertyToID("_MainTex");
    private static int depthID = Shader.PropertyToID("_DepthTex");

    private static int avgLuminanceTexRTID = Shader.PropertyToID("avgLuminanceTexRT");
    private static int eyeAdaptationEndRT = Shader.PropertyToID("eyeAdaptationEndRT");
    private static int eyeAdaptationSpeedFactorID = Shader.PropertyToID("eyeAdaptationSpeedFactor");
    private static int previousAvgLuminanceTexID = Shader.PropertyToID("_PreviousAvgLuminanceTex");
    private static int currentAvgLuminanceTexID = Shader.PropertyToID("_CurrentAvgLuminanceTex");

    private static int luminClampID = Shader.PropertyToID("luminClamp");
    private static int curveABCID = Shader.PropertyToID("curveABC");
    private static int curveDEFID = Shader.PropertyToID("curveDEF");
    private static int customDataID = Shader.PropertyToID("customData");
    private static int hdrColorTexID = Shader.PropertyToID("_HDRColorTex");
    private static int avgLuminanceTexID = Shader.PropertyToID("_AvgLuminanceTex");

    private static int caCenterID = Shader.PropertyToID("caCenter");
    private static int caCustomDataID = Shader.PropertyToID("caCustomData");

    //-------------------------


    //深度处理
    [SerializeField] private bool depthStripes;

    //-------------------------


    //模糊强度
    [SerializeField, Range(0, 10)] private int blurStrength;


    //-------------------------

    //眼睛适应
    [Space(10f), Header("EyeAdaptation"), SerializeField]
    private bool eyeAdaptation;

    //眼睛适应速度 根据插值 正/负  用不同的 渐变速度
    [SerializeField] private Vector2 eyeAdaptationSpeed;

    //-------------------------

    //颜色映射
    [Space(10f), Header("Tonemapping"), SerializeField]
    private bool toneMapping;

    //颜色映射范围
    //[SerializeField, Range(1f, 100f)] private float toneMappingRange = 100f;

    //暂时只有一个颜色 (不支持LERP)  luminance 的 允许的最小值/最大值亮度
    [SerializeField] private Vector2 tmluminanceClamp = new Vector2(0f, 2f);

    //ToneMapU2Func曲线 ABC DEF 曲线参数
    [SerializeField] private Vector3 tmCurveABC = new Vector3(0.25f, 0.306f, 0.099f),
        tmcurveDEF = new Vector3(0.35f, 0.025f, 0.40f);

    //.x->某种“白标”或中间灰度  .y->u2分子乘数  .z->log/mul/exp指数
    [SerializeField] private Vector3 tmCustomData = new Vector3(0.245f, 1.50f, 0.5f);

    //-------------------------

    //色差偏移
    [Space(10f), Header("ChromaticAberration"), SerializeField]
    private bool chromaticAberration;

    //色差偏移 中心点
    [SerializeField] private Vector2 caCenter = new Vector2(0.5f,0.5f);

    //色差偏移 距离阀值
    [SerializeField] private float caCenterDistanceThreshold = 0.2f;

    //色差偏移 距离强度
    [SerializeField] private float caFA = 1.25f;

    //色差偏移 偏移强度
    [SerializeField] private float caIntensity = 30f;

    //色差偏移 偏移扰动尺寸
    [SerializeField] private float caDistortSize = 0.75f;


    private RenderTexture eyeAdaptationPreRT;


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

        chromaticAberrationMat = new Material(Shader.Find("Hidden/My Pipeline/ChromaticAberration"))
        {
            name = "My ChromaticAberration Material",
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
        cb.GetTemporaryRT(resolved1TexID, width, height, 0, FilterMode.Bilinear, format);
        cb.GetTemporaryRT(resolved2TexID, width, height, 0, FilterMode.Bilinear, format);


        int nowRTID = cameraColorID;

        //Blur
        if (blurStrength > 0)
        {
            int endRTID = nowRTID == cameraColorID || nowRTID == resolved2TexID ? resolved1TexID : resolved2TexID;
            Blur(cb, nowRTID, endRTID, width, height, format);
            nowRTID = endRTID;
        }

        //ToneMapping
        if (toneMapping)
        {
            if (!eyeAdaptation)
            {
                if (eyeAdaptationPreRT)
                {
                    DestroyImmediate(eyeAdaptationPreRT);
                }
            }

            int endRTID = nowRTID == cameraColorID || nowRTID == resolved2TexID ? resolved1TexID : resolved2TexID;
            ToneMapping(cb, nowRTID, endRTID, width, height, format);
            nowRTID = endRTID;
        }
        else
        {
            if (eyeAdaptationPreRT)
            {
                DestroyImmediate(eyeAdaptationPreRT);
            }
        }

        //Chromatic Aberration
        if (chromaticAberration)
        {
            int endRTID = nowRTID == cameraColorID || nowRTID == resolved2TexID ? resolved1TexID : resolved2TexID;
            ChromaticAberration(cb, nowRTID, endRTID, width, height, format);
            nowRTID = endRTID;
        }

        //MSAA
        if (samples > 1 && nowRTID != cameraColorID)
        {
            Blit(cb, nowRTID, cameraColorID);
            nowRTID = cameraColorID;
        }


        Blit(cb, nowRTID, BuiltinRenderTextureType.CameraTarget);

        cb.ReleaseTemporaryRT(resolved1TexID);
        cb.ReleaseTemporaryRT(resolved2TexID);
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
        , Material mat, int pass = 0)
    {
        cb.SetGlobalTexture(mainTexID, srcID);

        cb.SetRenderTarget(destID, RenderBufferLoadAction.DontCare,
            RenderBufferStoreAction.Store);

        cb.DrawMesh(fullScreenTriangle, Matrix4x4.identity, mat, 0, (int) pass);
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

    private void Blur(CommandBuffer cb, int srcID, int destID, int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Blur");

        if (blurStrength == 1)
        {
            Blit(cb, srcID, destID, MainPass.Blur);
            cb.EndSample("Blur");
            return;
        }

        cb.GetTemporaryRT(tempTexID, width, height, 0, FilterMode.Bilinear, format);

        int _tempID = srcID;
        int passesLeft = blurStrength;

        if (blurStrength > 2)
        {
            cb.GetTemporaryRT(temp1TexID, width, height, 0, FilterMode.Bilinear, format);
        }

        for (; passesLeft > 2; passesLeft -= 2)
        {
            Blit(cb, _tempID, tempTexID, MainPass.Blur);
            _tempID = temp1TexID;
            Blit(cb, tempTexID, _tempID, MainPass.Blur);
        }

        if (passesLeft > 1)
        {
            Blit(cb, _tempID, tempTexID, MainPass.Blur);
            Blit(cb, tempTexID, destID, MainPass.Blur);
        }
        else
        {
            Blit(cb, _tempID, destID, MainPass.Blur);
        }

        cb.ReleaseTemporaryRT(tempTexID);
        if (blurStrength > 2)
        {
            cb.ReleaseTemporaryRT(temp1TexID);
        }

        cb.EndSample("Blur");
    }

    private void ToneMapping(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID
        , int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Tone Mapping");

        //AvgLuminance==========================================

        cb.BeginSample("AvgLuminance");


        int max = Mathf.Max(width, height);

        int iterator = (int) (Mathf.Log(max, 2));


        cb.GetTemporaryRT(avgLuminanceTexRTID, 1, 1, 0, FilterMode.Bilinear, format);

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

            //int endID = ((iterator - 1) & 1) == 0 ? tempTexID : temp1TexID;
            int endID = (iterator & 1) == 0 ? temp1TexID : tempTexID;
            Blit(cb, endID, avgLuminanceTexRTID, MainPass.Luminance);
            //释放了 但是下面如果立马开辟一样的 有缓存显示BUG
            cb.ReleaseTemporaryRT(endID);
        }
        else
        {
            Blit(cb, srcID, avgLuminanceTexRTID, MainPass.Luminance);
        }

        cb.EndSample("AvgLuminance");


        //EyeAdaptation==========================================

        cb.BeginSample("EyeAdaptation");

        if (eyeAdaptation)
        {
            if (eyeAdaptationPreRT == null)
            {
                eyeAdaptationPreRT = new RenderTexture(1, 1, 0, format)
                {
                    name = "eyeAdaptationPreRT"
                };

                Blit(cb, avgLuminanceTexRTID, eyeAdaptationPreRT);
            }
            else
            {
                cb.GetTemporaryRT(eyeAdaptationEndRT, 1, 1, 0, FilterMode.Bilinear, format);


                cb.SetGlobalVector(eyeAdaptationSpeedFactorID, eyeAdaptationSpeed);
                cb.SetGlobalTexture(previousAvgLuminanceTexID, eyeAdaptationPreRT);
                cb.SetGlobalTexture(currentAvgLuminanceTexID, avgLuminanceTexRTID);


                Blit(cb, avgLuminanceTexRTID, eyeAdaptationEndRT, toneMappingMat, (int) ToneMappingEnum.EyeAdaptation);
                Blit(cb, eyeAdaptationEndRT, eyeAdaptationPreRT);

                cb.ReleaseTemporaryRT(eyeAdaptationEndRT);
            }
        }
        else
        {
            if (eyeAdaptationPreRT)
            {
                DestroyImmediate(eyeAdaptationPreRT);
            }
        }

        cb.EndSample("EyeAdaptation");


        //ToneMapping==========================================

        cb.BeginSample("ToneMapping");


        cb.SetGlobalVector(luminClampID, tmluminanceClamp);
        cb.SetGlobalVector(curveABCID, tmCurveABC);
        cb.SetGlobalVector(curveDEFID, tmcurveDEF);
        cb.SetGlobalVector(customDataID, tmCustomData);
        cb.SetGlobalTexture(hdrColorTexID, srcID);
        if (eyeAdaptation)
        {
            cb.SetGlobalTexture(avgLuminanceTexID, eyeAdaptationPreRT);
        }
        else
        {
            cb.SetGlobalTexture(avgLuminanceTexID, avgLuminanceTexRTID);
        }

        Blit(cb, srcID, destID, toneMappingMat, (int) ToneMappingEnum.ToneMappingSimple);

        cb.EndSample("ToneMapping");

        cb.ReleaseTemporaryRT(avgLuminanceTexRTID);

        cb.EndSample("Tone Mapping");
    }

    private void ChromaticAberration(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID
        , int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Chromatic Aberration");

        cb.SetGlobalVector(caCenterID, caCenter);
        cb.SetGlobalVector(caCustomDataID, new Vector4(caCenterDistanceThreshold, caFA, caIntensity, caDistortSize));

        Blit(cb, srcID, destID, chromaticAberrationMat);

        cb.EndSample("Chromatic Aberration");
    }
}