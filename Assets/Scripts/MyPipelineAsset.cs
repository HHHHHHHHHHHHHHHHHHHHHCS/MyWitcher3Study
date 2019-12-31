using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

[CreateAssetMenu(menuName = "Rendering/MyPipeline", fileName = "MyPipelineAsset")]
public class MyPipelineAsset : RenderPipelineAsset
{
    public enum ShadowMapSize
    {
        _256 = 256,
        _512 = 512,
        _1024 = 1024,
        _2048 = 2048,
        _4096 = 4096,
    }

    public enum ShadowCascades
    {
        Zero = 0,
        Two = 2,
        Four = 4,
    }

    public enum MSAAMode
    {
        Off = 1,
        _2x = 2,
        _4x = 4,
        _8x = 8,
    }

    //后处理资源文件
    [SerializeField] private MyPostProcessingAsset postProcessingAsset = null;

    //后处理数值文件
    [SerializeField] private MyPostProcessingStack defaultStack = null;

    //如果都是大物体不用怎么动态合批  不建议勾选  不然Unity会每帧去计算是否要合批
    [SerializeField] private bool dynamicBatching = false;

    //实例化  减少 相同网格和材质的东西 切换绘制的时间
    [SerializeField] private bool instancing = false;


    //LOD抖动消融图片
    [SerializeField] private Texture2D ditherTexture = null;

    //LOD抖动消融过渡速度
    [SerializeField, Range(0f, 120f)] private float ditherAnimationSpeed = 30f;

    //LOD的过渡
    [SerializeField] private bool supportLODCrossFading = true;

    //阴影贴图分辨率
    [SerializeField] private ShadowMapSize shadowMapSize = ShadowMapSize._1024;

    //阴影的距离
    [SerializeField] private float shadowDistance = 100f;

    //阴影距离渐变
    [SerializeField, Range(0.01f, 2f)] private float shadowFadeRange = 1f;

    //阴影级联
    [SerializeField] private ShadowCascades shadowCascades = ShadowCascades.Four;

    //阴影级联 2级距离
    [SerializeField, HideInInspector] private float twoCascadesSplit = 0.25f;

    //阴影级联 4级距离
    [SerializeField, HideInInspector] private Vector3 fourCascadesSplit = new Vector3(0.067f, 0.2f, 0.467f);

    //RT的尺寸缩放
    [SerializeField, Range(0.25f, 2f)] private float renderScale = 1f;

    //抗锯齿
    [SerializeField] private MSAAMode msaaSamples = MSAAMode.Off;

    //允许HDR
    [SerializeField] private bool allowHDR = false;

    //同步Camera
    [SerializeField] private bool syncGameCamera = false;


    public bool HasShadowCascades => shadowCascades != ShadowCascades.Zero;

    public bool HasLODCrossFading => supportLODCrossFading;

    protected override IRenderPipeline InternalCreatePipeline()
    {
        Vector3 shadowCascadeSplit = shadowCascades == ShadowCascades.Four
            ? fourCascadesSplit
            : new Vector3(twoCascadesSplit, 0f);

        return new MyPipeline(dynamicBatching, instancing, postProcessingAsset, defaultStack, ditherTexture
            , ditherAnimationSpeed, (int) shadowMapSize, shadowDistance, shadowFadeRange
            , (int) shadowCascades, shadowCascadeSplit, renderScale, (int) msaaSamples, allowHDR, syncGameCamera);
    }
}