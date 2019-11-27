using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

public class MyPipelineShaderPreprocessor : IPreprocessShaders
{
    private static MyPipelineShaderPreprocessor instance;

    private static ShaderKeyword cascadedShadowsHardKeyword = new ShaderKeyword("_CASCADED_SHADOWS_HARD");
    private static ShaderKeyword cascadedShadowsSoftKeyword = new ShaderKeyword("_CASCADED_SHADOWS_SOFT");
    private static ShaderKeyword lodCrossFadeKeyword = new ShaderKeyword("LOD_FADE_CROSSFADE");

    private MyPipelineAsset pipelineAsset;
    private int shaderVariantCount, strippedCount;

    private bool stripCascadedShadows, stripLODCrossFading;

    public int callbackOrder { get; } = 0;

    public MyPipelineShaderPreprocessor()
    {
        instance = this;
        pipelineAsset = GraphicsSettings.renderPipelineAsset as MyPipelineAsset;
        if (pipelineAsset == null)
        {
            return;
        }

        stripCascadedShadows = !pipelineAsset.HasShadowCascades;
        stripLODCrossFading = !pipelineAsset.HasLODCrossFading;
    }

    //构建处理资源的时候
    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (pipelineAsset == null)
        {
            return;
        }

        shaderVariantCount += data.Count;
        for (int i = 0; i < data.Count; i++)
        {
            if (Strip(data[i]))
            {
                data.RemoveAt(i--);
                strippedCount += 1;
            }
        }
    }

    //PostProcessBuild 构建完成的回调
    [PostProcessBuild(0)]
    private static void LogVariantCount(BuildTarget target, string path)
    {
        instance.LogVariantCount();
        instance = null;
    }

    private void LogVariantCount()
    {
        if (pipelineAsset == null)
        {
            return;
        }

        int finalCount = shaderVariantCount - strippedCount;
        int percentage = Mathf.RoundToInt(100f * finalCount / shaderVariantCount);

        Debug.Log($"Included {finalCount} shader variants out of {shaderVariantCount} ({percentage}%).");
    }

    private bool Strip(ShaderCompilerData data)
    {
        return (stripCascadedShadows && (
                    data.shaderKeywordSet.IsEnabled(cascadedShadowsHardKeyword) ||
                    data.shaderKeywordSet.IsEnabled(cascadedShadowsSoftKeyword)
                )) || (stripLODCrossFading && data.shaderKeywordSet.IsEnabled(lodCrossFadeKeyword));
    }
}