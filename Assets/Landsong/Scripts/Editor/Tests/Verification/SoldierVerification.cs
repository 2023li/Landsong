#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class SoldierVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool value, string name)
        {
            if (!value)
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
            catch (InvalidDataException)
            {
                rejected = true;
            }

            Check(rejected, name);
        }

        [MenuItem("Landsong/ECS/Verification/Soldier")]
        public static string Run()
        {
            log = new StringBuilder();
            checks = 0;
            try
            {
                Configuration();
                Verify();
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
                File.WriteAllText("Library/LandsongEcs/soldiers-verification.txt", log.ToString());
            }
        }

        static void Configuration()
        {
            var source = AssetDatabase.LoadAssetAtPath<SoldierCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/SoldierCatalog.asset");
            var catalog = CatalogFixture.Clone(source);
            int at = 0;
            var soldier = catalog.Definitions[at];
            void Invalid(Action mutate, string name)
            {
                var growth = soldier.Growth;
                bool invalid = false;
                mutate();
                try
                {
                    using var blob = SoldierCatalogCompiler.Build(catalog, new ItemCatalogIndex(AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset")));
                }
                catch (InvalidOperationException)
                {
                    invalid = true;
                }
                finally
                {
                    soldier.Growth = growth;
                }

                Check(invalid, name);
            }

            try
            {
                Check(soldier.Growth.MaxLevel == 10 && soldier.Growth.BattleExperience == 10, "Existing soldier asset gets editable growth defaults");
                Invalid(() => soldier.Growth.HealthPerLevel = float.NaN, "Nonfinite growth rejected during baking");
                Invalid(() => soldier.Growth.FirstLevelExperience = 0, "Zero experience threshold rejected during baking");
                Invalid(() =>
                {
                    soldier.Growth.MaxLevel = 100;
                    soldier.Growth.ExperienceStep = 1000000;
                }, "Overflowing cumulative experience rejected during baking");
                var costs = soldier.RecruitmentCosts;
                soldier.RecruitmentCosts = new[]
                {
                    new LeveledItemAmountSource
                    {
                        Item = null,
                        Quantity = 1
                    }
                };
                Invalid(() =>
                {
                }, "Missing recruitment resource rejected during baking");
                soldier.RecruitmentCosts = costs;
            }
            finally
            {
                CatalogFixture.Destroy(catalog);
            }
        }

        static void Verify()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Wave eleven soldiers isolated", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                var state = em.GetComponentData<Session>(root);
                GameClock stateClock = em.GetComponentData<GameClock>(root);
                PopulationState statePopulation = em.GetComponentData<PopulationState>(root);
                NightRuntimeState stateNight = em.GetComponentData<NightRuntimeState>(root);
                PersistenceGate statePersistence = em.GetComponentData<PersistenceGate>(root);
                statePopulation.BasePopulation += 100;
                statePersistence.CheckpointPending = 0;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                    em.SetComponentData(root, statePopulation);
                    em.SetComponentData(root, stateNight);
                    em.SetComponentData(root, statePersistence);
                }

                var soldierDefinition = SoldierId.FromIndex(0);
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                var core = Entity.Null;
                using (var sites = WorldQueries.Entities<Building>(em))
                    foreach (var e in sites)
                        if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                            core = e;
                ItemId otherItem = default;
                for (int i = 0; i < ItemDefinitions.Count(em, root); i++)
                    if (ItemId.FromIndex(i) != gold)
                    {
                        otherItem = ItemId.FromIndex(i);
                        break;
                    }

                void Fund(ItemId item)
                {
                    var slots = em.GetBuffer<InventorySlot>(root);
                    for (int i = 0; i < 8; i++)
                        slots.Add(new InventorySlot { Provider = Id(core), Index = 5000 + slots.Length, SlotType = StorageSlotId.None, Item = item, Count = ItemDefinitions.Get(em, root, item).MaximumStack });
                }

                Fund(gold);
                Fund(otherItem);
                Entity Site(bool bell = false)
                {
                    BuildingId definition = default;
                    for (int i = 0; i < BuildingDefinitions.Count(em, root); i++)
                    {
                        var id = BuildingId.FromIndex(i);
                        ref var value = ref BuildingDefinitions.Get(em, root, id);
                        bool coreBuilding = false;
                        for (int n = 0; n < value.Capabilities.Housing.Population.Length; n++)
                            coreBuilding |= value.Capabilities.Housing.Population[n].IsCore;
                        if (!coreBuilding && (bell ? value.Capabilities.Defence.Bells.Length > 0 : value.Capabilities.Garrison.Enabled))
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
                    throw new InvalidOperationException("No military fixture footprint");
                }

                var home = Site();
                var secondHome = Site();
                var homeId = Id(home);
                var secondHomeId = Id(secondHome);
                ResultCode Cmd<T>(T request)
                    where T : unmanaged, IGameRequest => GameRequestExecution.Execute(em, root, request);
                var q = SoldierOps.RecruitQuote(em, root, homeId, soldierDefinition, 2);
                var pendingQuote = SoldierOps.RecruitQuote(em, root, secondHomeId, soldierDefinition, 1, true);
                int pendingStock = InventoryOps.Count(em, root, gold), pendingPopulation = PopulationOps.Employed(em);
                Check(Cmd(new RecruitSoldiersRequest { Garrison = secondHomeId, Soldier = soldierDefinition, Quantity = 1, Pending = 1 == 1 }) == ResultCode.Success, "Pool recruitment succeeds through command");
                Entity pendingUnit = Entity.Null;
                using (var units = WorldQueries.OrderedEntities<Soldier>(em))
                    foreach (var unit in units)
                        if (em.GetComponentData<Soldier>(unit).Garrison == 0)
                            pendingUnit = unit;
                Check(pendingUnit != Entity.Null && em.GetComponentData<Soldier>(pendingUnit).Slot == 0 && em.GetComponentData<Soldier>(pendingUnit).PendingSince == stateClock.Turn, "New recruit remains unassigned with original turn deadline");
                Check(GarrisonOps.GarrisonCount(em, secondHomeId) == 0 && InventoryOps.Count(em, root, gold) == pendingStock - pendingQuote.Costs.Sum(c => c.Amount) && PopulationOps.Employed(em) > pendingPopulation, "Pool recruitment pays exact resources and population without occupying a slot");
                Check(Cmd(new AssignSoldierRequest { Soldier = Id(pendingUnit), Garrison = secondHomeId, Slot = 2 }) == ResultCode.Success && em.GetComponentData<Soldier>(pendingUnit).Slot == 2 && GarrisonOps.AtSlot(em, secondHomeId, 1) == Entity.Null, "Selected pending soldier occupies specifically chosen slot");
                Check(Cmd(new DismissSoldierRequest { Soldier = Id(pendingUnit), Confirmed = 1 == 1 }) == ResultCode.Success, "Pending flow fixture releases its soldier");
                BuildingRecruitmentState pendingHomeRecruitment = em.GetComponentData<BuildingRecruitmentState>(secondHome);
                pendingHomeRecruitment.Count = 0;
                {
                    em.SetComponentData(secondHome, pendingHomeRecruitment);
                }

                Check(q.Code == ResultCode.Success && q.Quantity == 2 && q.RemainingLimit == em.GetComponentData<BuildingGarrisonStats>(home).Capacity, "Quote exposes two-person cost and per-site default limit");
                int stock = InventoryOps.Count(em, root, gold);
                var random = em.GetComponentData<SimulationRandomState>(root).State;
                int population = PopulationOps.Employed(em);
                Check(Cmd(new RecruitSoldiersRequest { Garrison = homeId, Soldier = soldierDefinition, Quantity = 2, Pending = 0 == 1 }) == ResultCode.Success, "Two soldiers recruited immediately by command");
                Check(stock - InventoryOps.Count(em, root, gold) == q.Costs.Sum(c => c.Amount), "Fallback gold fee equals quote");
                Check(em.GetComponentData<SimulationRandomState>(root).State == random, "Personal names do not consume simulation random state");
                Check(PopulationOps.Employed(em) == population + 2 * SoldierDefinitions.Get(em, root, soldierDefinition).PopulationCost, "Recruitment reserves military population separately");
                var a = GarrisonOps.AtSlot(em, homeId, 1);
                var b = GarrisonOps.AtSlot(em, homeId, 2);
                ulong aid = Id(a), bid = Id(b);
                Check(a != Entity.Null && b != Entity.Null && em.GetComponentData<Identity>(a).Name != SoldierDefinitions.Get(em, root, soldierDefinition).Metadata.Name, "Named persistent individuals occupy distinct real slots");
                Check(Cmd(new RenameSoldierRequest { Soldier = aid, Name = new FixedString128Bytes("守城先锋") }) == ResultCode.Success && em.GetComponentData<Identity>(a).Name.ToString() == "守城先锋", "Day rename retains stable identity");
                Check(Cmd(new RenameSoldierRequest { Soldier = aid, Name = new FixedString128Bytes("   ") }) == ResultCode.InvalidContent, "Empty soldier name rejected");
                Check(Cmd(new AssignSoldierRequest { Soldier = aid, Garrison = secondHomeId, Slot = 1 }) == ResultCode.Success, "Free no-distance cross-site assignment");
                Check(Cmd(new AssignSoldierRequest { Soldier = bid, Garrison = secondHomeId, Slot = 1 }) == ResultCode.NoCapacity, "Occupied destination cannot silently replace soldier");
                Check(Cmd(new SwapSoldiersRequest { FirstSoldier = aid, SecondSoldier = bid }) == ResultCode.Success && em.GetComponentData<Soldier>(a).Slot == 2 && em.GetComponentData<Soldier>(a).Garrison == homeId, "Exchange transfers actual slot and home, not display order");
                int limit = SoldierOps.RecruitQuote(em, root, homeId, soldierDefinition, 1).RemainingLimit;
                Check(limit == q.RemainingLimit - 2, "Moving troops does not refund recruitment quota");
                Check(Cmd(new UnassignSoldierRequest { Soldier = aid }) == ResultCode.Success && em.GetComponentData<Soldier>(a).Slot == 0, "Unassignment clears real slot");
                int pendingTurn = em.GetComponentData<Soldier>(a).PendingSince;
                {
                    state = em.GetComponentData<Session>(root);
                    stateClock = em.GetComponentData<GameClock>(root);
                    statePopulation = em.GetComponentData<PopulationState>(root);
                    stateNight = em.GetComponentData<NightRuntimeState>(root);
                    statePersistence = em.GetComponentData<PersistenceGate>(root);
                }

                stateClock.Turn++;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                    em.SetComponentData(root, statePopulation);
                    em.SetComponentData(root, stateNight);
                    em.SetComponentData(root, statePersistence);
                }

                NightOps.Plan(em, root, false);
                Check(Cmd(new UnassignSoldierRequest { Soldier = aid }) == ResultCode.Success && em.GetComponentData<Soldier>(a).PendingSince == pendingTurn, "Repeated unassign does not renew pending age");
                Check(SoldierOps.RecruitQuote(em, root, homeId, soldierDefinition, 1).RemainingLimit == q.RemainingLimit, "Next turn restores recruitment quota");
                Check(Cmd(new FillGarrisonRequest { Garrison = homeId }) == ResultCode.Success && em.GetComponentData<Soldier>(a).Slot == 1 && em.GetComponentData<Soldier>(b).Garrison == secondHomeId, "Fill assigns pending to empty slots only");
                Check(Cmd(new AssignSoldierRequest { Soldier = bid, Garrison = homeId, Slot = 2 }) == ResultCode.Success, "Second individual assigned to second slot");
                Check(Cmd(new SwapSoldiersRequest { FirstSoldier = aid, SecondSoldier = bid }) == ResultCode.Success, "Reverse age versus slot order for shrink test");
                BuildingGarrisonStats statsGarrison = em.GetComponentData<BuildingGarrisonStats>(home);
                int oldCapacity = statsGarrison.Capacity;
                statsGarrison.Capacity = 1;
                {
                    em.SetComponentData(home, statsGarrison);
                }

                GarrisonOps.ReconcileGarrisons(em, root);
                Check(em.GetComponentData<Soldier>(a).Garrison == 0 && em.GetComponentData<Soldier>(b).Slot == 1, "Capacity shrink evicts highest slot even if it owns older ID");
                statsGarrison.Capacity = oldCapacity;
                {
                    em.SetComponentData(home, statsGarrison);
                }

                GarrisonOps.ReconcileGarrisons(em, root);
                Check(em.GetComponentData<Soldier>(a).Garrison == 0, "Restored capacity never auto-fills pending soldiers");
                Check(Cmd(new FillGarrisonRequest { Garrison = homeId }) == ResultCode.Success, "Explicit fill after capacity restoration");
                var saved = SnapshotCodec.Capture(em, root);
                var decoded = SnapshotCodec.Decode(em, root, saved);
                SnapshotCodec.Restore(em, root, decoded);
                a = WorldQueries.Find(em, aid);
                b = WorldQueries.Find(em, bid);
                home = WorldQueries.Find(em, homeId);
                secondHome = WorldQueries.Find(em, secondHomeId);
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "New soldier fields and recruitment ledger round-trip byte-exactly");
                var malformed = SnapshotCodec.Decode(em, root, saved);
                var records = malformed.Records.OfType<SoldierSnapshot>().ToArray();
                records[0].Soldier.Slot = records[1].Soldier.Slot;
                Reject(() => SnapshotCodec.Restore(em, root, malformed), "Duplicate real slot rejected before world replacement");
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected soldier snapshot leaves live state untouched");
                malformed = SnapshotCodec.Decode(em, root, saved);
                malformed.Records.OfType<SoldierSnapshot>().First().Soldier.Experience = -1;
                Reject(() => SnapshotCodec.Restore(em, root, malformed), "Negative soldier experience rejected");
                foreach (int fail in new[]
                {
                    0,
                    1,
                    2
                }

                )
                {
                    var before = SnapshotCodec.Capture(em, root);
                    var request = new RecruitSoldiersRequest
                    {
                        Garrison = secondHomeId,
                        Soldier = soldierDefinition,
                        Quantity = 2
                    };
                    using (HistoryOps.ForRequest(em, root, new QueuedGameplayRequest { Kind = request.Kind, Target = request.Target }))
                        Check(SoldierOps.RecruitSoldiers(em, root, request, index =>
                        {
                            if (index == fail)
                                throw new InvalidOperationException("injected");
                        }) == ResultCode.PreparationFailed, "Injected recruitment failure " + fail);
                    Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Whole quantity / money / IDs / quota / manual history rollback " + fail);
                }

                foreach (string failure in new[]
                {
                    "record-created",
                    "garrisons-prepared",
                    "root-published",
                    "before-retire"
                }

                )
                {
                    bool threw = false;
                    try
                    {
                        SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved), probe: at =>
                        {
                            if (at == failure)
                                throw new InvalidOperationException("probe");
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        threw = true;
                    }

                    Check(threw && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Soldier restore transactional fault " + failure);
                }

                int free = PopulationOps.Employed(em);
                stock = InventoryOps.Count(em, root, gold);
                Check(Cmd(new DismissSoldierRequest { Soldier = aid, Confirmed = 0 == 1 }) == ResultCode.ConfirmationRequired && em.Exists(a), "Dismiss requires explicit second confirmation");
                Check(Cmd(new DismissSoldierRequest { Soldier = aid, Confirmed = 1 == 1 }) == ResultCode.Success && !em.Exists(a), "Confirmed dismissal deletes only chosen individual");
                Check(PopulationOps.Employed(em) == free - SoldierDefinitions.Get(em, root, soldierDefinition).PopulationCost && InventoryOps.Count(em, root, gold) == stock, "Dismiss releases population with no refund");
                SnapshotCodec.Restore(em, root, decoded);
                a = WorldQueries.Find(em, aid);
                b = WorldQueries.Find(em, bid);
                home = WorldQueries.Find(em, homeId);
                secondHome = WorldQueries.Find(em, secondHomeId);
                var growth = SoldierDefinitions.Get(em, root, soldierDefinition).Growth;
                var soldier = em.GetComponentData<Soldier>(a);
                soldier.Experience = UnitProgression.LevelThreshold(growth, 2);
                em.SetComponentData(a, soldier);
                Check(UnitProgression.Level(growth, soldier.Experience) == 2 && UnitProgression.Level(growth, soldier.Experience - 1) == 1, "Level curve exact threshold");
                Check(SoldierOps.SoldierStats(em, root, a).Damage > SoldierOps.SoldierStats(em, root, b).Damage, "Personal growth changes actual combat stats");
                var bell1 = Site(true);
                var bell2 = Site(true);
                var bell1Id = Id(bell1);
                var bell2Id = Id(bell2);
                NightPlanOps.Prepare(em, root);
                BattleLifecycle.PrepareNight(em, root);
                {
                    state = em.GetComponentData<Session>(root);
                    stateClock = em.GetComponentData<GameClock>(root);
                    statePopulation = em.GetComponentData<PopulationState>(root);
                    stateNight = em.GetComponentData<NightRuntimeState>(root);
                    statePersistence = em.GetComponentData<PersistenceGate>(root);
                }

                state.Phase = Phase.Night;
                stateNight.Kind = NightKind.Invasion;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                    em.SetComponentData(root, statePopulation);
                    em.SetComponentData(root, stateNight);
                    em.SetComponentData(root, statePersistence);
                }

                void DeployAt(Entity unit, float3 point)
                {
                    var actor = em.GetComponentData<Combatant>(unit);
                    actor.Deployed = 1;
                    em.SetComponentData(unit, actor);
                    em.SetComponentData(unit, LocalTransform.FromPosition(point));
                }

                NavigationOps.TryNearestOpen(em, root, EntityState.Position(em, bell1), 12, out var nearBell);
                DeployAt(a, nearBell);
                DeployAt(b, nearBell + new float3(0, 0, 1));
                BuildingBellStats bsBell = em.GetComponentData<BuildingBellStats>(bell1);
                bsBell.Radius = 100;
                {
                    em.SetComponentData(bell1, bsBell);
                }

                {
                    bsBell = em.GetComponentData<BuildingBellStats>(bell2);
                }

                bsBell.Radius = .001f;
                {
                    em.SetComponentData(bell2, bsBell);
                }

                Check(Cmd(new RingBellRequest { Building = bell1Id }) == ResultCode.Success && em.GetComponentData<UnitOrder>(a).Source == bell1Id, "Bell rallies by current position to reachable perimeter");
                Cmd(new RingBellRequest { Building = bell2Id });
                Check(em.GetComponentData<UnitOrder>(a).Source != bell1Id && em.GetComponentData<UnitOrder>(a).Kind != OrderKind.Rally, "New bell clears previous command even outside new range");
                Cmd(new RingBellRequest { Building = bell2Id });
                Check(em.GetComponentData<BellState>(root).ActiveBell == 0, "Repeated bell click cancels active bell");
                var enemy = EnemyEntities.Spawn(em, root, EnemyId.FromIndex(0), EntityState.Position(em, a), false);
                EnemyCombatants.Configure(em, root, enemy, true, homeId, EntityState.Position(em, a));
                var engaged = em.GetComponentData<Combatant>(a);
                engaged.Target = enemy;
                em.SetComponentData(a, engaged);
                Cmd(new RingBellRequest { Building = bell1Id });
                Check(em.GetComponentData<UnitOrder>(a).Kind != OrderKind.Rally, "Bell never pulls a soldier out of close engagement");
                Check(Cmd(new AssignSoldierRequest { Soldier = aid, Garrison = secondHomeId, Slot = 0 }) == ResultCode.WrongPhase && Cmd(new DismissSoldierRequest { Soldier = aid, Confirmed = 1 == 1 }) == ResultCode.WrongPhase, "Day management commands cannot mutate night roster");
                Check(Cmd(new RecallGarrisonRequest { Garrison = homeId, Cancel = 0 == 1 }) == ResultCode.Success && em.GetComponentData<Soldier>(a).RecallState == 1 && em.GetComponentData<UnitOrder>(a).Kind == OrderKind.Recall, "Garrison recall directly orders its deployed soldiers");
                Check(em.GetComponentData<Combatant>(a).Target == Entity.Null, "Recall forcibly clears current engagement target");
                float health = em.GetComponentData<Health>(a).Current;
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = a, Amount = 1 });
                Check(em.GetComponentData<Health>(a).Current == health - 1, "Returning soldier remains damageable");
                Cmd(new RingBellRequest { Building = bell1Id });
                Check(em.GetComponentData<UnitOrder>(a).Kind == OrderKind.Recall, "Bell cannot override active recall");
                Cmd(new RecallGarrisonRequest { Garrison = homeId, Cancel = 1 == 1 });
                Check(em.GetComponentData<Soldier>(a).RecallState == 0, "Recall cancellable before arrival");
                Cmd(new RecallGarrisonRequest { Garrison = homeId, Cancel = 0 == 1 });
                var destination = em.GetComponentData<UnitOrder>(a).Destination;
                em.SetComponentData(a, LocalTransform.FromPosition(destination));
                UnitOrders.TickSoldierOrders(em, root);
                Check(em.GetComponentData<Soldier>(a).RecallState == 2 && em.GetComponentData<Combatant>(a).Deployed == 0 && em.GetComponentData<VisualState>(a).Visible == 0, "Arrival shelters and hides soldier without deleting it");
                Cmd(new RecallGarrisonRequest { Garrison = homeId, Cancel = 1 == 1 });
                Check(em.GetComponentData<Soldier>(a).RecallState == 2, "Cancel cannot redeploy already returned soldiers");
                Check(SoldierOps.SoldierBattleExperience(em, root, a) == 0, "No experience from deployment alone");
                var ca = em.GetComponentData<Combatant>(a);
                ca.Participated = 1;
                em.SetComponentData(a, ca);
                SoldierOps.ReportSoldierExperience(em, root);
                SoldierOps.ReportSoldierExperience(em, root);
                int experienceRows = 0;
                foreach (var entry in em.GetBuffer<BattleReportEntry>(root))
                    if (entry.Kind == EventKind.SoldierExperience && entry.Id == aid)
                        experienceRows++;
                Check(experienceRows == 1, "Experience report preview is idempotent");
                int experience = em.GetComponentData<Soldier>(a).Experience;
                BattleLifecycle.Dawn(em, root);
                BattleLifecycle.Dawn(em, root);
                Check(em.GetComponentData<Soldier>(a).Experience == experience + growth.BattleExperience, "Dawn grants soldier experience exactly once");
                Check(em.GetComponentData<Health>(a).Current == em.GetComponentData<Health>(a).Maximum && em.GetComponentData<Soldier>(a).RecallState == 0, "Dawn fully heals survivors and clears recall state");
                {
                    state = em.GetComponentData<Session>(root);
                    stateClock = em.GetComponentData<GameClock>(root);
                    statePopulation = em.GetComponentData<PopulationState>(root);
                    stateNight = em.GetComponentData<NightRuntimeState>(root);
                    statePersistence = em.GetComponentData<PersistenceGate>(root);
                }

                stateClock.Turn++;
                stateNight.Kind = NightKind.Peaceful;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                    em.SetComponentData(root, statePopulation);
                    em.SetComponentData(root, stateNight);
                    em.SetComponentData(root, statePersistence);
                }

                Check(SoldierOps.SoldierBattleExperience(em, root, a) == 0, "Peaceful patrol never grants combat experience");
                em.DestroyEntity(enemy);
                // Queued soldiers stay home when recalled, and losing a home interrupts only those still returning.
                stateNight.Kind = NightKind.Invasion;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                    em.SetComponentData(root, statePopulation);
                    em.SetComponentData(root, stateNight);
                    em.SetComponentData(root, statePersistence);
                }

                var sb = em.GetComponentData<Soldier>(b);
                sb.RecallState = 0;
                em.SetComponentData(b, sb);
                var cb = em.GetComponentData<Combatant>(b);
                cb.Deployed = 0;
                em.SetComponentData(b, cb);
                Cmd(new RecallGarrisonRequest { Garrison = homeId, Cancel = 0 == 1 });
                Check(em.GetComponentData<Soldier>(b).RecallState == 2, "Recall cancels not-yet-deployed queue for this night");
                sb.RecallState = 0;
                em.SetComponentData(b, sb);
                DeployAt(b, nearBell);
                Cmd(new RecallGarrisonRequest { Garrison = homeId, Cancel = 0 == 1 });
                BuildingLifecycle.Ruin(em, root, home);
                UnitOrders.TickSoldierOrders(em, root);
                Check(em.GetComponentData<Soldier>(b).RecallState == 0 && em.GetComponentData<Combatant>(b).Deployed == 1, "Home ruin aborts return and leaves deployed soldier fighting");
                BuildingLifecycle.DawnBuildings(em, root);
                Check(em.GetComponentData<Soldier>(b).Garrison == 0 && em.GetComponentData<Soldier>(b).Slot == 0, "Ruin commit clears both home and real slot");
                // Restore a valid day for independent cost tests.
                SnapshotCodec.Restore(em, root, decoded);
                home = WorldQueries.Find(em, homeId);
                MultiCost(em, root, homeId, soldierDefinition, gold, otherItem);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void MultiCost(EntityManager em, Entity root, ulong home, SoldierId soldier, ItemId gold, ItemId other)
        {
            var original = em.GetComponentData<SoldierCatalog>(root);
            var source = AssetDatabase.LoadAssetAtPath<SoldierCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/SoldierCatalog.asset");
            var items = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset");
            var catalog = CatalogFixture.Clone(source);
            catalog.Definitions[soldier.Index].RecruitmentCosts = new[]
            {
                new LeveledItemAmountSource
                {
                    Item = items.Definitions[gold.Index],
                    Quantity = 10,
                    Order = 0
                },
                new LeveledItemAmountSource
                {
                    Item = items.Definitions[other.Index],
                    Quantity = 2,
                    Order = 1
                },
                new LeveledItemAmountSource
                {
                    Item = items.Definitions[gold.Index],
                    Quantity = 5,
                    Order = 2
                }
            };
            using var modified = SoldierCatalogCompiler.Build(catalog, new ItemCatalogIndex(items));
            em.SetComponentData(root, new SoldierCatalog { Value = modified });
            try
            {
                var s = em.GetComponentData<Session>(root);
                s.Phase = Phase.Day;
                em.SetComponentData(root, s);
                var q = SoldierOps.RecruitQuote(em, root, home, soldier, 2);
                Check(q.Costs.Count == 2 && q.Costs.First(c => c.Item == gold).Amount == 30 && q.Costs.First(c => c.Item == other).Amount == 4, "Explicit multi-resource costs replace gold fallback and aggregate duplicate items");
                InventoryOps.Remove(em, root, other, InventoryOps.Count(em, root, other));
                int stock = InventoryOps.Count(em, root, gold);
                Check(SoldierOps.RecruitSoldiers(em, root, new RecruitSoldiersRequest { Garrison = home, Soldier = soldier, Quantity = 2 }) == ResultCode.InsufficientResources && InventoryOps.Count(em, root, gold) == stock, "Missing secondary resource cannot partially charge gold");
                InventoryOps.Add(em, root, other, 4);
                stock = InventoryOps.Count(em, root, gold);
                int otherStock = InventoryOps.Count(em, root, other);
                Check(SoldierOps.RecruitSoldiers(em, root, new RecruitSoldiersRequest { Garrison = home, Soldier = soldier, Quantity = 2 }) == ResultCode.Success, "Multi-resource quantity recruitment executes successfully");
                Check(InventoryOps.Count(em, root, gold) == stock - 30 && InventoryOps.Count(em, root, other) == otherStock - 4, "Multi-resource recruitment charges exactly the aggregated quote");
            }
            finally
            {
                em.SetComponentData(root, original);
                CatalogFixture.Destroy(catalog);
            }
        }
    }
}
#endif
