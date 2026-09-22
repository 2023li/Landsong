using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CombineSelectedMeshes
{
    [MenuItem("Landsong/Mesh/合并选中建筑网格")]
    private static void CombineSelected()
    {
        GameObject root = Selection.activeGameObject;

        if (root == null)
        {
            EditorUtility.DisplayDialog(
                "合并网格",
                "请先在 Hierarchy 中选择建筑根对象。",
                "确定");
            return;
        }

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);

        if (meshFilters.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "合并网格",
                "选中对象的子级中没有找到 MeshFilter。",
                "确定");
            return;
        }

        // 按材质分组，避免合并后材质错乱。
        var materialGroups =
            new Dictionary<Material, List<CombineInstance>>();

        MeshRenderer firstRenderer = null;

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
                continue;

            MeshRenderer renderer = meshFilter.GetComponent<MeshRenderer>();

            if (renderer == null || !renderer.enabled)
                continue;

            firstRenderer ??= renderer;

            Material[] materials = renderer.sharedMaterials;
            Mesh mesh = meshFilter.sharedMesh;

            Matrix4x4 localToRoot =
                root.transform.worldToLocalMatrix *
                meshFilter.transform.localToWorldMatrix;

            for (int subMeshIndex = 0;
                 subMeshIndex < mesh.subMeshCount;
                 subMeshIndex++)
            {
                Material material =
                    subMeshIndex < materials.Length
                        ? materials[subMeshIndex]
                        : null;

                if (!materialGroups.TryGetValue(
                        material,
                        out List<CombineInstance> group))
                {
                    group = new List<CombineInstance>();
                    materialGroups.Add(material, group);
                }

                group.Add(new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = subMeshIndex,
                    transform = localToRoot
                });
            }
        }

        if (materialGroups.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "合并网格",
                "没有找到可合并的已启用 MeshRenderer。",
                "确定");
            return;
        }

        // 先把使用同一材质的网格合成一个网格。
        var materialMeshes = new List<Mesh>();
        var finalCombine = new List<CombineInstance>();
        var finalMaterials = new List<Material>();

        foreach (var pair in materialGroups)
        {
            Mesh materialMesh = new Mesh
            {
                name = $"{root.name}_{GetSafeMaterialName(pair.Key)}",
                indexFormat = IndexFormat.UInt32
            };

            materialMesh.CombineMeshes(
                pair.Value.ToArray(),
                true,   // 同材质合为一个 SubMesh
                true,   // 应用各零件 Transform
                false);

            materialMeshes.Add(materialMesh);
            finalMaterials.Add(pair.Key);

            finalCombine.Add(new CombineInstance
            {
                mesh = materialMesh,
                subMeshIndex = 0,
                transform = Matrix4x4.identity
            });
        }

        // 最终生成一个 Mesh，不同材质保留为不同 SubMesh。
        Mesh combinedMesh = new Mesh
        {
            name = $"{root.name}_Combined",
            indexFormat = IndexFormat.UInt32
        };

        combinedMesh.CombineMeshes(
            finalCombine.ToArray(),
            false,  // 保留不同材质对应的 SubMesh
            false,
            false);

        combinedMesh.RecalculateBounds();

        foreach (Mesh temporaryMesh in materialMeshes)
            Object.DestroyImmediate(temporaryMesh);

        string folder = "Assets/CombinedMeshes";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "CombinedMeshes");

        string assetPath = AssetDatabase.GenerateUniqueAssetPath(
            Path.Combine(folder, $"{root.name}_Combined.asset"));

        AssetDatabase.CreateAsset(combinedMesh, assetPath);
        AssetDatabase.SaveAssets();

        GameObject combinedObject =
            new GameObject($"{root.name}_Combined");

        Undo.RegisterCreatedObjectUndo(
            combinedObject,
            "创建合并网格");

        combinedObject.transform.SetParent(root.transform, false);

        MeshFilter combinedFilter =
            combinedObject.AddComponent<MeshFilter>();

        MeshRenderer combinedRenderer =
            combinedObject.AddComponent<MeshRenderer>();

        combinedFilter.sharedMesh = combinedMesh;
        combinedRenderer.sharedMaterials = finalMaterials.ToArray();

        if (firstRenderer != null)
        {
            combinedRenderer.shadowCastingMode =
                firstRenderer.shadowCastingMode;

            combinedRenderer.receiveShadows =
                firstRenderer.receiveShadows;

            combinedRenderer.lightProbeUsage =
                firstRenderer.lightProbeUsage;

            combinedRenderer.reflectionProbeUsage =
                firstRenderer.reflectionProbeUsage;
        }

        // 只关闭原始渲染器，不删除原始对象和碰撞体。
        foreach (MeshFilter meshFilter in meshFilters)
        {
            MeshRenderer renderer =
                meshFilter.GetComponent<MeshRenderer>();

            if (renderer != null && renderer != combinedRenderer)
                Undo.RecordObject(renderer, "关闭原始渲染器");

            if (renderer != null && renderer != combinedRenderer)
                renderer.enabled = false;
        }

        Selection.activeGameObject = combinedObject;

        Debug.Log(
            $"已合并 {meshFilters.Length} 个 MeshFilter，" +
            $"生成 {finalMaterials.Count} 个材质 SubMesh。\n" +
            $"网格位置：{assetPath}",
            combinedObject);
    }

    private static string GetSafeMaterialName(Material material)
    {
        return material != null
            ? material.name.Replace("/", "_")
            : "NoMaterial";
    }

    [MenuItem(
        "Tools/Mesh/合并选中建筑网格",
        true)]
    private static bool ValidateCombineSelected()
    {
        return Selection.activeGameObject != null;
    }
}
