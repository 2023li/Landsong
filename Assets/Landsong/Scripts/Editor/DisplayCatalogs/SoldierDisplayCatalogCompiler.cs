#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.Content;
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class SoldierDisplayCatalogCompiler
    {
        public const string Path = "Assets/Landsong/ECSContent/Catalogs/Generated/Display/SoldierDisplayCatalog.asset";
        public static string SourcePath => AssetDatabase.GetAssetPath(ContentAuthoringContext.Catalog<SoldierCatalogAsset>());
        public static SoldierDisplayCatalog Compile(SoldierCatalogAsset source, string outputPath = Path)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var result = AssetDatabase.LoadAssetAtPath<SoldierDisplayCatalog>(outputPath);
            if (result == null)
            {
                result = ScriptableObject.CreateInstance<SoldierDisplayCatalog>();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outputPath));
                AssetDatabase.CreateAsset(result, outputPath);
            }

            result.Entries = source.Definitions.Select(d => new ContentDisplay { Id = d.Metadata.Id, Description = d.Metadata.Description, Icon = d.Metadata.Icon, }).ToArray();
            EditorUtility.SetDirty(result);
            return result;
        }

        public static void Validate()
        {
            var source = ContentAuthoringContext.Catalog<SoldierCatalogAsset>();
            var runtime = AssetDatabase.LoadAssetAtPath<SoldierDisplayCatalog>(Path);
            if (source == null || runtime == null || source.Definitions.Length != runtime.Entries.Length)
                throw new InvalidOperationException("Soldier显示目录缺失或未编译。");
            for (int i = 0; i < source.Definitions.Length; i++)
            {
                var a = source.Definitions[i];
                var b = runtime.Entries[i];
                if (b == null || a.Metadata.Id != b.Id || a.Metadata.Description != b.Description || a.Metadata.Icon != b.Icon)
                    throw new InvalidOperationException("Soldier显示目录已过期：" + a.Metadata.Id);
            }
        }
    }
}
#endif
