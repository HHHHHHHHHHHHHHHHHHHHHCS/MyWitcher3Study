using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class LitShaderGUI : ShaderGUI
{
    private enum ClipMode
    {
        Off,
        On,
        Shadows
    }

    private MaterialEditor editor;
    private Object[] materials;
    private MaterialProperty[] properties;

    private bool showPresets;

    private CullMode Cull
    {
        set => FindProperty("_Cull", properties).floatValue = (float) value;
    }

    private BlendMode SrcBlend
    {
        set => FindProperty("_SrcBlend", properties).floatValue = (float) value;
    }

    private BlendMode DstBlend
    {
        set => FindProperty("_DstBlend", properties).floatValue = (float) value;
    }

    private bool ZWrite
    {
        set => FindProperty("_ZWrite", properties).floatValue = value ? 1 : 0;
    }

    private ClipMode Clipping
    {
        set
        {
            FindProperty("_Clipping", properties).floatValue = (float) value;
            SetKeywordEnabled("_CLIPPING_OFF", value == ClipMode.Off);
            SetKeywordEnabled("_CLIPPING_ON", value == ClipMode.On);
            SetKeywordEnabled("_CLIPPING_SHADOWS", value == ClipMode.Shadows);
        }
    }

    private bool ReceiveShadows
    {
        set
        {
            FindProperty("_ReceiveShadows", properties).floatValue = value ? 1 : 0;
            SetKeywordEnabled("_RECEIVE_SHADOWS", value);
        }
    }

    private RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int) value;
            }
        }
    }

    private bool PremultiplyAlpha
    {
        set
        {
            FindProperty("_PremulAlpha", properties).floatValue
                = value ? 1 : 0;
            SetKeywordEnabled("_PREMULTIPLY_ALPHA", value);
        }
    }


    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] _properties)
    {
        base.OnGUI(materialEditor, _properties);

        editor = materialEditor;
        materials = materialEditor.targets;
        properties = _properties;

        CastShadowsToggle();

        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in editor.targets)
            {
                m.globalIlluminationFlags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }

        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            ClipDoubleSidedPreset();
            FadePreset();
            FadeWithShadowsPreset();
            TransparentPreset();
            TransparentWithPresetPreset();
        }
    }

    private void SetPassEnabled(string pass, bool enabled)
    {
        foreach (Material m in materials)
        {
            m.SetShaderPassEnabled(pass, enabled);
        }
    }

    private bool? IsPassEnabled(string pass)
    {
        bool enabled = ((Material) materials[0]).GetShaderPassEnabled(pass);
        for (int i = 0; i < materials.Length; i++)
        {
            if (enabled != ((Material) materials[i]).GetShaderPassEnabled(pass))
            {
                return null;
            }
        }

        return enabled;
    }

    private void SetKeywordEnabled(string keyword, bool enabled)
    {
        if (enabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        }
        else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    private void CastShadowsToggle()
    {
        bool? enabled = IsPassEnabled("ShadowCaster");
        if (!enabled.HasValue)
        {
            EditorGUI.showMixedValue = true;
            enabled = false;
        }

        EditorGUI.BeginChangeCheck();
        enabled = EditorGUILayout.Toggle("Cast Shadows", enabled.Value);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo("Cast Shadows");
            SetPassEnabled("ShadowCaster", enabled.Value);
        }

        EditorGUI.showMixedValue = false;
    }

    private void OpaquePreset()
    {
        if (!GUILayout.Button("Opaque"))
        {
            return;
        }

        editor.RegisterPropertyChangeUndo("Opaque Preset");
        Clipping = ClipMode.Off;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.Geometry;
    }

    private void ClipPreset()
    {
        if (!GUILayout.Button("Clip"))
        {
            return;
        }

        editor.RegisterPropertyChangeUndo("Clip Preset");
        Clipping = ClipMode.On;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        PremultiplyAlpha = false;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.AlphaTest;
    }

    void ClipDoubleSidedPreset()
    {
        if (!GUILayout.Button("Clip Double-Sided"))
        {
            return;
        }

        editor.RegisterPropertyChangeUndo("Clip Double-Sided Preset");
        Clipping = ClipMode.On;
        Cull = CullMode.Off;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.Zero;
        ZWrite = true;
        ReceiveShadows = true;
        PremultiplyAlpha = false;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.AlphaTest;
    }

    void FadePreset()
    {
        if (!GUILayout.Button("Fade"))
        {
            return;
        }

        editor.RegisterPropertyChangeUndo("Fade Preset");
        Clipping = ClipMode.Off;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.SrcAlpha;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = false;
        PremultiplyAlpha = false;
        SetPassEnabled("ShadowCaster", false);
        RenderQueue = RenderQueue.Transparent;
    }

    void FadeWithShadowsPreset()
    {
        if (!GUILayout.Button("Fade with Shadows"))
        {
            return;
        }

        editor.RegisterPropertyChangeUndo("Fade with Shadows Preset");
        Clipping = ClipMode.Shadows;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.SrcAlpha;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = true;
        PremultiplyAlpha = false;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.Transparent;
    }

    void TransparentPreset()
    {
        if (!GUILayout.Button("Transparent"))
        {
            return;
        }

        editor.RegisterPropertyChangeUndo("Transparent Preset");
        Clipping = ClipMode.Off;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = false;
        PremultiplyAlpha = true;
        SetPassEnabled("ShadowCaster", false);
        RenderQueue = RenderQueue.Transparent;
    }

    void TransparentWithPresetPreset()
    {
        if (!GUILayout.Button("Transparent with Shadows"))
        {
            return;
        }

        editor.RegisterPropertyChangeUndo("Transparent with Shadows Preset");
        Clipping = ClipMode.Shadows;
        Cull = CullMode.Back;
        SrcBlend = BlendMode.One;
        DstBlend = BlendMode.OneMinusSrcAlpha;
        ZWrite = false;
        ReceiveShadows = true;
        PremultiplyAlpha = true;
        SetPassEnabled("ShadowCaster", true);
        RenderQueue = RenderQueue.Transparent;
    }
}