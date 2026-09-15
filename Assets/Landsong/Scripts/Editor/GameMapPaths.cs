using System;
using System.IO;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.GridSystem;
using UnityEditor;

namespace Landsong.EditorTools
{
    public static class GameMapPaths
    {
        public const string Root = "Assets/Landsong/GameMaps";
        public const string Template = Root + "/模板_TWC配置.asset";
        public const string Rules = Root + "/公共地形规则.asset";
        public const string Menu = Root + "/MapMenuCatalog.asset";
        public static string Data(string scenePath)
        {
            scenePath = scenePath.Replace('\\', '/');
            if (!scenePath.StartsWith(Root + "/", StringComparison.Ordinal) || !scenePath.EndsWith(".unity", StringComparison.Ordinal) || scenePath.Contains("/Generated/") || scenePath.Contains("/Source/"))
                throw new InvalidOperationException("请先将制图场景保存到 Assets/Landsong/GameMaps/<地图名>/ 下。");
            return Path.GetDirectoryName(scenePath).Replace('\\', '/') + "/" + Path.GetFileNameWithoutExtension(scenePath) + "Data";
        }
        public static string Source(string scenePath) => Data(scenePath) + "/Source/" + Path.GetFileNameWithoutExtension(scenePath) + "_TWC配置.asset";
        public static string Generated(string scenePath) => Data(scenePath) + "/Generated";
        public static string Output(string scenePath, string suffix) => Generated(scenePath) + "/" + Path.GetFileNameWithoutExtension(scenePath) + suffix;
        public static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        public static string[] BakedScenes() => AssetDatabase.FindAssets("t:MapAsset", new[] { Root })
            .Select(g => AssetDatabase.LoadAssetAtPath<MapAsset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(m => m != null && m.IsBaked).Select(m => AssetDatabase.GUIDToAssetPath(m.EntitySceneGuid)).OrderBy(p => p, StringComparer.Ordinal).ToArray();
        public static string[] BakedMapPaths() => AssetDatabase.FindAssets("t:MapAsset", new[] { Root })
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => AssetDatabase.LoadAssetAtPath<MapAsset>(p).IsBaked).ToArray();
    }
}
