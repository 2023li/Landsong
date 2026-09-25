#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.Content;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Landsong.EditorTools;
using Landsong.VisualSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    public static partial class ContentAuthoringWorkflowVerification
    {
        [MenuItem("Landsong/ECS/Verification/Content authoring workflow")]
        public static string Run()
        {
            using var staging = new ContentCreationScene();
            var log = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            int count = 0;
            void Check(bool valid, string message)
            {
                if (!valid) throw new InvalidOperationException(message);
                count++;
                log.AppendLine("PASS " + message);
            }
            void Reject(Action action, string message)
            {
                bool failed = false;
                try { action(); } catch (InvalidOperationException) { failed = true; }
                Check(failed, message);
            }
            string output = "Assets/Landsong/_Verification_Content_" + Guid.NewGuid().ToString("N");
            var formal = ContentAuthoringContext.Content();
            string original = EditorJsonUtility.ToJson(formal.Buildings);
            var content = Object.Instantiate(formal);
            content.Buildings = Object.Instantiate(formal.Buildings);
            content.Soldiers = Object.Instantiate(formal.Soldiers);
            content.Heroes = Object.Instantiate(formal.Heroes);
            content.Enemies = Object.Instantiate(formal.Enemies);
            content.Technologies = Object.Instantiate(formal.Technologies);
            try
            {
                Check(ContentAuthoringContext.Catalog<BuildingCatalogAsset>() == formal.Buildings, "Registration follows the world content set");
                Check(BuildingDisplayCatalogCompiler.SourcePath == AssetDatabase.GetAssetPath(formal.Buildings), "Display source follows the same catalog");
                ContentAuthoringValidation.ValidateCurrent();
                Check(true, "Formal building structures and legacy visual boundary validate");
                var editingScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                bool sceneWasDirty = editingScene.isDirty;
                string dirtyTrace = "";
                void CaptureDirty(UnityEngine.SceneManagement.Scene changed)
                {
                    if (changed == editingScene) dirtyTrace = Environment.StackTrace;
                }
                EditorSceneManager.sceneDirtied += CaptureDirty;
                BuildingDefinitionAsset building;
                try { building = BuildingAuthoringWorkflow.Create(content, "verification.building", "新建筑验证", output); }
                finally { EditorSceneManager.sceneDirtied -= CaptureDirty; }
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene() == editingScene && editingScene.isDirty == sceneWasDirty,
                    "Creating a building preserves the active scene and its existing dirty state" + (editingScene.isDirty != sceneWasDirty ? "\n" + dirtyTrace : ""));
                Check(content.Buildings.Definitions.Last() == building, "Blank building creation registers exactly its new definition");
                ConfigureBuildingLevels(building);
                var display = BuildingDisplayCatalogCompiler.Compile(content.Buildings, output + "/BuildingDisplayCatalog.asset");
                Check(display.Entries.Last().Id == building.Metadata.Id, "New building compiles into an isolated display catalog");
                using (var creation = new ContentCreationAssets())
                {
                    var technology = ScriptableObject.CreateInstance<TechnologyDefinitionAsset>();
                    technology.Metadata.Id = "verification.technology";
                    technology.Metadata.Name = "新科技验证";
                    technology.ResearchPointCost = 5;
                    technology.HasTreePosition = true;
                    technology.TreePosition = new Vector2(400, 200);
                    technology.Rewards.Blueprints = new[] { new BlueprintRewardSource { Building = building, GrantedLevel = 2 } };
                    creation.Create(technology, output + "/Definitions/Technology/verification.technology.asset");
                    ContentAuthoringRegistration.Register(content.Technologies, technology);
                    ContentAuthoringRegistration.Register(content.Technologies, technology);
                    Check(content.Technologies.Definitions.Count(d => d == technology) == 1, "Technology registration is idempotent");
                    Reject(() => ContentAuthoringRegistration.Register(content.Buildings, technology), "Registration rejects a definition from another domain");
                    using (var compiled = TechnologyCatalogBaking.Compile(content))
                        Check(compiled.Value.Definitions.Length == content.Technologies.Definitions.Length, "New technology and its new-building reward compile together");
                    var technologyDisplay = TechnologyDisplayCatalogCompiler.Compile(content.Technologies, output + "/TechnologyDisplayCatalog.asset");
                    Check(technologyDisplay.Entries.Last().Id == technology.Metadata.Id, "New technology enters the generated tree display");
                    creation.Complete();
                }
                var id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(building));
                var prefabId = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(building.Prefab));
                building.MaximumDurability = 173;
                EditorUtility.SetDirty(building);
                AssetDatabase.SaveAssetIfDirty(building);
                Check(BuildingAuthoringWorkflow.Create(content, building.Metadata.Id, "重复执行", output) == building
                    && building.MaximumDurability == 173 && content.Buildings.Definitions.Count(d => d == building) == 1
                    && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(building)) == id
                    && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(building.Prefab)) == prefabId,
                    "Repeated creation preserves GUIDs, registration and manual tuning");
                Reject(() => BuildingAuthoringWorkflow.Create(content, "../escape", "越界", output), "Unsafe identity rejected before creating paths");
                Reject(() => BuildingAuthoringWorkflow.Create(content, formal.Buildings.Definitions[0].Metadata.Id, "重复", output), "Existing foreign identity cannot be overwritten");
                var clone = staging.Instantiate(building.Prefab);
                try
                {
                    clone.GetComponent<BuildingPlacementPreviewBinding>().Configure(1, "");
                    Check(clone.GetComponentsInChildren<MeshRenderer>(true).Any(r => r.enabled), "New building's placement preview is visibly bound");
                    clone.GetComponentInChildren<EntityVisualAuthoring>().Owner = clone.transform.GetChild(0).gameObject;
                    Reject(() => BuildingAuthoringWorkflow.ValidateStructure(clone, building), "Wrong renderer owner is diagnosed");
                    clone.GetComponentInChildren<EntityVisualAuthoring>().Owner = clone;
                    Object.DestroyImmediate(clone.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SelectionAnchor").gameObject);
                    Reject(() => BuildingAuthoringWorkflow.ValidateStructure(clone, building), "Missing selection anchor is diagnosed");
                }
                finally { Object.DestroyImmediate(clone); }
                Check(typeof(WorldPresentationView).GetField("Visuals") == null, "New units cannot register through legacy string mappings");
                VerifyUnits(content, output, Check, Reject);
                Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene() == editingScene && editingScene.isDirty == sceneWasDirty,
                    "Unit creation and failures preserve the active scene and its existing dirty state");
                VerifyBuildingRuntime(content, building, Check);
                Check(EditorJsonUtility.ToJson(formal.Buildings) == original, "Creation verification leaves the formal catalog unchanged");
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            catch (Exception error) { log.AppendLine("FAIL " + error); throw; }
            finally
            {
                Object.DestroyImmediate(content.Buildings);
                Object.DestroyImmediate(content.Soldiers);
                Object.DestroyImmediate(content.Heroes);
                Object.DestroyImmediate(content.Enemies);
                Object.DestroyImmediate(content.Technologies);
                Object.DestroyImmediate(content);
                if (AssetDatabase.IsValidFolder(output)) AssetDatabase.DeleteAsset(output);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/content-authoring-workflow-verification.txt", log.ToString());
            }
        }

        static void ConfigureBuildingLevels(BuildingDefinitionAsset definition)
        {
            definition.MaximumLevel = 2;
            definition.Capabilities.Upgrade.Enabled = true;
            definition.Capabilities.Research.Enabled = true;
            definition.Capabilities.Research.Levels = new[]
            {
                new BuildingResearchLevelSource { Level = 1, PointsPerTurn = 3 },
                new BuildingResearchLevelSource { Level = 2, PointsPerTurn = 7 }
            };
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            var path = AssetDatabase.GetAssetPath(definition.Prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var second = Object.Instantiate(root.GetComponentInChildren<BuildingVisualSlotAuthoring>().gameObject, root.transform);
                second.name = "Operational_L2";
                second.GetComponent<BuildingVisualSlotAuthoring>().Level = 2;
                foreach (var visual in root.GetComponentsInChildren<EntityVisualAuthoring>(true)) visual.Owner = root;
                BuildingPreviewBindingAuthoring.Rebind(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            BuildingAuthoringWorkflow.Validate(definition);
        }

        static void VerifyBuildingRuntime(GameContentSetAsset content, BuildingDefinitionAsset definition, Action<bool, string> check)
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("New content workflow", WorldFlags.Game);
            try
            {
                foreach (var rootObject in scene.GetRootGameObjects())
                    foreach (var authoring in rootObject.GetComponentsInChildren<GameContentSetAuthoring>(true))
                        authoring.Content = content;
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var id = BuildingDefinitions.Find(em, root, new FixedString128Bytes(definition.Metadata.Id));
                check(id.IsValid, "Created building bakes into the actual world catalog");
                var technology = TechnologyDefinitions.Find(em, root, "verification.technology");
                FeatureUnlocks.Unlock(em, root, FeatureDefinitions.Find(em, root, ResearchOps.FeatureId));
                em.SetComponentData(root, new ResearchState { Points = 5 });
                check(GameRequestExecution.Execute(em, root, new QueueResearchRequest { Technology = technology }) == ResultCode.Success,
                    "New technology queues through the real research request");
                ResearchOps.Settle(em, root);
                check(ResearchOps.Completed(em, root, technology) == 1 && BuildingBlueprints.Has(em, root, id, 1),
                    "Completing the new technology grants the new building blueprint");
                var grid = em.GetComponentData<GridData>(root);
                int2? free = null;
                for (int i = 0; i < grid.Value.Value.Cells.Length; i++)
                {
                    var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                    if (GridOps.CanPlace(em, root, id, cell, 0)) { free = cell; break; }
                }
                check(free.HasValue, "New building has a legal placement cell");
                FeatureUnlocks.Unlock(em, root, FeatureDefinitions.Find(em, root, "feature.Building"));
                var result = GameRequestExecution.Execute(em, root, new BuildRequest { Definition = id, Position = GridOps.Position(grid, free.Value, new int2(1, 1)) });
                check(result == ResultCode.Success, "New blueprint is reachable through the real build request: " + result);
                using var query = em.CreateEntityQuery(typeof(BuildingDefinitionRef), typeof(Building));
                using var buildings = query.ToEntityArray(Allocator.Temp);
                var created = buildings.First(e => em.GetComponentData<BuildingDefinitionRef>(e).Definition == id);
                var construct = typeof(ConstructionSettlement).GetMethod("Settle", BindingFlags.Static | BindingFlags.NonPublic);
                for (int i = 0; i < definition.ConstructionTurns; i++) construct.Invoke(null, new object[] { em, root, created });
                check(BuildingStatus.Operational(em, created), "Created building completes actual construction");
                check(em.Exists(em.GetComponentData<BuildingSelectionAnchor>(created).Value), "Created building has a baked selection anchor");
                check(BuildingVisualResolver.Select(em, created) != Entity.Null, "Created building resolves its visible placeholder slot");
                ulong identity = em.GetComponentData<Identity>(created).Id;
                var produce = typeof(BuildingProductionSettlement).GetMethod("Settle", BindingFlags.Static | BindingFlags.NonPublic);
                int points = em.GetComponentData<ResearchState>(root).Points;
                produce.Invoke(null, new object[] { em, root, created });
                check(em.GetComponentData<ResearchState>(root).Points == points + 3, "New level-one research module contributes configured points");
                result = GameRequestExecution.Execute(em, root, new UpgradeBuildingRequest { Building = identity });
                check(result == ResultCode.Success && em.GetComponentData<Building>(created).Level == 2, "New building upgrades through the real request: " + result);
                var selected = BuildingVisualResolver.Select(em, created);
                bool selectedSecondLevel = false;
                foreach (var slot in em.GetBuffer<BuildingVisualSlot>(created))
                    selectedSecondLevel |= slot.Level == 2 && slot.Slot == selected;
                check(selectedSecondLevel, "Upgrade selects the new level-two visual slot");
                points = em.GetComponentData<ResearchState>(root).Points;
                produce.Invoke(null, new object[] { em, root, created });
                check(em.GetComponentData<ResearchState>(root).Points == points + 7, "Upgraded research uses the level-two configuration");
                var snapshot = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, snapshot));
                var restored = WorldQueries.Find(em, identity);
                check(restored != Entity.Null && em.GetComponentData<Building>(restored).Level == 2,
                    "New building and its upgraded level survive a current-version snapshot restore");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
