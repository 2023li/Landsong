#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    // Real package baking + isolated ECS worlds. Never replaces the scene the designer has open.
    public static class EcsVerification
    {
        static StringBuilder report;
        static int assertions;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL: " + label); assertions++; report.AppendLine("PASS " + label); }
        [MenuItem("Landsong/ECS/Verify baseline")]
        public static string Run()
        {
            report = new StringBuilder(); assertions = 0;
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
                Check(catalog != null && catalog.Definitions.Length > 0, "Native catalog exists");
                var scenes = AssetDatabase.FindAssets("t:Scene", new[] { Landsong.ECS.Presentation.EcsSceneFlow.MapSceneRoot.TrimEnd('/') }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity")).ToArray();
                Check(scenes.Length >= 2, "Both maps have native SubScenes");
                foreach (var path in scenes) VerifyScene(path);
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception ex) { report.AppendLine(ex.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/verification.txt", report.ToString()); }
        }
        internal static void Bake(World world, GameObject[] roots, BlobAssetStore store)
        {
            var assembly = typeof(GameWorldAuthoring.Baker).BaseType.Assembly;
            var utility = assembly.GetType("Unity.Entities.BakingUtility", true);
            var settingsType = assembly.GetType("Unity.Entities.BakingSettings", true);
            var settings = Activator.CreateInstance(settingsType, true);
            settingsType.GetProperty("BlobAssetStore").SetValue(settings, store);
            utility.GetMethod("BakeGameObjects", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { world, roots, settings });
        }
        static void VerifyScene(string path)
        {
            report.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Landsong ECS verification", WorldFlags.Game);
            try
            {
                Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager; var root = Sim.Root(em);
                Check(root != Entity.Null, "Baking creates exactly one simulation root");
                var authoring = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameWorldAuthoring>()).Single();
                Check(em.GetBuffer<ContentPrefab>(root).Length > 30, "Entity prefab catalog baked");
                foreach (var p in em.GetBuffer<ContentPrefab>(root))
                {
                    var kind = Sim.Definition(em, root, p.Definition).Kind;
                    if (kind == ContentKind.Soldier || kind == ContentKind.Hero || kind == ContentKind.Enemy) Check(em.HasBuffer<AI.TacticalActionData>(p.Prefab), "DBP native task buffer: " + Sim.Definition(em, root, p.Definition).Id);
                }
                GameLoopSystem.Initialize(em, root);
                Check(em.GetComponentData<Session>(root).Turn == 1, "Day 1 initialized");
                Check(Sim.Population(em, root) >= 10, "Core population present");
                Check(Sim.Entities<Building>(em).Length == authoring.Map.InitialBuildings.Length, "Initial buildings preserved");
                var mapGrid = em.GetComponentData<GridData>(root); var sampleCell = mapGrid.Value.Value.Min;
                for (var i = 0; i < mapGrid.Value.Value.Cells.Length; i++) if (mapGrid.Value.Value.Cells[i].Exists != 0) { sampleCell = mapGrid.Value.Value.Min + new int2(i % mapGrid.Value.Value.Size.x, i / mapGrid.Value.Value.Size.x); if (mapGrid.Value.Value.Cells[i].Height > 0) break; }
                var surface = GridOps.Position(mapGrid, sampleCell, new int2(1));
                Check(GridOps.RaycastSurface(mapGrid, surface + new float3(0, 20, 0), new float3(0, -1, 0), out var picked) && math.distance(picked, surface) < .001f, "Picking respects authored origin and surface height");
                Check(!GridOps.RaycastSurface(mapGrid, mapGrid.Origin - new float3(100, -20, 100), new float3(0, -1, 0), out _), "Picking rejects points outside map");
                var bytes = SnapshotCodec.Capture(em, root); var initial = SnapshotCodec.Decode(em, root, bytes);
                var inventory = em.GetBuffer<InventorySlot>(root); Check(inventory.Length >= 5, "Core supplies inventory");
                var gold = em.GetComponentData<GameSettings>(root).Gold;
                var goldBefore = InventoryOps.Count(em, root, gold);
                Check(!InventoryOps.Remove(em, root, gold, goldBefore + 1), "Insufficient payment rejected atomically");
                Check(InventoryOps.Count(em, root, gold) == goldBefore, "Rejected payment preserves stock");
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 5 });
                Check(NightOps.Begin(em, root) == ResultCode.ConfirmationRequired && em.GetComponentData<Session>(root).Phase == Phase.Day, "Pending items block unconfirmed entry into night");
                Check(InventoryOps.RemoveWithPending(em, root, gold, 3) && InventoryOps.Count(em, root, gold) == goldBefore && InventoryOps.PendingCount(em, root, gold) == 2, "Repair-payment path consumes pending items before inventory");
                Check(!InventoryOps.RemoveWithPending(em, root, gold, goldBefore + 3) && InventoryOps.PendingCount(em, root, gold) == 2, "Failed combined payment preserves pending items");
                SnapshotCodec.Restore(em, root, initial);
                Check(InventoryOps.Count(em, root, gold) == goldBefore, "Save roundtrip stock");
                var ids = Sim.Entities<Identity>(em); var unique = new System.Collections.Generic.HashSet<ulong>(); foreach (var e in ids) Check(unique.Add(em.GetComponentData<Identity>(e).Id), "Persistent ID unique"); ids.Dispose();
                EconomyOps.Settle(em, root); var once = SnapshotCodec.Capture(em, root); EconomyOps.Settle(em, root);
                Check(once.SequenceEqual(SnapshotCodec.Capture(em, root)), "Day settlement is idempotent");
                SnapshotCodec.Restore(em, root, initial);
                Check(NightOps.Begin(em, root) == ResultCode.Success, "Day -> dusk");
                Check(em.GetComponentData<Session>(root).Turn == 1, "Night belongs to same turn");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Save }) == ResultCode.WrongPhase, "Saving blocked at night");
                var dusk = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                var s = em.GetComponentData<Session>(root); s.CheckpointPending = 0; em.SetComponentData(root, s);
                NightOps.Tick(em, root, .1f); Check(em.GetComponentData<Session>(root).Phase == Phase.Night, "Deployment starts real-time night");
                SnapshotCodec.Restore(em, root, dusk);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Deployment, "Dusk rollback restores restartable node");
                NightOps.Dawn(em, root); Check(em.GetComponentData<Session>(root).Turn == 2, "Only dawn increments turn");
                SnapshotCodec.Restore(em, root, initial);
                TestMilitary(em, root);
                SnapshotCodec.Restore(em, root, initial);
                TestBuildings(em, root);
                using (var owned = Sim.Entities<Persistent>(em)) foreach (var entity in owned) Check(em.HasComponent<SimulationOwner>(entity), "Runtime instance has explicit simulation ownership");
                em.DestroyEntity(root); SimulationLifetimeSystem.Cleanup(em);
                using (var owned = Sim.Entities<SimulationOwner>(em)) Check(owned.Length == 0, "Unloaded map releases runtime instances");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void TestBuildings(EntityManager em, Entity root)
        {
            var warehouse = Sim.FindDefinition(em, root, "b仓库"); var house = Sim.FindDefinition(em, root, "b居民房");
            var gold = em.GetComponentData<GameSettings>(root).Gold;
            // Tests use free unoccupied logical cells, independently of placement eligibility.
            var e = BuildingOps.Create(em, root, warehouse, new int2(-1000, -1000), 0, 1, true);
            var id = em.GetComponentData<Identity>(e).Id;
            Check(em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).Any(s => s.Provider == id), "Warehouse owns its slots");
            BuildingOps.Ruin(em, root, e);
            Check(em.GetComponentData<Building>(e).Workers == 0, "Ruined jobs released");
            foreach (var slot in em.GetBuffer<InventorySlot>(root)) Check(slot.Provider != id, "Ruined storage removed");
            Check(BuildingOps.Demolish(em, root, e) == ResultCode.Success && !em.Exists(e), "Ruined building demolition");
            var h = BuildingOps.Create(em, root, house, new int2(-1000, -1000), 0, 1, true);
            Check(em.GetComponentData<Building>(h).Population == 2, "House completion gives two residents");
            BuildingOps.Ruin(em, root, h); Check(em.GetComponentData<Building>(h).Population == 0, "House ruin kills residents");
            var tower = BuildingOps.Create(em, root, Sim.FindDefinition(em, root, "b瞭望塔"), new int2(-1200, -1200), 0, 1, true);
            var intel = NightOps.Intelligence(em, root);
            Check(intel > 0 && NightOps.Begin(em, root, true) == ResultCode.Success, "Operational intelligence is snapshotted at dusk");
            BuildingOps.Ruin(em, root, tower);
            Check(NightOps.Intelligence(em, root) == intel, "Tower ruin does not remove already gathered intelligence this night");
            var core = Entity.Null; using (var buildings = Sim.Entities<Building>(em)) foreach (var b in buildings) if (em.GetComponentData<BuildingStats>(b).IsCore != 0) core = b;
            Check(core != Entity.Null, "PlayerHome imported as core");
            BuildingOps.Ruin(em, root, core); Check(em.GetComponentData<Session>(root).Phase == Phase.GameOver, "Only core loss ends dynasty");
            var ended = em.GetComponentData<Session>(root); ended.Phase = Phase.Ended; em.SetComponentData(root, ended);
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.EndDynasty }) == ResultCode.WrongPhase, "Ended dynasty cannot be ended twice");
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.RetryDay }) == ResultCode.WrongPhase, "Deleted dynasty cannot retry an old node");
        }
        static void TestMilitary(EntityManager em, Entity root)
        {
            var state = em.GetComponentData<Session>(root); state.BasePopulation = 100; em.SetComponentData(root, state);
            var inventory = em.GetBuffer<InventorySlot>(root); var original = inventory[0];
            for (var i = 0; i < 20; i++) { var slot = original; slot.Index = 1000 + i; slot.Item = em.GetComponentData<GameSettings>(root).Gold; slot.Count = 1000; inventory.Add(slot); }
            var garrison = BuildingOps.Create(em, root, Sim.FindDefinition(em, root, "b驻军营地"), new int2(-100, -100), 0, 1, true);
            var id = em.GetComponentData<Identity>(garrison).Id; var militia = Sim.FindDefinition(em, root, "militia");
            Check(MilitaryOps.Recruit(em, root, new Command { Target = id, Definition = militia }, false) == ResultCode.Success, "Soldier recruitment reserves population");
            var troop = MilitaryOps.AtSlot(em,id,1);
            var stableId = em.GetComponentData<Identity>(troop).Id;
            Check(MilitaryOps.Recruit(em, root, new Command { Target = id, Definition = militia }, false) == ResultCode.Success, "Second soldier recruited for capacity test");
            var stats = em.GetComponentData<BuildingStats>(garrison); stats.Garrison = 1; em.SetComponentData(garrison, stats);
            MilitaryOps.ReconcileGarrisons(em, root);
            Check(MilitaryOps.GarrisonCount(em, id) == 1 && em.GetComponentData<Soldier>(troop).Garrison == id && em.GetComponentData<Soldier>(troop).Slot == 1, "Reduced garrison capacity retains slot one and unassigns overflow");
            Check(NightOps.Begin(em, root) == ResultCode.ConfirmationRequired, "Unassigned soldier dismissal requires night-entry confirmation");
            MilitaryOps.PrepareNight(em, root); var actor = em.GetComponentData<Combatant>(troop); actor.Deployed = 1; em.SetComponentData(troop, actor);
            BuildingOps.Ruin(em, root, garrison);
            Check(em.GetComponentData<Soldier>(troop).Garrison == 0, "Ruined garrison moves soldiers to unassigned pool");
            Check(em.GetComponentData<Health>(troop).Current == em.GetComponentData<Health>(troop).Maximum, "Already deployed soldiers keep health when garrison is ruined");
            MilitaryOps.PrepareNight(em, root); Check(Sim.Find(em, stableId) == Entity.Null, "Unassigned pool disbands before next night");
            int templeDefinition = Sim.FindDefinition(em, root, "b泰坦神殿"); var templeGrid = em.GetComponentData<GridData>(root); Entity temple = Entity.Null;
            for (int y = 10; y < templeGrid.Value.Value.Size.y - 10 && temple == Entity.Null; y++) for (int x = 10; x < templeGrid.Value.Value.Size.x - 10; x++) if (GridOps.CanPlace(em, root, templeDefinition, new int2(x, y), 0)) { temple = BuildingOps.Create(em, root, templeDefinition, new int2(x, y), 0, 1, true); break; }
            Check(temple != Entity.Null, "Hero fixture has a legal temple footprint");
            var templeId = em.GetComponentData<Identity>(temple).Id;
            Check(MilitaryOps.Recruit(em, root, new Command { Target = templeId }, true) == ResultCode.InsufficientPopulation, "Titan requires thirty temple workers");
            var b = em.GetComponentData<Building>(temple); b.Workers = 30; b.Offering = 1; b.Subsidy = 1; em.SetComponentData(temple, b);
            Check(MilitaryOps.Recruit(em, root, new Command { Target = templeId }, true) == ResultCode.Success, "Titan recruited as separate persistent hero");
            EconomyOps.Settle(em, root); b = em.GetComponentData<Building>(temple);
            Check(b.PaidOfferingTurn == em.GetComponentData<Session>(root).Turn, "Continuous offering paid during day settlement");
            Entity hero; using (var heroes = Sim.Entities<Hero>(em)) hero = heroes[0];
            Check(em.GetComponentData<Hero>(hero).Experience == 1, "Offering grants experience");
            var nightState = em.GetComponentData<Session>(root); nightState.Phase = Phase.Night; nightState.NightKind = NightKind.Peaceful; em.SetComponentData(root, nightState);
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.WakeHero, Target = templeId }) == ResultCode.Success, "Paid Titan can be awakened even during a peaceful night");
            Check(MilitaryOps.Wake(em, root, temple) == ResultCode.Unavailable, "Awakening cannot be charged twice");
            var heroId = em.GetComponentData<Identity>(hero).Id;
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.SelectHero, Target = heroId }) == ResultCode.Success, "Deployed hero can be selected");
            var anchor = Sim.Position(em, hero);
            GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.MoveHero, Position = anchor });
            GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.SelectHero });
            Check(em.GetComponentData<UnitOrder>(hero).Kind == OrderKind.Move && math.all(em.GetComponentData<Combatant>(hero).Home == anchor), "Deselected hero keeps its last positional order and guard anchor");
            Check(em.GetComponentData<Combatant>(hero).Participated == 0, "Awakening alone does not count as actual combat");
            var enemy = Sim.Spawn(em, root, Sim.FindDefinition(em, root, "raider"), Sim.Position(em, hero), false);
            MilitaryOps.ConfigureCombatant(em, root, enemy, 1, false, true, 0, Sim.Position(em, enemy));
            CombatOps.ApplyDamage(em, root, new DamageRequest { Source = hero, Target = enemy, Amount = 0 });
            Check(em.GetComponentData<Combatant>(hero).Participated == 0, "Zero-effect attack grants no participation");
            CombatOps.ApplyDamage(em, root, new DamageRequest { Source = hero, Target = enemy, Amount = 1 });
            Check(em.GetComponentData<Combatant>(hero).Participated == 1, "Effective hostile damage counts as participation");
            em.DestroyEntity(enemy);
            BuildingOps.Ruin(em, root, temple);
            var h = em.GetComponentData<Hero>(hero);
            Check(h.Recruited == 0 && h.Experience == 0 && h.DeathPending != 0 && h.CooldownUntil == 0, "Temple ruin marks hero dead; cooldown waits for dawn");
            MilitaryOps.Dawn(em, root); h = em.GetComponentData<Hero>(hero);
            Check(h.DeathPending == 0 && h.CooldownUntil == em.GetComponentData<Session>(root).Turn + 11, "Dawn commits hero population release and full cooldown");
            BuildingOps.Demolish(em, root, temple);
            var replacement = BuildingOps.Create(em, root, Sim.FindDefinition(em, root, "b泰坦神殿"), new int2(-110, -110), 0, 1, true);
            var replacementState = em.GetComponentData<Building>(replacement); replacementState.Workers = 30; em.SetComponentData(replacement, replacementState);
            var replacementId = em.GetComponentData<Identity>(replacement).Id;
            Check(MilitaryOps.Recruit(em, root, new Command { Target = replacementId }, true) == ResultCode.Unavailable, "Rebuilding temple cannot bypass hero cooldown");
            state = em.GetComponentData<Session>(root); state.Turn = h.CooldownUntil; em.SetComponentData(root, state);
            Check(MilitaryOps.Recruit(em, root, new Command { Target = replacementId }, true) == ResultCode.Success, "Replacement temple recruits after original cooldown");
            Check(em.GetComponentData<Hero>(hero).Sanctum == replacementId && em.GetComponentData<Hero>(hero).Experience == 0, "Recruitment rebinds original hero identity with cleared experience");
        }
        [MenuItem("Landsong/ECS/Build Windows baseline")]
        public static string Build() => BuildPlayer(true);
        [MenuItem("Landsong/ECS/Build Windows release")]
        public static string BuildRelease() => BuildPlayer(false);
        static string BuildPlayer(bool development)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (!scenes.SequenceEqual(Landsong.ECS.Presentation.EcsSceneFlow.BuildScenes)) throw new InvalidOperationException("Build settings must contain exactly Boot, Start, LoadingTransition, Game in that order.");
            var folder = development ? "Builds/ECSBaseline" : "Builds/ECSRelease";
            Directory.CreateDirectory(folder);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = folder + "/Landsong.exe", target = BuildTarget.StandaloneWindows64, options = development ? BuildOptions.Development : BuildOptions.None });
            var summary = result.summary; var message = summary.result + "; errors=" + summary.totalErrors + "; warnings=" + summary.totalWarnings + "; " + summary.totalTime;
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText(development ? "Library/LandsongEcs/build.txt" : "Library/LandsongEcs/build-release.txt", message);
            if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0) throw new InvalidOperationException(message);
            return message;
        }
    }
}
#endif
