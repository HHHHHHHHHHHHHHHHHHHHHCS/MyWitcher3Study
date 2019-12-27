using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/MyPostProcessingAsset", fileName = "MyPostProcessingAsset")]
public class MyPostProcessingAsset : ScriptableObject
{
    //暗角 复杂模式下 颜色遮罩
    [SerializeField] private Texture2D vignetteComplexMaskTexture;

    //平均luminance 颜色收集
    [SerializeField] private ComputeShader averageLuminanceHistogramCS;

    //平均luminance 计算最终亮度
    [SerializeField] private ComputeShader averageLuminanceCalculationCS;


    public Texture2D VignetteComplexMaskTexture =>
        vignetteComplexMaskTexture == null ? Texture2D.blackTexture : vignetteComplexMaskTexture;

    public ComputeShader AverageLuminanceHistogramCS => averageLuminanceHistogramCS;

    public ComputeShader AverageLuminanceCalculationCS => averageLuminanceCalculationCS;
}