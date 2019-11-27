using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.Rendering;
using UnityEngine;

[CustomEditor(typeof(MyPipelineAsset))]
public class MyPipelineAssetEditor : Editor
{
    private SerializedProperty shadowCascades;
    private SerializedProperty twoCascadeSplit;
    private SerializedProperty fourCascadesSplit;

    private void OnEnable()
    {
        shadowCascades = serializedObject.FindProperty("shadowCascades");
        twoCascadeSplit = serializedObject.FindProperty("twoCascadesSplit");
        fourCascadesSplit = serializedObject.FindProperty("fourCascadesSplit");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        switch (shadowCascades.enumValueIndex)
        {
            case 0:
                return;
            case 1:
                CoreEditorUtils.DrawCascadeSplitGUI<float>( ref twoCascadeSplit);
                break;
            case 2:
                CoreEditorUtils.DrawCascadeSplitGUI<Vector3>(ref fourCascadesSplit);
                break;
        }

        serializedObject.ApplyModifiedProperties();
    }
}