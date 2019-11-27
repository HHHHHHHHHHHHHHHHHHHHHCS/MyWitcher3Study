using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class DoubleSidedMeshMenuItem : MonoBehaviour
{
    [MenuItem("Assets/Create/Double-Sided Mesh")]
    public static void MakeDoubleSidedMeshAsset()
    {
        var sourceMesh = Selection.activeObject as Mesh;
        if (sourceMesh == null)
        {
            Debug.Log("You must have a mesh asset selected.");
            return;
        }

        Mesh insideMesh = Object.Instantiate(sourceMesh);
        int[] triangles = insideMesh.triangles;
        System.Array.Reverse(triangles);
        insideMesh.triangles = triangles;

        Vector3[] normals = insideMesh.normals;
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = -normals[i];
        }

        insideMesh.normals = normals;

        //合并Mesh  第一个是子Mesh   第二个是使用原来的矩阵   第三个灯光信息
        var combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(
            new[]
            {
                new CombineInstance() {mesh = insideMesh},
                new CombineInstance() {mesh = sourceMesh}
            }, true, false, false);

        DestroyImmediate(insideMesh);

        AssetDatabase.CreateAsset(combinedMesh,
            System.IO.Path.Combine("Assets", sourceMesh.name + " Double-Sided.asset"));
    }
}