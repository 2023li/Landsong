#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class HeroVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            checks++;
            log.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verification/Hero")]
        public static string Run()
        {
            log = new StringBuilder();
            checks = 0;
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Hero verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var s = em.GetComponentData<Session>(root);
                GameClock sClock = em.GetComponentData<GameClock>(root);
                SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
                PopulationState sPopulation = em.GetComponentData<PopulationState>(root);
                NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
                PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                sPopulation.BasePopulation += 100;
                sPersistence.CheckpointPending = 0;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sPopulation);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                var definition = BuildingDefinitions.Find(em, root, "b泰坦神殿");
                var grid = em.GetComponentData<GridData>(root);
                Entity temple = Entity.Null;
                for (int y = 15; y < grid.Value.Value.Size.y - 15 && temple == Entity.Null; y++)
                    for (int x = 15; x < grid.Value.Value.Size.x - 15; x++)
                        if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0))
                        {
                            temple = BuildingCreation.Create(em, root, definition, new int2(x, y), 0, 1, true);
                            break;
                        }

                Check(temple != Entity.Null, "Legal temple fixture");
                ulong siteId = em.GetComponentData<Identity>(temple).Id;
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                Entity core = Entity.Null;
                using (var buildings = WorldQueries.Entities<Building>(em))
                    foreach (var e in buildings)
                        if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                            core = e;
                for (int i = 0; i < 30; i++)
                    em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = em.GetComponentData<Identity>(core).Id, Index = 5000 + i, SlotType = StorageSlotId.None, Item = gold, Count = ItemDefinitions.Get(em, root, gold).MaximumStack });
                Check(HeroOps.HeroAvailability(em, root, temple, false).Contains("工人"), "Recruit quote explains worker shortage");
                BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(temple);
                BuildingSanctumState bSanctum = em.GetComponentData<BuildingSanctumState>(temple);
                bWorkforce.Workers = 30;
                {
                    em.SetComponentData(temple, bWorkforce);
                    em.SetComponentData(temple, bSanctum);
                }

                var beforeRecruit = SnapshotCodec.Capture(em, root);
                var recruitRequest = new RecruitHeroRequest
                {
                    Sanctum = siteId
                };
                using (HistoryOps.ForRequest(em, root, new QueuedGameplayRequest { Kind = recruitRequest.Kind, Target = recruitRequest.Target }))
                    Check(HeroOps.Recruit(em, root, recruitRequest, at =>
                    {
                        throw new InvalidOperationException("probe");
                    }) == ResultCode.PreparationFailed, "Recruitment after-charge failure is handled in manual history context");
                Check(beforeRecruit.SequenceEqual(SnapshotCodec.Capture(em, root)), "Recruitment failure restores population identity and inventory");
                Check(HeroOps.Recruit(em, root, new RecruitHeroRequest { Sanctum = siteId }) == ResultCode.Success, "Recruit legal persistent hero");
                Entity hero;
                using (var heroes = WorldQueries.Entities<Hero>(em))
                    hero = heroes[0];
                ulong id = em.GetComponentData<Identity>(hero).Id;
                Check(!em.HasComponent<Soldier>(hero), "Hero stays outside soldier pool");
                Check(HeroOps.HeroAvailability(em, root, temple, false) == "已招募", "Quote explains duplicate recruit");
                var saved = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved));
                hero = WorldQueries.Find(em, id);
                temple = WorldQueries.Find(em, siteId);
                Check(em.HasComponent<HeroCombat>(hero) && em.GetComponentData<HeroCombat>(hero).EffectiveSeconds == 0, "Restore rebuilds empty transient combat intervals");
                var malformed = SnapshotCodec.Decode(em, root, saved);
                malformed.Records.OfType<HeroSnapshot>().First().Hero.Experience = -1;
                bool rejected = false;
                try
                {
                    SnapshotCodec.Restore(em, root, malformed);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }

                Check(rejected && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Invalid hero experience rejected without mutation");
                foreach (var fail in new[]
                {
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
                            if (at == fail)
                                throw new InvalidOperationException("probe");
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        threw = true;
                    }

                    Check(threw && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Hero restore rollback " + fail);
                }

                {
                    s = em.GetComponentData<Session>(root);
                    sClock = em.GetComponentData<GameClock>(root);
                    sControl = em.GetComponentData<SimulationControl>(root);
                    sPopulation = em.GetComponentData<PopulationState>(root);
                    sNight = em.GetComponentData<NightRuntimeState>(root);
                    sPersistence = em.GetComponentData<PersistenceGate>(root);
                }

                s.Phase = Phase.Night;
                sNight.Kind = NightKind.Peaceful;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sPopulation);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                int funds = InventoryOps.Count(em, root, gold);
                Check(HeroOps.Wake(em, root, temple) == ResultCode.Unavailable && funds == InventoryOps.Count(em, root, gold), "Unpaid offering cannot wake or charge");
                {
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(temple);
                    bSanctum = em.GetComponentData<BuildingSanctumState>(temple);
                }

                bSanctum.PaidOfferingTurn = sClock.Turn;
                {
                    em.SetComponentData(temple, bWorkforce);
                    em.SetComponentData(temple, bSanctum);
                }

                foreach (var fail in new[]
                {
                    "paid",
                    "deployed"
                }

                )
                {
                    int before = InventoryOps.Count(em, root, gold), reportCount = em.GetBuffer<BattleReportEntry>(root).Length;
                    var historyCount = em.GetBuffer<HistoryEntry>(root).Length;
                    var oldOrder = em.GetComponentData<UnitOrder>(hero);
                    var oldNavigation = em.GetComponentData<NavigationState>(hero);
                    var oldTactics = em.GetComponentData<TacticalState>(hero);
                    using (HistoryOps.ForRequest(em, root, new QueuedGameplayRequest { Kind = CommandKind.WakeHero, Target = siteId }))
                        Check(HeroOps.Wake(em, root, temple, at =>
                        {
                            if (at == fail)
                                throw new InvalidOperationException("probe");
                        }) == ResultCode.PreparationFailed, "Wake fault handled " + fail);
                    Check(historyCount == em.GetBuffer<HistoryEntry>(root).Length && oldOrder.Equals(em.GetComponentData<UnitOrder>(hero)) && oldNavigation.Equals(em.GetComponentData<NavigationState>(hero)) && oldTactics.Equals(em.GetComponentData<TacticalState>(hero)), "Wake fault restores history and transient navigation/order/tactics " + fail);
                    Check(before == InventoryOps.Count(em, root, gold) && reportCount == em.GetBuffer<BattleReportEntry>(root).Length && em.GetComponentData<Combatant>(hero).Deployed == 0 && em.GetComponentData<BuildingSanctumState>(temple).WokenTurn != sClock.Turn, "Wake fault restores costs and deployment " + fail);
                }

                Check(HeroOps.Wake(em, root, temple) == ResultCode.Success, "Paid hero wakes during peaceful night");
                Check(GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, EntityState.Position(em, hero))), "Awakening uses legal perimeter cell");
                funds = InventoryOps.Count(em, root, gold);
                Check(HeroOps.Wake(em, root, temple) == ResultCode.Unavailable && funds == InventoryOps.Count(em, root, gold), "Duplicate awakening cannot charge twice");
                HeroOps.RecordHeroContribution(em, root, hero, 10);
                Check(HeroOps.HeroBattleExperience(em, root, hero) == 0, "Peaceful awakening earns no combat experience");
                sNight.Kind = NightKind.Invasion;
                sClock.Time = 10;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sPopulation);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                Check(HeroOps.HeroBattleExperience(em, root, hero) == 0, "No combat effect means zero experience");
                HeroOps.RecordHeroContribution(em, root, hero, 0);
                Check(em.GetComponentData<HeroCombat>(hero).ContactUntil == 0, "Zero-effect support cannot start contact");
                HeroOps.RecordHeroContribution(em, root, hero, 1);
                HeroOps.RecordHeroContribution(em, root, hero, 1);
                sClock.Time = 12;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sPopulation);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                HeroOps.TickHeroExperience(em, root);
                Check(math.abs(em.GetComponentData<HeroCombat>(hero).EffectiveSeconds - 2) < .001f, "Concurrent effects count union time not effect count");
                sControl.Paused = 1;
                sClock.Time = 13;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sPopulation);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                HeroOps.TickHeroExperience(em, root);
                Check(em.GetComponentData<HeroCombat>(hero).EffectiveSeconds == 2, "Pause does not accrue experience");
                sControl.Paused = 0;
                sClock.Time = 30;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sPopulation);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                HeroOps.TickHeroExperience(em, root);
                Check(em.GetComponentData<HeroCombat>(hero).EffectiveSeconds == 3, "Idle time stops at contact grace window");
                int xp = HeroOps.HeroBattleExperience(em, root, hero);
                Check(xp > 0, "Effective contact grants configurable experience");
                var plan = NightPlanOps.State(em, root);
                int originalThreat = plan.BaseThreat;
                plan.BaseThreat = 100000;
                em.SetComponentData(root, plan);
                Check(HeroOps.HeroBattleExperience(em, root, hero) >= xp, "Higher event threat increases experience with configured cap");
                plan.BaseThreat = originalThreat;
                em.SetComponentData(root, plan);
                var growth = HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(hero).Definition).Growth;
                Check(HeroOps.AddHeroExperience(growth, 0, int.MaxValue) == UnitProgression.LevelThreshold(growth.Progression, growth.Progression.MaxLevel), "Hero XP saturates safely at maximum level");
                HeroOps.ReportHeroExperience(em, root);
                HeroOps.ReportHeroExperience(em, root);
                int reports = 0;
                foreach (var entry in em.GetBuffer<BattleReportEntry>(root))
                    if (entry.Kind == EventKind.HeroExperience)
                        reports++;
                Check(reports == 1, "Report preview deduplicates experience");
                Check(GameRequestExecution.Execute(em, root, new SelectHeroRequest { Hero = id }) == ResultCode.Success, "Select deployed hero");
                var anchor = EntityState.Position(em, hero);
                Check(GameRequestExecution.Execute(em, root, new MoveHeroRequest { Destination = new float3(float.NaN) }) == ResultCode.InvalidTarget, "Reject nonfinite movement");
                Check(GameRequestExecution.Execute(em, root, new MoveHeroRequest { Destination = anchor }) == ResultCode.Success, "Hold at legal ground position");
                Check(GameRequestExecution.Execute(em, root, new FocusHeroRequest { Enemy = id }) == ResultCode.Unavailable, "Forced focus fire stays disabled");
                GameRequestExecution.Execute(em, root, new SelectHeroRequest());
                Check(math.all(em.GetComponentData<Combatant>(hero).Home == anchor) && em.GetComponentData<UnitOrder>(hero).Kind == OrderKind.Move, "Deselection keeps last positional anchor");
                Check(GameRequestExecution.Execute(em, root, new RecallHeroRequest()) == ResultCode.InvalidTarget, "Empty recall request rejects an absent selection without reading tag data");
                GameRequestExecution.Execute(em, root, new SelectHeroRequest { Hero = id });
                Check(GameRequestExecution.Execute(em, root, new RecallHeroRequest()) == ResultCode.Success && em.GetComponentData<UnitOrder>(hero).Kind == OrderKind.Recall && math.all(em.GetComponentData<UnitOrder>(hero).Destination == anchor), "Typed recall request returns the selected hero to its retained anchor");
                GameRequestExecution.Execute(em, root, new SelectHeroRequest());
                BattleLifecycle.Dawn(em, root);
                Check(em.GetComponentData<Hero>(hero).Experience == xp, "Dawn commits preview once");
                BattleLifecycle.Dawn(em, root);
                Check(em.GetComponentData<Hero>(hero).Experience == xp, "Repeated dawn cannot duplicate XP");
                {
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(temple);
                    bSanctum = em.GetComponentData<BuildingSanctumState>(temple);
                }

                bWorkforce.Workers = 0;
                {
                    em.SetComponentData(temple, bWorkforce);
                    em.SetComponentData(temple, bSanctum);
                }

                Check(EntityState.Alive(em, hero), "Worker shortage does not kill hero");
                GameRequestExecution.Execute(em, root, new SelectHeroRequest { Hero = id });
                HeroOps.KillHero(em, root, hero);
                Check(em.GetComponentData<HeroSelection>(root).SelectedHero == Entity.Null && em.GetComponentData<Hero>(hero).Experience == 0, "Death clears selection and experience");
                Check(em.GetComponentData<Hero>(hero).DeathPending == 1, "Night death defers cooldown until dawn");
                BattleLifecycle.Dawn(em, root);
                Check(em.GetComponentData<Hero>(hero).CooldownUntil == sClock.Turn + 1 + HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(hero).Definition).RevivalCooldownTurns, "Full cooldown starts at following dawn");
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/heroes-verification.txt", log.ToString());
            }
        }
    }
}
#endif
