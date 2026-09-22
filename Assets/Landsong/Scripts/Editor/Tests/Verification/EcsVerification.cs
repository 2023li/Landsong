#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
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
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL: " + label);
            assertions++;
            report.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verify baseline")]
        public static string Run()
        {
            report = new StringBuilder();
            assertions = 0;
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
                Check(catalog != null && catalog.Definitions.Length > 0, "Native building catalog exists");
                var scenes = Landsong.EditorTools.GameMapPaths.BakedScenes().ToArray();
                Check(scenes.Length > 0, "Retained gameplay maps have native SubScenes");
                foreach (var path in scenes)
                    VerifyScene(path);
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception ex)
            {
                report.AppendLine(ex.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/verification.txt", report.ToString());
            }
        }

        internal static void Bake(World world, GameObject[] roots, BlobAssetStore store)
        {
            var assembly = typeof(GameWorldMapAuthoring.Baker).BaseType.Assembly;
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
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                Check(root != Entity.Null, "Baking creates exactly one simulation root");
                var authoring = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameWorldMapAuthoring>()).Single();
                Check(em.GetBuffer<BuildingPrefab>(root).Length == BuildingDefinitions.Count(em, root), "Every building definition has its own baked prefab mapping");
                foreach (var prefab in em.GetBuffer<SoldierPrefab>(root))
                    Check(em.HasBuffer<AI.TacticalActionData>(prefab.Prefab), "Soldier prefab has native DBP task buffer: " + SoldierDefinitions.Get(em, root, prefab.Definition).Metadata.Id);
                foreach (var prefab in em.GetBuffer<HeroPrefab>(root))
                    Check(em.HasBuffer<AI.TacticalActionData>(prefab.Prefab), "Hero prefab has native DBP task buffer: " + HeroDefinitions.Get(em, root, prefab.Definition).Metadata.Id);
                foreach (var prefab in em.GetBuffer<EnemyPrefab>(root))
                    Check(em.HasBuffer<AI.TacticalActionData>(prefab.Prefab), "Enemy prefab has native DBP task buffer: " + EnemyDefinitions.Get(em, root, prefab.Definition).Metadata.Id);
                WorldInitialization.Initialize(em, root);
                Check(em.GetComponentData<GameClock>(root).Turn == 1, "Day 1 initialized");
                Check(PopulationOps.Population(em, root) >= 10, "Core population present");
                Check(WorldQueries.Entities<Building>(em).Length == authoring.Map.InitialBuildings.Length, "Initial buildings preserved");
                var mapGrid = em.GetComponentData<GridData>(root);
                var sampleCell = mapGrid.Value.Value.Min;
                for (var i = 0; i < mapGrid.Value.Value.Cells.Length; i++)
                    if (mapGrid.Value.Value.Cells[i].Exists != 0)
                    {
                        sampleCell = mapGrid.Value.Value.Min + new int2(i % mapGrid.Value.Value.Size.x, i / mapGrid.Value.Value.Size.x);
                        if (mapGrid.Value.Value.Cells[i].Height > 0)
                            break;
                    }

                var surface = GridOps.Position(mapGrid, sampleCell, new int2(1));
                Check(GridOps.RaycastSurface(mapGrid, surface + new float3(0, 20, 0), new float3(0, -1, 0), out var picked) && math.distance(picked, surface) < .001f, "Picking respects authored origin and surface height");
                Check(!GridOps.RaycastSurface(mapGrid, mapGrid.Origin - new float3(100, -20, 100), new float3(0, -1, 0), out _), "Picking rejects points outside map");
                var bytes = SnapshotCodec.Capture(em, root);
                var initial = SnapshotCodec.Decode(em, root, bytes);
                var inventory = em.GetBuffer<InventorySlot>(root);
                Check(inventory.Length >= 5, "Core supplies inventory");
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                var goldBefore = InventoryOps.Count(em, root, gold);
                Check(!InventoryOps.Remove(em, root, gold, goldBefore + 1), "Insufficient payment rejected atomically");
                Check(InventoryOps.Count(em, root, gold) == goldBefore, "Rejected payment preserves stock");
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 5 });
                Check(NightOps.Begin(em, root) == ResultCode.ConfirmationRequired && em.GetComponentData<Session>(root).Phase == Phase.Day, "Pending items block unconfirmed entry into night");
                Check(InventoryOps.RemoveWithPending(em, root, gold, 3) && InventoryOps.Count(em, root, gold) == goldBefore && InventoryOps.PendingCount(em, root, gold) == 2, "Repair-payment path consumes pending items before inventory");
                Check(!InventoryOps.RemoveWithPending(em, root, gold, goldBefore + 3) && InventoryOps.PendingCount(em, root, gold) == 2, "Failed combined payment preserves pending items");
                SnapshotCodec.Restore(em, root, initial);
                Check(InventoryOps.Count(em, root, gold) == goldBefore, "Save roundtrip stock");
                var ids = WorldQueries.Entities<Identity>(em);
                var unique = new System.Collections.Generic.HashSet<ulong>();
                foreach (var e in ids)
                    Check(unique.Add(em.GetComponentData<Identity>(e).Id), "Persistent ID unique");
                ids.Dispose();
                DailyEconomySettlement.Settle(em, root);
                var once = SnapshotCodec.Capture(em, root);
                DailyEconomySettlement.Settle(em, root);
                Check(once.SequenceEqual(SnapshotCodec.Capture(em, root)), "Day settlement is idempotent");
                SnapshotCodec.Restore(em, root, initial);
                Check(NightOps.Begin(em, root) == ResultCode.Success, "Day -> dusk");
                Check(em.GetComponentData<GameClock>(root).Turn == 1, "Night belongs to same turn");
                Check(GameRequestExecution.Execute(em, root, new CreateSaveRequest { }) == ResultCode.WrongPhase, "Saving blocked at night");
                var dusk = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                sPersistence.CheckpointPending = 0;
                {
                    em.SetComponentData(root, sPersistence);
                }

                var preparation = em.GetComponentData<NightSettings>(root).NightPreparationSeconds;
                NightOps.Tick(em, root, preparation + .01f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Night, "Configured preparation starts active night without resetting its clock");
                Check(math.abs(em.GetComponentData<GameClock>(root).PhaseTime - preparation - .01f) < .001f, "Preparation remains part of the unified night duration");
                SnapshotCodec.Restore(em, root, dusk);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Deployment, "Dusk rollback restores restartable node");
                NightOps.Dawn(em, root);
                Check(em.GetComponentData<GameClock>(root).Turn == 2, "Only dawn increments turn");
                SnapshotCodec.Restore(em, root, initial);
                TestMilitary(em, root);
                SnapshotCodec.Restore(em, root, initial);
                TestBuildings(em, root);
                using (var owned = WorldQueries.Entities<Persistent>(em))
                    foreach (var entity in owned)
                        Check(em.HasComponent<SimulationOwner>(entity), "Runtime instance has explicit simulation ownership");
                em.DestroyEntity(root);
                SimulationLifetimeSystem.Cleanup(em);
                using (var owned = WorldQueries.Entities<SimulationOwner>(em))
                    Check(owned.Length == 0, "Unloaded map releases runtime instances");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void TestBuildings(EntityManager em, Entity root)
        {
            var warehouse = BuildingDefinitions.Find(em, root, "b仓库");
            var house = BuildingDefinitions.Find(em, root, "b居民房");
            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            // Tests use free unoccupied logical cells, independently of placement eligibility.
            var e = BuildingCreation.Create(em, root, warehouse, new int2(-1000, -1000), 0, 1, true);
            var id = em.GetComponentData<Identity>(e).Id;
            Check(em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).Any(s => s.Provider == id), "Warehouse owns its slots");
            BuildingLifecycle.Ruin(em, root, e);
            Check(em.GetComponentData<BuildingWorkforceState>(e).Workers == 0, "Ruined jobs released");
            foreach (var slot in em.GetBuffer<InventorySlot>(root))
                Check(slot.Provider != id, "Ruined storage removed");
            Check(BuildingLifecycle.Demolish(em, root, e) == ResultCode.Success && !em.Exists(e), "Ruined building demolition");
            var h = BuildingCreation.Create(em, root, house, new int2(-1000, -1000), 0, 1, true);
            Check(em.GetComponentData<BuildingHousingState>(h).Population == 2, "House completion gives two residents");
            BuildingLifecycle.Ruin(em, root, h);
            Check(em.GetComponentData<BuildingHousingState>(h).Population == 0, "House ruin kills residents");
            var tower = BuildingCreation.Create(em, root, BuildingDefinitions.Find(em, root, "b瞭望塔"), new int2(-1200, -1200), 0, 1, true);
            var intel = NightOps.Intelligence(em, root);
            Check(intel > 0 && NightOps.Begin(em, root, true) == ResultCode.Success, "Operational intelligence is snapshotted at dusk");
            BuildingLifecycle.Ruin(em, root, tower);
            Check(NightOps.Intelligence(em, root) == intel, "Tower ruin does not remove already gathered intelligence this night");
            var core = Entity.Null;
            using (var buildings = WorldQueries.Entities<Building>(em))
                foreach (var b in buildings)
                    if (em.GetComponentData<BuildingHousingStats>(b).IsCore != 0)
                        core = b;
            Check(core != Entity.Null, "PlayerHome imported as core");
            BuildingLifecycle.Ruin(em, root, core);
            Check(em.GetComponentData<Session>(root).Phase == Phase.GameOver, "Only core loss ends dynasty");
            var ended = em.GetComponentData<Session>(root);
            ended.Phase = Phase.Ended;
            em.SetComponentData(root, ended);
            Check(GameRequestExecution.Execute(em, root, new EndDynastyRequest { }) == ResultCode.WrongPhase, "Ended dynasty cannot be ended twice");
            Check(GameRequestExecution.Execute(em, root, new RetryDayRequest { }) == ResultCode.WrongPhase, "Deleted dynasty cannot retry an old node");
        }

        static void TestMilitary(EntityManager em, Entity root)
        {
            GameClock stateClock = em.GetComponentData<GameClock>(root);
            PopulationState statePopulation = em.GetComponentData<PopulationState>(root);
            statePopulation.BasePopulation = 100;
            {
                em.SetComponentData(root, stateClock);
                em.SetComponentData(root, statePopulation);
            }

            var inventory = em.GetBuffer<InventorySlot>(root);
            var original = inventory[0];
            for (var i = 0; i < 20; i++)
            {
                var slot = original;
                slot.Index = 1000 + i;
                slot.Item = em.GetComponentData<CurrencySettings>(root).Gold;
                slot.Count = 1000;
                inventory.Add(slot);
            }

            var garrison = BuildingCreation.Create(em, root, BuildingDefinitions.Find(em, root, "b驻军营地"), new int2(-100, -100), 0, 1, true);
            var id = em.GetComponentData<Identity>(garrison).Id;
            var militia = SoldierDefinitions.Find(em, root, "militia");
            Check(SoldierOps.RecruitSoldiers(em, root, new RecruitSoldiersRequest { Garrison = id, Soldier = militia, Quantity = 1 }) == ResultCode.Success, "Soldier recruitment reserves population");
            var troop = GarrisonOps.AtSlot(em, id, 1);
            var stableId = em.GetComponentData<Identity>(troop).Id;
            Check(SoldierOps.RecruitSoldiers(em, root, new RecruitSoldiersRequest { Garrison = id, Soldier = militia, Quantity = 1 }) == ResultCode.Success, "Second soldier recruited for capacity test");
            BuildingGarrisonStats statsGarrison = em.GetComponentData<BuildingGarrisonStats>(garrison);
            statsGarrison.Capacity = 1;
            {
                em.SetComponentData(garrison, statsGarrison);
            }

            GarrisonOps.ReconcileGarrisons(em, root);
            Check(GarrisonOps.GarrisonCount(em, id) == 1 && em.GetComponentData<Soldier>(troop).Garrison == id && em.GetComponentData<Soldier>(troop).Slot == 1, "Reduced garrison capacity retains slot one and unassigns overflow");
            Check(NightOps.Begin(em, root) == ResultCode.ConfirmationRequired, "Unassigned soldier dismissal requires night-entry confirmation");
            BattleLifecycle.PrepareNight(em, root);
            var actor = em.GetComponentData<Combatant>(troop);
            actor.Deployed = 1;
            em.SetComponentData(troop, actor);
            BuildingLifecycle.Ruin(em, root, garrison);
            Check(em.GetComponentData<Soldier>(troop).Garrison == 0, "Ruined garrison moves soldiers to unassigned pool");
            Check(em.GetComponentData<Health>(troop).Current == em.GetComponentData<Health>(troop).Maximum, "Already deployed soldiers keep health when garrison is ruined");
            BattleLifecycle.PrepareNight(em, root);
            Check(WorldQueries.Find(em, stableId) == Entity.Null, "Unassigned pool disbands before next night");
            var templeDefinition = BuildingDefinitions.Find(em, root, "b泰坦神殿");
            var templeGrid = em.GetComponentData<GridData>(root);
            Entity temple = Entity.Null;
            for (int y = 10; y < templeGrid.Value.Value.Size.y - 10 && temple == Entity.Null; y++)
                for (int x = 10; x < templeGrid.Value.Value.Size.x - 10; x++)
                    if (GridOps.CanPlace(em, root, templeDefinition, new int2(x, y), 0))
                    {
                        temple = BuildingCreation.Create(em, root, templeDefinition, new int2(x, y), 0, 1, true);
                        break;
                    }

            Check(temple != Entity.Null, "Hero fixture has a legal temple footprint");
            var templeId = em.GetComponentData<Identity>(temple).Id;
            Check(HeroOps.Recruit(em, root, new RecruitHeroRequest { Sanctum = templeId }) == ResultCode.InsufficientPopulation, "Titan requires thirty temple workers");
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(temple);
            BuildingSanctumState bSanctum = em.GetComponentData<BuildingSanctumState>(temple);
            bWorkforce.Workers = 30;
            bSanctum.Offering = 1;
            bWorkforce.Subsidy = 1;
            {
                em.SetComponentData(temple, bWorkforce);
                em.SetComponentData(temple, bSanctum);
            }

            Check(HeroOps.Recruit(em, root, new RecruitHeroRequest { Sanctum = templeId }) == ResultCode.Success, "Titan recruited as separate persistent hero");
            DailyEconomySettlement.Settle(em, root);
            {
                bWorkforce = em.GetComponentData<BuildingWorkforceState>(temple);
                bSanctum = em.GetComponentData<BuildingSanctumState>(temple);
            }

            Check(bSanctum.PaidOfferingTurn == em.GetComponentData<GameClock>(root).Turn, "Continuous offering paid during day settlement");
            Entity hero;
            using (var heroes = WorldQueries.Entities<Hero>(em))
                hero = heroes[0];
            Check(em.GetComponentData<Hero>(hero).Experience == 1, "Offering grants experience");
            var nightState = em.GetComponentData<Session>(root);
            NightRuntimeState nightStateNight = em.GetComponentData<NightRuntimeState>(root);
            nightState.Phase = Phase.Night;
            nightStateNight.Kind = NightKind.Peaceful;
            {
                em.SetComponentData(root, nightState);
                em.SetComponentData(root, nightStateNight);
            }

            Check(GameRequestExecution.Execute(em, root, new WakeHeroRequest { Sanctum = templeId }) == ResultCode.Success, "Paid Titan can be awakened even during a peaceful night");
            Check(HeroOps.Wake(em, root, temple) == ResultCode.Unavailable, "Awakening cannot be charged twice");
            var heroId = em.GetComponentData<Identity>(hero).Id;
            Check(GameRequestExecution.Execute(em, root, new SelectHeroRequest { Hero = heroId }) == ResultCode.Success, "Deployed hero can be selected");
            var anchor = EntityState.Position(em, hero);
            GameRequestExecution.Execute(em, root, new MoveHeroRequest { Destination = anchor });
            GameRequestExecution.Execute(em, root, new SelectHeroRequest { });
            Check(em.GetComponentData<UnitOrder>(hero).Kind == OrderKind.Move && math.all(em.GetComponentData<Combatant>(hero).Home == anchor), "Deselected hero keeps its last positional order and guard anchor");
            Check(em.GetComponentData<Combatant>(hero).Participated == 0, "Awakening alone does not count as actual combat");
            var enemy = EnemyEntities.Spawn(em, root, EnemyDefinitions.Find(em, root, "raider"), EntityState.Position(em, hero), false);
            EnemyCombatants.Configure(em, root, enemy, true, 0, EntityState.Position(em, enemy));
            CombatOps.ApplyDamage(em, root, new DamageRequest { Source = hero, Target = enemy, Amount = 0 });
            Check(em.GetComponentData<Combatant>(hero).Participated == 0, "Zero-effect attack grants no participation");
            CombatOps.ApplyDamage(em, root, new DamageRequest { Source = hero, Target = enemy, Amount = 1 });
            Check(em.GetComponentData<Combatant>(hero).Participated == 1, "Effective hostile damage counts as participation");
            em.DestroyEntity(enemy);
            BuildingLifecycle.Ruin(em, root, temple);
            var h = em.GetComponentData<Hero>(hero);
            Check(h.Recruited == 0 && h.Experience == 0 && h.DeathPending != 0 && h.CooldownUntil == 0, "Temple ruin marks hero dead; cooldown waits for dawn");
            BattleLifecycle.Dawn(em, root);
            h = em.GetComponentData<Hero>(hero);
            Check(h.DeathPending == 0 && h.CooldownUntil == em.GetComponentData<GameClock>(root).Turn + 11, "Dawn commits hero population release and full cooldown");
            BuildingLifecycle.Demolish(em, root, temple);
            var replacement = BuildingCreation.Create(em, root, BuildingDefinitions.Find(em, root, "b泰坦神殿"), new int2(-110, -110), 0, 1, true);
            BuildingWorkforceState replacementStateWorkforce = em.GetComponentData<BuildingWorkforceState>(replacement);
            replacementStateWorkforce.Workers = 30;
            {
                em.SetComponentData(replacement, replacementStateWorkforce);
            }

            var replacementId = em.GetComponentData<Identity>(replacement).Id;
            Check(HeroOps.Recruit(em, root, new RecruitHeroRequest { Sanctum = replacementId }) == ResultCode.Unavailable, "Rebuilding temple cannot bypass hero cooldown");
            {
                stateClock = em.GetComponentData<GameClock>(root);
                statePopulation = em.GetComponentData<PopulationState>(root);
            }

            stateClock.Turn = h.CooldownUntil;
            {
                em.SetComponentData(root, stateClock);
                em.SetComponentData(root, statePopulation);
            }

            Check(HeroOps.Recruit(em, root, new RecruitHeroRequest { Sanctum = replacementId }) == ResultCode.Success, "Replacement temple recruits after original cooldown");
            Check(em.GetComponentData<Hero>(hero).Sanctum == replacementId && em.GetComponentData<Hero>(hero).Experience == 0, "Recruitment rebinds original hero identity with cleared experience");
        }

        [MenuItem("Landsong/ECS/Build Windows baseline")]
        public static string Build() => BuildPlayer(true);
        [MenuItem("Landsong/ECS/Build Windows release")]
        public static string BuildRelease() => BuildPlayer(false);
        static string BuildPlayer(bool development)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building.");
            if (UnityEngine.Application.isBatchMode && !EditorSceneManager.GetSceneManagerSetup().Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.OpenScene(Landsong.ECS.Presentation.EcsSceneFlow.Boot);
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (!scenes.SequenceEqual(Landsong.ECS.Presentation.EcsSceneFlow.BuildScenes))
                throw new InvalidOperationException("Build settings must contain exactly Boot, Start, LoadingTransition, Game in that order.");
            var folder = development ? "Builds/ECSBaseline" : "Builds/ECSRelease";
            Directory.CreateDirectory(folder);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = folder + "/Landsong.exe", target = BuildTarget.StandaloneWindows64, options = development ? BuildOptions.Development : BuildOptions.None });
            var summary = result.summary;
            var message = summary.result + "; errors=" + summary.totalErrors + "; warnings=" + summary.totalWarnings + "; " + summary.totalTime;
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText(development ? "Library/LandsongEcs/build.txt" : "Library/LandsongEcs/build-release.txt", message);
            if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                throw new InvalidOperationException(message);
            return message;
        }
    }
}
#endif
