#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
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
        static StringBuilder log; static int count;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); count++; log.AppendLine("PASS " + label); }
        [MenuItem("Landsong/ECS/Verification/Combat")]
        public static string Run()
        {
            log = new StringBuilder(); count = 0;
            try { Configuration(); WorldTest(); log.AppendLine("Assertions: " + count); return log.ToString(); }
            catch (Exception e) { log.AppendLine(e.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/combat-verification.txt", log.ToString()); }
        }
        static void Configuration()
        {
            var p = CombatProfile.Default; Check(CombatProfile.Valid(p), "Usable editable defaults");
            p.ChaseSeconds = 0; Check(!CombatProfile.Valid(p), "Unbounded or zero pursuit rejected");
            p = CombatProfile.Default; p.Armor = float.NaN; Check(!CombatProfile.Valid(p), "Nonfinite armor rejected");
            p = CombatProfile.Default; p.WarningSeconds = p.ProjectileLifetime; Check(!CombatProfile.Valid(p), "Warning cannot outlive projectile");
            p = CombatProfile.Default; p.Reduction = 1; Check(!CombatProfile.Valid(p), "Permanent full mitigation rejected");
            Check(ProjectileOps.Mitigate(20, 8, 3, .25f) == 11.25f, "Flat armor minus penetration then damage reduction");
            Check(ProjectileOps.Mitigate(3, 10, 0, 0) == 0, "Armor cannot heal target");
        }
        static NightPreparation[] Prepared(EntityManager em, Entity root)
        { var b = em.GetBuffer<NightPreparation>(root); var result = new NightPreparation[b.Length]; for (int i = 0; i < result.Length; i++) result[i] = b[i]; return result; }
        static void WorldTest()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity"); using var store = new BlobAssetStore(128); using var world = new World("11C combat", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var s = em.GetComponentData<Session>(root); s.CheckpointPending = 0; em.SetComponentData(root, s); var original = SnapshotCodec.Capture(em, root);
                var grid = em.GetComponentData<GridData>(root); var occupancy = em.GetBuffer<Occupancy>(root); int2 center = default; bool found = false;
                for (int y = grid.Value.Value.Min.y + 12; y < grid.Value.Value.Min.y + grid.Value.Value.Size.y - 12 && !found; y++) for (int x = grid.Value.Value.Min.x + 12; x < grid.Value.Value.Min.x + grid.Value.Value.Size.x - 12; x++)
                { bool open = true; for (int dy = -7; dy <= 7 && open; dy++) for (int dx = -7; dx <= 7; dx++) if (!GridOps.Traversable(grid, occupancy, new int2(x + dx, y + dy))) { open = false; break; } if (open) { center = new int2(x, y); found = true; break; } }
                Check(found, "Open combat arena inside map"); var origin = GridOps.Position(grid, center, new int2(1)) + new float3(0, .5f, 0);
                int soldier = Sim.FirstDefinition(em, root, ContentKind.Soldier), enemy = Sim.FindDefinition(em, root, "raider"), shotDefinition = Sim.FirstDefinition(em, root, ContentKind.Projectile);
                Entity Unit(byte faction, float3 position)
                { var e = Sim.Spawn(em, root, faction == 0 ? soldier : enemy, position, false); MilitaryOps.ConfigureCombatant(em, root, e, faction, false, true, 0, origin); return e; }
                s.Phase = Phase.Night; s.NightKind = NightKind.Invasion; s.Time = 1; em.SetComponentData(root, s);
                var a = Unit(0, origin); var b = Unit(1, origin + new float3(1, 0, 0));
                float Life(Entity e) => em.GetComponentData<Health>(e).Current;
                Entity Shot(Entity source, Entity target, ProjectileMode mode, float radius = 0, float warning = 0)
                {
                    var unit = em.GetComponentData<Combatant>(source); var e = Sim.Spawn(em, root, shotDefinition, Sim.Position(em, source) + new float3(0, .3f, 0), false);
                    Sim.Set(em, e, new Projectile { Source = source, Target = target, SourceId = em.GetComponentData<Identity>(source).Id, TargetId = em.GetComponentData<Identity>(target).Id, Faction = unit.Faction, Damage = 10, Speed = 100, Lifetime = 10, Radius = radius, Warning = warning, Landing = Sim.Position(em, target) + new float3(0, .3f, 0), Mode = mode }); return e;
                }
                void Tick(float delta)
                { var session = em.GetComponentData<Session>(root); session.Time += delta; em.SetComponentData(root, session); world.SetTime(new TimeData(session.Time, delta)); world.GetOrCreateSystem<ProjectileSystem>().Update(world.Unmanaged); world.GetOrCreateSystem<DamageSystem>().Update(world.Unmanaged); }
                float hp = Life(b); Shot(a, b, ProjectileMode.Tracking); Tick(.1f); Check(Life(b) == hp - 10, "Tracking projectile actual systems resolve one hit"); Tick(.1f); Check(Life(b) == hp - 10, "Consumed projectile cannot double hit");
                float own = Life(a); Shot(a, a, ProjectileMode.Tracking); Tick(.1f); Check(Life(a) == own, "Projectile payload refuses friendly fire");
                var d = em.GetComponentData<Combatant>(b); d.Profile.Armor = 8; d.Profile.Reduction = .5f; em.SetComponentData(b, d); hp = Life(b);
                var shot = Shot(a, b, ProjectileMode.Tracking); var payload = em.GetComponentData<Projectile>(shot); payload.Penetration = 4; em.SetComponentData(shot, payload); Tick(.1f); Check(Life(b) == hp - 3, "Projectile penetration and frozen defender profile applied");
                d.Profile.Armor = 0; d.Profile.Reduction = 0; em.SetComponentData(b, d);
                shot = Shot(a, b, ProjectileMode.Tracking); var sourceActor = em.GetComponentData<Combatant>(a); sourceActor.Damage = 999; sourceActor.Profile.Penetration = 999; em.SetComponentData(a, sourceActor); hp = Life(b); Tick(.1f); Check(Life(b) == hp - 10, "Already launched payload ignores later attacker changes");
                shot = Shot(a, b, ProjectileMode.Tracking); em.SetComponentData(b, new Health { Maximum = 100, Current = 0 }); Tick(.1f); Check(!em.Exists(shot), "Tracking target death invalidates projectile"); em.SetComponentData(b, new Health { Maximum = 100, Current = 100 });
                var c = Unit(1, origin + new float3(1, 0, 1)); hp = Life(b); float otherHp = Life(c); own = Life(a);
                shot = Shot(a, b, ProjectileMode.Ground, 2, .5f); Tick(.1f); Check(em.Exists(shot) && Life(b) == hp, "Ground warning delays damage");
                s = em.GetComponentData<Session>(root); s.Paused = 1; em.SetComponentData(root, s); float remaining = em.GetComponentData<Projectile>(shot).Warning; Tick(1); Check(em.GetComponentData<Projectile>(shot).Warning == remaining, "Pause freezes ground warning"); s.Paused = 0; em.SetComponentData(root, s); Tick(.5f); Tick(.1f);
                Check(Life(b) == hp - 10 && Life(c) == otherHp - 10 && Life(a) == own, "Area hits each hostile once without friendly fire");
                shot = Shot(a, b, ProjectileMode.Ground, 2); var landing = em.GetComponentData<Projectile>(shot).Landing; em.SetComponentData(b, LocalTransform.FromPosition(origin + new float3(6, 0, 0))); hp = Life(b); Tick(.1f); Check(Life(b) == hp && !em.Exists(shot), "Ground landing stays fixed when target moves");
                using (var blockOwner = ProjectileOps.Blocks(em, root, Allocator.Temp))
                {
                    var blocks = blockOwner;
                    var to = origin + new float3(5, 0, 0); int at = GridOps.Index(grid, center + new int2(2, 0)); blocks[at] = new ProjectileBlock { Environment = 1 };
                    Check(ProjectileOps.Blocked(grid, blocks, origin, to, 0, 0), "Swept segment catches one-cell environmental blocker");
                    blocks[at] = new ProjectileBlock { Owner = 777 }; Check(ProjectileOps.Blocked(grid, blocks, origin, to, 0, 0), "Blocking building intercepts other targets"); Check(!ProjectileOps.Blocked(grid, blocks, origin, to, 0, 777), "Intended building target is hittable through own footprint");
                    blocks[at] = default; Check(!ProjectileOps.Blocked(grid, blocks, origin, to, 0, 0), "Ruined removed blocker no longer intercepts");
                }
                em.SetComponentData(b, LocalTransform.FromPosition(origin + new float3(2, 0, 0))); em.SetComponentData(a, new Perception { Enemy = b, EnemyPosition = Sim.Position(em, b) });
                TacticalOps.Update(em, root); var tactical = em.GetComponentData<TacticalState>(a); Check(tactical.Pursued == b && tactical.HasSlot != 0, "Automatic pursuit reserves an engagement position");
                var ally = Unit(0, origin); em.SetComponentData(ally, new Perception { Enemy = b, EnemyPosition = Sim.Position(em, b) }); TacticalOps.Update(em, root);
                Check(math.distance(em.GetComponentData<TacticalState>(a).Engagement, em.GetComponentData<TacticalState>(ally).Engagement) >= .69f, "Two attackers reserve separate body-width slots");
                s = em.GetComponentData<Session>(root); s.Time += 20; em.SetComponentData(root, s); em.SetComponentData(a, LocalTransform.FromPosition(origin + new float3(4, 0, 0))); TacticalOps.Update(em, root);
                Check(em.GetComponentData<TacticalState>(a).Returning != 0 && em.GetComponentData<Perception>(a).Enemy == Entity.Null, "Timed-out pursuit relinquishes enemy and returns");
                em.SetComponentData(a, new UnitOrder { Kind = OrderKind.Move, Destination = origin + new float3(3, 0, 0) }); TacticalOps.Update(em, root); Check(em.GetComponentData<TacticalState>(a).Returning == 0, "Player position command overrides automatic return");
                em.SetComponentData(a, LocalTransform.FromPosition(origin)); em.SetComponentData(ally, LocalTransform.FromPosition(origin)); em.SetComponentData(a, new Steering()); em.SetComponentData(ally, new Steering());
                world.SetTime(new TimeData(s.Time, .1f)); world.GetOrCreateSystem<NavigationSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                Check(math.distance(Sim.Position(em, a), Sim.Position(em, ally)) > .01f, "Actual navigation job separates overlapping bodies");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                NightPlanOps.Prepare(em, root); var prepared = Prepared(em, root); Check(prepared.Any(p => Sim.Definition(em, root, p.Definition).Kind == ContentKind.Building), "Building defense participates in dusk snapshot");
                var expected = prepared.First(p => p.Definition == soldier); Check(CombatProfile.Valid(expected.Combat) && expected.Interval > 0 && expected.ProjectileSpeed > 0, "Prepared unit carries complete combat profile");
                s = em.GetComponentData<Session>(root); s.Phase = Phase.Deployment; em.SetComponentData(root, s); var saved = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved));
                Check(expected.Equals(Prepared(em, root).First(p => p.Definition == soldier)), "Expanded prepared payload survives dusk reconstruction");
                var malformed = SnapshotCodec.Decode(em, root, saved); malformed.Preparation[0].Combat.Reduction = float.NaN; bool rejected = false; try { SnapshotCodec.Restore(em, root, malformed); } catch (InvalidDataException) { rejected = true; } Check(rejected && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Malformed defense rejected before mutation");
                foreach (var fail in new[] { "garrisons-prepared", "root-published", "before-retire" })
                { bool threw = false; try { SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved), probe: at => { if (at == fail) throw new InvalidOperationException("probe"); }); } catch (InvalidOperationException) { threw = true; } Check(threw && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Expanded preparation rollback " + fail); }
                Modifiers(em, root, soldier);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void Modifiers(EntityManager em, Entity root, int soldier)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset"); var catalog = UnityEngine.Object.Instantiate(source);
            int at = Array.FindIndex(source.Definitions, d => d.Data.Kind == ContentKind.Buff); var buff = UnityEngine.Object.Instantiate(source.Definitions[at]); catalog.Definitions = (GameDefinitionAsset[])source.Definitions.Clone(); catalog.Definitions[at] = buff;
            var kinds = new[] { RuleKind.RangeBonus, RuleKind.AttackSpeedBonus, RuleKind.SpeedBonus, RuleKind.ArmorBonus, RuleKind.DamageReductionBonus, RuleKind.PenetrationBonus, RuleKind.ProjectileSpeedBonus, RuleKind.BlastRadiusBonus };
            buff.Data.Rules = kinds.Select(kind => new RuleSource { Kind = kind, Target = source.Definitions[soldier].Data.Id, Value = kind == RuleKind.ArmorBonus ? 4 : kind == RuleKind.PenetrationBonus ? 3 : kind == RuleKind.BlastRadiusBonus ? 1 : .2f }).ToArray();
            var original = em.GetComponentData<ContentCatalog>(root);
            try
            {
                using var blob = GameWorldAuthoring.BuildCatalog(catalog); em.SetComponentData(root, new ContentCatalog { Value = blob });
                var grants = em.GetBuffer<Entitlement>(root); for (int i = grants.Length - 1; i >= 0; i--) if (grants[i].Definition == at) grants.RemoveAt(i);
                var s = em.GetComponentData<Session>(root); s.Phase = Phase.Day; em.SetComponentData(root, s);
                var before = MilitaryOps.CurrentStats(em, root, soldier, false); Sim.Grant(em, root, at); var after = MilitaryOps.CurrentStats(em, root, soldier, false);
                Check(after.Range > before.Range && after.Interval < before.Interval && after.Speed > before.Speed, "Range attack-speed movement modifiers execute");
                Check(after.Combat.Armor == before.Combat.Armor + 4 && after.Combat.Reduction > before.Combat.Reduction && after.Combat.Penetration == before.Combat.Penetration + 3, "Defense reduction penetration modifiers execute");
                Check(after.ProjectileSpeed > before.ProjectileSpeed && after.Combat.BlastRadius == before.Combat.BlastRadius + 1, "Projectile speed and area modifiers execute");
                var plan = NightPlanOps.State(em, root); plan.PreparedTurn = 0; em.SetComponentData(root, plan); NightPlanOps.Prepare(em, root); s.Phase = Phase.Deployment; em.SetComponentData(root, s);
                var bytes = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
                Check(after.Equals(MilitaryOps.Stats(em, root, soldier, false)), "All military modifier fields reconstruct from dusk");
                grants = em.GetBuffer<Entitlement>(root); for (int i = grants.Length - 1; i >= 0; i--) if (grants[i].Definition == at) grants.RemoveAt(i);
                Check(MilitaryOps.CurrentStats(em, root, soldier, false).Combat.Armor < after.Combat.Armor && MilitaryOps.Stats(em, root, soldier, false).Equals(after), "Night source loss cannot change frozen stats");
            }
            finally { em.SetComponentData(root, original); UnityEngine.Object.DestroyImmediate(buff); UnityEngine.Object.DestroyImmediate(catalog); }
        }
    }
}
#endif
