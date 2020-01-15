using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/MyPostProcessingAsset", fileName = "MyPostProcessingAsset")]
public class MyPostProcessingAsset : ScriptableObject
{


    //暗角 复杂模式下 颜色遮罩
    [SerializeField] private Texture2D vignetteComplexMaskTexture = null;

    //平均luminance 颜色收集
    [SerializeField] private ComputeShader averageLuminanceHistogramCS = null;

    //平均luminance 计算最终亮度
    [SerializeField] private ComputeShader averageLuminanceCalculationCS = null;

    //雨远处模型
    [SerializeField] private Mesh distantRainShaftsMesh = null;

    //雨远处材质
    [SerializeField] private Material distantRainShaftsMaterial = null;

    //月亮模型
    [SerializeField] private Mesh moonMesh = null;

    //月亮材质球
    [SerializeField] private Material moonMaterial = null;


    public Texture2D VignetteComplexMaskTexture =>
        vignetteComplexMaskTexture == null ? Texture2D.blackTexture : vignetteComplexMaskTexture;

    public ComputeShader AverageLuminanceHistogramCS => averageLuminanceHistogramCS;

    public ComputeShader AverageLuminanceCalculationCS => averageLuminanceCalculationCS;

    public Mesh DistantRainShaftsMesh => distantRainShaftsMesh;

    public Material DistantRainShaftsMaterial => distantRainShaftsMaterial;

    public Mesh MoonMesh => moonMesh;

    public Material MoonMaterial => moonMaterial;

}