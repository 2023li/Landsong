using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.EditorTools;
using Landsong.ECS.Authoring;
using Landsong.ECS.Presentation;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class GameMapWorkflowVerification
    {
        [MenuItem("Landsong/ECS/Verification/Map authoring workflow")]
        public static string Run()
        {
            GameMapWorkflow.RequireEditMode();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存场景再运行地图验证。");
            var log = new StringBuilder(); int checks = 0;
            void Check(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); checks++; log.AppendLine("PASS " + message); }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var folder = GameMapPaths.Root + "/_Verification_" + Guid.NewGuid().ToString("N");
            var menu = AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu);
            var menuJson = EditorJsonUtility.ToJson(menu);
            var gameBytes = File.ReadAllBytes(EcsSceneFlow.Game);
            var source = GameMapPaths.Root + "/Map_Test01/Map_Test01.unity";
            var original = AssetDatabase.LoadAssetAtPath<MapAsset>(GameMapPaths.Output(source, "_Map.asset"));
            var originalJson = EditorJsonUtility.ToJson(original);
            try
            {
                GameMapPaths.Folder(folder); var path = folder + "/WorkflowTest.unity";
                Check(AssetDatabase.CopyAsset(source, path), "Copy an existing source scene as an independent new map");
                var scene = EditorSceneManager.OpenScene(path);
                var content = GameMapWorkflow.All<MapContentAuthoring>(scene).Single();
                GameMapWorkflow.Initialize(content);
                Check(content.MapId != original.MapId && content.TargetMap != original && !content.IncludeInMenu, "Copied scene receives independent identity and runtime outputs");
                var map = content.TargetMap; var mapName = map.name; var sceneAsset = content.EntityScene; var children = content.transform.childCount;
                var config = File.ReadAllBytes(GameMapPaths.Source(path));
                GameMapWorkflow.Initialize(content);
                Check(content.TargetMap == map && content.EntityScene == sceneAsset && content.transform.childCount == children, "Repeated initialization preserves assets and object structure");
                Check(config.SequenceEqual(File.ReadAllBytes(GameMapPaths.Source(path))), "Repeated initialization preserves authored TWC data");
                GameMapWorkflow.Validate(content);
                Check(!map.IsBaked && !GameMapPaths.BakedMapPaths().Contains(AssetDatabase.GetAssetPath(map)), "Validation can use current source terrain before first bake; drafts stay outside runtime verification");
                GameMapWorkflow.Bake(content);
                Check(map.IsBaked && map.Cells.Length > 0 && map.InitialBuildings.Length > 0, "First bake creates terrain and initial buildings together");
                Check(AssetDatabase.GUIDToAssetPath(map.EntitySceneGuid) == AssetDatabase.GetAssetPath(sceneAsset), "Runtime scene resolved through its GUID reference");
                var target = EditorSceneManager.OpenPreviewScene(EcsMapIncrementalImport.TargetScene(map));
                try
                {
                    var world = GameMapWorkflow.All<GameWorldAuthoring>(target).Single();
                    Check(world.Map == map && world.Catalog == content.Catalog && world.Seed == content.Seed, "Generated entity scene carries exact authored settings");
                    var meshes = GameMapWorkflow.All<MeshFilter>(target).ToArray();
                    Check(meshes.Length > 0 && meshes.All(f => f.sharedMesh != null && f.sharedMesh.vertexCount > 0), "Generated terrain meshes exist and survive saving and reopening");
                }
                finally { EditorSceneManager.ClosePreviewScene(target); }
                content.IncludeInMenu = true; content.DisplayName = "地图流程验证"; GameMapWorkflow.Bake(content);
                Check(menu.Maps.Last().Id == content.MapId, "Successful bake registers a new map without reordering existing maps");
                content.IncludeInMenu = false; GameMapWorkflow.Bake(content);
                Check(menu.Maps.All(e => e.Id != content.MapId), "Unpublishing removes menu and host registration");
                Check(map.name == mapName, "Repeated baking preserves the runtime asset name");
                var before = EditorJsonUtility.ToJson(map); var bytes = File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map));
                menu.Maps = menu.Maps.Concat(new[] { new EcsMapMenuCatalog.Entry { Id = "_Missing_Test_Map" } }).ToArray();
                bool publishRejected = false;
                try { GameMapWorkflow.Bake(content); } catch (InvalidOperationException) { publishRejected = true; }
                Check(publishRejected && before == EditorJsonUtility.ToJson(map) && bytes.SequenceEqual(File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map))), "Publish failure rolls runtime map and entity scene back together");
                EditorJsonUtility.FromJsonOverwrite(menuJson, menu); EditorUtility.SetDirty(menu); AssetDatabase.SaveAssets();
                content.Previews[0].transform.position += new Vector3(100000, 0, 100000);
                bool rejected = false; try { GameMapWorkflow.Bake(content); } catch (InvalidOperationException) { rejected = true; }
                Check(rejected && before == EditorJsonUtility.ToJson(map) && bytes.SequenceEqual(File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map))), "Invalid building placement preserves last valid runtime outputs");
                Check(originalJson == EditorJsonUtility.ToJson(original), "Creating and baking a new map leaves the original map untouched");
                var source2 = GameMapPaths.Root + "/Map_Test2/Map_Test2.unity";
                var copy2 = folder + "/LargeMap.unity";
                Check(AssetDatabase.CopyAsset(source2, copy2), "Copy the full gameplay map for regeneration verification");
                var scene2 = EditorSceneManager.OpenScene(copy2);
                var large = GameMapWorkflow.All<MapContentAuthoring>(scene2).Single();
                var original2 = large.TargetMap;
                GameMapWorkflow.Initialize(large); GameMapWorkflow.Bake(large);
                Check(large.TargetMap.Cells.Length == original2.Cells.Length && large.TargetMap.Cells.Zip(original2.Cells, (a, b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b)).All(v => v), "Regenerating the full gameplay map preserves every runtime terrain cell");
                var emptyPath = folder + "/Empty.unity";
                var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); EditorSceneManager.SaveScene(empty, emptyPath);
                var root = new GameObject("地图根"); var fresh = root.AddComponent<MapContentAuthoring>();
                GameMapWorkflow.Initialize(fresh);
                Check(root.GetComponent<GiantGrey.TileWorldCreator.TileWorldCreatorManager>() != null && fresh.UnityGrid != null && fresh.BakeProfile != null && fresh.MapDefinition != null && fresh.TargetMap != null && fresh.EntityScene != null && !fresh.TargetMap.IsBaked && fresh.Previews.Length == 0,
                    "A blank saved scene initializes all scripts, assets and structure without requiring a core building");
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorJsonUtility.FromJsonOverwrite(menuJson, menu); EditorUtility.SetDirty(menu); AssetDatabase.SaveAssets();
                File.WriteAllBytes(EcsSceneFlow.Game, gameBytes); AssetDatabase.ImportAsset(EcsSceneFlow.Game, ImportAssetOptions.ForceUpdate);
                AssetDatabase.DeleteAsset(folder);
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/map-authoring-verification.txt", DateTimeOffset.Now.ToString("O") + "\n" + log);
            }
        }
    }
}
