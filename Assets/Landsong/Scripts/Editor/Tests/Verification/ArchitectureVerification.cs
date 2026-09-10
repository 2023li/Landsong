#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.GridSystem;
using Landsong.VisualSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class ArchitectureVerification
    {
        [MenuItem("Landsong/ECS/Verification/Architecture")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var report = new StringBuilder(); var assertions = 0;
            void Check(bool ok, string label) { if (!ok) throw new InvalidOperationException("FAIL " + label); assertions++; report.AppendLine("PASS " + label); }
            try
            {
                const string ecsScripts = "Assets/Landsong/Scripts/ECS";
                Check(AssetDatabase.IsValidFolder(ecsScripts) && !Directory.Exists("Assets/Landsong/ECS") && !File.Exists("Assets/Landsong/ECS.meta"), "Single script root: ECS lives under Scripts");
                var assemblies = new[] { "Landsong.ECS.asmdef", "Presentation/Landsong.ECS.Presentation.asmdef", "Editor/Landsong.ECS.Editor.asmdef" };
                Check(assemblies.All(path => AssetDatabase.LoadMainAssetAtPath(ecsScripts + "/" + path) != null), "All three ECS assembly definitions preserved");
                var scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { ecsScripts }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                Check(scripts.Length > 0 && scripts.All(path => UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(path) ==
                    (path.StartsWith(ecsScripts + "/Editor/", StringComparison.Ordinal) ? "Landsong.ECS.Editor.dll" :
                     path.StartsWith(ecsScripts + "/Presentation/", StringComparison.Ordinal) ? "Landsong.ECS.Presentation.dll" : "Landsong.ECS.dll")), "Scripts retain their assembly boundaries");
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes" }).Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.StartsWith("Assets/Landsong/Scenes/EntityMaps/", StringComparison.Ordinal) || Path.GetDirectoryName(path)?.Replace('\\', '/') == "Assets/Landsong/Scenes"))
                {
                    var scene = EditorSceneManager.OpenPreviewScene(path);
                    try { Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Formal scene scripts intact: " + path); }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Landsong" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid); var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Check(root != null && root.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Prefab scripts intact: " + path);
                }
                foreach (var guid in AssetDatabase.FindAssets("t:MapAsset", new[] { "Assets/Landsong/ECSContent/Maps" }))
                {
                    var map = AssetDatabase.LoadAssetAtPath<MapAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    var source = EditorSceneManager.OpenPreviewScene(EcsMapIncrementalImport.SourceScene(map));
                    try
                    {
                        Check(source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "TWC source scripts intact: " + map.MapId);
                        var content = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MapContentAuthoring>(true)).Single();
                        Check(content.TargetMap == map, "TWC source binds native map: " + map.MapId);
                        var terrain = EcsMapIncrementalImport.ReadTerrain(content);
                        Check(terrain.Cells.Length == map.Cells.Length && terrain.Min == map.Min && terrain.Size == map.Size && terrain.Origin == map.Origin && Mathf.Approximately(terrain.CellSize, map.CellSize), "TWC coordinate contract unchanged: " + map.MapId);
                        Check(terrain.Cells.Zip(map.Cells, (a, b) => a.Exists == b.Exists && a.Buildable == b.Buildable && a.Traversable == b.Traversable && a.Elevation == b.Elevation && a.Surface == b.Surface && a.Terrain == b.Terrain && Mathf.Approximately(a.Height, b.Height)).All(equal => equal), "All TWC terrain cells preserved: " + map.MapId);
                        EcsMapIncrementalImport.ValidateInitialBuildings(map, terrain);
                        Check(content.TryCollectInitialBuildings(out var initial, out var error), "Native initial previews valid: " + map.MapId + " " + error);
                        string Key(InitialSource item) => item.Definition + ":" + item.Cell + ":" + item.Rotation + ":" + item.Level;
                        Check(initial.Select(Key).OrderBy(s => s).SequenceEqual(map.InitialBuildings.Select(Key).OrderBy(s => s)), "Initial definitions/positions/rotations/levels preserved: " + map.MapId);
                        var before = JsonUtility.ToJson(map); var invalid = (CellSource[])terrain.Cells.Clone();
                        var first = map.InitialBuildings[0].Cell - terrain.Min; invalid[first.y * terrain.Size.x + first.x].Exists = false;
                        var rejected = false;
                        try { EcsMapIncrementalImport.ValidateInitialBuildings(map, new EcsMapIncrementalImport.TerrainSnapshot { Cells = invalid, Min = terrain.Min, Size = terrain.Size, Origin = terrain.Origin, CellSize = terrain.CellSize }); }
                        catch (InvalidOperationException) { rejected = true; }
                        Check(rejected && before == JsonUtility.ToJson(map), "Invalid terrain rejected without changing native map: " + map.MapId);
                    }
                    finally { EditorSceneManager.ClosePreviewScene(source); }
                }
                var naming = AssetDatabase.LoadAssetAtPath<LS_BuildingViewNamingConfig>(LS_BuildingViewNamingConfig.DefaultAssetPath);
                Check(naming != null && naming.Entries.All(e => e.Definition != null), "Art naming bindings use native definitions");
                Check(naming.TryValidate(out var namingError), "Art naming validation: " + namingError);
                foreach (var path in new[] { "Assets/_a资源整理/建筑制作预览.unity", "Assets/_a资源整理/建筑拍照.unity" })
                {
                    var scene = EditorSceneManager.OpenPreviewScene(path);
                    try { Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Art scene scripts intact: " + path); }
                    finally { EditorSceneManager.ClosePreviewScene(scene); }
                }
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error) { report.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/architecture-verification.txt", report.ToString()); }
        }
    }
}
#endif
