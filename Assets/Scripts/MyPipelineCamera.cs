using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//ImageEffectAllowedInSceneView 把当前脚本拷贝给预览场景的摄像机
//Camera Tag必须是MainCamera
[ImageEffectAllowedInSceneView, RequireComponent(typeof(Camera))]
public class MyPipelineCamera : MonoBehaviour
{
    [SerializeField] private MyPostProcessingStack postProcessingStack = null;

    public MyPostProcessingStack PostProcessingStack => postProcessingStack;
}