using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Experimental.GlobalIllumination;
using LightType = UnityEngine.LightType;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Lightmapping = UnityEngine.Experimental.GlobalIllumination.Lightmapping;
using Random = UnityEngine.Random;

public class MyPipeline : RenderPipeline
{
    private const int maxVisibleLights = 16;


    private const string cascadedShadowsHardKeyword = "_CASCADED_SHADOWS_HARD";
    private const string cascadedShadowsSoftKeyword = "_CASCADED_SHADOWS_SOFT";
    private const string shadowsHardKeyword = "_SHADOWS_HARD";
    private const string shadowsSoftKeyword = "_SHADOWS_SOFT";
    private const string shadowmaskKeyword = "_SHADOWMASK";
    private const string distanceShadowmaskKeyword = "_DISTANCE_SHADOWMASK";
    private const string subtractiveLightingKeyword = "_SUBTRACTIVE_LIGHTING";

    private static int ditherTextureID = Shader.PropertyToID("_DitherTexture");
    private static int ditherTextureSTID = Shader.PropertyToID("_DitherTexture_ST");
    private static int visibleLightColorsID = Shader.PropertyToID("_VisibleLightColors");
    private static int visibleLightDirectionsOrPositionsID = Shader.PropertyToID("_VisibleLightDirectionsOrPositions");
    private static int visibleLightAttenuationsID = Shader.PropertyToID("_VisibleLightAttenuations");
    private static int visibleLightSpotDirectionsID = Shader.PropertyToID("_VisibleLightSpotDirections");
    private static int visibleLightOcclusionMaskID = Shader.PropertyToID("_VisibleLightOcclusionMasks");
    private static int lightIndicesOffsetAndCountID = Shader.PropertyToID("unity_LightIndicesOffsetAndCount");
    private static int shadowMapID = Shader.PropertyToID("_ShadowMap");
    private static int cascadedShadowMapID = Shader.PropertyToID("_CascadedShadowMap");
    private static int worldToShadowMatricesID = Shader.PropertyToID("_WorldToShadowMatrices");
    private static int worldToShadowCascadeMatricesID = Shader.PropertyToID("_WorldToShadowCascadeMatrices");
    private static int shadowBiasID = Shader.PropertyToID("_ShadowBias");
    private static int shadowDataID = Shader.PropertyToID("_ShadowData");
    private static int shadowMapSizeID = Shader.PropertyToID("_ShadowMapSize");
    private static int cascadedShadowMapSizedID = Shader.PropertyToID("_CascadedShadowMapSize");
    private static int cascadedShadowStrengthID = Shader.PropertyToID("_CascadedShadowStrength");
    private static int globalShadowDataID = Shader.PropertyToID("_GlobalShadowData");
    private static int cascadeCullingSpheresID = Shader.PropertyToID("_CascadeCullingSpheres");
    private static int subtractiveShadowColorID = Shader.PropertyToID("_SubtractiveShadowColor");

    private static int cameraColorTextureID = Shader.PropertyToID("_CameraColorTexture");
    private static int cameraDepthTextureID = Shader.PropertyToID("_CameraDepthTexture");

    private static Camera mainCamera;

    private static Vector4[] occlusionMasks =
    {
        new Vector4(-1f, 0f, 0f, 0f),
        new Vector4(1f, 0f, 0f, 0f),
        new Vector4(0f, 1f, 0f, 0f),
        new Vector4(0f, 0f, 1f, 0f),
        new Vector4(0f, 0f, 0f, 1f),
    };

    private readonly CommandBuffer cameraBuffer = new CommandBuffer()
    {
        name = "Render Camera"
    };

    private readonly CommandBuffer shadowBuffer = new CommandBuffer()
    {
        name = "Render Shadows"
    };

    private readonly CommandBuffer postProcessingBuffer = new CommandBuffer()
    {
        name = "Post-Processing"
    };

    private MyPostProcessingAsset postProcessingAsset;
    private MyPostProcessingStack defaultStack;

    private Texture2D ditherTexture;
    private float ditherAnimationFrameDuration;
    private Vector4[] ditherSTs;
    private float lastDitherTime;
    private int ditherSTIndex = -1;

    private CullResults cull;
    private Material errorMaterial;
    private DrawRendererFlags drawFlags;

    private Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    private Vector4[] visibleLightDirectionsOrPositions = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    private Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];

    private RenderTexture shadowMap, cascadedShadowMap;
    private float shadowDistance;
    private Vector4 globalShadowData;
    private int shadowMapSize;
    private Vector4[] shadowData = new Vector4[maxVisibleLights];
    private Matrix4x4[] worldToShadowMatrices = new Matrix4x4[maxVisibleLights];
    private int shadowTileCount;
    private int shadowCascades;
    private Vector3 shadowCascadeSplit;
    private Matrix4x4[] worldToShadowCascadeMatrices = new Matrix4x4[5];
    private Vector4[] cascadeCullingSpheres = new Vector4[4];
    private bool mainLightExists;
    private Vector4[] visibleLightOcclusionMasks = new Vector4[maxVisibleLights];

    private float renderScale;
    private int msaaSamples;
    private bool allowHDR;

    public MyPipeline(bool dynamicBatching, bool instancing
        , MyPostProcessingAsset _postProcessingAsset, MyPostProcessingStack _defaultStack,
        Texture2D _ditherTexture, float _ditherAnimationSpeed, int _shadowMapSize, float _shadowDistance
        , float _shadowFadeRange, int _shadowCascades, Vector3 _shadowCascadeSplit, float _renderScale
        , int _msaaSamples, bool _allowHDR, bool _syncGameCamera)
    {
        //Unity 认为光的强度是在伽马空间中定义的，即使我们是在线性空间中工作。
        GraphicsSettings.lightsUseLinearIntensity = true;

        //如果Z相反 阴影的z最远是1
        if (SystemInfo.usesReversedZBuffer)
        {
            worldToShadowCascadeMatrices[4].m33 = 1f;
        }

        if (dynamicBatching)
        {
            drawFlags = DrawRendererFlags.EnableDynamicBatching;
        }

        ditherTexture = _ditherTexture;
        if (_ditherAnimationSpeed > 0f)
        {
            ConfigureDitherAnimation(_ditherAnimationSpeed);
        }

        if (instancing)
        {
            drawFlags |= DrawRendererFlags.EnableInstancing;
        }

        postProcessingAsset = _postProcessingAsset;
        defaultStack = _defaultStack;

        shadowMapSize = _shadowMapSize;
        shadowDistance = _shadowDistance;
        globalShadowData.y = 1f / _shadowFadeRange;
        shadowCascades = _shadowCascades;
        shadowCascadeSplit = _shadowCascadeSplit;
        renderScale = _renderScale;
        //设置msaa 如果硬件不支持 则为自动回退为1
        QualitySettings.antiAliasing = _msaaSamples;
        msaaSamples = Mathf.Max(QualitySettings.antiAliasing, 1);
        allowHDR = _allowHDR;

#if UNITY_EDITOR
        if (SceneView.onSceneGUIDelegate != null)
        {
            SceneView.onSceneGUIDelegate -= OnSceneView;
        }

        if (_syncGameCamera)
        {
            SceneView.onSceneGUIDelegate += OnSceneView;
        }
#endif

#if UNITY_EDITOR
        Lightmapping.SetDelegate(lightmappingLightDelegate);
#endif
    }

#if UNITY_EDITOR
    public override void Dispose()
    {
        base.Dispose();
        if (SceneView.onSceneGUIDelegate != null) SceneView.onSceneGUIDelegate -= OnSceneView;
        Lightmapping.ResetDelegate();
    }
#endif

#if UNITY_EDITOR
    private void OnSceneView(SceneView view)
    {
        if (view != null && view.camera != null && view.camera.cameraType == CameraType.SceneView
            && mainCamera != null)
        {
            var transform = mainCamera.transform;
            transform.position = view.camera.transform.position;
            transform.rotation = view.camera.transform.rotation;
        }
    }
#endif

    public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
    {
        base.Render(renderContext, cameras);

        ConfigureDitherPattern(renderContext);

        foreach (var camera in cameras)
        {
            Render(renderContext, camera);
        }
    }

    public void Render(ScriptableRenderContext context, Camera camera)
    {
        if (!CullResults.GetCullingParameters(camera, out var cullingParameters))
        {
            return;
        }

        cullingParameters.shadowDistance = Mathf.Min(shadowDistance, camera.farClipPlane);

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView)
        {
            //将UI几何体发射到“场景”视图中以进行渲染。
            ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
        }
        else if (mainCamera == null && camera.cameraType == CameraType.Game && camera == Camera.main)
        {
            mainCamera = camera;
        }
#endif

        //CullResults cull = CullResults.Cull(ref cullingParameters, context);
        CullResults.Cull(ref cullingParameters, context, ref cull);

        if (cull.visibleLights.Count > 0)
        {
            ConfigureLights();
            if (mainLightExists)
            {
                RenderCascadedShadows(context);
            }
            else
            {
                cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
                cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            }

            if (shadowTileCount > 0)
            {
                RenderShadows(context);
            }
            else
            {
                cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
                cameraBuffer.DisableShaderKeyword(shadowsSoftKeyword);
            }
        }
        else
        {
            cameraBuffer.SetGlobalVector(lightIndicesOffsetAndCountID, Vector4.zero);
            cameraBuffer.DisableShaderKeyword(cascadedShadowsHardKeyword);
            cameraBuffer.DisableShaderKeyword(cascadedShadowsSoftKeyword);
            cameraBuffer.DisableShaderKeyword(shadowsHardKeyword);
            cameraBuffer.DisableShaderKeyword(shadowsSoftKeyword);
        }

        context.SetupCameraProperties(camera);

        var myPipelineCamera = camera.GetComponent<MyPipelineCamera>();
        MyPostProcessingStack activeStack = myPipelineCamera ? myPipelineCamera.PostProcessingStack : defaultStack;
        activeStack.Setup(postProcessingAsset);

        bool scaledRendering = renderScale != 1f && camera.cameraType == CameraType.Game;

        int renderWidth = camera.pixelWidth;
        int renderHeight = camera.pixelHeight;
        if (scaledRendering)
        {
            renderWidth = (int) (renderWidth * renderScale);
            renderHeight = (int) (renderHeight * renderScale);
        }

        int renderSamples = camera.allowMSAA ? msaaSamples : 1;
        bool renderToTexture = scaledRendering || renderSamples > 1 || activeStack;

        bool needsDepth = activeStack && activeStack.NeedsDepth;
        //如果MSAA != 1 , 则 主贴图需要 24位深度 用来ZTestWrite画
        bool needsDirectDepth = needsDepth && renderSamples == 1;
        //专门用DepthOnly 来画深度图
        bool needsDepthOnlyPass = needsDepth && renderSamples > 1;

        RenderTextureFormat format =
            allowHDR && camera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;

        if (renderToTexture)
        {
            //需要深度进行处理的的时候  要单独的深度图   不让它进行MSAA
            //否则可以跟随主颜色进行MSAA
            cameraBuffer.GetTemporaryRT(cameraColorTextureID, renderWidth, renderHeight, needsDirectDepth ? 0 : 24
                , FilterMode.Bilinear, format, RenderTextureReadWrite.Default, renderSamples);

            if (needsDepth)
            {
                cameraBuffer.GetTemporaryRT(cameraDepthTextureID, renderWidth, renderHeight, 24
                    , FilterMode.Point, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear, 1);
            }

            if (needsDirectDepth)
            {
                cameraBuffer.SetRenderTarget(cameraColorTextureID, RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store, cameraDepthTextureID, RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store);
            }
            else
            {
                cameraBuffer.SetRenderTarget(cameraColorTextureID, RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store);
            }
        }


        CameraClearFlags clearFlags = camera.clearFlags;
        cameraBuffer.ClearRenderTarget((clearFlags & CameraClearFlags.Depth) != 0
            , (clearFlags & CameraClearFlags.Color) != 0, camera.backgroundColor);


        cameraBuffer.BeginSample("Render Camera");

        cameraBuffer.SetGlobalVectorArray(visibleLightColorsID, visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirectionsOrPositionsID, visibleLightDirectionsOrPositions);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsID, visibleLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visibleLightSpotDirectionsID, visibleLightSpotDirections);
        cameraBuffer.SetGlobalVectorArray(visibleLightOcclusionMaskID, visibleLightOcclusionMasks);

        globalShadowData.z = 1f - cullingParameters.shadowDistance * globalShadowData.y;
        cameraBuffer.SetGlobalVector(globalShadowDataID, globalShadowData);
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        //这样就可以走SRP的SubShader 如果没有则都走
        //Shader SubShader Tags{"RenderPipeline"="MySRPPipeline"}
        //Shader.globalRenderPipeline = "MySRPPipeline";

        //我们必须通过提供相机和一个shader pass 作为draw setting的构造函数的参数。
        //这个相机用来设置排序和裁剪层级(culling layers),
        //而shader pass 控制使用那个shader pass进行渲染。
        //如果Pass未指定LightMode，Unity会自动将其设置为SRPDefaultUnlit
        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("SRPDefaultUnlit"))
        {
            flags = drawFlags,
            rendererConfiguration = RendererConfiguration.None
        };
        if (cull.visibleLights.Count > 0)
        {
            drawSettings.rendererConfiguration = RendererConfiguration.PerObjectLightIndices8;
        }

        drawSettings.rendererConfiguration |= RendererConfiguration.PerObjectReflectionProbes
                                              | RendererConfiguration.PerObjectLightmaps
                                              | RendererConfiguration.PerObjectLightProbe
                                              | RendererConfiguration.PerObjectLightProbeProxyVolume
                                              | RendererConfiguration.PerObjectShadowMask
                                              | RendererConfiguration.PerObjectOcclusionProbe
                                              | RendererConfiguration.PerObjectOcclusionProbeProxyVolume;

        drawSettings.sorting.flags = SortFlags.CommonOpaque;
        //因为 Unity 更喜欢将对象空间化地分组以减少overdraw
        var filterSettings = new FilterRenderersSettings(true)
        {
            renderQueueRange = RenderQueueRange.opaque
        };
        context.DrawRenderers(
            cull.visibleRenderers, ref drawSettings, filterSettings);


        context.DrawSkybox(camera);

        var moonOnlyDrawSettings = new DrawRendererSettings(
            camera, new ShaderPassName("MoonOnly"))
        {
            flags = drawFlags,
            sorting = {flags = SortFlags.CommonOpaque}
        };
        context.DrawRenderers(cull.visibleRenderers, ref moonOnlyDrawSettings, filterSettings);

        if (activeStack)
        {
            if (needsDepth)
            {
                if (needsDepthOnlyPass)
                {
                    var depthOnlyDrawSettings = new DrawRendererSettings(
                        camera, new ShaderPassName("DepthOnly"))
                    {
                        flags = drawFlags, sorting = {flags = SortFlags.CommonOpaque}
                    };


                    cameraBuffer.SetRenderTarget(cameraDepthTextureID, RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store);
                    cameraBuffer.ClearRenderTarget(true, false, Color.clear);
                    context.ExecuteCommandBuffer(cameraBuffer);
                    cameraBuffer.Clear();
                    context.DrawRenderers(cull.visibleRenderers, ref depthOnlyDrawSettings, filterSettings);
                }
            }


            activeStack.RenderAfterOpaque(
                postProcessingBuffer, cameraColorTextureID, cameraDepthTextureID
                , renderWidth, renderHeight, renderSamples, format);
            context.ExecuteCommandBuffer(postProcessingBuffer);
            postProcessingBuffer.Clear();

            if (needsDirectDepth)
            {
                cameraBuffer.SetRenderTarget(
                    cameraColorTextureID, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store
                    , cameraDepthTextureID, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            }
            else
            {
                cameraBuffer.SetRenderTarget(cameraColorTextureID, RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store);
            }

            context.ExecuteCommandBuffer(cameraBuffer);
            cameraBuffer.Clear();
        }


        drawSettings.sorting.flags = SortFlags.CommonTransparent;
        filterSettings.renderQueueRange = RenderQueueRange.transparent;
        context.DrawRenderers(
            cull.visibleRenderers, ref drawSettings, filterSettings);

        DrawDefaultPipeline(context, camera);

        if (renderToTexture)
        {
            if (activeStack && camera.cameraType == CameraType.Game)
            {
                activeStack.RenderAfterTransparent(postProcessingBuffer, cameraColorTextureID, cameraDepthTextureID
                    , renderWidth, renderHeight, renderSamples, format);
                context.ExecuteCommandBuffer(postProcessingBuffer);
                postProcessingBuffer.Clear();
            }
            else
            {
                cameraBuffer.Blit(cameraColorTextureID, BuiltinRenderTextureType.CameraTarget);
            }


            cameraBuffer.ReleaseTemporaryRT(cameraColorTextureID);
            if (needsDepth)
            {
                cameraBuffer.ReleaseTemporaryRT(cameraDepthTextureID);
            }
        }

        cameraBuffer.EndSample("Render Camera");
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        context.Submit();

        if (shadowMap)
        {
            RenderTexture.ReleaseTemporary(shadowMap);
            shadowMap = null;
        }

        if (cascadedShadowMap)
        {
            RenderTexture.ReleaseTemporary(cascadedShadowMap);
            cascadedShadowMap = null;
        }
    }

    private void ConfigureDitherAnimation(float ditherAnimationSpeed)
    {
        ditherAnimationFrameDuration = 1f / ditherAnimationSpeed;
        Random.State state = Random.state;
        Random.InitState(0);
        ditherSTs = new Vector4[16];
        for (int i = 0; i < ditherSTs.Length; i++)
        {
            //水平每隔N帧数 偏移  ,  垂直每隔随机帧数偏移
            ditherSTs[i] = new Vector4(
                (i & 1) == 0 ? (1f / 64f) : (-1f / 64f),
                (i & 2) == 0 ? (1f / 64f) : (-1f / 64f),
                Random.value, Random.value
            );
        }

        Random.state = state;
    }

    private void ConfigureDitherPattern(ScriptableRenderContext context)
    {
        if (ditherSTIndex < 0)
        {
            ditherSTIndex = 0;
            lastDitherTime = Time.unscaledTime;

            cameraBuffer.SetGlobalTexture(ditherTextureID, ditherTexture);
            cameraBuffer.SetGlobalVector(ditherTextureSTID, new Vector4(1f / 64f, 1f / 64f, 0f, 0f));
            context.ExecuteCommandBuffer(cameraBuffer);
            cameraBuffer.Clear();
        }
        else if (ditherAnimationFrameDuration > 0f && Application.isPlaying)
        {
            float currentTime = Time.unscaledTime;
            if (currentTime - lastDitherTime >= ditherAnimationFrameDuration)
            {
                lastDitherTime = currentTime;
                ditherSTIndex = ditherSTIndex < 15 ? ditherSTIndex + 1 : 0;
                cameraBuffer.SetGlobalVector(
                    ditherTextureSTID, ditherSTs[ditherSTIndex]);
            }

            context.ExecuteCommandBuffer(cameraBuffer);
            cameraBuffer.Clear();
        }
    }

    private void ConfigureLights()
    {
        mainLightExists = false;
        bool shadowmaskExists = false;
        bool subtractiveLighting = false;
        shadowTileCount = 0;
        for (int i = 0; i < cull.visibleLights.Count && i < maxVisibleLights; i++)
        {
            VisibleLight light = cull.visibleLights[i];
            visibleLightColors[i] = light.finalColor;

            Vector4 attenuation = Vector4.zero;
            attenuation.w = 1f;
            Vector4 shadow = Vector4.zero;

            LightBakingOutput baking = light.light.bakingOutput;
            visibleLightOcclusionMasks[i] = occlusionMasks[baking.occlusionMaskChannel + 1];
            if (baking.lightmapBakeType == LightmapBakeType.Mixed)
            {
                shadowmaskExists |= baking.mixedLightingMode == MixedLightingMode.Shadowmask;
                if (baking.mixedLightingMode == MixedLightingMode.Subtractive)
                {
                    subtractiveLighting = true;
                    cameraBuffer.SetGlobalColor(subtractiveShadowColorID, RenderSettings.subtractiveShadowColor.linear);
                }
            }

            if (light.lightType == LightType.Directional)
            {
                //光线按照局部Z轴照射  第三列是Z轴旋转
                Vector4 v = light.localToWorld.GetColumn(2);
                //在shader中 我们需要的光的方向是 从表面到光的  所以要求反
                //第四个分量总是零 只用对 x y z 求反
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visibleLightDirectionsOrPositions[i] = v;
                shadow = ConfigureShadows(i, light.light);
                //z=1 是方向光
                shadow.z = 1f;
                if (i == 0 && shadow.x > 0f && shadowCascades > 0)
                {
                    mainLightExists = true;
                    shadowTileCount -= 1;
                }
            }
            else
            {
                //第三个储存的是位置 w是1
                visibleLightDirectionsOrPositions[i]
                    = light.localToWorld.GetColumn(3);

                attenuation.x = 1f / Mathf.Max(light.range * light.range, 0.000001f);

                if (light.lightType == LightType.Spot)
                {
                    //聚光灯需要 方向 拿Z轴  即矩阵第三行
                    Vector4 v = light.localToWorld.GetColumn(2);

                    v.x = -v.x;
                    v.y = -v.y;
                    v.z = -v.z;
                    visibleLightSpotDirections[i] = v;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    //灯光角的一半   外面不显示
                    float outerCos = Mathf.Cos(outerRad);
                    //内圈 不衰减
                    float outerTan = Mathf.Tan(outerRad);
                    //外圈衰减
                    float innerCos =
                        Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.0001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;

                    shadow = ConfigureShadows(i, light.light);
                }
                else
                {
                    visibleLightSpotDirections[i] = Vector4.one;
                }
            }

            visibleLightAttenuations[i] = attenuation;
            shadowData[i] = shadow;
        }

        bool useDistanceShadowmask = QualitySettings.shadowmaskMode == ShadowmaskMode.DistanceShadowmask;
        CoreUtils.SetKeyword(cameraBuffer, shadowmaskKeyword, shadowmaskExists && !useDistanceShadowmask);
        CoreUtils.SetKeyword(cameraBuffer, distanceShadowmaskKeyword, shadowmaskExists && useDistanceShadowmask);
        CoreUtils.SetKeyword(cameraBuffer, subtractiveLightingKeyword, subtractiveLighting);


        //剔除额外的光 和 主光源
        if (mainLightExists || cull.visibleLights.Count > maxVisibleLights)
        {
            int[] lightIndices = cull.GetLightIndexMap();

            if (mainLightExists)
            {
                lightIndices[0] = -1;
            }

            for (int i = maxVisibleLights; i < cull.visibleLights.Count; i++)
            {
                lightIndices[i] = -1;
            }

            cull.SetLightIndexMap(lightIndices);
        }
    }


    private Vector4 ConfigureShadows(int lightIndex, Light shadowLight)
    {
        Vector4 shadow = Vector4.zero;
        Bounds shadowBounds;
        if (shadowLight.shadows != LightShadows.None)
        {
            //这个剔除 如果 没有阴影接受者  或者阴影接受者不再视野内
            if (cull.GetShadowCasterBounds(lightIndex, out shadowBounds))
            {
                shadowTileCount += 1;
                shadow.x = shadowLight.shadowStrength;
                shadow.y = shadowLight.shadows == LightShadows.Soft ? 1f : 0f;
            }
        }

        return shadow;
    }


    private void RenderCascadedShadows(ScriptableRenderContext context)
    {
        float tileSize = shadowMapSize / 2;
        cascadedShadowMap = SetShadowRenderTarget();
        shadowBuffer.BeginSample("Render Main Shadows");

        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
        Light shadowLight = cull.visibleLights[0].light;
        shadowBuffer.SetGlobalFloat(
            shadowBiasID, shadowLight.shadowBias);
        var shadowSettings = new DrawShadowsSettings(cull, 0);
        var tileMatrix = Matrix4x4.identity;
        tileMatrix.m00 = tileMatrix.m11 = 0.5f;

        for (int i = 0; i < shadowCascades; i++)
        {
            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;
            cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                0, i, shadowCascades, shadowCascadeSplit, (int) tileSize
                , shadowLight.shadowNearPlane,
                out viewMatrix, out projectionMatrix, out splitData);

            Vector2 tileOffset = ConfigureShadowTile(i, 2, tileSize);
            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            cascadeCullingSpheres[i] = shadowSettings.splitData.cullingSphere = splitData.cullingSphere;
            //储存半径平方 用于比较
            cascadeCullingSpheres[i].w *= splitData.cullingSphere.w;
            context.DrawShadows(ref shadowSettings);
            CalculateWorldToShadowMatrix(ref viewMatrix, ref projectionMatrix
                , out worldToShadowCascadeMatrices[i]);
            tileMatrix.m03 = tileOffset.x * 0.5f;
            tileMatrix.m13 = tileOffset.y * 0.5f;
            worldToShadowCascadeMatrices[i] = tileMatrix * worldToShadowCascadeMatrices[i];
        }

        shadowBuffer.DisableScissorRect();
        shadowBuffer.SetGlobalTexture(cascadedShadowMapID, cascadedShadowMap);
        shadowBuffer.SetGlobalVectorArray(cascadeCullingSpheresID, cascadeCullingSpheres);
        shadowBuffer.SetGlobalMatrixArray(worldToShadowCascadeMatricesID, worldToShadowCascadeMatrices);
        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(cascadedShadowMapSizedID
            , new Vector4(invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize));
        shadowBuffer.SetGlobalFloat(cascadedShadowStrengthID, shadowLight.shadowStrength);
        bool hard = shadowLight.shadows == LightShadows.Hard;
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsHardKeyword, hard);
        CoreUtils.SetKeyword(shadowBuffer, cascadedShadowsSoftKeyword, !hard);
        shadowBuffer.EndSample("Render Main Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }

    private void RenderShadows(ScriptableRenderContext context)
    {
        int split;
        if (shadowTileCount <= 1)
        {
            split = 1;
        }
        else if (shadowTileCount <= 4)
        {
            split = 2;
        }
        else if (shadowTileCount <= 9)
        {
            split = 3;
        }
        else
        {
            split = 4;
        }

        //虽然也可以用tex2DArray 但是不支持一些老机型手机
        //所以这里用图片分割成4*4块
        float tileSize = shadowMapSize / split;
        float tileScale = 1f / split;
        globalShadowData.x = tileScale;

        shadowMap = SetShadowRenderTarget();

        shadowBuffer.BeginSample("Render Addition Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();


        int tileIndex = 0;
        bool hardShadows = false;
        bool softShadows = false;
        for (int i = mainLightExists ? 1 : 0; i < cull.visibleLights.Count && i < maxVisibleLights; i++)
        {
            //剔除没有强度的 或者不需要的
            if (shadowData[i].x <= 0f)
            {
                continue;
            }

            Matrix4x4 viewMatrix, projectionMatrix;
            ShadowSplitData splitData;

            //是否能生成有效的矩阵 如果没有 x=0 表示不启用阴影
            bool validShadows;

            if (shadowData[i].z > 0f)
            {
                //参数 1:灯光index  2:cascadeIndex  3:cascadeCount   4:cascade 分级距离
                //5: 分辨率   6:nearPlane 如果太近不画
                validShadows = cull.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    i, 0, 1, Vector3.right, (int) tileSize
                    , cull.visibleLights[i].light.shadowNearPlane
                    , out viewMatrix, out projectionMatrix, out splitData);
            }
            else
            {
                validShadows = cull.ComputeSpotShadowMatricesAndCullingPrimitives(
                    i, out viewMatrix, out projectionMatrix, out splitData);
            }

            if (!validShadows)
            {
                shadowData[i].x = 0f;
                continue;
            }

            //设置渲染到贴图上的区域(起始位置和大小)
            Vector2 tileOffset = ConfigureShadowTile(tileIndex, split, tileSize);
            shadowData[i].z = tileOffset.x * tileScale;
            shadowData[i].w = tileOffset.y * tileScale;

            shadowBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            shadowBuffer.SetGlobalFloat(shadowBiasID, cull.visibleLights[i].light.shadowBias);
            context.ExecuteCommandBuffer(shadowBuffer);
            shadowBuffer.Clear();

            var shadowSettings = new DrawShadowsSettings(cull, i);
            //用球剔除 xyz是中心点  w是半径
            shadowSettings.splitData.cullingSphere = splitData.cullingSphere;
            context.DrawShadows(ref shadowSettings);


            CalculateWorldToShadowMatrix(
                ref viewMatrix, ref projectionMatrix, out worldToShadowMatrices[i]);


            if (shadowData[i].y <= 0f)
            {
                hardShadows = true;
            }
            else
            {
                softShadows = true;
            }

            tileIndex += 1;
        }

        //渲染完成禁用裁剪   不然平常渲染也会收到影响
        shadowBuffer.DisableScissorRect();

        shadowBuffer.SetGlobalTexture(shadowMapID, shadowMap);
        shadowBuffer.SetGlobalMatrixArray(worldToShadowMatricesID, worldToShadowMatrices);
        shadowBuffer.SetGlobalVectorArray(shadowDataID, shadowData);
        float invShadowMapSize = 1f / shadowMapSize;
        shadowBuffer.SetGlobalVector(shadowMapSizeID
            , new Vector4(invShadowMapSize, invShadowMapSize, shadowMapSize, shadowMapSize));
        //if (haveSoftShadow == LightShadows.Soft)
        //{
        //    shadowBuffer.EnableShaderKeyword(shadowSoftKeyword);
        //}
        //else
        //{
        //    shadowBuffer.DisableShaderKeyword(shadowSoftKeyword);
        //}
        //下面是上面的封装
        CoreUtils.SetKeyword(shadowBuffer, shadowsHardKeyword, hardShadows);
        CoreUtils.SetKeyword(shadowBuffer, shadowsSoftKeyword, softShadows);

        shadowBuffer.EndSample("Render Addition Shadows");
        context.ExecuteCommandBuffer(shadowBuffer);
        shadowBuffer.Clear();
    }


    private RenderTexture SetShadowRenderTarget()
    {
        RenderTexture texture = RenderTexture.GetTemporary(
            shadowMapSize, shadowMapSize, 16, RenderTextureFormat.Shadowmap);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        CoreUtils.SetRenderTarget(shadowBuffer, texture
            , RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.Depth);

        return texture;
    }

    private Vector2 ConfigureShadowTile(int tileIndex, int split, float tileSize)
    {
        Vector2 tileOffset;
        tileOffset.x = tileIndex % split;
        tileOffset.y = tileIndex / split;
        var tileViewport = new Rect(
            tileOffset.x * tileSize, tileOffset.y * tileSize, tileSize, tileSize);

        shadowBuffer.SetViewport(tileViewport);
        //启动裁剪  不然采样阴影贴图边界的时候会受到另外一边的贴图的值影响
        //尤其是软阴影的时候
        shadowBuffer.EnableScissorRect(new Rect(
            tileViewport.x + 4f, tileViewport.y + 4f,
            tileSize - 8f, tileSize - 8f
        ));

        return tileOffset;
    }

    private void CalculateWorldToShadowMatrix(
        ref Matrix4x4 viewMatrix, ref Matrix4x4 projectionMatrix
        , out Matrix4x4 worldToShadowMatrix)
    {
        //如果Z是翻转的
        if (SystemInfo.usesReversedZBuffer)
        {
            projectionMatrix.m20 = -projectionMatrix.m20;
            projectionMatrix.m21 = -projectionMatrix.m21;
            projectionMatrix.m22 = -projectionMatrix.m22;
            projectionMatrix.m23 = -projectionMatrix.m23;
        }

        //原来的位置是 [-1,+1]
        //用这个矩阵 先缩放 0.5 在偏移 +0.5
        //var scaleOffset = Matrix4x4.TRS(
        //    Vector3.one * 0.5f, Quaternion.identity, Vector3.one * 0.5f);
        //上面的运算结果就是下面这个
        //用于偏移worldToShadow 摄像机的的是中点  但是我们物体转到过去的时候是左下角所以要偏移
        var scaleOffset = Matrix4x4.identity;
        scaleOffset.m00 = scaleOffset.m11 = scaleOffset.m22 = 0.5f;
        scaleOffset.m03 = scaleOffset.m13 = scaleOffset.m23 = 0.5f;

        //从右到左乘法
        worldToShadowMatrix = scaleOffset * (projectionMatrix * viewMatrix);
    }


    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    private void DrawDefaultPipeline(ScriptableRenderContext context, Camera camera)
    {
        if (errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            errorMaterial = new Material(errorShader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        var drawSettings = new DrawRendererSettings(camera, new ShaderPassName("ForwardBase"));

        drawSettings.SetShaderPassName(1, new ShaderPassName("PrepassBase"));
        drawSettings.SetShaderPassName(2, new ShaderPassName("Always"));
        drawSettings.SetShaderPassName(3, new ShaderPassName("Vertex"));
        drawSettings.SetShaderPassName(4, new ShaderPassName("VertexLMRGBM"));
        drawSettings.SetShaderPassName(5, new ShaderPassName("VertexLM"));
        drawSettings.SetOverrideMaterial(errorMaterial, 0);

        var filterSettings = new FilterRenderersSettings(true);

        context.DrawRenderers(
            cull.visibleRenderers, ref drawSettings, filterSettings);
    }

#if UNITY_EDITOR
    private static Lightmapping.RequestLightsDelegate lightmappingLightDelegate =
        (Light[] inputLights, NativeArray<LightDataGI> outputLights) =>
        {
            LightDataGI lightData = new LightDataGI();
            for (int i = 0; i < inputLights.Length; i++)
            {
                Light light = inputLights[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        //必须显式
                        var directionalLight = new DirectionalLight();
                        //提取光的信息
                        LightmapperUtils.Extract(light, ref directionalLight);
                        //把信息初始化进去
                        lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        var pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight);
                        lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        var spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight);
                        lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        var rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight);
                        lightData.Init(ref rectangleLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                //告诉Unity 烘焙用哪种衰减
                lightData.falloff = FalloffType.InverseSquared;
                outputLights[i] = lightData;
            }
        };
#endif
}