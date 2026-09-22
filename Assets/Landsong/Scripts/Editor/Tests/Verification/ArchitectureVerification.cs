#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.AI;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.EditorTools;
using Landsong.GridSystem;
using Landsong.VisualSystem;
using Opsive.BehaviorDesigner.Runtime.Groups;
using Unity.Entities;
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
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            var report = new StringBuilder();
            var assertions = 0;
            void Check(bool ok, string label)
            {
                if (!ok)
                    throw new InvalidOperationException("FAIL " + label);
                assertions++;
                report.AppendLine("PASS " + label);
            }

            try
            {
                const string ecsScripts = "Assets/Landsong/Scripts/ECS";
                Check(AssetDatabase.IsValidFolder(ecsScripts) && !Directory.Exists("Assets/Landsong/ECS") && !File.Exists("Assets/Landsong/ECS.meta"), "Single script root: ECS lives under Scripts");
                VerifyAssemblyBoundaries(Check);
                VerifySystemOrdering(Check);
                ReleaseContentValidation.ValidateCommon();
                Check(true, "Runtime display catalog matches authoring and four scene GUIDs resolve");
                Check(!Directory.EnumerateFiles("Assets/Landsong/Scripts", "*Migration.cs", SearchOption.AllDirectories).Any(), "One-time migrations are outside the Assets compilation tree");
                foreach (var name in new[]
                {
                    "SceneTransitionManager",
                    "MonoSingleton",
                    "Singleton",
                    "MoyoConfig"
                }

                )
                    Check(!File.Exists("Assets/Moyo/" + name + ".cs"), "Retired framework type absent: " + name);
                Check(typeof(BuildingCropVisualController).GetField("plantPoints", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) == null, "Crop controller accepts only authored child points");
                var buildings = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
                var farm = buildings.Definitions.Single(asset => asset.Metadata.Id == "b农田");
                var farmPrefabPath = AssetDatabase.GetAssetPath(farm.Prefab);
                Check(!string.IsNullOrEmpty(farmPrefabPath), "Formal farm has an explicit entity prefab");
                var buildingPrefabPaths = buildings.Definitions.Select(asset => AssetDatabase.GetAssetPath(asset.Prefab)).Distinct().ToArray();
                Check(Directory.EnumerateFiles(ContentAssetPaths.CatalogSources, "*Catalog.asset", SearchOption.TopDirectoryOnly).Count() == 23, "Source catalog assets stay in the single authoring directory");
                Check(Directory.EnumerateFiles(ContentAssetPaths.DisplayCatalogs, "*DisplayCatalog.asset", SearchOption.TopDirectoryOnly).Count() == 22, "Generated display catalogs stay in the single generated directory");
                Check(buildings.Definitions.Count() == 29 && buildingPrefabPaths.Length == 29, "Building catalog keeps 29 unique formal prefab roots");
                foreach (var definition in buildings.Definitions)
                {
                    var referencedPath = AssetDatabase.GetAssetPath(definition.Prefab);
                    Check(referencedPath.StartsWith(ContentAssetPaths.Buildings + "/", StringComparison.Ordinal), "Building root belongs to the building content root: " + definition.Metadata.Id);
                }
                Check(AssetDatabase.LoadMainAssetAtPath(ContentAssetPaths.AnimatedUnitShader) != null, "Animated unit shader stays in the shared shader directory");
                Check(AssetDatabase.IsValidFolder(ContentAssetPaths.UiPrefabs), "UI prefabs stay under the dedicated UI root");
                Check(!AssetDatabase.IsValidFolder(ContentAssetPaths.Root + "/Prefabs"), "Retired mixed prefab root is absent");
                Check(!AssetDatabase.IsValidFolder(ContentAssetPaths.Presentation + "/Units"), "Presentation no longer owns unit packages");
                Check(!Directory.EnumerateDirectories(ContentAssetPaths.Units, "Models", SearchOption.AllDirectories).Any(), "Unit packages contain no disconnected model snapshot directories");
                var authoredScenes = Landsong.ECS.Presentation.EcsSceneFlow.BuildScenes.Concat(Landsong.EditorTools.GameMapPaths.BakedScenes()).Concat(Landsong.EditorTools.GameMapPaths.BakedMapPaths().Select(path => EcsMapIncrementalImport.SourceScene(AssetDatabase.LoadAssetAtPath<MapAsset>(path)))).Distinct().ToArray();
                Check(buildingPrefabPaths.Length > 0, "Formal building catalog supplies current authored prefabs");
                foreach (var path in buildingPrefabPaths.Concat(authoredScenes))
                {
                    Check(!string.IsNullOrEmpty(path) && File.Exists(path), "Authored building or scene asset exists: " + path);
                    Check(!File.ReadAllText(path).Contains("  plantPoints:"), "Saved crop asset has no old serialized points: " + path);
                }

                foreach (var path in Landsong.ECS.Presentation.EcsSceneFlow.BuildScenes.Concat(Landsong.EditorTools.GameMapPaths.BakedScenes()))
                {
                    var scene = EditorSceneManager.OpenPreviewScene(path);
                    try
                    {
                        Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Formal scene scripts intact: " + path);
                    }
                    finally
                    {
                        EditorSceneManager.ClosePreviewScene(scene);
                    }
                }

                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Landsong" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Check(root != null && root.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Prefab scripts intact: " + path);
                }

                foreach (var mapPath in Landsong.EditorTools.GameMapPaths.BakedMapPaths())
                {
                    var map = AssetDatabase.LoadAssetAtPath<MapAsset>(mapPath);
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
                        var before = JsonUtility.ToJson(map);
                        var invalid = (CellSource[])terrain.Cells.Clone();
                        var first = map.InitialBuildings[0].Cell - terrain.Min;
                        invalid[first.y * terrain.Size.x + first.x].Exists = false;
                        var rejected = false;
                        try
                        {
                            EcsMapIncrementalImport.ValidateInitialBuildings(map, new EcsMapIncrementalImport.TerrainSnapshot { Cells = invalid, Min = terrain.Min, Size = terrain.Size, Origin = terrain.Origin, CellSize = terrain.CellSize });
                        }
                        catch (InvalidOperationException)
                        {
                            rejected = true;
                        }

                        Check(rejected && before == JsonUtility.ToJson(map), "Invalid terrain rejected without changing native map: " + map.MapId);
                    }
                    finally
                    {
                        EditorSceneManager.ClosePreviewScene(source);
                    }
                }

                var naming = AssetDatabase.LoadAssetAtPath<LS_BuildingViewNamingConfig>(LS_BuildingViewNamingConfig.DefaultAssetPath);
                Check(naming != null && naming.Entries.All(e => e.Definition != null), "Art naming bindings use native definitions");
                Check(naming.TryValidate(out var namingError), "Art naming validation: " + namingError);
                foreach (var path in buildingPrefabPaths)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Check(prefab != null && prefab.GetComponentsInChildren<Transform>(true).All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0), "Authored building prefab scripts intact: " + path);
                }

                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/architecture-verification.txt", report.ToString());
            }
        }

        [Serializable]
        sealed class AssemblyDefinition
        {
            public string name = string.Empty;
            public string[] references = Array.Empty<string>();
            public string[] defineConstraints = Array.Empty<string>();
        }

        static void VerifyAssemblyBoundaries(Action<bool, string> check)
        {
            const string scriptsRoot = "Assets/Landsong/Scripts/";
            var partitions = new[]
            {
                (Folder: "ECS/Editor", Assembly: "Landsong.ECS.Editor"),
                (Folder: "ECS/Authoring", Assembly: "Landsong.ECS.Authoring"),
                (Folder: "ECS/AI", Assembly: "Landsong.ECS.AI"),
                (Folder: "ECS", Assembly: "Landsong.ECS"),
                (Folder: "Content", Assembly: "Landsong.Content.Runtime"),
                (Folder: "Presentation", Assembly: "Landsong.ECS.Presentation"),
                (Folder: "Animation", Assembly: "Landsong.Animation"),
                (Folder: "AnimationPreview", Assembly: "Landsong.Animation.Preview"),
                (Folder: "UI", Assembly: "Landsong.UI"),
                (Folder: "Application", Assembly: "Landsong.Application"),
                (Folder: "Verification", Assembly: "Landsong.Verification")
            };
            foreach (var partition in partitions)
            {
                string folder = scriptsRoot + partition.Folder;
                string definition = folder + "/" + partition.Assembly + ".asmdef";
                check(AssetDatabase.LoadMainAssetAtPath(definition) != null, "Assembly definition present: " + partition.Assembly);
                var sourcePaths = AssetDatabase.FindAssets("t:MonoScript", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).Where(path => partition.Folder != "ECS" || new[] { "Editor", "Authoring", "AI" }.All(child => !path.StartsWith(scriptsRoot + "ECS/" + child + "/", StringComparison.Ordinal))).ToArray();
                var wrongOwners = sourcePaths.Select(path => (Path: path, Assembly: UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(path))).Where(entry => entry.Assembly != partition.Assembly + ".dll").Select(entry => entry.Path + " => " + (entry.Assembly ?? "<unassigned>")).ToArray();
                check(sourcePaths.Length > 0 && wrongOwners.Length == 0, "Scripts compile in their declared partition: " + partition.Assembly + (wrongOwners.Length == 0 ? "" : "\n" + string.Join("\n", wrongOwners)));
                var config = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(definition));
                check(config.name == partition.Assembly, "Assembly identity matches its partition: " + partition.Assembly);
                if (partition.Assembly == "Landsong.Verification" || partition.Assembly == "Landsong.Animation.Preview")
                    check(config.defineConstraints.Contains("UNITY_EDITOR || DEVELOPMENT_BUILD"), "Whole smoke assembly excluded from Release");
                var references = (config.references ?? Array.Empty<string>()).Select(ResolveAssemblyReference).ToArray();
                string[] forbidden = partition.Assembly switch
                {
                    "Landsong.ECS" => new[]
                    {
                        "Landsong.ECS.Authoring",
                        "Landsong.ECS.AI",
                        "Landsong.ECS.Presentation",
                        "Landsong.UI",
                        "Landsong.Application",
                        "Landsong.Verification",
                        "Moyo.Runtime",
                        "Unity.Entities.Hybrid",
                        "Unity.Entities.Graphics"
                    },
                    "Landsong.ECS.Authoring" => new[]
                    {
                        "Landsong.ECS.AI",
                        "Landsong.ECS.Presentation",
                        "Landsong.UI",
                        "Landsong.Application",
                        "Landsong.Verification",
                        "Moyo.Runtime"
                    },
                    "Landsong.ECS.AI" => new[]
                    {
                        "Landsong.ECS.Authoring",
                        "Landsong.ECS.Presentation",
                        "Landsong.UI",
                        "Landsong.Application",
                        "Landsong.Verification",
                        "Moyo.Runtime"
                    },
                    "Landsong.Content.Runtime" => new[]
                    {
                        "Landsong.ECS.Authoring",
                        "Landsong.ECS.AI",
                        "Landsong.ECS.Presentation",
                        "Landsong.UI",
                        "Landsong.Application",
                        "Moyo.Runtime"
                    },
                    "Landsong.ECS.Presentation" => new[]
                    {
                        "Landsong.ECS.Authoring",
                        "Landsong.UI",
                        "Landsong.Application",
                        "Landsong.Verification",
                        "Moyo.Runtime"
                    },
                    "Landsong.UI" => new[]
                    {
                        "Landsong.ECS.Authoring",
                        "Landsong.Application",
                        "Landsong.Verification"
                    },
                    "Landsong.Application" => new[]
                    {
                        "Landsong.ECS.Authoring",
                        "Landsong.Verification"
                    },
                    "Landsong.Animation" => new[]
                    {
                        "Landsong.ECS.Authoring",
                        "Landsong.ECS.AI",
                        "Landsong.UI",
                        "Landsong.Application",
                        "Landsong.Verification",
                        "Landsong.Animation.Preview"
                    },
                    _ => Array.Empty<string>()};
                check(!references.Intersect(forbidden).Any(), "No reverse application/UI dependency: " + partition.Assembly);
                if (partition.Assembly == "Landsong.ECS")
                    check(!references.Any(reference => reference.StartsWith("Opsive.", StringComparison.Ordinal)), "Core has no behavior-tree assembly dependency");
            }

            var coreReferences = typeof(CombatSystem).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
            check(!coreReferences.Any(reference => reference.StartsWith("Opsive.", StringComparison.Ordinal) || reference == "Unity.Entities.Hybrid" || reference == "Unity.Entities.Graphics" || reference.StartsWith("Rukhanka.", StringComparison.Ordinal) || reference.StartsWith("Landsong.Animation", StringComparison.Ordinal) || reference == "Landsong.ECS.Authoring" || reference == "Landsong.ECS.AI"), "Compiled Core independently owns simulation and persistence");
            check(typeof(BuildingVisualSlot).Assembly == typeof(CombatSystem).Assembly && typeof(BuildingVisualPurpose).Assembly == typeof(CombatSystem).Assembly && typeof(BuildingVisualSelection).Assembly == typeof(CombatSystem).Assembly, "Building visual data contracts belong to Core");
            check((byte)BuildingVisualPurpose.Construction == 0 && (byte)BuildingVisualPurpose.Operational == 1 && (byte)BuildingVisualPurpose.Preview == 2 && (byte)BuildingVisualPurpose.Ruined == 3 && (byte)BuildingVisualPurpose.Repairing == 4, "Baked building purpose values preserved");
            check(typeof(TacticalAction).Assembly.GetName().Name == "Landsong.ECS.AI" && Type.GetType("Landsong.ECS.AI.TacticalAction, Landsong.ECS.AI") == typeof(TacticalAction), "Behavior-tree task resolves from the AI adapter assembly");
            check(typeof(GameWorldTemplateAuthoring).Assembly.GetName().Name == "Landsong.ECS.Authoring", "Authoring and baking live outside Core");
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Landsong/ECSContent" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var yaml = File.ReadAllText(path);
                if (!yaml.Contains("Landsong.ECS.AI.TacticalAction"))
                    continue;
                check(!yaml.Contains("Landsong.ECS.AI.TacticalAction, Landsong.ECS,"), "Behavior-tree task has no stale assembly-qualified identity: " + path);
            }

            VerifyAssemblyGraph(partitions.Select(partition => scriptsRoot + partition.Folder + "/" + partition.Assembly + ".asmdef"), check);
            check(!AssetDatabase.IsValidFolder(scriptsRoot + "ECS/Presentation"), "UI and shared presentation no longer live inside the simulation tree");
            var editorScripts = AssetDatabase.FindAssets("t:MonoScript", new[] { scriptsRoot + "Editor" }).Select(AssetDatabase.GUIDToAssetPath).ToArray();
            check(editorScripts.Length > 0 && editorScripts.All(path => UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(path) == "Assembly-CSharp-Editor.dll"), "Project authoring and verification keep their default editor assembly boundary");
        }

        static void VerifyAssemblyGraph(IEnumerable<string> definitions, Action<bool, string> check)
        {
            var graph = definitions.Select(path => JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path))).ToDictionary(definition => definition.name, definition => definition.references.Select(ResolveAssemblyReference).ToArray());
            var visited = new HashSet<string>();
            var active = new HashSet<string>();
            void Visit(string name)
            {
                if (active.Contains(name))
                    throw new InvalidOperationException("Assembly dependency cycle: " + name);
                if (!visited.Add(name))
                    return;
                active.Add(name);
                foreach (var reference in graph[name])
                    if (graph.ContainsKey(reference))
                        Visit(reference);
                active.Remove(name);
            }

            foreach (var name in graph.Keys)
                Visit(name);
            check(true, "Runtime, authoring, AI and editor assembly graph is acyclic");
        }

        static void VerifySystemOrdering(Action<bool, string> check)
        {
            // Create and sort a disposable scheduling graph; no simulation update or player data is used.
            using var world = new World("Landsong architecture ordering");
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, typeof(BeginSimulationEntityCommandBufferSystem), typeof(GameLoopSystem), typeof(PerceptionSystem), typeof(TacticalSlotSystem), typeof(StartTacticalBehaviorSystem), typeof(BehaviorTreeSystemGroup), typeof(TacticalDecisionBoundarySystem), typeof(CombatResolutionGroup), typeof(CombatSystem), typeof(NavigationSystem), typeof(ProjectileSystem), typeof(DamageSystem));
            var simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var combat = world.GetExistingSystemManaged<CombatResolutionGroup>();
            simulation.SortSystems();
            int Position(ComponentSystemGroup group, SystemHandle handle)
            {
                using var systems = group.GetAllSystems();
                for (var i = 0; i < systems.Length; i++)
                    if (systems[i].Equals(handle))
                        return i;
                throw new InvalidOperationException("System not registered in expected group: " + group.GetType().Name);
            }

            var loopAt = Position(simulation, world.GetOrCreateSystem<GameLoopSystem>());
            var perceptionAt = Position(simulation, world.GetOrCreateSystem<PerceptionSystem>());
            var tacticalAt = Position(simulation, world.GetOrCreateSystem<TacticalSlotSystem>());
            var startAt = Position(simulation, world.GetOrCreateSystem<StartTacticalBehaviorSystem>());
            var behaviorAt = Position(simulation, world.GetExistingSystemManaged<BehaviorTreeSystemGroup>().SystemHandle);
            var boundaryAt = Position(simulation, world.GetOrCreateSystem<TacticalDecisionBoundarySystem>());
            var combatGroupAt = Position(simulation, combat.SystemHandle);
            check(loopAt < perceptionAt && perceptionAt < tacticalAt && tacticalAt < startAt && startAt < behaviorAt && behaviorAt < boundaryAt && boundaryAt < combatGroupAt, "Sorted frame order: game loop, perception, behavior, adapter boundary, combat group");
            var attackAt = Position(combat, world.GetOrCreateSystem<CombatSystem>());
            var navigationAt = Position(combat, world.GetOrCreateSystem<NavigationSystem>());
            var projectileAt = Position(combat, world.GetOrCreateSystem<ProjectileSystem>());
            var damageAt = Position(combat, world.GetOrCreateSystem<DamageSystem>());
            check(attackAt < navigationAt && attackAt < projectileAt && projectileAt < damageAt, "Combat subgroup preserves attack-before-movement and attack-projectile-damage partial order");
        }

        static string ResolveAssemblyReference(string reference)
        {
            if (!reference.StartsWith("GUID:", StringComparison.Ordinal))
                return reference;
            string path = AssetDatabase.GUIDToAssetPath(reference.Substring(5));
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException("Assembly reference GUID does not resolve: " + reference);
            return JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path)).name;
        }
    }
}
#endif
