using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.EditorTools;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
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
            var log = new StringBuilder();
            int checks = 0;
            void Check(bool valid, string message)
            {
                if (!valid)
                    throw new InvalidOperationException(message);
                checks++;
                log.AppendLine("PASS " + message);
            }

            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var folder = GameMapPaths.Root + "/_Verification_" + Guid.NewGuid().ToString("N");
            var generatedMaps = new HashSet<string>(StringComparer.Ordinal);
            EcsMapMenuCatalog Menu() => AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu);
            var menuJson = EditorJsonUtility.ToJson(Menu());
            var gameBytes = File.ReadAllBytes(EcsSceneFlow.Game);
            var source = VerificationMap.SourceScene;
            MapAsset Original() => AssetDatabase.LoadAssetAtPath<MapAsset>(GameMapPaths.Output(source, "_Map.asset"));
            var originalJson = EditorJsonUtility.ToJson(Original());
            try
            {
                var template = AssetDatabase.LoadAssetAtPath<GameObject>(MapWorldComposition.TemplatePath);
                var templateContent = template == null ? null : template.GetComponent<GameContentSetAuthoring>();
                Check(template != null && template.GetComponents<GameWorldTemplateAuthoring>().Length == 1 && template.GetComponent<GameWorldMapAuthoring>() == null, "Shared world prefab contains one map-independent template and no map instance authoring");
                Check(templateContent != null && template.GetComponents<GameContentSetAuthoring>().Length == 1 && templateContent.Content != null, "Shared world prefab contains one central content-set reference");
                Check(typeof(NightSettingsAuthoring).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly).Length == 0, "Night authoring exposes no second serialized timing source");
                Check(template.GetComponent<NightSettingsAuthoring>().Settings.Equals(templateContent.Content.Night) && template.GetComponent<NightSettingsAuthoring>().DayReturn.Equals(templateContent.Content.DayReturn), "Night flow resolves only from the central content set");
                Check(template.GetComponents<MonoBehaviour>().All(component => component == null || !component.GetType().Name.EndsWith("CatalogAuthoring", StringComparison.Ordinal)), "Shared world prefab contains no per-domain catalog authoring components");
                Check(typeof(GameContentSetAsset).GetFields().Where(f => typeof(ScriptableObject).IsAssignableFrom(f.FieldType)).All(f => f.GetValue(templateContent.Content) != null), "Central content set assigns every domain catalog");
                GameMapPaths.Folder(folder);
                var path = folder + "/WorkflowTest.unity";
                Check(AssetDatabase.CopyAsset(source, path), "Copy an existing source scene as an independent new map");
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
                var content = GameMapWorkflow.All<MapContentAuthoring>(scene).Single();
                GameMapWorkflow.Initialize(content);
                generatedMaps.Add(content.MapId);
                Check(content.MapId != Original().MapId && content.TargetMap != Original() && Menu().Maps.All(e => e.Id != content.MapId), "Copied scene receives independent identity and runtime outputs without menu membership");
                var map = content.TargetMap;
                var mapName = map.name;
                var sceneAsset = content.EntityScene;
                var children = content.transform.childCount;
                var config = File.ReadAllBytes(GameMapPaths.Source(path));
                GameMapWorkflow.Initialize(content);
                Check(content.TargetMap == map && content.EntityScene == sceneAsset && content.transform.childCount == children, "Repeated initialization preserves assets and object structure");
                Check(config.SequenceEqual(File.ReadAllBytes(GameMapPaths.Source(path))), "Repeated initialization preserves authored TWC data");
                GameMapWorkflow.Validate(content);
                Check(!map.IsBaked && !GameMapPaths.BakedMapPaths().Contains(AssetDatabase.GetAssetPath(map)), "Validation can use current source terrain before first bake; drafts stay outside runtime verification");
                GameMapWorkflow.Bake(content);
                Check(map.IsBaked && map.Cells.Length > 0 && map.InitialBuildings.Length > 0, "First bake creates terrain and initial buildings together");
                Check(map.Cells.Any(c => c.EdgeZone && c.Exists && c.Traversable) && map.Cells.Where(c => c.EdgeZone).All(c => !c.Buildable), "Computed edge band forbids construction and retains valid entrances");
                var sourceTerrain = EcsMapIncrementalImport.ReadTerrain(content);
                Check(map.Cells.Zip(sourceTerrain.Cells, (baked, authored) => baked.EdgeZone == authored.EdgeZone && baked.Exists == authored.Exists && baked.Traversable == authored.Traversable).All(same => same), "Baking preserves authored water, blocked edges and traversable surfaces");
                Check(AssetDatabase.GUIDToAssetPath(map.EntitySceneGuid) == AssetDatabase.GetAssetPath(sceneAsset), "Runtime scene resolved through its GUID reference");
                var target = EditorSceneManager.OpenPreviewScene(EcsMapIncrementalImport.TargetScene(map));
                try
                {
                    var world = GameMapWorkflow.All<GameWorldMapAuthoring>(target).Single();
                    Check(GameMapWorkflow.All<GameWorldTemplateAuthoring>(target).Count() == 1 && GameMapWorkflow.All<GameContentSetAuthoring>(target).Count() == 1, "Generated entity scene contains one template state source and one content source");
                    Check(world.Map == map && world.GetComponent<GameContentSetAuthoring>().Content == templateContent.Content && world.GetComponent<GameContentSetAuthoring>().Content.Buildings == content.Buildings && world.Seed == content.Seed, "Generated entity scene carries exact map settings and the shared content set");
                    Check(world.GetComponent<NightSettingsAuthoring>().Settings.Equals(templateContent.Content.Night), "Generated entity scene references central night flow instead of owning map timing");
                    var meshes = GameMapWorkflow.All<MeshFilter>(target).ToArray();
                    Check(meshes.Length > 0 && meshes.All(f => f.sharedMesh != null && f.sharedMesh.vertexCount > 0), "Generated terrain meshes exist and survive saving and reopening");
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(target);
                }

                Check(Menu().Maps.All(e => e.Id != content.MapId), "Baking alone does not publish a draft map");
                var mapBeforeMenu = EditorJsonUtility.ToJson(map);
                var entitiesBeforeMenu = File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map));
                Menu().Maps = Menu().Maps.Concat(new[] { new EcsMapMenuCatalog.Entry() }).ToArray();
                content.DisplayName = "地图流程验证";
                GameMapWorkflow.AddToMapMenu(content);
                Check(Menu().Maps.All(e => !string.IsNullOrWhiteSpace(e.Id)), "Completely empty manual placeholder rows do not prevent registration");
                Check(Menu().Maps.Last().Id == content.MapId, "Menu button registers an already baked map without reordering existing maps");
                Check(mapBeforeMenu == EditorJsonUtility.ToJson(map) && entitiesBeforeMenu.SequenceEqual(File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map))), "Menu button does not rebake or modify runtime terrain and buildings");
                GameMapWorkflow.AddToMapMenu(content);
                Check(Menu().Maps.Count(e => e.Id == content.MapId) == 1, "Repeated menu registration is idempotent");
                content.DisplayName = "更新地图资料";
                GameMapWorkflow.Bake(content);
                Check(Menu().Maps.Single(e => e.Id == content.MapId).DisplayName == content.DisplayName, "Rebaking preserves membership and refreshes map metadata");
                Menu().Maps = Menu().Maps.Where(e => e.Id != content.MapId).ToArray();
                GameMapWorkflow.Bake(content);
                Check(Menu().Maps.All(e => e.Id != content.MapId), "A removed menu entry is not recreated by baking");
                Check(map.name == mapName, "Repeated baking preserves the runtime asset name");
                var before = EditorJsonUtility.ToJson(map);
                var bytes = File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map));
                Menu().Maps = Menu().Maps.Concat(new[] { new EcsMapMenuCatalog.Entry { Id = "_Missing_Test_Map" } }).ToArray();
                bool publishRejected = false;
                try
                {
                    GameMapWorkflow.Bake(content);
                }
                catch (InvalidOperationException)
                {
                    publishRejected = true;
                }

                Check(publishRejected && before == EditorJsonUtility.ToJson(map) && bytes.SequenceEqual(File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map))), "Publish failure rolls runtime map and entity scene back together");
                EditorJsonUtility.FromJsonOverwrite(menuJson, Menu());
                EditorUtility.SetDirty(Menu());
                AssetDatabase.SaveAssets();
                content.Previews[0].transform.position += new Vector3(100000, 0, 100000);
                bool rejected = false;
                try
                {
                    GameMapWorkflow.Bake(content);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected && before == EditorJsonUtility.ToJson(map) && bytes.SequenceEqual(File.ReadAllBytes(EcsMapIncrementalImport.TargetScene(map))), "Invalid building placement preserves last valid runtime outputs");
                Check(originalJson == EditorJsonUtility.ToJson(Original()), "Creating and baking a new map leaves the original map untouched");
                var source2 = VerificationMap.SourceScene;
                var copy2 = folder + "/LargeMap.unity";
                Check(AssetDatabase.CopyAsset(source2, copy2), "Copy the full gameplay map for regeneration verification");
                EditorSceneManager.CloseScene(scene, true);
                var scene2 = EditorSceneManager.OpenScene(copy2, OpenSceneMode.Additive);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene2);
                var large = GameMapWorkflow.All<MapContentAuthoring>(scene2).Single();
                var original2 = large.TargetMap;
                GameMapWorkflow.Initialize(large);
                generatedMaps.Add(large.MapId);
                GameMapWorkflow.Bake(large);
                Check(large.TargetMap.Cells.Length == original2.Cells.Length && large.TargetMap.Cells.Zip(original2.Cells, (a, b) => JsonUtility.ToJson(a) == JsonUtility.ToJson(b)).All(v => v), "Regenerating the full gameplay map preserves every runtime terrain cell");
                var emptyPath = folder + "/Empty.unity";
                EditorSceneManager.CloseScene(scene2, true);
                var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                EditorSceneManager.SaveScene(empty, emptyPath);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(empty);
                var root = new GameObject("地图根");
                var fresh = root.AddComponent<MapContentAuthoring>();
                GameMapWorkflow.Initialize(fresh);
                var menuBeforeDraft = EditorJsonUtility.ToJson(Menu());
                bool draftRejected = false;
                try
                {
                    GameMapWorkflow.AddToMapMenu(fresh);
                }
                catch (InvalidOperationException)
                {
                    draftRejected = true;
                }

                Check(draftRejected && menuBeforeDraft == EditorJsonUtility.ToJson(Menu()), "An unbaked map cannot be registered and leaves the menu unchanged");
                Check(fresh.transform.Find("出生区域") == null, "Initialization does not create obsolete authored spawn objects");
                var configuration = root.GetComponent<GiantGrey.TileWorldCreator.TileWorldCreatorManager>().configuration;
                Check(fresh.EdgeWidth == 5 && configuration.blueprintLayerFolders.SelectMany(f => f.blueprintLayers).All(l => l.layerName != "边缘区"), "New map configures base and edge width directly without a painted edge layer");
                Check(root.GetComponent<GiantGrey.TileWorldCreator.TileWorldCreatorManager>() != null && fresh.UnityGrid != null && fresh.BakeProfile != null && fresh.MapDefinition != null && fresh.TargetMap != null && fresh.EntityScene != null && !fresh.TargetMap.IsBaked && fresh.Previews.Length == 0, "A blank saved scene initializes all scripts, assets and structure without requiring a core building");
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine("FAIL " + error);
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/map-authoring-verification.txt", DateTimeOffset.Now.ToString("O") + "\n" + log);
                for (int i = UnityEngine.SceneManagement.SceneManager.sceneCount - 1; i >= 0; i--)
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                    if (scene.path.StartsWith(folder + "/", StringComparison.Ordinal))
                        EditorSceneManager.CloseScene(scene, true);
                }

                EditorJsonUtility.FromJsonOverwrite(menuJson, Menu());
                EditorUtility.SetDirty(Menu());
                AssetDatabase.SaveAssets();
                if (!gameBytes.SequenceEqual(File.ReadAllBytes(EcsSceneFlow.Game)))
                {
                    var temporary = EcsSceneFlow.Game + ".verification.tmp";
                    File.WriteAllBytes(temporary, gameBytes);
                    File.Replace(temporary, EcsSceneFlow.Game, null);
                    AssetDatabase.ImportAsset(EcsSceneFlow.Game, ImportAssetOptions.ForceUpdate);
                }

                AssetDatabase.DeleteAsset(folder);
                foreach (var mapId in generatedMaps)
                    AssetDatabase.DeleteAsset(RuntimeMapArtifacts.MapRoot(mapId));
                if (previous.IsValid() && previous.isLoaded)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/map-authoring-verification.txt", DateTimeOffset.Now.ToString("O") + "\n" + log);
            }
        }
    }
}
