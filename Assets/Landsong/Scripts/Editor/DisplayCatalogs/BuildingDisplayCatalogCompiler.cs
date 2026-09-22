#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.Content;
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingDisplayCatalogCompiler
    {
        public const string Path = "Assets/Landsong/ECSContent/Catalogs/Generated/Display/BuildingDisplayCatalog.asset";
        public static string SourcePath => AssetDatabase.GetAssetPath(ContentAuthoringContext.Catalog<BuildingCatalogAsset>());
        public static BuildingDisplayCatalog Compile(BuildingCatalogAsset source, string outputPath = Path)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var result = AssetDatabase.LoadAssetAtPath<BuildingDisplayCatalog>(outputPath);
            if (result == null)
            {
                result = ScriptableObject.CreateInstance<BuildingDisplayCatalog>();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
                AssetDatabase.CreateAsset(result, outputPath);
            }

            result.Entries = source.Definitions.Select(d => new ContentDisplay { Id = d.Metadata.Id, Description = d.Metadata.Description, Icon = d.Metadata.Icon, }).ToArray();
            EditorUtility.SetDirty(result);
            return result;
        }

        public static void Validate()
        {
            var source = ContentAuthoringContext.Catalog<BuildingCatalogAsset>();
            var runtime = AssetDatabase.LoadAssetAtPath<BuildingDisplayCatalog>(Path);
            if (source == null || runtime == null || source.Definitions.Length != runtime.Entries.Length)
                throw new InvalidOperationException("Building显示目录缺失或未编译。");
            for (int i = 0; i < source.Definitions.Length; i++)
            {
                var a = source.Definitions[i];
                var b = runtime.Entries[i];
                if (b == null || a.Metadata.Id != b.Id || a.Metadata.Description != b.Description || a.Metadata.Icon != b.Icon)
                    throw new InvalidOperationException("Building显示目录已过期：" + a.Metadata.Id);
            }
        }
    }
}
#endif
