using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InstancedMaterialProperties : MonoBehaviour
{
    private static MaterialPropertyBlock propertyBlock;
    private static readonly int colorID = Shader.PropertyToID("_Color");
    private static readonly int metallicID = Shader.PropertyToID("_Metallic");
    private static readonly int smoothnessID = Shader.PropertyToID("_Smoothness");
    private static readonly int emissionColorID = Shader.PropertyToID("_EmissionColor");


    [SerializeField] private Color color = Color.white;

    [SerializeField, Range(0f, 1f)] private float metallic = 0f;

    [SerializeField, Range(0f, 1f)] private float smoothness = 0.5f;

    [SerializeField] private float pulseEmissionFreqency;

    [SerializeField, ColorUsage(false, true)]
    private Color emissionColor = Color.black;

    private MeshRenderer _meshRenderer;

    private MeshRenderer MeshRenderer
    {
        get
        {
            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
            }

            return _meshRenderer;
        }
    }

    private void Awake()
    {
        OnValidate();

        if (pulseEmissionFreqency <= 0f)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        Color originalEmissionColor = emissionColor;
        emissionColor *= 0.5f +
                         0.5f * Mathf.Cos(2f * Mathf.PI * pulseEmissionFreqency * Time.time);
        OnValidate();
        //MeshRenderer.UpdateGIMaterials();
        //因为我们只改变了一个自发光颜色  所以没有必要全部重新刷新
        DynamicGI.SetEmissive(MeshRenderer, emissionColor);
        emissionColor = originalEmissionColor;
    }

    private void OnValidate()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        propertyBlock.SetColor(colorID, color);
        propertyBlock.SetFloat(metallicID, metallic);
        propertyBlock.SetFloat(smoothnessID, smoothness);
        propertyBlock.SetColor(emissionColorID, emissionColor);
        MeshRenderer.SetPropertyBlock(propertyBlock);
    }


}