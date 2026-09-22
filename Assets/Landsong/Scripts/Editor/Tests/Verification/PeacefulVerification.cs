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
    public static class PeacefulVerification
    {
        static StringBuilder log;
        static int count;
        static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException("FAIL " + name);
            count++;
            log.AppendLine("PASS " + name);
        }

        static void Reject(Action action, string name)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            Check(rejected, name);
        }

        [MenuItem("Landsong/ECS/Verification/Peaceful")]
        public static string Run()
        {
            log = new StringBuilder();
            count = 0;
            try
            {
                Configuration();
                Simulation();
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/peaceful-verification.txt", log.ToString());
            }
        }

        static BlobAssetReference<OpportunityCatalogBlob> BuildOpportunities(OpportunityCatalogAsset source) => OpportunityCatalogCompiler.Build(source, new BuffCatalogIndex(AssetDatabase.LoadAssetAtPath<BuffCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuffCatalog.asset")), new BuildingCatalogIndex(AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset")), new FeatureCatalogIndex(AssetDatabase.LoadAssetAtPath<FeatureCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/FeatureCatalog.asset")), new ItemCatalogIndex(AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset")));
        static BlobAssetReference<ItemCatalogBlob> BuildItems(ItemCatalogAsset source) => ItemCatalogCompiler.Build(source, new ItemGroupCatalogIndex(AssetDatabase.LoadAssetAtPath<ItemGroupCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemGroupCatalog.asset")));
        static void Configuration()
        {
            var opportunities = AssetDatabase.LoadAssetAtPath<OpportunityCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/OpportunityCatalog.asset");
            using (var blob = BuildOpportunities(opportunities))
                Check(opportunities.Definitions.Any(d => d.Metadata.Id == "night.fairy"), "Separate fairy registered in formal catalog");
            var rules = PeacefulRules.Default;
            rules.Interval = float.NaN;
            Reject(() => PeacefulRulesAuthoring.Validate(rules), "Nonfinite scheduling rejected");
            var copy = CatalogFixture.Clone(opportunities);
            try
            {
                var visitor = copy.Definitions.First(d => d.Metadata.Id == "night.visitor");
                visitor.VisitorProfile.EndFraction = .9f;
                Reject(() =>
                {
                    using var blob = BuildOpportunities(copy);
                }, "Second half opportunity rejected");
            }
            finally
            {
                CatalogFixture.Destroy(copy);
            }

            var items = CatalogFixture.Clone(AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset"));
            try
            {
                items.Definitions[0].Theft.UnitValue = 0;
                Reject(() =>
                {
                    using var blob = BuildItems(items);
                }, "Free theft budget bypass rejected");
            }
            finally
            {
                CatalogFixture.Destroy(items);
            }
        }

        static BattleReportEntry[] Report(EntityManager em, Entity root)
        {
            using var rows = em.GetBuffer<BattleReportEntry>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        static void Simulation()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Wave thirteen isolated", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var session = em.GetComponentData<Session>(root);
                GameClock sessionClock = em.GetComponentData<GameClock>(root);
                SimulationControl sessionControl = em.GetComponentData<SimulationControl>(root);
                NightRuntimeState sessionNight = em.GetComponentData<NightRuntimeState>(root);
                HeroSelection sessionHeroSelection = em.GetComponentData<HeroSelection>(root);
                IntelligenceModeState sessionIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
                PersistenceGate sessionPersistence = em.GetComponentData<PersistenceGate>(root);
                sessionPersistence.CheckpointPending = 0;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                    em.SetComponentData(root, sessionHeroSelection);
                    em.SetComponentData(root, sessionIntelligenceMode);
                    em.SetComponentData(root, sessionPersistence);
                }

                var original = SnapshotCodec.Capture(em, root);
                var thief = OpportunityDefinitions.Find(em, root, "night.visitor");
                var fairy = OpportunityDefinitions.Find(em, root, "night.fairy");
                var soldier = SoldierId.FromIndex(0);
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                Check(InventoryOps.Add(em, root, gold, 100) == 100, "Owned isolated fixture supplies stock through inventory API");
                Entity core = Entity.Null;
                using (var sites = WorldQueries.OrderedEntities<Building>(em))
                    foreach (var site in sites)
                        if (PeacefulOps.Items(em, root, em.GetComponentData<Identity>(site).Id).Count > 0)
                        {
                            core = site;
                            break;
                        }

                Check(core != Entity.Null, "Stocked storage source exists");
                ulong provider = em.GetComponentData<Identity>(core).Id;
                Check(NavigationOps.TryNearestOpen(em, root, EntityState.Position(em, core), 16, out var position), "Legal responder deployment");
                Entity Unit(bool hero = false)
                {
                    Entity e;
                    if (hero)
                    {
                        e = HeroEntities.Spawn(em, root, HeroId.FromIndex(0), position, false);
                        HeroCombatants.Configure(em, root, e, true, provider, position);
                    }
                    else
                    {
                        e = SoldierEntities.Spawn(em, root, soldier, position, false);
                        SoldierCombatants.Configure(em, root, e, true, provider, position);
                    }

                    if (hero)
                        EntityState.Set(em, e, new Hero { Recruited = 1, Sanctum = provider });
                    else
                        EntityState.Set(em, e, new Soldier());
                    return e;
                }

                var unit = Unit();
                var heroUnit = Unit(true);
                {
                    session = em.GetComponentData<Session>(root); // Spawning advances the stable entity ID counter.
                    sessionClock = em.GetComponentData<GameClock>(root);
                    sessionControl = em.GetComponentData<SimulationControl>(root);
                    sessionNight = em.GetComponentData<NightRuntimeState>(root);
                    sessionHeroSelection = em.GetComponentData<HeroSelection>(root);
                    sessionIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
                    sessionPersistence = em.GetComponentData<PersistenceGate>(root);
                } // Spawning advances the stable entity ID counter.

                session.Phase = Phase.Night;
                sessionNight.Kind = NightKind.Peaceful;
                sessionClock.PhaseTime = 3;
                sessionClock.Time = 3;
                sessionNight.Duration = 15;
                sessionHeroSelection.SelectedHero = heroUnit;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                    em.SetComponentData(root, sessionHeroSelection);
                    em.SetComponentData(root, sessionIntelligenceMode);
                    em.SetComponentData(root, sessionPersistence);
                }

                NightResultOps.Reset(em, root);
                var scheduler = em.GetComponentData<PeacefulState>(root);
                scheduler.Next = 99;
                em.SetComponentData(root, scheduler);
                int stock = InventoryOps.Count(em, root, gold);
                Entity Spawn(OpportunityId definition = default) => PeacefulOps.TrySpawn(em, root, !definition.IsValid ? thief : definition, provider, 13579);
                log.AppendLine($"Fixture source items={PeacefulOps.Items(em, root, provider).Count}, operational={BuildingStatus.Operational(em, core)}, response={PeacefulOps.Eligible(em, unit, OpportunityDefinitions.Get(em, root, thief).VisitorProfile)}, route={PeacefulOps.Route(em, root, position, 8).Count}, position={position}, core={em.GetComponentData<BuildingPlacementState>(core).Cell}");
                var visitor = Spawn();
                Check(visitor != Entity.Null, "Valid thief spawns by stocked building");
                Check(InventoryOps.Count(em, root, gold) == stock, "Spawn neither reserves nor removes cargo");
                Check(Spawn() == Entity.Null, "At most one live visitor per source");
                var o = em.GetComponentData<Opportunity>(visitor);
                Check(o.Expires - sessionClock.Time >= 4, "Minimum response duration");
                var grid = em.GetComponentData<GridData>(root);
                var route = em.GetBuffer<VisitorPathPoint>(visitor);
                Check(route.Length > 1, "Nontrivial escape path");
                foreach (var step in route)
                    Check(GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, step.Position)), "Every escape step is legally walkable");
                // Selection priority applies only to responders that can arrive in time.
                // Put the slower hero on a legal route cell, independent of the authored building footprint.
                em.SetComponentData(heroUnit, LocalTransform.FromPosition(route[0].Position));
                Check(NightOps.PickUp(em, root, visitor) == ResultCode.Success && em.Exists(visitor), "Click assigns interception, does not instantly remove visitor");
                Check(em.GetComponentData<Opportunity>(visitor).Responder == heroUnit, "Selected eligible hero takes priority");
                Check(em.GetComponentData<UnitOrder>(heroUnit).Kind == OrderKind.Capture, "Explicit non-damaging ECS order");
                Check(NightOps.PickUp(em, root, visitor) == ResultCode.Busy, "Duplicate clicks do not assign duplicate responders");
                em.SetComponentData(heroUnit, LocalTransform.FromPosition(EntityState.Position(em, visitor)));
                PeacefulOps.Tick(em, root, 0);
                Check(!em.Exists(visitor) && Report(em, root).Any(e => e.Kind == EventKind.TheftPrevented), "Proximity catches and reports prevention");
                Check(InventoryOps.Count(em, root, gold) == stock && em.GetComponentData<Combatant>(heroUnit).Participated == 0 && HeroOps.HeroBattleExperience(em, root, heroUnit) == 0, "Capture causes no damage, expense or combat XP");
                visitor = Spawn(fairy);
                Check(visitor != Entity.Null, "Fairy uses separate definition");
                PeacefulOps.Finish(em, root, visitor, true);
                Check(InventoryOps.Count(em, root, gold) == stock && em.GetBuffer<NightItemReward>(root).Length == 1, "Fairy gift is journaled, not immediately deposited");
                visitor = Spawn();
                Check(visitor != Entity.Null, "Same building supports later opportunity");
                PeacefulOps.Finish(em, root, visitor, false);
                var theft = Report(em, root).Where(e => e.Kind == EventKind.Theft).ToArray();
                Check(theft.Length == 1 && !theft[0].SourceName.IsEmpty && theft[0].Amount <= 5, "Escape only removes capped item and retains source name");
                using (var messages = em.GetBuffer<GameEvent>(root).ToNativeArray(Allocator.Temp))
                    Check(!messages.Any(e => e.Kind == EventKind.Theft), "Live event notifications do not disclose stolen items");
                int value = em.GetComponentData<PeacefulState>(root).StolenValue;
                Check(value > 0 && value <= PeacefulOps.Rules(em, root).TheftValueBudget, "Per-night stolen value budget consumed");
                var seeded = em.GetComponentData<PeacefulState>(root);
                uint global = em.GetComponentData<SimulationRandomState>(root).State;
                for (int i = 0; i < 8; i++)
                {
                    visitor = Spawn();
                    if (visitor != Entity.Null)
                        PeacefulOps.Finish(em, root, visitor, false);
                }

                Check(em.GetComponentData<PeacefulState>(root).StolenValue <= PeacefulOps.Rules(em, root).TheftValueBudget && em.GetComponentData<SimulationRandomState>(root).State == global, "Repeated escape respects budget without global RNG mutation");
                using var stockCopy = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp);
                var slots = em.GetBuffer<InventorySlot>(root);
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    slot.Count = 0;
                    slots[i] = slot;
                }

                Check(Spawn() == Entity.Null, "Empty source cannot spawn thief");
                visitor = Spawn(fairy);
                Check(visitor != Entity.Null, "Fairy needs no inventory");
                PeacefulOps.Finish(em, root, visitor, false);
                slots = em.GetBuffer<InventorySlot>(root);
                slots.CopyFrom(stockCopy);
                {
                    session = em.GetComponentData<Session>(root);
                    sessionClock = em.GetComponentData<GameClock>(root);
                    sessionControl = em.GetComponentData<SimulationControl>(root);
                    sessionNight = em.GetComponentData<NightRuntimeState>(root);
                    sessionHeroSelection = em.GetComponentData<HeroSelection>(root);
                    sessionIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
                    sessionPersistence = em.GetComponentData<PersistenceGate>(root);
                }

                sessionControl.Paused = 1;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                    em.SetComponentData(root, sessionHeroSelection);
                    em.SetComponentData(root, sessionIntelligenceMode);
                    em.SetComponentData(root, sessionPersistence);
                }

                var before = em.GetComponentData<PeacefulState>(root);
                PeacefulOps.Tick(em, root, 9);
                Check(em.GetComponentData<PeacefulState>(root).Equals(before) && Spawn() == Entity.Null, "Pause freezes scheduler and denies spawn");
                sessionControl.Paused = 0;
                sessionIntelligenceMode.Enabled = 1;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                    em.SetComponentData(root, sessionHeroSelection);
                    em.SetComponentData(root, sessionIntelligenceMode);
                    em.SetComponentData(root, sessionPersistence);
                }

                visitor = Spawn();
                Check(visitor != Entity.Null && GameRequestExecution.Execute(em, root, new PickUpRequest { Loot = em.GetComponentData<Identity>(visitor).Id }) == ResultCode.Busy, "Intel viewing does not suppress world but blocks capture input");
                PeacefulOps.Finish(em, root, visitor, false, true);
                sessionIntelligenceMode.Enabled = 0;
                sessionNight.Kind = NightKind.Invasion;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                    em.SetComponentData(root, sessionHeroSelection);
                    em.SetComponentData(root, sessionIntelligenceMode);
                    em.SetComponentData(root, sessionPersistence);
                }

                Check(Spawn() == Entity.Null, "Peaceful and battle mutually exclusive");
                Boundaries(em, root, provider, unit, heroUnit, thief, fairy);
                LootAndReport(em, root, original, position);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Boundaries(EntityManager em, Entity root, ulong provider, Entity unit, Entity hero, OpportunityId thief, OpportunityId fairy)
        {
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            HeroSelection sHeroSelection = em.GetComponentData<HeroSelection>(root);
            s.Phase = Phase.Night;
            sNight.Kind = NightKind.Peaceful;
            sClock.PhaseTime = 3;
            sClock.Time = 3;
            sHeroSelection.SelectedHero = Entity.Null;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
            }

            var heroTransform = em.GetComponentData<LocalTransform>(hero);
            sHeroSelection.SelectedHero = hero;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
            }

            em.SetComponentData(hero, LocalTransform.FromPosition(heroTransform.Position + new float3(1000, 0, 0)));
            var unreachable = PeacefulOps.TrySpawn(em, root, fairy, provider, 13579);
            Check(unreachable != Entity.Null && PeacefulOps.Claim(em, root, unreachable) == ResultCode.Success && em.GetComponentData<Opportunity>(unreachable).Responder != hero, "Selected unreachable hero falls back to an intercepting unit");
            PeacefulOps.Finish(em, root, unreachable, false, true);
            em.SetComponentData(hero, heroTransform);
            {
                s = em.GetComponentData<Session>(root);
                sClock = em.GetComponentData<GameClock>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
                sHeroSelection = em.GetComponentData<HeroSelection>(root);
            }

            sHeroSelection.SelectedHero = Entity.Null;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
            }

            var e = PeacefulOps.TrySpawn(em, root, fairy, provider, 13579);
            Check(e != Entity.Null && PeacefulOps.Claim(em, root, e) == ResultCode.Success, "No selected hero falls back to qualified automatic unit");
            PeacefulOps.Finish(em, root, e, false, true);
            sClock.PhaseTime = 8;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
            }

            Check(PeacefulOps.TrySpawn(em, root, fairy, provider, 42) == Entity.Null, "No visitors after first half");
            sClock.PhaseTime = 3;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
            }

            var actor = em.GetComponentData<Combatant>(unit);
            var heroActor = em.GetComponentData<Combatant>(hero);
            var inactive = actor;
            inactive.Deployed = 0;
            em.SetComponentData(unit, inactive);
            inactive = heroActor;
            inactive.Deployed = 0;
            em.SetComponentData(hero, inactive);
            Check(PeacefulOps.TrySpawn(em, root, fairy, provider, 42) == Entity.Null, "No actionable friendly unit cancels opportunity before spawn");
            em.SetComponentData(unit, actor);
            em.SetComponentData(hero, heroActor);
            var bell = WorldQueries.Find(em, provider);
            var originalStats = em.GetComponentData<BuildingHousingStats>(bell);
            BuildingBellStats originalStatsBell = em.GetComponentData<BuildingBellStats>(bell);
            BuildingBellStats bellStatsBell = originalStatsBell;
            bellStatsBell.Radius = 30;
            {
                em.SetComponentData(bell, bellStatsBell);
            }

            e = PeacefulOps.TrySpawn(em, root, fairy, provider, 13579);
            Check(e != Entity.Null, "Bell visitor fixture");
            BellOps.Bell(em, root, bell);
            Check(em.GetComponentData<Opportunity>(e).Responder != Entity.Null, "Bell dispatches a nearby eligible responder");
            var responder = em.GetComponentData<Opportunity>(e).Responder;
            BellOps.Bell(em, root, bell);
            PeacefulOps.Tick(em, root, 0);
            Check(em.Exists(e) && em.GetComponentData<Opportunity>(e).Responder == Entity.Null && em.GetComponentData<UnitOrder>(responder).Kind != OrderKind.Capture, "Second bell click cancels capture assignment without deleting visitor");
            PeacefulOps.Finish(em, root, e, false, true);
            {
                em.SetComponentData(bell, originalStats);
                em.SetComponentData(bell, originalStatsBell);
            }

            var recalled = em.GetComponentData<Soldier>(unit);
            recalled.RecallState = 1;
            em.SetComponentData(unit, recalled);
            Check(!PeacefulOps.Eligible(em, unit, OpportunityDefinitions.Get(em, root, fairy).VisitorProfile), "Recalling soldiers cannot accept capture jobs");
            recalled.RecallState = 0;
            em.SetComponentData(unit, recalled);
            e = PeacefulOps.TrySpawn(em, root, fairy, provider, 42);
            Check(e != Entity.Null, "Cancellation path fixture");
            var grid = em.GetComponentData<GridData>(root);
            var next = em.GetBuffer<VisitorPathPoint>(e)[0].Position;
            int at = GridOps.Index(grid, GridOps.Cell(grid, next));
            var occupied = em.GetBuffer<Occupancy>(root);
            var old = occupied[at];
            occupied[at] = new Occupancy
            {
                Owner = ulong.MaxValue,
                MovementCost = 0
            };
            PeacefulOps.Tick(em, root, .1f);
            Check(!em.Exists(e) && Report(em, root).Any(r => r.Kind == EventKind.VisitorCancelled), "Newly blocked escape route cancels without theft");
            occupied = em.GetBuffer<Occupancy>(root);
            occupied[at] = old;
            var source = WorldQueries.Find(em, provider);
            var identity = em.GetComponentData<Identity>(source);
            var named = identity;
            named.Name = "国库";
            em.SetComponentData(source, named);
            e = PeacefulOps.TrySpawn(em, root, thief, provider, 42);
            Check(e != Entity.Null, "Named source fixture");
            em.SetComponentData(source, identity);
            PeacefulOps.Finish(em, root, e, false);
            Check(Report(em, root).Any(r => r.Kind == EventKind.VisitorEscaped && r.SourceName.ToString() == "国库"), "Source rename after spawn cannot rewrite captured report name");
            var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset");
            var copy = CatalogFixture.Clone(catalog);
            var original = em.GetComponentData<ItemCatalog>(root);
            try
            {
                using var inventory = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp);
                var slot = inventory.First(x => x.Provider == provider && x.Count > 0);
                foreach (var protection in new[]
                {
                    ItemProtection.Quest,
                    ItemProtection.Unique,
                    ItemProtection.Bound
                }

                )
                {
                    var item = copy.Definitions[slot.Item.Index];
                    var t = item.Theft;
                    t.Protection = protection;
                    item.Theft = t;
                    using var blob = BuildItems(copy);
                    em.SetComponentData(root, new ItemCatalog { Value = blob });
                    Check(!PeacefulOps.Stealable(em, root, slot), protection + " resource excluded");
                    em.SetComponentData(root, original);
                }
            }
            finally
            {
                em.SetComponentData(root, original);
                foreach (var d in copy.Definitions)
                    UnityEngine.Object.DestroyImmediate(d);
                UnityEngine.Object.DestroyImmediate(copy);
            }

            string Schedule()
            {
                NightResultOps.Reset(em, root);
                em.GetBuffer<BattleReportEntry>(root).Clear();
                var signature = new StringBuilder();
                foreach (float time in new[]
                {
                    2.5f,
                    5,
                    7.5f,
                    10,
                    14
                }

                )
                {
                    sClock.PhaseTime = time;
                    sClock.Time = time;
                    {
                        em.SetComponentData(root, s);
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sNight);
                        em.SetComponentData(root, sHeroSelection);
                    }

                    PeacefulOps.Tick(em, root, 0);
                    using var visitors = WorldQueries.OrderedEntities<Opportunity>(em);
                    foreach (var v in visitors)
                    {
                        signature.Append(em.GetComponentData<OpportunityDefinitionRef>(v).Definition.Index).Append(':').Append(em.GetComponentData<Opportunity>(v).Provider).Append(';');
                        PeacefulOps.Finish(em, root, v, false, true);
                    }
                }

                var state = em.GetComponentData<PeacefulState>(root);
                Check(state.Spawned <= PeacefulOps.Rules(em, root).MaximumPerNight, "Scheduler respects total budget");
                foreach (var c in em.GetBuffer<OpportunityCount>(root))
                    Check(c.Count <= OpportunityDefinitions.Get(em, root, c.Definition).VisitorProfile.MaximumPerNight, "Per-kind repeat cap enforced");
                return signature.ToString();
            }

            string first = Schedule(), again = Schedule();
            Check(first == again && first.Length > 0, "Local seed replays same definitions and equal-weight source picks");
            Check(em.GetComponentData<PeacefulState>(root).Spawned > 1, "Multiple opportunities per quiet night remain possible");
            em.GetBuffer<BattleReportEntry>(root).Clear();
            s.Phase = Phase.Retreat;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
            }

            Check(NightReportOps.Prepare(em, root) == NightEndPose.Guard, "Timeout cleanup uses guard pose");
            s.Phase = Phase.Night;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
            }

            em.GetBuffer<BattleReportEntry>(root).Clear();
            Check(NightReportOps.Prepare(em, root) == NightEndPose.Celebrate, "Clean end uses cheering pose");
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.ResidentsLost, Amount = 2, SourceName = "受损民居" });
            Check(NightReportOps.Prepare(em, root) == NightEndPose.Aid, "Civilian loss overrides celebratory mood");
        }

        static void LootAndReport(EntityManager em, Entity root, byte[] original, float3 position)
        {
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            s.Phase = Phase.Night;
            sNight.Kind = NightKind.Invasion;
            sPersistence.CheckpointPending = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            NightResultOps.Reset(em, root);
            var raider = EnemyDefinitions.Find(em, root, "raider");
            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            int stock = InventoryOps.Count(em, root, gold);
            var enemy = EnemyEntities.Spawn(em, root, raider, position, false);
            EnemyCombatants.Configure(em, root, enemy, true, 0, position);
            CombatOps.Death(em, root, enemy, Entity.Null);
            using (var drops = WorldQueries.Entities<Loot>(em))
                Check(drops.Length == 0 && em.GetBuffer<NightItemReward>(root).Length > 0, "Ordinary kill rewards directly enter journal, no clickable drops");
            int countBefore = em.GetBuffer<NightItemReward>(root).Length;
            CombatOps.Death(em, root, enemy, Entity.Null);
            Check(em.GetBuffer<NightItemReward>(root).Length == countBefore && InventoryOps.Count(em, root, gold) == stock, "Repeated death does not duplicate gains or deposit early");
            EnemyId boss = default;
            for (int i = 0; i < EnemyDefinitions.Count(em, root); i++)
                if ((EnemyDefinitions.Get(em, root, EnemyId.FromIndex(i)).Behavior & EnemyBehaviorFlags.Boss) != 0)
                    boss = EnemyId.FromIndex(i);
            var e = EnemyEntities.Spawn(em, root, boss, position, false);
            EnemyCombatants.Configure(em, root, e, true, 0, position);
            CombatOps.Death(em, root, e, Entity.Null);
            Entity drop = Entity.Null;
            using (var drops = WorldQueries.Entities<Loot>(em))
            {
                Check(drops.Length > 0, "Boss special rule creates clickable guaranteed reward");
                drop = drops[0];
            }

            var dropValue = em.GetComponentData<Loot>(drop);
            Check(dropValue.Rarity == 3 && !dropValue.SourceName.IsEmpty, "Special rarity and stable source snapshot");
            // Drops may land on an additional surface beneath the primary terrain (e.g. under a bridge).
            SurfaceNavigationGraph.Ensure(em, root);
            var landing = EntityState.Position(em, drop);
            bool legalLanding = false;
            foreach (var node in em.GetBuffer<SurfaceNavNode>(root))
                if (node.Open != 0 && math.distancesq(node.Position, landing) < .0001f)
                {
                    legalLanding = true;
                    break;
                }

            Check(legalLanding, "Special drop lands on an open navigation surface at the correct height");
            NightOps.PickUp(em, root, drop);
            countBefore = em.GetBuffer<NightItemReward>(root).Length;
            Check(NightOps.PickUp(em, root, drop) == ResultCode.InvalidTarget && em.GetBuffer<NightItemReward>(root).Length == countBefore, "Drop identity prevents double claims");
            var more = LootEntities.Spawn(em, root, LootId.FromIndex(0), position, false);
            EntityState.Set(em, more, new Loot { Item = gold, Count = 100000, Rarity = 2 });
            EntityState.Set(em, more, new VisualState { Visible = 1 });
            {
                s = em.GetComponentData<Session>(root);
                sClock = em.GetComponentData<GameClock>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
                sPersistence = em.GetComponentData<PersistenceGate>(root);
            }

            s.Phase = Phase.Celebration;
            sClock.PhaseTime = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            NightOps.Tick(em, root, 9.9f);
            Check(em.Exists(more), "Unclicked loot does not expire in ending");
            int historyBefore = em.HasBuffer<BattleHistoryEntry>(root) ? em.GetBuffer<BattleHistoryEntry>(root).Length : 0;
            InventoryOps.Add(em, root, gold, 100000000);
            Check(NightOps.EndEarly(em, root) == ResultCode.Success, "Manual next phase accepts completed combat");
            Check(!em.Exists(more) && em.GetComponentData<Session>(root).Phase == Phase.Day, "Manual end collects unclaimed loot and enters dawn");
            Check(em.GetBuffer<PendingItem>(root).Length > 0 && Report(em, root).Any(r => r.Kind == EventKind.RewardOverflow), "Full warehouse moves rewards to pending without missing-loot penalty");
            int count = em.GetBuffer<BattleHistoryEntry>(root).Length;
            Check(count > historyBefore, "Committed complete report archived");
            NightOps.Dawn(em, root);
            Check(em.GetBuffer<BattleHistoryEntry>(root).Length == count, "Duplicate dawn never duplicates history");
            var saved = SnapshotCodec.Capture(em, root);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved));
            Check(em.GetBuffer<BattleHistoryEntry>(root).Length == count, "Committed report survives save and restore");
            var seed = em.GetComponentData<PeacefulState>(root);
            seed.Random = 4444;
            seed.StolenValue = 7;
            em.SetComponentData(root, seed);
            em.GetBuffer<OpportunityCount>(root).Add(new OpportunityCount { Definition = OpportunityId.FromIndex(0), Count = 2 });
            foreach (string point in new[]
            {
                "root-reset",
                "root-published"
            }

            )
            {
                Reject(() => SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original), probe: stage =>
                {
                    if (stage == point)
                        throw new InvalidOperationException("Owned wave13 injection");
                }), "Restore failure rollback at " + point);
                Check(em.GetBuffer<BattleHistoryEntry>(root).Length == count && em.GetComponentData<PeacefulState>(root).Equals(seed), "History and local RNG rollback at " + point);
                Check(em.GetBuffer<OpportunityCount>(root).Length == 1 && em.GetBuffer<OpportunityCount>(root)[0].Count == 2, "Per-kind opportunity counts rollback at " + point);
            }

            var bad = SnapshotCodec.Decode(em, root, saved);
            if (bad.BattleHistory.Length > 0)
            {
                bad.BattleHistory[0].Entry.Value = float.NaN;
                Reject(() => SnapshotCodec.Restore(em, root, bad), "Nonfinite report history rejected before publication");
            }

            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            Check(em.GetBuffer<NightItemReward>(root).Length == 0 && em.GetBuffer<BattleHistoryEntry>(root).Length == historyBefore, "Node replay removes uncommitted rewards and future report history");
        }
    }
}
#endif
