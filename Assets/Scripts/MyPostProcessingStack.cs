using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
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

    private enum VignetteEnum
    {
        VignetteSimple = 0,
        VignetteComplex,
    }

    private static Mesh fullScreenTriangle;

    private static Material mainMat, toneMappingMat, sharpenMat, drunkEffectMat, chromaticAberrationMat, vignetteMat;

    private static int tempTexID = Shader.PropertyToID("tempTex");
    private static int temp1TexID = Shader.PropertyToID("temp1Tex");
    private static int resolved1TexID = Shader.PropertyToID("_MyPostProcessingStackResolved1Tex");
    private static int resolved2TexID = Shader.PropertyToID("_MyPostProcessingStackResolved2Tex");

    private static int mainTexID = Shader.PropertyToID("_MainTex");
    private static int depthID = Shader.PropertyToID("_DepthTex");

    private static int avgLumaRTID = Shader.PropertyToID("avgLumaRT");
    private static int avgLumaBufferID = Shader.PropertyToID("avgLumaBuffer");
    private static int avgLumaHistDataID = Shader.PropertyToID("avgLumaHistData");
    private static int avgLumaCalcDataID = Shader.PropertyToID("avgLumaCalcData");


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

    private static int sharpenNearFarID = Shader.PropertyToID("sharpenNearFar");
    private static int sharpenDistLumScaleBiasID = Shader.PropertyToID("sharpenDistLumScaleBias");

    private static int drunkDataID = Shader.PropertyToID("drunkData");
    private static int drunkCenterID = Shader.PropertyToID("drunkCenter");

    private static int vignetteSimpleIntensityID = Shader.PropertyToID("vignetteSimpleIntensity");
    private static int vignetteSimpleThresholdID = Shader.PropertyToID("vignetteSimpleThreshold");
    private static int vignetteComplexIntensityID = Shader.PropertyToID("vignetteComplexIntensity");
    private static int vignetteComplexWeightsID = Shader.PropertyToID("vignetteComplexWeights");
    private static int vignetteComplexDarkColorID = Shader.PropertyToID("vignetteComplexDarkColor");
    private static int vignetteComplexMaskID = Shader.PropertyToID("_VignetteComplexMaskTex");

    private static int caCenterID = Shader.PropertyToID("caCenter");
    private static int caCustomDataID = Shader.PropertyToID("caCustomData");

    #region 深度处理

    //深度处理
    [SerializeField] private bool depthStripes = false;

    #endregion


    #region 模糊强度

    //模糊强度
    [SerializeField, Range(0, 10)] private int blurStrength = 0;

    #endregion

    #region 平均亮度

    [SerializeField, Space(10f), Header("AverageLuminance")]
    private bool avgLumiComputeShader = true;

    //平均亮度 天空盒lerp的t值
    [SerializeField, Range(0, 1f)] private float avgLumiSkyLerp = 0.5f;

    //平均亮度 天空盒lerp的b值 
    [SerializeField, Range(0, 1f)] private float avgLumiSkyValue = 0;

    #endregion

    #region 眼睛适应

    //眼睛适应
    [Space(10f), Header("EyeAdaptation"), SerializeField]
    private bool eyeAdaptation = false;

    //眼睛适应速度 根据插值 正/负  用不同的 渐变速度
    [SerializeField] private Vector2 eyeAdaptationSpeed = new Vector2(0.05f, 0.05f);

    #endregion

    #region 颜色映射

    //颜色映射
    [Space(10f), Header("Tonemapping"), SerializeField]
    private bool toneMapping = false;

    //颜色映射范围
    //[SerializeField, Range(1f, 100f)] private float toneMappingRange = 100f;

    //暂时只有一个颜色 (不支持LERP)  luminance 的 允许的最小值/最大值亮度
    [SerializeField] private Vector2 tmluminanceClamp = new Vector2(0f, 2f);

    //ToneMapU2Func曲线 ABC DEF 曲线参数
    [SerializeField] private Vector3 tmCurveABC = new Vector3(0.25f, 0.306f, 0.099f),
        tmCurveDEF = new Vector3(0.35f, 0.025f, 0.40f);

    //.x->某种“白标”或中间灰度  .y->u2分子乘数  .z->log/mul/exp指数
    [SerializeField] private Vector3 tmCustomData = new Vector3(0.245f, 1.50f, 0.5f);

    #endregion

    #region 锐化效果

    //锐化效果
    [Space(10f), Header("Sharpen"), SerializeField]
    private bool sharpen;

    //锐化效果 近/远强度
    [SerializeField] private float sharpenNear = 1, sharpenFar = 0;

    //锐化效果 锐化强度缩放/偏移
    [SerializeField] private float sharpenDistanceScale = 1, sharpenDistanceBias = 0;

    //锐化效果 锐化亮度缩放/偏移
    [SerializeField] private float sharpenLumScale = 1, sharpenLumBias = 0;

    #endregion

    #region 醉酒效果

    //醉酒效果
    [Space(10f), Header("DrunkEffect"), SerializeField]
    private bool drunkEffect;

    //醉酒的旋转像素的半径
    [SerializeField] private float drunkRadius = 1.0f;

    //醉酒的强度[0-1]
    [SerializeField, Range(0, 1f)] private float drunkIntensity = 1.0f;

    //醉酒的旋转速度
    [SerializeField] private float drunkRotationSpeed = 0.05f;

    //醉酒的中心点
    [SerializeField] private Vector2 drunkCenter = new Vector2(0.5f, 0.5f);

    #endregion

    #region 暗角

    //暗角
    [Space(10f), Header("Vignette"), SerializeField]
    private bool vignette;

    //使用简单模式
    [SerializeField] private bool simpleVignette = true;

    //暗角 简单模式下 强度
    [SerializeField] private float vignetteSimpleIntensity = 0.75f;

    //暗角 简单模式下 阀值
    [SerializeField] private float vignetteSimpleThreshold = 0.55f;

    //暗角 复杂模式下 强度
    [SerializeField] private float vignetteComplexIntensity = 0.8f;

    //暗角 复杂模式下 颜色权重
    [SerializeField] private Vector3 vignetteComplexWeights = new Vector3(0.98f, 0.98f, 0.98f);

    //暗角 复杂模式下 黑暗的颜色
    [SerializeField, ColorUsage(false)]
    private Color vignetteComplexDarkColor = new Color(3f / 255, 4f / 255, 5f / 255);

    #endregion

    #region 色差偏移

    //色差偏移
    [Space(10f), Header("ChromaticAberration"), SerializeField]
    private bool chromaticAberration;

    //色差偏移 中心点
    [SerializeField] private Vector2 caCenter = new Vector2(0.5f, 0.5f);

    //色差偏移 距离阀值
    [SerializeField] private float caCenterDistanceThreshold = 0.2f;

    //色差偏移 距离强度
    [SerializeField] private float caFA = 1.25f;

    //色差偏移 偏移强度
    [SerializeField] private float caIntensity = 30f;

    //色差偏移 偏移扰动尺寸
    [SerializeField] private float caDistortSize = 0.75f;

    #endregion

    private MyPostProcessingAsset postProcessingAsset;

    private RenderTexture eyeAdaptationPreRT;
    private ComputeBuffer avgLuminBuffer;

    public bool NeedsDepth => depthStripes || toneMapping;

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

        sharpenMat = new Material(Shader.Find("Hidden/My Pipeline/Sharpen"))
        {
            name = "My Sharpen Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        drunkEffectMat = new Material(Shader.Find("Hidden/My Pipeline/DrunkEffect"))
        {
            name = "My DrunkEffect Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        chromaticAberrationMat = new Material(Shader.Find("Hidden/My Pipeline/ChromaticAberration"))
        {
            name = "My ChromaticAberration Material",
            hideFlags = HideFlags.HideAndDontSave
        };

        vignetteMat = new Material(Shader.Find("Hidden/My Pipeline/Vignette"))
        {
            name = "My Vignette Material",
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    public void Setup(MyPostProcessingAsset asset)
    {
        postProcessingAsset = asset;
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
            ToneMapping(cb, nowRTID, cameraDepthID, endRTID, width, height, format);
            nowRTID = endRTID;
        }
        else
        {
            if (eyeAdaptationPreRT)
            {
                DestroyImmediate(eyeAdaptationPreRT);
            }

            if (toneMapping)
            {
                avgLuminBuffer.Release();
            }
        }

        /*
        if (sharpen)
        {
            int endRTID = nowRTID == cameraColorID || nowRTID == resolved2TexID ? resolved1TexID : resolved2TexID;
            Sharpen(cb, nowRTID, endRTID, width, height, format);
            nowRTID = endRTID;
        }

        //Drunk Effect
        if (drunkEffect)
        {
            int endRTID = nowRTID == cameraColorID || nowRTID == resolved2TexID ? resolved1TexID : resolved2TexID;
            DrunkEffect(cb, nowRTID, endRTID, width, height, format);
            nowRTID = endRTID;
        }


        //Vignette
        if (vignette)
        {
            int endRTID = nowRTID == cameraColorID || nowRTID == resolved2TexID ? resolved1TexID : resolved2TexID;
            Vignette(cb, nowRTID, endRTID, width, height, format);
            nowRTID = endRTID;
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
        */

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


    private void ToneMapping(CommandBuffer cb, RenderTargetIdentifier srcID, int _depthID
        , RenderTargetIdentifier destID, int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Tone Mapping");


        //AvgLuminance==========================================
        cb.BeginSample("AvgLuminance");

        if (avgLumiComputeShader)
        {
            cb.GetTemporaryRT(avgLuminanceTexRTID, 1, 1, 0, FilterMode.Bilinear, RenderTextureFormat.R16,
                RenderTextureReadWrite.Linear, 1, true);

            cb.GetTemporaryRT(avgLumaRTID, width / 4, height / 4, 0, FilterMode.Bilinear, format);

            cb.Blit(srcID, avgLumaRTID);

            if (avgLuminBuffer == null)
            {
                avgLuminBuffer = new ComputeBuffer(256, sizeof(uint));
            }

            ComputeShader histogramCS = postProcessingAsset.AverageLuminanceHistogramCS;
            int hKernel = histogramCS.FindKernel("CSMain");
            cb.SetComputeBufferParam(histogramCS, hKernel, avgLumaBufferID, avgLuminBuffer);
            cb.SetComputeVectorParam(histogramCS, avgLumaHistDataID
                , new Vector3(width / 4f, avgLumiSkyLerp, avgLumiSkyValue));
            cb.SetComputeTextureParam(histogramCS, hKernel, mainTexID, avgLumaRTID);
            cb.SetComputeTextureParam(histogramCS, hKernel, depthID, _depthID);
            cb.DispatchCompute(histogramCS, hKernel, height / 4, 1, 1);


            ComputeShader calcCS = postProcessingAsset.AverageLuminanceCalculationCS;
            int cKernel = calcCS.FindKernel("CSMain");
            cb.SetComputeVectorParam(calcCS, avgLumaCalcDataID, new Vector4((int) width / 4, (int) height / 4, 0f, 1f));
            cb.SetComputeBufferParam(calcCS, cKernel, avgLumaBufferID, avgLuminBuffer);
            cb.SetComputeTextureParam(calcCS, cKernel, mainTexID, avgLuminanceTexRTID);
            cb.DispatchCompute(calcCS, cKernel, 64, 1, 1);
        }
        else
        {
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
        cb.SetGlobalVector(curveDEFID, tmCurveDEF);
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

    private void Sharpen(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID
        , int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Sharpen");

        cb.SetGlobalVector(sharpenNearFarID, new Vector2(sharpenNear, sharpenFar));
        cb.SetGlobalVector(sharpenDistLumScaleBiasID
            , new Vector4(sharpenDistanceScale, sharpenDistanceBias
                , sharpenLumScale, sharpenLumBias));

        Blit(cb, srcID, destID, sharpenMat);

        cb.EndSample("Sharpen");
    }

    private void DrunkEffect(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID
        , int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Chromatic Aberration");

        cb.SetGlobalVector(drunkDataID, new Vector3(drunkRadius, drunkIntensity, drunkRotationSpeed));
        cb.SetGlobalVector(drunkCenterID, drunkCenter);

        Blit(cb, srcID, destID, drunkEffectMat);

        cb.EndSample("Chromatic Aberration");
    }

    private void Vignette(CommandBuffer cb, RenderTargetIdentifier srcID, RenderTargetIdentifier destID
        , int width, int height, RenderTextureFormat format)
    {
        cb.BeginSample("Vignette");

        if (simpleVignette)
        {
            cb.SetGlobalFloat(vignetteSimpleIntensityID, vignetteSimpleIntensity);
            cb.SetGlobalFloat(vignetteSimpleThresholdID, vignetteSimpleThreshold);
            Blit(cb, srcID, destID, vignetteMat, (int) VignetteEnum.VignetteSimple);
        }
        else
        {
            cb.SetGlobalFloat(vignetteComplexIntensityID, vignetteComplexIntensity);
            cb.SetGlobalVector(vignetteComplexWeightsID, vignetteComplexWeights);
            cb.SetGlobalVector(vignetteComplexDarkColorID, vignetteComplexDarkColor);
            cb.SetGlobalTexture(vignetteComplexMaskID, postProcessingAsset.VignetteComplexMaskTexture);
            Blit(cb, srcID, destID, vignetteMat, (int) VignetteEnum.VignetteComplex);
        }

        cb.EndSample("Vignette");
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