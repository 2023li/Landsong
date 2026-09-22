#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Pathfinding.ECS;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class CombatVerification
    {
        static StringBuilder log;
        static int count;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            count++;
            log.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verification/Combat")]
        public static string Run()
        {
            log = new StringBuilder();
            count = 0;
            try
            {
                Configuration();
                WorldTest();
                log.AppendLine("Assertions: " + count);
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
                File.WriteAllText("Library/LandsongEcs/combat-verification.txt", log.ToString());
            }
        }

        static void Configuration()
        {
            var p = CombatProfile.Default;
            Check(CombatProfile.Valid(p), "Usable editable defaults");
            p.ChaseSeconds = 0;
            Check(!CombatProfile.Valid(p), "Unbounded or zero pursuit rejected");
            p = CombatProfile.Default;
            p.Armor = float.NaN;
            Check(!CombatProfile.Valid(p), "Nonfinite armor rejected");
            p = CombatProfile.Default;
            p.WarningSeconds = p.ProjectileLifetime;
            Check(!CombatProfile.Valid(p), "Warning cannot outlive projectile");
            p = CombatProfile.Default;
            p.Reduction = 1;
            Check(!CombatProfile.Valid(p), "Permanent full mitigation rejected");
            Check(ProjectileOps.Mitigate(20, 8, 3, .25f) == 11.25f, "Flat armor minus penetration then damage reduction");
            Check(ProjectileOps.Mitigate(3, 10, 0, 0) == 0, "Armor cannot heal target");
        }

        static PreparedSoldier[] Prepared(EntityManager em, Entity root)
        {
            var b = em.GetBuffer<PreparedSoldier>(root);
            var result = new PreparedSoldier[b.Length];
            for (int i = 0; i < result.Length; i++)
                result[i] = b[i];
            return result;
        }

        static void WorldTest()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("11C combat", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var s = em.GetComponentData<Session>(root);
                GameClock sClock = em.GetComponentData<GameClock>(root);
                SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
                NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
                PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                sPersistence.CheckpointPending = 0;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                var original = SnapshotCodec.Capture(em, root);
                var grid = em.GetComponentData<GridData>(root);
                var occupancy = em.GetBuffer<Occupancy>(root);
                int2 center = default;
                bool found = false;
                for (int y = grid.Value.Value.Min.y + 12; y < grid.Value.Value.Min.y + grid.Value.Value.Size.y - 12 && !found; y++)
                    for (int x = grid.Value.Value.Min.x + 12; x < grid.Value.Value.Min.x + grid.Value.Value.Size.x - 12; x++)
                    {
                        bool open = true;
                        for (int dy = -7; dy <= 7 && open; dy++)
                            for (int dx = -7; dx <= 7; dx++)
                                if (!GridOps.Traversable(grid, occupancy, new int2(x + dx, y + dy)))
                                {
                                    open = false;
                                    break;
                                }

                        if (open)
                        {
                            center = new int2(x, y);
                            found = true;
                            break;
                        }
                    }

                Check(found, "Open combat arena inside map");
                var origin = GridOps.Position(grid, center, new int2(1)) + new float3(0, .5f, 0);
                var soldier = SoldierId.FromIndex(0);
                var enemy = EnemyDefinitions.Find(em, root, "raider");
                var shotDefinition = ProjectileId.FromIndex(0);
                Entity Unit(byte faction, float3 position)
                {
                    if (faction == 0)
                    {
                        var e = SoldierEntities.Spawn(em, root, soldier, position, false);
                        SoldierCombatants.Configure(em, root, e, true, 0, origin);
                        return e;
                    }
                    else
                    {
                        var e = EnemyEntities.Spawn(em, root, enemy, position, false);
                        EnemyCombatants.Configure(em, root, e, true, 0, origin);
                        return e;
                    }
                }

                s.Phase = Phase.Night;
                sNight.Kind = NightKind.Invasion;
                sClock.Time = 1;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                var a = Unit(0, origin);
                var b = Unit(1, origin + new float3(1, 0, 0));
                float Life(Entity e) => em.GetComponentData<Health>(e).Current;
                Entity Shot(Entity source, Entity target, ProjectileMode mode, float radius = 0, float warning = 0)
                {
                    var unit = em.GetComponentData<Combatant>(source);
                    var e = ProjectileEntities.Spawn(em, root, shotDefinition, EntityState.Position(em, source) + new float3(0, .3f, 0), false);
                    EntityState.Set(em, e, new Projectile { Source = source, Target = target, SourceId = em.GetComponentData<Identity>(source).Id, TargetId = em.GetComponentData<Identity>(target).Id, Faction = unit.Faction, Damage = 10, Speed = 100, Lifetime = 10, Radius = radius, Warning = warning, Landing = EntityState.Position(em, target) + new float3(0, .3f, 0), Mode = mode });
                    return e;
                }

                void Tick(float delta)
                {
                    GameClock sessionClock = em.GetComponentData<GameClock>(root);
                    sessionClock.Time += delta;
                    {
                        em.SetComponentData(root, sessionClock);
                    }

                    world.SetTime(new TimeData(sessionClock.Time, delta));
                    world.GetOrCreateSystem<ProjectileSystem>().Update(world.Unmanaged);
                    world.GetOrCreateSystem<DamageSystem>().Update(world.Unmanaged);
                }

                float hp = Life(b);
                Shot(a, b, ProjectileMode.Tracking);
                Tick(.1f);
                Check(Life(b) == hp - 10, "Tracking projectile actual systems resolve one hit");
                Tick(.1f);
                Check(Life(b) == hp - 10, "Consumed projectile cannot double hit");
                float own = Life(a);
                Shot(a, a, ProjectileMode.Tracking);
                Tick(.1f);
                Check(Life(a) == own, "Projectile payload refuses friendly fire");
                var d = em.GetComponentData<Combatant>(b);
                d.Profile.Armor = 8;
                d.Profile.Reduction = .5f;
                em.SetComponentData(b, d);
                hp = Life(b);
                var shot = Shot(a, b, ProjectileMode.Tracking);
                var payload = em.GetComponentData<Projectile>(shot);
                payload.Penetration = 4;
                em.SetComponentData(shot, payload);
                Tick(.1f);
                Check(Life(b) == hp - 3, "Projectile penetration and frozen defender profile applied");
                d.Profile.Armor = 0;
                d.Profile.Reduction = 0;
                em.SetComponentData(b, d);
                shot = Shot(a, b, ProjectileMode.Tracking);
                var sourceActor = em.GetComponentData<Combatant>(a);
                sourceActor.Damage = 999;
                sourceActor.Profile.Penetration = 999;
                em.SetComponentData(a, sourceActor);
                hp = Life(b);
                Tick(.1f);
                Check(Life(b) == hp - 10, "Already launched payload ignores later attacker changes");
                shot = Shot(a, b, ProjectileMode.Tracking);
                em.SetComponentData(b, new Health { Maximum = 100, Current = 0 });
                Tick(.1f);
                Check(!em.Exists(shot), "Tracking target death invalidates projectile");
                em.SetComponentData(b, new Health { Maximum = 100, Current = 100 });
                var c = Unit(1, origin + new float3(1, 0, 1));
                hp = Life(b);
                float otherHp = Life(c);
                own = Life(a);
                shot = Shot(a, b, ProjectileMode.Ground, 2, .5f);
                Tick(.1f);
                Check(em.Exists(shot) && Life(b) == hp, "Ground warning delays damage");
                {
                    s = em.GetComponentData<Session>(root);
                    sClock = em.GetComponentData<GameClock>(root);
                    sControl = em.GetComponentData<SimulationControl>(root);
                    sNight = em.GetComponentData<NightRuntimeState>(root);
                    sPersistence = em.GetComponentData<PersistenceGate>(root);
                }

                sControl.Paused = 1;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                float remaining = em.GetComponentData<Projectile>(shot).Warning;
                Tick(1);
                Check(em.GetComponentData<Projectile>(shot).Warning == remaining, "Pause freezes ground warning");
                sControl.Paused = 0;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                Tick(.5f);
                Tick(.1f);
                Check(Life(b) == hp - 10 && Life(c) == otherHp - 10 && Life(a) == own, "Area hits each hostile once without friendly fire");
                shot = Shot(a, b, ProjectileMode.Ground, 2);
                var landing = em.GetComponentData<Projectile>(shot).Landing;
                em.SetComponentData(b, LocalTransform.FromPosition(origin + new float3(6, 0, 0)));
                hp = Life(b);
                Tick(.1f);
                Check(Life(b) == hp && !em.Exists(shot), "Ground landing stays fixed when target moves");
                using (var blockOwner = ProjectileOps.Blocks(em, root, Allocator.Temp))
                {
                    var blocks = blockOwner;
                    var to = origin + new float3(5, 0, 0);
                    int at = GridOps.Index(grid, center + new int2(2, 0));
                    blocks[at] = new ProjectileBlock
                    {
                        Environment = 1
                    };
                    Check(ProjectileOps.Blocked(grid, blocks, origin, to, 0, 0), "Swept segment catches one-cell environmental blocker");
                    blocks[at] = new ProjectileBlock
                    {
                        Owner = 777
                    };
                    Check(ProjectileOps.Blocked(grid, blocks, origin, to, 0, 0), "Blocking building intercepts other targets");
                    Check(!ProjectileOps.Blocked(grid, blocks, origin, to, 0, 777), "Intended building target is hittable through own footprint");
                    blocks[at] = default;
                    Check(!ProjectileOps.Blocked(grid, blocks, origin, to, 0, 0), "Ruined removed blocker no longer intercepts");
                }

                em.SetComponentData(b, LocalTransform.FromPosition(origin + new float3(2, 0, 0)));
                em.SetComponentData(a, new Perception { Enemy = b, EnemyPosition = EntityState.Position(em, b) });
                TacticalOps.Update(em, root);
                var tactical = em.GetComponentData<TacticalState>(a);
                Check(tactical.Pursued == b && tactical.HasSlot != 0, "Automatic pursuit reserves an engagement position");
                var ally = Unit(0, origin);
                em.SetComponentData(ally, new Perception { Enemy = b, EnemyPosition = EntityState.Position(em, b) });
                TacticalOps.Update(em, root);
                Check(math.distance(em.GetComponentData<TacticalState>(a).Engagement, em.GetComponentData<TacticalState>(ally).Engagement) >= .69f, "Two attackers reserve separate body-width slots");
                {
                    s = em.GetComponentData<Session>(root);
                    sClock = em.GetComponentData<GameClock>(root);
                    sControl = em.GetComponentData<SimulationControl>(root);
                    sNight = em.GetComponentData<NightRuntimeState>(root);
                    sPersistence = em.GetComponentData<PersistenceGate>(root);
                }

                sClock.Time += 20;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                em.SetComponentData(a, LocalTransform.FromPosition(origin + new float3(4, 0, 0)));
                TacticalOps.Update(em, root);
                Check(em.GetComponentData<TacticalState>(a).Returning != 0 && em.GetComponentData<Perception>(a).Enemy == Entity.Null, "Timed-out pursuit relinquishes enemy and returns");
                em.SetComponentData(a, new UnitOrder { Kind = OrderKind.Move, Destination = origin + new float3(3, 0, 0) });
                TacticalOps.Update(em, root);
                Check(em.GetComponentData<TacticalState>(a).Returning == 0, "Player position command overrides automatic return");
                em.SetComponentData(a, LocalTransform.FromPosition(origin));
                em.SetComponentData(ally, LocalTransform.FromPosition(origin));
                em.SetComponentData(a, new Steering());
                em.SetComponentData(ally, new Steering());
                world.SetTime(new TimeData(sClock.Time, .1f));
                world.GetOrCreateSystem<NavigationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<FallbackResolveMovementSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<AstarNavigationMoveSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                Check(em.HasComponent<AstarNavigationAgent>(a) && em.HasComponent<Pathfinding.ECS.RVO.RVOAgent>(a), "Combatants are enrolled in A* Pro ECS local avoidance");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                NightPlanOps.Prepare(em, root);
                var prepared = Prepared(em, root);
                Check(em.GetBuffer<PreparedBuildingDefense>(root).Length > 0, "Building defense participates in dusk snapshot");
                var expected = prepared.First(p => p.Definition == soldier).Stats;
                Check(CombatProfile.Valid(expected.Combat) && expected.Interval > 0 && expected.ProjectileSpeed > 0, "Prepared unit carries complete combat profile");
                {
                    s = em.GetComponentData<Session>(root);
                    sClock = em.GetComponentData<GameClock>(root);
                    sControl = em.GetComponentData<SimulationControl>(root);
                    sNight = em.GetComponentData<NightRuntimeState>(root);
                    sPersistence = em.GetComponentData<PersistenceGate>(root);
                }

                s.Phase = Phase.Deployment;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }

                BattleLifecycle.PrepareNight(em, root);
                var saved = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved));
                Check(expected.Equals(Prepared(em, root).First(p => p.Definition == soldier).Stats), "Expanded prepared payload survives dusk reconstruction");
                var malformed = SnapshotCodec.Decode(em, root, saved);
                malformed.PreparedSoldiers[0].Stats.Combat.Reduction = float.NaN;
                bool rejected = false;
                try
                {
                    SnapshotCodec.Restore(em, root, malformed);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }

                Check(rejected && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Malformed defense rejected before mutation");
                foreach (var fail in new[]
                {
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
                            if (at == fail)
                                throw new InvalidOperationException("probe");
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        threw = true;
                    }

                    Check(threw && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Expanded preparation rollback " + fail);
                }

                Modifiers(em, root, soldier);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Modifiers(EntityManager em, Entity root, SoldierId soldier)
        {
            var source = AssetDatabase.LoadAssetAtPath<BuffCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuffCatalog.asset");
            var catalog = CatalogFixture.Clone(source);
            var soldierSource = AssetDatabase.LoadAssetAtPath<SoldierCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/SoldierCatalog.asset");
            var at = BuffId.FromIndex(0);
            var buff = catalog.Definitions[at.Index];
            var kinds = new[]
            {
                NumericEffectKind.AttackRangeMultiplier,
                NumericEffectKind.AttackSpeedMultiplier,
                NumericEffectKind.MovementSpeedMultiplier,
                NumericEffectKind.Armor,
                NumericEffectKind.DamageReduction,
                NumericEffectKind.Penetration,
                NumericEffectKind.ProjectileSpeedMultiplier,
                NumericEffectKind.BlastRadius
            };
            buff.Effects = new DefinitionEffectsSource
            {
                Soldiers = kinds.Select((kind, i) => new SoldierNumericEffectSource { Target = soldierSource.Definitions[soldier.Index], Effect = kind, Magnitude = i == 3 ? 4 : i == 5 ? 3 : i == 7 ? 1 : .2f }).ToArray()
            };
            var original = em.GetComponentData<BuffCatalog>(root);
            try
            {
                using var blob = BuffCatalogCompiler.Build(catalog, new BuildingCatalogIndex(AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset")), new HeroCatalogIndex(AssetDatabase.LoadAssetAtPath<HeroCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/HeroCatalog.asset")), new ItemCatalogIndex(AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemCatalog.asset")), new SoldierCatalogIndex(soldierSource), new TalentCatalogIndex(AssetDatabase.LoadAssetAtPath<TalentCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TalentCatalog.asset")), new TechnologyCatalogIndex(AssetDatabase.LoadAssetAtPath<TechnologyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TechnologyCatalog.asset")));
                em.SetComponentData(root, new BuffCatalog { Value = blob });
                var grants = em.GetBuffer<OwnedBuff>(root);
                for (int i = grants.Length - 1; i >= 0; i--)
                    if (grants[i].Buff == at)
                        grants.RemoveAt(i);
                var s = em.GetComponentData<Session>(root);
                s.Phase = Phase.Day;
                em.SetComponentData(root, s);
                var before = SoldierCombatStats.Current(em, root, soldier);
                PermanentBuffs.Grant(em, root, at, 1);
                var after = SoldierCombatStats.Current(em, root, soldier);
                Check(after.Range > before.Range && after.Interval < before.Interval && after.Speed > before.Speed, "Range attack-speed movement modifiers execute");
                Check(after.Combat.Armor == before.Combat.Armor + 4 && after.Combat.Reduction > before.Combat.Reduction && after.Combat.Penetration == before.Combat.Penetration + 3, "Defense reduction penetration modifiers execute");
                Check(after.ProjectileSpeed > before.ProjectileSpeed && after.Combat.BlastRadius == before.Combat.BlastRadius + 1, "Projectile speed and area modifiers execute");
                var plan = NightPlanOps.State(em, root);
                plan.PreparedTurn = 0;
                em.SetComponentData(root, plan);
                NightPlanOps.Prepare(em, root);
                s.Phase = Phase.Deployment;
                em.SetComponentData(root, s);
                var bytes = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
                Check(after.Equals(SoldierCombatStats.ForNight(em, root, soldier)), "All military modifier fields reconstruct from dusk");
                grants = em.GetBuffer<OwnedBuff>(root);
                for (int i = grants.Length - 1; i >= 0; i--)
                    if (grants[i].Buff == at)
                        grants.RemoveAt(i);
                Check(SoldierCombatStats.Current(em, root, soldier).Combat.Armor < after.Combat.Armor && SoldierCombatStats.ForNight(em, root, soldier).Equals(after), "Night source loss cannot change frozen stats");
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
