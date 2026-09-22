#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    // Owns export paths and Unity asset replacement; geometry code does not choose storage locations.
    internal static class LS_BuildingViewAssetStore
    {
        internal static (string BuildingFolder, string MeshRoot, string PrefabPath) ResolveOutputPaths(LS_BuildingViewNamingConfig namingConfig, LS_BuildingViewNamingConfig.ResolvedView resolvedView)
        {
            var buildingName = SanitizeFileName(resolvedView.BuildingFolderName);
            var prefabName = SanitizeFileName(resolvedView.PrefabBaseName);
            var buildingFolder = $"{NormalizeAssetPath(namingConfig.ViewOutputRoot).TrimEnd('/')}/{buildingName}/Generated";
            var meshRoot = $"{buildingFolder}/OptimizedMeshes/{prefabName}";
            return (buildingFolder, meshRoot, $"{buildingFolder}/{prefabName}.prefab");
        }

        internal static Mesh SaveMeshAsset(Mesh mesh, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        internal static GameObject SaveOrReplacePrefab(GameObject root, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var success);
            return success ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : null;
        }

        internal static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException($"无效的资源目录：{folder}");
            }

            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        internal static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        internal static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (value ?? string.Empty).Select(character => invalid.Contains(character) || character == '/' || character == '\\' ? '_' : character).ToArray();
            var result = new string (chars).Trim().Trim('.');
            return string.IsNullOrWhiteSpace(result) ? "Building" : result;
        }
    }
}
#endif
