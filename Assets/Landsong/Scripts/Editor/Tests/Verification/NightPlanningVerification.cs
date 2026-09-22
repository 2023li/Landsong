#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.AI;
using Landsong.ECS.Authoring.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class NightPlanningVerification
    {
        static StringBuilder log;
        static int checks;
        static T[] Buffer<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            using var values = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return values.ToArray();
        }

        static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException("FAIL " + name);
            checks++;
            log.AppendLine("PASS " + name);
        }

        static void Reject(Action action, string name)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (Exception e)when (e is InvalidOperationException || e is InvalidDataException)
            {
                rejected = true;
            }

            Check(rejected, name);
        }

        [MenuItem("Landsong/ECS/Verification/NightPlanning")]
        public static string Run()
        {
            log = new StringBuilder();
            checks = 0;
            try
            {
                Configuration();
                Map();
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception e)
            {
                log.AppendLine(e.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/night-planning-verification.txt", log.ToString());
            }
        }

        static BlobAssetReference<NightEventCatalogBlob> Build(NightEventCatalogAsset source) => NightEventCatalogCompiler.Build(source, new EnemyCatalogIndex(AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/EnemyCatalog.asset")), new BuildingCatalogIndex(AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset")), new ItemCatalogIndex(AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset")), new TechnologyCatalogIndex(AssetDatabase.LoadAssetAtPath<TechnologyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TechnologyCatalog.asset")), new BuffCatalogIndex(AssetDatabase.LoadAssetAtPath<BuffCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuffCatalog.asset")), new FeatureCatalogIndex(AssetDatabase.LoadAssetAtPath<FeatureCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/FeatureCatalog.asset")), new QuestCatalogIndex(AssetDatabase.LoadAssetAtPath<QuestCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/QuestCatalog.asset")), new ExpeditionCatalogIndex(AssetDatabase.LoadAssetAtPath<ExpeditionCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ExpeditionCatalog.asset")));
        static void Configuration()
        {
            var source = AssetDatabase.LoadAssetAtPath<NightEventCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/NightEventCatalog.asset");
            using (var built = Build(source))
                Check(built.Value.Events.Length >= 4, "Authored peaceful raid boss and return events");
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/EnemyCatalog.asset");
            Check(enemies.Definitions.Any(e => e.Metadata.Id == "invader"), "Distinct core assault enemy placeholder");
            var palace = AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>("Assets/Landsong/ECSContent/Definitions/Building/b王宫.asset");
            Check(palace.ResourceConnectionActionPower == 20 && palace.PlacementAndVisuals.SpawnExclusionPadding == 30, "Palace saves 20 resource action power and 30-point patrol movement budget");
            {
                var settings = ContentAuthoringContext.Content().Night;
                Check(settings.NightSeconds == 300 && settings.NightPreparationSeconds == 3 && settings.NightClosureSeconds == 10 && settings.DawnSeconds == 3 && settings.TotalNightSeconds == 313, "Night defaults sum 3 preparation + 300 formal + 10 closure, with 3 dawn outside night");
                var invalid = settings;
                invalid.NightPreparationSeconds = -1;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Negative preparation duration rejected");
                invalid = settings;
                invalid.DawnSeconds = -1;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Negative dawn duration rejected");
                invalid = settings;
                invalid.NightClosureSeconds = -1;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Negative closure duration rejected");
                invalid = settings;
                invalid.RetreatDelaySeconds = float.NaN;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Non-finite closure delay rejected");
                invalid = settings;
                invalid.CelebrationDelaySeconds = 20;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Closure delay sum cannot exceed total closure duration");
                invalid = settings;
                invalid.VictoryAdvanceDelaySeconds = -1;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Negative victory advance delay rejected");
                invalid = settings;
                invalid.WaveIntervalSeconds = 0;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Non-positive default wave interval rejected");
                Check(settings.RetreatDelaySeconds == 2 && settings.ClosureVictoryCaptionAt == 4 && settings.ClosureCelebrationAt == 6, "Default timeout closure beats occur at seconds 2, 4 and 6");
                Check(settings.BattleVictoryCaptionAt == 2 && settings.BattleCelebrationAt == 4 && settings.BattleAdvanceAt == 7, "Early victory follows the configured two-two-three second beats");
                Check(settings.WaveIntervalSeconds == 15 && math.abs(NightPlanOps.DefaultWaveAt(settings, 1, 3) * settings.NightSeconds - 15) < .001f, "Default battle waves use a compact fifteen-second maximum interval");
                invalid = settings;
                invalid.NightPreparationSeconds = 400;
                invalid.DawnSeconds = 500;
                NightSettingsAuthoring.Validate(invalid);
                Check(invalid.NightSeconds == 300 && invalid.TotalNightSeconds == 710, "Independent preparation and dawn never subtract from formal night");
                invalid = settings;
                invalid.NightSeconds = float.NaN;
                Reject(() => NightSettingsAuthoring.Validate(invalid), "Non-finite night duration rejected");
            }

            var captions = UnityEngine.ScriptableObject.CreateInstance<Landsong.ECS.Presentation.NightCaptionDefinition>();
            try
            {
                Check(captions.NightCaptionDelay == 3 && captions.PeacefulAdvanceDelay == 2 && captions.Caption(Phase.Night, NightKind.Peaceful, 2.99f) == "", "Night caption stays hidden during the first three formal-night seconds");
                Check(captions.Caption(Phase.Night, NightKind.Peaceful, 3) == captions.PeacefulNightCaption, "Peaceful caption appears three seconds into formal night");
                Check(!captions.PeacefulAdvanceReady(Phase.Night, NightKind.Peaceful, 4.99f) && captions.PeacefulAdvanceReady(Phase.Night, NightKind.Peaceful, 5), "Peaceful advance appears two seconds after its caption");
                Check(!captions.PeacefulAdvanceReady(Phase.Night, NightKind.Invasion, 20), "Battle notice never exposes peaceful advancement");
                Check(captions.Caption(Phase.Night, NightKind.Boss, 3) == captions.InvasionNightCaption, "Battle caption shares the configured delay");
                Check(captions.Caption(Phase.Retreat, NightKind.Boss, 303, 3.99f, 4) == "" && captions.Caption(Phase.Retreat, NightKind.Boss, 304, 4, 4) == captions.VictoryNightCaption, "Victory caption begins exactly at the configured closure beat");
                Check(captions.Caption(Phase.Celebration, NightKind.Boss, 100, battleVictoryElapsed: 1.99f, battleVictoryAt: 2) == "" && captions.Caption(Phase.Celebration, NightKind.Boss, 100, battleVictoryElapsed: 2, battleVictoryAt: 2) == captions.VictoryNightCaption, "Early victory caption waits two seconds after the last enemy falls");
                captions.NightCaptionDelay = 5;
                Check(captions.Caption(Phase.Night, NightKind.Peaceful, 4) == "" && captions.Caption(Phase.Night, NightKind.Peaceful, 5) != "" && captions.PeacefulAdvanceReady(Phase.Night, NightKind.Peaceful, 7), "Caption and following button delay are independently configurable");
                Check(captions.Caption(Phase.Deployment, NightKind.Peaceful, 10) == "" && captions.Caption(Phase.Retreat, NightKind.Peaceful, 10) == "", "Caption does not leak into sunset or closure");
            }
            finally { UnityEngine.Object.DestroyImmediate(captions); }
            var copy = UnityEngine.Object.Instantiate(source);
            void Validate()
            {
                using var blob = Build(copy);
            }

            try
            {
                copy.Events[0].Weight = float.NaN;
                Reject(Validate, "NaN event weight rejected");
                copy.Events[0].Weight = 1;
                var key = copy.Events[1].Id;
                copy.Events[1].Id = copy.Events[0].Id;
                Reject(Validate, "Duplicate event key rejected");
                copy.Events[1].Id = key;
                copy.Events[1].WaveTimes = new[]
                {
                    0f,
                    .5f,
                    1f
                };
                Reject(Validate, "Wave scheduled after retreat boundary rejected");
                copy.Events[1].WaveTimes = Array.Empty<float>();
                copy.Events[2].FollowUp = "unknown";
                Reject(Validate, "Unresolved aftermath rejected");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }

        static void Map()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Wave ten isolated night verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                EnemyId Def(string id) => EnemyDefinitions.Find(em, root, new FixedString128Bytes(id));
                var original = SnapshotCodec.Capture(em, root);
                var settings = em.GetComponentData<NightSettings>(root);
                void Reset()
                {
                    em.SetComponentData(root, settings);
                    SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                }

                void PhaseTo(Phase phase)
                {
                    var s = em.GetComponentData<Session>(root);
                    GameClock sClock = em.GetComponentData<GameClock>(root);
                    PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                    s.Phase = phase;
                    sClock.PhaseTime = 0;
                    sClock.DawnRemaining = 0;
                    sPersistence.CheckpointPending = 0;
                    {
                        em.SetComponentData(root, s);
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sPersistence);
                    }
                }

                void Arrive()
                {
                    using var units = WorldQueries.Entities<Combatant>(em);
                    foreach (var unit in units)
                    {
                        var actor = em.GetComponentData<Combatant>(unit);
                        if (actor.Faction != 0 || actor.Deployed == 0 || !EntityState.Alive(em, unit))
                            continue;
                        var order = em.GetComponentData<UnitOrder>(unit);
                        Check(order.Kind == OrderKind.Recall && order.Source != 0, "Survivor receives reachable building recall");
                        var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(unit);
                        transform.Position = order.Destination;
                        em.SetComponentData(unit, transform);
                    }

                    NightOps.Tick(em, root, .1f);
                }

                void Force(int turn, bool boss = false)
                {
                    var s = em.GetComponentData<Session>(root);
                    GameClock sClock = em.GetComponentData<GameClock>(root);
                    PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                    sClock.Turn = turn;
                    s.Phase = Phase.Day;
                    sPersistence.CheckpointPending = 0;
                    {
                        em.SetComponentData(root, s);
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sPersistence);
                    }

                    var cfg = settings;
                    cfg.FirstInvasion = 1;
                    cfg.InvasionChance = 1;
                    cfg.FirstBoss = boss ? turn : 99999;
                    em.SetComponentData(root, cfg);
                    EntityState.Set(em, root, new NightPlanState { BossDefinition = EnemyId.None });
                    NightOps.Plan(em, root, false);
                }

                Entity Core()
                {
                    using var sites = WorldQueries.OrderedEntities<Building>(em);
                    foreach (var e in sites)
                        if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                            return e;
                    throw new InvalidOperationException();
                }

                Entity Garrison()
                {
                    BuildingId definition = default;
                    for (int i = 0; i < BuildingDefinitions.Count(em, root); i++)
                    {
                        var id = BuildingId.FromIndex(i);
                        ref var value = ref BuildingDefinitions.Get(em, root, id);
                        bool coreBuilding = false;
                        for (int n = 0; n < value.Capabilities.Housing.Population.Length; n++)
                            coreBuilding |= value.Capabilities.Housing.Population[n].IsCore;
                        if (!coreBuilding && value.Capabilities.Garrison.Enabled)
                        {
                            definition = id;
                            break;
                        }
                    }

                    var grid = em.GetComponentData<GridData>(root);
                    for (int y = 15; y < grid.Value.Value.Size.y - 15; y++)
                        for (int x = 15; x < grid.Value.Value.Size.x - 15; x++)
                            if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0))
                                return BuildingCreation.Create(em, root, definition, new int2(x, y), 0, 1, true);
                    throw new InvalidOperationException("No test garrison footprint");
                }

                Entity Soldier(Entity home)
                {
                    var e = SoldierEntities.Spawn(em, root, SoldierId.FromIndex(0), EntityState.Position(em, home), true);
                    SoldierCombatants.Configure(em, root, e, false, Id(home), EntityState.Position(em, home));
                    EntityState.Set(em, e, new Soldier { Garrison = Id(home), Slot = GarrisonOps.FreeSlot(em, home), PopulationCost = 1 });
                    return e;
                }

                using (var conditionBuilder = new BlobBuilder(Allocator.Temp))
                {
                    ref var condition = ref conditionBuilder.ConstructRoot<NightEventDefinition>();
                    condition.Id = "verification.building-condition";
                    condition.MinTurn = 1;
                    var building = Core();
                    var before = em.GetComponentData<Building>(building);
                    var required = conditionBuilder.Allocate(ref condition.Conditions.Buildings, 1);
                    required[0] = new NightBuildingCondition
                    {
                        Building = em.GetComponentData<BuildingDefinitionRef>(building).Definition,
                        Count = 1,
                        MinimumLevel = before.Level + 1
                    };
                    using var blob = conditionBuilder.CreateBlobAssetReference<NightEventDefinition>(Allocator.Persistent);
                    Check(!NightPlanOps.Eligible(em, root, ref blob.Value, false), "Building below night condition level is rejected");
                    var upgraded = before;
                    upgraded.Level++;
                    em.SetComponentData(building, upgraded);
                    Check(NightPlanOps.Eligible(em, root, ref blob.Value, false), "Operational building satisfies required night level");
                    upgraded.Stage = LifeStage.Construction;
                    em.SetComponentData(building, upgraded);
                    Check(!NightPlanOps.Eligible(em, root, ref blob.Value, false), "Construction stage does not satisfy night building condition");
                    em.SetComponentData(building, before);
                }

                Force(3);
                var baseline = SnapshotCodec.Capture(em, root);
                NightRuntimeState lockedNight = em.GetComponentData<NightRuntimeState>(root);
                NightOps.Plan(em, root, false);
                Check(baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Same-turn plan call is entirely idempotent including RNG");
                var barracks = Garrison();
                var soldier = Soldier(barracks);
                var barracksPlacement = em.GetComponentData<BuildingPlacementState>(barracks);
                var barracksGrid = em.GetComponentData<GridData>(root);
                var exitProbe = GridOps.Position(barracksGrid, barracksPlacement.Cell + new int2(barracksPlacement.Size.x, 0), new int2(1));
                Check(NavigationOps.TryNearestOpenOnSurface(em, root, exitProbe, 12, barracksPlacement.Surface, barracksPlacement.Elevation, out var matchingExit), "Deployment resolves an open exit on the garrison surface and elevation");
                bool exitMatchesLayer = false;
                foreach (var node in em.GetBuffer<SurfaceNavNode>(root))
                    exitMatchesLayer |= node.Open != 0 && node.Surface == barracksPlacement.Surface && node.Elevation == barracksPlacement.Elevation && math.distancesq(node.Position, matchingExit) < .0001f;
                Check(exitMatchesLayer, "Resolved deployment exit cannot fall through to an overlapping lower layer");
                using (var patrolAreas = TacticalPatrolAreas.Build(em, root, Allocator.Temp))
                {
                    var expectedBudget = BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(barracks).Definition).PlacementAndVisuals.SpawnExclusionPadding;
                    using var reach = new NightSpatialOps.Reach(em, root, matchingExit);
                    bool complete = true;
                    float maximumCost = 0;
                    for (int sector = 0; sector < TacticalPatrolAreas.SectorCount; sector++)
                    {
                        if (!patrolAreas.TryGetValue(new TacticalPatrolKey(Id(barracks), sector), out var patrolPoint))
                        {
                            complete = false;
                            continue;
                        }
                        var measured = reach.Distance(patrolPoint.Position);
                        complete &= patrolPoint.Budget == expectedBudget && patrolPoint.Cost <= expectedBudget + .001f && math.abs(measured - patrolPoint.Cost) <= .001f;
                        maximumCost = math.max(maximumCost, patrolPoint.Cost);
                    }
                    Check(complete && maximumCost > 0, "Garrison patrol points use SpawnExclusionPadding as weighted navigation budget");
                }
                var power = MilitaryStrength.Calculate(em, root);
                var unit = em.GetComponentData<Soldier>(soldier);
                unit.Garrison = 0;
                em.SetComponentData(soldier, unit);
                Check(MilitaryStrength.Calculate(em, root) < power, "Unassigned troops excluded from effective combat strength");
                unit.Garrison = Id(barracks);
                em.SetComponentData(soldier, unit);
                NightOps.Plan(em, root, false);
                Check(em.GetComponentData<NightRuntimeState>(root).StartCombatStrength == lockedNight.StartCombatStrength && em.GetComponentData<NightRuntimeState>(root).Threat == lockedNight.Threat, "Day recruitment cannot raise locked invasion budget");
                var waves = em.GetBuffer<NightWave>(root);
                float sum = 0;
                foreach (var w in waves)
                {
                    Check(w.At >= 0 && w.At <= 1 && w.Count > 0, "Wave uses normalized time and positive count");
                    sum += w.Count * EnemyDefinitions.Get(em, root, w.Definition).ThreatValue * w.PowerScale;
                }
                for (int i = 1; i < waves.Length; i++)
                    Check((waves[i].At - waves[i - 1].At) * settings.NightSeconds <= settings.WaveIntervalSeconds + .001f, "Default waves stay within the compact maximum interval");
                Check(waves.Length == 0 || waves[waves.Length - 1].At <= .5f, "Default waves finish within the first half of formal night");

                Check(math.abs(sum - lockedNight.Threat) < .01f, "Composition normalized to exact threat budget");
                var gridData = em.GetComponentData<GridData>(root);
                foreach (var region in em.GetBuffer<SpawnRegion>(root))
                {
                    Check(GridOps.Index(gridData, GridOps.Cell(gridData, region.Center)) >= 0, "Dynamic region lies inside actual grid");
                }

                int legalRegion = -1;
                float3 point = default;
                for (int i = 0; i < em.GetBuffer<SpawnRegion>(root).Length; i++)
                    if (NightSpatialOps.SpawnPoint(em, root, i, em.GetBuffer<SpawnRegion>(root)[i].Center, out point))
                    {
                        legalRegion = i;
                        break;
                    }

                Check(legalRegion >= 0, "Map Test2 has a legal safe entry");
                Check(NightSpatialOps.Inside(em.GetBuffer<SpawnRegion>(root)[legalRegion], point), "Resolved spawn point inside shared intelligence region");
                var assault = NightSpatialOps.Target(em, root, Def("invader"), point, point, 0, false);
                Check(assault == Core(), "Core assault chooses reachable core");
                var repeat = NightSpatialOps.Target(em, root, Def("raider"), point, point, 0, false);
                Check(repeat != Entity.Null && repeat == NightSpatialOps.Target(em, root, Def("raider"), point, point, 0, false), "Harassment target randomization deterministic without consuming day RNG");
                PhaseTo(Phase.Night);
                var oldPosition = EntityState.Position(em, barracks);
                BuildingLifecycle.Ruin(em, root, barracks);
                var fallback = NightSpatialOps.Target(em, root, Def("raider"), point, oldPosition, Id(barracks), true);
                Check(fallback != Entity.Null && fallback != barracks && NightSpatialOps.ValidTarget(em, fallback), "Ruined original target replaced by reachable nearby site or core");
                // Restore this fixture's garrison before testing blocked deployment.
                var repaired = em.GetComponentData<Building>(barracks);
                repaired.Stage = LifeStage.Operational;
                repaired.RuinPending = 0;
                em.SetComponentData(barracks, repaired);
                GridOps.Occupy(em, root, barracks);
                using (var occupied = em.GetBuffer<Occupancy>(root).ToNativeArray(Allocator.Temp))
                {
                    var slots = em.GetBuffer<Occupancy>(root);
                    for (int i = 0; i < slots.Length; i++)
                        slots[i] = new Occupancy
                        {
                            Owner = 999999
                        };
                    Check(!NightSpatialOps.SpawnPoint(em, root, legalRegion, point, out _), "Blocked invasion region reports failure, no teleport");
                    Check(NightSpatialOps.Target(em, root, Def("invader"), point, point, 0, false) == Entity.Null, "Unreachable targets are rejected instead of attack-through-walls");
                    Check(!NavigationOps.TryNearestOpen(em, root, EntityState.Position(em, barracks), 12, out _), "Blocked garrison exit reports failure");
                    PhaseTo(Phase.Night);
                    BattleLifecycle.PrepareNight(em, root);
                    NightOps.Tick(em, root, .1f);
                    Check(em.GetComponentData<Combatant>(soldier).Deployed == 0 && em.GetComponentData<Combatant>(soldier).DeployAt == float.MaxValue, "Failed deployment stays home and does not retry spam");
                    var stranded = em.GetComponentData<Combatant>(soldier);
                    stranded.Deployed = 1;
                    em.SetComponentData(soldier, stranded);
                    em.SetComponentData(soldier, new VisualState { Visible = 1 });
                    NightReturnOps.Begin(em, root);
                    Check(em.HasComponent<DayReturnState>(soldier) && em.GetComponentData<VisualState>(soldier).Visible != 0, "Blocked return stays visible until its independent timeout");
                    PhaseTo(Phase.Day);
                    NightReturnOps.Tick(em, root, 31);
                    Check(em.GetComponentData<Combatant>(soldier).Deployed == 0 && !em.HasComponent<DayReturnState>(soldier), "Blocked return times out without blocking day");
                    slots = em.GetBuffer<Occupancy>(root);
                    slots.CopyFrom(occupied);
                }

                Reset();
                barracks = Garrison();
                soldier = Soldier(barracks);
                var sid = Id(soldier);
                Soldier(barracks);
                Soldier(barracks);
                Soldier(barracks);
                NightPlanOps.Prepare(em, root);
                PhaseTo(Phase.Settlement);
                BattleLifecycle.PrepareNight(em, root);
                using (var all = WorldQueries.OrderedEntities<Soldier>(em))
                {
                    var first = em.GetComponentData<Combatant>(all[0]).DeployAt;
                    var last = em.GetComponentData<Combatant>(all[all.Length - 1]).DeployAt;
                    Check(last > first, "All soldiers receive deterministic garrison batches");
                }

                var prep = Buffer<PreparedSoldier>(em, root);
                var snapshot = SnapshotCodec.Capture(em, root); // Settlement cannot be loaded; capture next at legal dusk.
                PhaseTo(Phase.Deployment);
                snapshot = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, snapshot));
                soldier = WorldQueries.Find(em, sid);
                Check(prep.SequenceEqual(Buffer<PreparedSoldier>(em, root)), "Dusk rebuild retains exact prepared modifiers");
                Check(em.GetComponentData<Combatant>(soldier).Damage == prep.First(p => p.Definition == em.GetComponentData<SoldierDefinitionRef>(soldier).Definition).Stats.Damage, "Rebuilt unit uses prepared attack");
                var modifierState = CourtOps.State(em, root);
                modifierState.TemporaryAttack = .5f;
                modifierState.TemporaryUntil = 10;
                EntityState.Set(em, root, modifierState);
                var troopDef = em.GetComponentData<SoldierDefinitionRef>(soldier).Definition;
                var frozenDamage = em.GetComponentData<Combatant>(soldier).Damage;
                Check(SoldierCombatStats.Current(em, root, troopDef).Damage > frozenDamage, "Fixture changes live court modifier after preparation");
                SoldierCombatants.Configure(em, root, soldier, false, em.GetComponentData<Soldier>(soldier).Garrison, EntityState.Position(em, soldier));
                Check(em.GetComponentData<Combatant>(soldier).Damage == frozenDamage, "Night reconfiguration cannot replace frozen combat modifiers");
                foreach (var fail in new[]
                {
                    "root-reset",
                    "record-created",
                    "garrisons-prepared",
                    "root-published",
                    "before-retire"
                }

                )
                {
                    var before = SnapshotCodec.Capture(em, root);
                    bool threw = false;
                    try
                    {
                        SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, before), probe: stage =>
                        {
                            if (stage == fail)
                                throw new InvalidOperationException("injected");
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        threw = true;
                    }

                    Check(threw && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Night state rolls back restore failure at " + fail);
                }

                Reset();
                PhaseTo(Phase.Deployment);
                NightRuntimeState peacefulNight = em.GetComponentData<NightRuntimeState>(root);
                peacefulNight.Kind = NightKind.Peaceful;
                {
                    em.SetComponentData(root, peacefulNight);
                }

                em.GetBuffer<NightWave>(root).Clear();
                NightOps.Tick(em, root, 2.9f);
                Check(GameRequestExecution.Execute(em, root, new AdvanceRequest()) == ResultCode.WrongPhase, "Preparation cannot be skipped before the configured notice");
                NightOps.Tick(em, root, .11f);
                Check(NightOps.CanEndNight(em, root) && em.GetComponentData<GameClock>(root).Turn == 1, "Peaceful next-phase becomes available after preparation");
                Check(GameRequestExecution.Execute(em, root, new AdvanceRequest()) == ResultCode.Success && em.GetComponentData<Session>(root).Phase == Phase.Day, "Peaceful skip bypasses closure and enters dawn directly");
                Check(em.GetComponentData<GameClock>(root).DawnRemaining == settings.DawnSeconds, "Peaceful skip starts the full independent sunrise");
                Check(em.GetComponentData<GameClock>(root).Turn == 2 && em.GetComponentData<Session>(root).Phase == Phase.Day, "Peaceful dawn advances immediately without awaiting soldiers");
                Arrive();
                Reset();
                PhaseTo(Phase.Deployment);
                em.GetBuffer<NightWave>(root).Clear();
                Check(em.GetComponentData<NightRuntimeState>(root).Duration == 313, "Runtime night budget is the sum of all three night stages");
                NightOps.Tick(em, root, settings.NightPreparationSeconds);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Night, "Formal night begins after preparation");
                NightOps.Tick(em, root, settings.NightSeconds - .25f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Night, "Formal night keeps all 300 configured seconds");
                NightOps.Tick(em, root, .25f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Retreat && math.abs(NightOps.Progress(em, root) - 303f / 313f) < .0001f, "Closure begins at second 303 with the unified progress intact");
                Check(GameRequestExecution.Execute(em, root, new WakeHeroRequest()) == ResultCode.WrongPhase, "Closure rejects new combat deployment");
                NightOps.Tick(em, root, settings.NightClosureSeconds - .25f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Retreat, "Closure keeps its full independent interval");
                NightOps.Tick(em, root, .25f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Day && em.GetComponentData<GameClock>(root).DawnRemaining == settings.DawnSeconds, "Natural night completes at second 313 and then starts dawn");
                NightOps.Tick(em, root, settings.DawnSeconds - .25f);
                Check(em.GetComponentData<GameClock>(root).DawnRemaining == .25f && em.GetComponentData<Session>(root).Phase == Phase.Day, "Dawn remains in the interactive day for its own duration");
                NightOps.Tick(em, root, .25f);
                Check(em.GetComponentData<GameClock>(root).DawnRemaining == 0, "Daylight starts after the configured dawn interval");
                Reset();
                PhaseTo(Phase.Night);
                Check(GameRequestExecution.Execute(em, root, new SetNightSpeedRequest { Speed = 2 }) == ResultCode.Success, "Peaceful 2x accepted");
                NightOps.Tick(em, root, 1);
                Check(em.GetComponentData<GameClock>(root).PhaseTime == 2, "Peaceful speed scales simulated time");
                GameRequestExecution.Execute(em, root, new PauseRequest());
                var paused = em.GetComponentData<GameClock>(root).Time;
                NightOps.Tick(em, root, 10);
                Check(em.GetComponentData<GameClock>(root).Time == paused, "Pause freezes night time");
                Check(GameRequestExecution.Execute(em, root, new RingBellRequest { Building = Id(Core()) }) == ResultCode.Busy, "Pause rejects tactical commands at authority boundary");
                GameRequestExecution.Execute(em, root, new PauseRequest());
                Reset();
                Force(3);
                PhaseTo(Phase.Deployment);
                Check(GameRequestExecution.Execute(em, root, new SetNightSpeedRequest { Speed = 2 }) == ResultCode.WrongPhase, "Battle fixed at 1x");
                NightOps.Tick(em, root, 1);
                Check(em.GetComponentData<GameClock>(root).PhaseTime == 1 && NightPlanOps.State(em, root).AnySpawned == 0 && NightOps.Progress(em, root) > 0 && NightOps.Progress(em, root) < .9f, "Preparation counts toward the unified night clock and progress before enemy entry");
                NightOps.Tick(em, root, 1);
                NightOps.Tick(em, root, 1);
                NightOps.Tick(em, root, 1);
                Check(NightPlanOps.State(em, root).AnySpawned == 1 && NightPlanOps.State(em, root).CombatElapsed == 4, "Warning and protection do not extend the night duration");
                Entity enemy = Entity.Null;
                using (var all = WorldQueries.Entities<Combatant>(em))
                    foreach (var e in all)
                        if (em.GetComponentData<Combatant>(e).Faction == 1 && EntityState.Alive(em, e))
                        {
                            enemy = e;
                            break;
                        }

                Check(enemy != Entity.Null, "Actual lawful enemy instantiated");
                var hp = em.GetComponentData<Health>(enemy).Current;
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = enemy, Amount = 1000 });
                Check(em.GetComponentData<Health>(enemy).Current == hp, "Entry protection blocks incoming damage");
                NightOps.Tick(em, root, 1);
                Check(NightPlanOps.State(em, root).ClockStarted == 1, "Unified clock continues after protection");
                var actor = em.GetComponentData<Combatant>(enemy);
                Check(NightSpatialOps.Inside(em.GetBuffer<SpawnRegion>(root)[em.GetBuffer<NightWave>(root)[0].Region], EntityState.Position(em, enemy)), "Actual spawn did not drift outside intelligence region");
                var plan = NightPlanOps.State(em, root);
                GameClock endingClock = em.GetComponentData<GameClock>(root);
                endingClock.PhaseTime = NightOps.ClosureStartsAt(em, root);
                {
                    em.SetComponentData(root, endingClock);
                }

                NightOps.Tick(em, root, .01f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Retreat && NightOps.Progress(em, root) < 1, "Closure starts before the full night expires");
                Check(em.GetComponentData<UnitOrder>(enemy).Kind != OrderKind.Recall, "Enemy does not retreat at closure entry");
                NightOps.Tick(em, root, 1.98f);
                Check(em.GetComponentData<UnitOrder>(enemy).Kind != OrderKind.Recall, "Enemy waits for the complete retreat delay");
                NightOps.Tick(em, root, .02f);
                Check(em.GetComponentData<UnitOrder>(enemy).Kind == OrderKind.Recall, "Enemy receives retreat at the two-second beat");
                NightOps.Tick(em, root, 3.98f);
                using (var closureSoldiers = WorldQueries.Entities<Soldier>(em))
                    foreach (var closureUnit in closureSoldiers)
                        if (EntityState.Alive(em, closureUnit))
                            Check(em.GetComponentData<VisualState>(closureUnit).Celebrating == 0, "Soldier waits until the six-second cheering beat");
                NightOps.Tick(em, root, .02f);
                using (var closureSoldiers = WorldQueries.Entities<Soldier>(em))
                    foreach (var closureUnit in closureSoldiers)
                        if (EntityState.Alive(em, closureUnit) && em.GetComponentData<Combatant>(closureUnit).Deployed != 0)
                            Check(em.GetComponentData<VisualState>(closureUnit).Celebrating == (byte)NightEndPose.Celebrate, "Soldier cheers after the six-second beat");
                NightOps.Tick(em, root, settings.NightClosureSeconds - 6);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Day, "Night timeout enters dawn without report confirmation or waiting for soldiers");
                Check(em.GetComponentData<GameClock>(root).Turn == 4 && em.GetComponentData<GameClock>(root).DawnRemaining == settings.DawnSeconds, "Dawn starts a new interactive day with a separate clock");
                Check(em.GetBuffer<BattleHistoryEntry>(root).Length > 0, "Battle results are archived automatically at dawn");
                Reset();
                Force(3);
                PhaseTo(Phase.Night);
                var pendingWaves = em.GetBuffer<NightWave>(root);
                for (int i = 0; i < pendingWaves.Length; i++)
                {
                    var wave = pendingWaves[i];
                    wave.Spawned = (byte)(i == 0 ? 2 : 0);
                    pendingWaves[i] = wave;
                }

                plan = NightPlanOps.State(em, root);
                plan.AnySpawned = 1;
                EntityState.Set(em, root, plan);
                NightOps.Tick(em, root, .1f);
                pendingWaves = em.GetBuffer<NightWave>(root);
                bool warningPulledForward = false;
                foreach (var pendingWave in pendingWaves)
                    warningPulledForward |= pendingWave.Spawned == 0 && pendingWave.Warned != 0;
                Check(em.GetComponentData<Session>(root).Phase == Phase.Night && !NightOps.CanEndNight(em, root) && warningPulledForward, "Clearing a wave pulls the next pending wave warning forward without announcing victory");
                pendingWaves = em.GetBuffer<NightWave>(root);
                for (int i = 0; i < pendingWaves.Length; i++)
                {
                    var wave = pendingWaves[i];
                    wave.Spawned = 2;
                    pendingWaves[i] = wave;
                }

                NightOps.Tick(em, root, .1f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Celebration && !NightOps.CanEndNight(em, root), "Completed waves enter the timed victory sequence without exposing advancement immediately");
                NightOps.Tick(em, root, settings.BattleCelebrationAt - .01f);
                using (var victorySoldiers = WorldQueries.Entities<Soldier>(em))
                    foreach (var victoryUnit in victorySoldiers)
                        if (EntityState.Alive(em, victoryUnit) && em.GetComponentData<Combatant>(victoryUnit).Deployed != 0)
                            Check(em.GetComponentData<VisualState>(victoryUnit).Celebrating == 0, "Soldiers wait for the configured victory caption and celebration beats");
                NightOps.Tick(em, root, .01f);
                using (var victorySoldiers = WorldQueries.Entities<Soldier>(em))
                    foreach (var victoryUnit in victorySoldiers)
                        if (EntityState.Alive(em, victoryUnit) && em.GetComponentData<Combatant>(victoryUnit).Deployed != 0)
                            Check(em.GetComponentData<VisualState>(victoryUnit).Celebrating == (byte)NightEndPose.Celebrate, "Soldiers cheer four seconds after victory");
                NightOps.Tick(em, root, settings.VictoryAdvanceDelaySeconds - .01f);
                Check(!NightOps.CanEndNight(em, root), "Next-stage remains locked throughout the three-second cheer");
                NightOps.Tick(em, root, .01f);
                Check(NightOps.CanEndNight(em, root), "Next-stage unlocks seven seconds after victory");
                using (var victorySoldiers = WorldQueries.Entities<Soldier>(em))
                    foreach (var victoryUnit in victorySoldiers)
                        if (EntityState.Alive(em, victoryUnit) && em.GetComponentData<Combatant>(victoryUnit).Deployed != 0)
                            Check(em.GetComponentData<VisualState>(victoryUnit).Celebrating == 0, "Soldiers leave the cheer pose so patrol can continue while the player waits");
                NightOps.Tick(em, root, 11);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Celebration && NightOps.CanEndNight(em, root), "Victory remains available without freezing the remaining night into a countdown");
                Entity returning = Entity.Null;
                using (var units = WorldQueries.Entities<Soldier>(em))
                    foreach (var returningCandidate in units)
                        if (EntityState.Alive(em, returningCandidate) && em.GetComponentData<Combatant>(returningCandidate).Deployed != 0)
                        {
                            returning = returningCandidate;
                            break;
                        }

                Check(returning != Entity.Null, "Return fixture has a living deployed soldier");
                var returnPosition = EntityState.Position(em, returning);
                bool movedAway = false;
                using (var reach = new NightSpatialOps.Reach(em, root, returnPosition))
                    foreach (var node in em.GetBuffer<SurfaceNavNode>(root))
                    {
                        float distance = math.distance(node.Position, returnPosition);
                        if (node.Open == 0 || distance < 6 || distance > 9 || !reach.Point(node.Position))
                            continue;
                        var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(returning);
                        transform.Position = node.Position;
                        em.SetComponentData(returning, transform);
                        movedAway = true;
                        break;
                    }

                Check(movedAway, "Return fixture starts on a reachable surface away from home");
                returnPosition = EntityState.Position(em, returning);
                Check(GameRequestExecution.Execute(em, root, new AdvanceRequest()) == ResultCode.Success && em.GetComponentData<Session>(root).Phase == Phase.Day, "Victory advancement enters dawn without replaying timeout closure");
                Check(EntityState.Position(em, returning).Equals(returnPosition) && em.GetComponentData<VisualState>(returning).Visible != 0, "Dawn preserves deployed survivors for physical return");
                Check(NightOps.Progress(em, root) == 1, "Victory advancement completes night progress without a report gate");
                Check(GameRequestExecution.Execute(em, root, new AdvanceRequest()) == ResultCode.WrongPhase && em.GetComponentData<GameClock>(root).DawnRemaining == settings.DawnSeconds, "Dawn allows day management but cannot skip straight into another night");
                Check(em.HasComponent<DayReturnState>(returning) && em.GetComponentData<Combatant>(returning).Deployed != 0 && EntityState.Position(em, returning).Equals(returnPosition), "Daytime preserves the returning soldier and its physical position");
                var returnState = em.GetComponentData<DayReturnState>(returning);
                Check(returnState.Remaining >= 15 && returnState.Remaining <= 30, "Each survivor receives a 15 to 30 second return deadline");
                using (var returns = WorldQueries.Entities<DayReturnState>(em))
                    Check(returns.Length < 2 || returns.ToArray().Select(e => em.GetComponentData<DayReturnState>(e).Remaining).Distinct().Count() > 1, "Return deadlines are staggered across soldiers");
                // Transient walking state must neither block a day save nor modify the roster schema.
                var walkingSave = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Decode(em, root, walkingSave);
                var invalidDawn = SnapshotCodec.Decode(em, root, walkingSave);
                invalidDawn.Clock.DawnRemaining = float.NaN;
                Reject(() => SnapshotCodec.Restore(em, root, invalidDawn), "Non-finite saved dawn is rejected");
                Check(em.GetComponentData<Soldier>(returning).RecallState == 0, "Daytime return keeps the persistent roster saveable");
                var frozenControl = em.GetComponentData<SimulationControl>(root);
                frozenControl.Paused = 1;
                em.SetComponentData(root, frozenControl);
                NightOps.Tick(em, root, 40);
                Check(em.GetComponentData<DayReturnState>(returning).Remaining == returnState.Remaining && EntityState.Position(em, returning).Equals(returnPosition), "Pause freezes the daytime return deadline");
                Check(em.GetComponentData<GameClock>(root).DawnRemaining == settings.DawnSeconds, "Pause freezes dawn as well as soldier return");
                frozenControl.Paused = 0;
                em.SetComponentData(root, frozenControl);
                NightOps.Tick(em, root, 1);
                Check(em.GetComponentData<DayReturnState>(returning).Remaining < returnState.Remaining && em.GetComponentData<Session>(root).Phase == Phase.Day, "Return timer advances while day remains interactive");
                Arrive();
                Check(!em.HasComponent<DayReturnState>(returning) && em.GetComponentData<VisualState>(returning).Visible == 0, "Physical arrival completes background return immediately");
                NightOps.Tick(em, root, settings.DawnSeconds);
                Check(em.GetComponentData<GameClock>(root).DawnRemaining == 0, "Dawn completes after its independent duration");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, walkingSave));
                Check(em.GetComponentData<GameClock>(root).DawnRemaining == settings.DawnSeconds, "Dawn timer survives a day save and restore");
                using (var restoredReturns = WorldQueries.Entities<DayReturnState>(em))
                    Check(restoredReturns.Length == 0, "Loading a day save treats transient walking as already returned");
                Reset();
                Force(20, true);
                var bossDefinition = NightPlanOps.State(em, root).BossDefinition;
                plan = NightPlanOps.State(em, root);
                plan.AnySpawned = 1;
                plan.BossEscaped = 1;
                EntityState.Set(em, root, plan);
                Check(plan.BaseThreat <= NightPlanOps.Rules(em, root).ThreatFloor + em.GetComponentData<NightRuntimeState>(root).StartCombatStrength * NightPlanOps.Rules(em, root).ThreatPerStrengthCap, "Late turns obey surviving-force difficulty ceiling");
                NightPlanOps.Commit(em, root);
                Check(em.GetBuffer<UnresolvedBoss>(root).Length == 1 && em.GetBuffer<UnresolvedBoss>(root)[0].DueTurn == 25, "Escaped boss creates delayed same-identity aftermath");
                NightPlanOps.Commit(em, root);
                Check(em.GetBuffer<UnresolvedBoss>(root).Length == 1 && em.GetBuffer<NightEventHistory>(root)[0].Count == 1, "Dawn event commit idempotent");
                Force(25);
                Check(NightPlanOps.State(em, root).Event.ToString() == "night.boss.return" && NightPlanOps.State(em, root).BossDefinition == bossDefinition, "Pending boss takes priority even outside periodic boss night");
                NightPlanOps.BossDeath(em, root, bossDefinition);
                plan = NightPlanOps.State(em, root);
                plan.AnySpawned = 1;
                EntityState.Set(em, root, plan);
                NightPlanOps.Commit(em, root);
                Check(em.GetBuffer<UnresolvedBoss>(root).Length == 0, "Boss death clears unresolved threat at dawn");
                ref var evt = ref em.GetComponentData<NightEventCatalog>(root).Value.Value.Events[NightPlanOps.Find(em, root, new FixedString64Bytes("night.boss.return"))];
                Check(!NightPlanOps.Eligible(em, root, ref evt, false), "Return-only event excluded from normal draw");
                byte previousOnce = evt.Once;
                evt.Once = 1;
                Check(!NightPlanOps.Eligible(em, root, ref evt, true), "Once flag respects completed history");
                evt.Once = previousOnce;
                Reset();
                Force(3);
                snapshot = SnapshotCodec.Capture(em, root);
                var invalid = SnapshotCodec.Decode(em, root, snapshot);
                invalid.NightPlan.BaseThreat = -1;
                Reject(() => SnapshotCodec.Restore(em, root, invalid), "Invalid night plan rejected before destructive restore");
                Check(snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected night snapshot leaves world intact");
                var archive = world.GetOrCreateSystemManaged<CheckpointSystem>();
                archive.Update();
                Check(NightOps.Begin(em, root) == ResultCode.Success, "Night entry transaction succeeds with new root buffers");
                archive.Update();
                PhaseTo(Phase.Night);
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = Core(), Amount = 9999999 });
                archive.Update();
                var recovery = em.GetComponentData<RecoveryState>(root);
                var lostArchive = archive.Export(root);
                archive.Retry(root, false);
                NightRuntimeState retryNight = em.GetComponentData<NightRuntimeState>(root);
                Check(retryNight.Threat < lockedNight.Threat && retryNight.StartCombatStrength == lockedNight.StartCombatStrength, "Core retry lowers only locked budget, not recomputed day strength");
                var composition = Buffer<NightWave>(em, root);
                var baseThreat = NightPlanOps.State(em, root).BaseThreat;
                archive.Import(root, lostArchive, false);
                archive.Retry(root, true);
                Check(composition.SequenceEqual(Buffer<NightWave>(em, root)) && baseThreat == NightPlanOps.State(em, root).BaseThreat, "Same recovery ticket yields identical day and dusk plan");
                Check(em.GetComponentData<RecoveryState>(root).Seed == recovery.Seed, "Choosing nodes does not create another seed");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
