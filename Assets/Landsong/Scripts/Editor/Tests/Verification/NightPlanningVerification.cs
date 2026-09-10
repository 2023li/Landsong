#if UNITY_EDITOR
using System;
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
        static StringBuilder log; static int checks;
        static T[] Buffer<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData { using var values = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); return values.ToArray(); }
        public static void CopyNight(BlobBuilder builder, ref ContentBlob target, BlobAssetReference<ContentBlob> source)
        {
            if (!source.IsCreated) return;
            target.Night = source.Value.Night;
            var events = builder.Allocate(ref target.NightEvents, source.Value.NightEvents.Length); for (int i = 0; i < events.Length; i++) events[i] = source.Value.NightEvents[i];
            var enemies = builder.Allocate(ref target.NightEnemies, source.Value.NightEnemies.Length); for (int i = 0; i < enemies.Length; i++) enemies[i] = source.Value.NightEnemies[i];
            var rules = builder.Allocate(ref target.NightConditions, source.Value.NightConditions.Length); for (int i = 0; i < rules.Length; i++) rules[i] = source.Value.NightConditions[i];
        }
        static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("FAIL " + name); checks++; log.AppendLine("PASS " + name); }
        static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (Exception e) when (e is InvalidOperationException || e is InvalidDataException) { rejected = true; } Check(rejected, name); }
        [MenuItem("Landsong/ECS/Verification/NightPlanning")]
        public static string Run()
        {
            log = new StringBuilder(); checks = 0;
            try { Configuration(); Map(); log.AppendLine("Assertions: " + checks); return log.ToString(); }
            catch (Exception e) { log.AppendLine(e.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/night-planning-verification.txt", log.ToString()); }
        }
        static void Configuration()
        {
            var c = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset"); NightContentValidation.Validate(c);
            Check(c.NightEvents.Length >= 4, "Authored peaceful raid boss and return events"); Check(c.Find("invader") >= 0, "Distinct core assault enemy placeholder"); Check(c.Settings.RetreatSeconds == 5, "Five second retreat default");
            var copy = UnityEngine.Object.Instantiate(c);
            try
            {
                copy.NightEvents[0].Weight = float.NaN; Reject(() => NightContentValidation.Validate(copy), "NaN event weight rejected"); copy.NightEvents[0].Weight = 1;
                copy.NightEvents[1].Id = copy.NightEvents[0].Id; Reject(() => NightContentValidation.Validate(copy), "Duplicate event key rejected"); copy.NightEvents[1].Id = "night.raid";
                copy.NightEvents[1].WaveTimes = new[] { 0f, .5f, 1f }; Reject(() => NightContentValidation.Validate(copy), "Wave scheduled after retreat boundary rejected"); copy.NightEvents[1].WaveTimes = Array.Empty<float>();
                copy.NightEvents[2].FollowUp = "unknown"; Reject(() => NightContentValidation.Validate(copy), "Unresolved aftermath rejected");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static void Map()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity"); using var store = new BlobAssetStore(128); using var world = new World("Wave ten isolated night verification", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                int Def(string id) => Sim.FindDefinition(em, root, new FixedString128Bytes(id));
                var original = SnapshotCodec.Capture(em, root); var settings = em.GetComponentData<GameSettings>(root);
                void Reset() { SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); em.SetComponentData(root, settings); }
                void PhaseTo(Phase phase) { var s = em.GetComponentData<Session>(root); s.Phase = phase; s.PhaseTime = 0; s.CheckpointPending = 0; em.SetComponentData(root, s); }
                void Force(int turn, bool boss = false)
                {
                    var s = em.GetComponentData<Session>(root); s.Turn = turn; s.Phase = Phase.Day; s.CheckpointPending = 0; em.SetComponentData(root, s);
                    var cfg = settings; cfg.FirstInvasion = 1; cfg.InvasionChance = 1; cfg.FirstBoss = boss ? turn : 99999; em.SetComponentData(root, cfg);
                    Sim.Set(em, root, new NightPlanState { BossDefinition = -1 }); NightOps.Plan(em, root, false);
                }
                Entity Core() { using var sites = Sim.OrderedEntities<Building>(em); foreach (var e in sites) if (em.GetComponentData<BuildingStats>(e).IsCore != 0) return e; throw new InvalidOperationException(); }
                Entity Garrison()
                {
                    var blob = em.GetComponentData<ContentCatalog>(root).Value; int definition = -1;
                    for (int i = 0; i < blob.Value.Definitions.Length; i++) if (blob.Value.Definitions[i].Kind == ContentKind.Building && Sim.Rule(em, root, i, RuleKind.Garrison).Level >= 0 && Sim.Rule(em, root, i, RuleKind.Population).B == 0) { definition = i; break; }
                    var grid = em.GetComponentData<GridData>(root);
                    for (int y = 15; y < grid.Value.Value.Size.y - 15; y++) for (int x = 15; x < grid.Value.Value.Size.x - 15; x++)
                        if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0)) return BuildingOps.Create(em, root, definition, new int2(x, y), 0, 1, true);
                    throw new InvalidOperationException("No test garrison footprint");
                }
                Entity Soldier(Entity home)
                { var e = Sim.Spawn(em, root, Sim.FirstDefinition(em, root, ContentKind.Soldier), Sim.Position(em, home), true); MilitaryOps.ConfigureCombatant(em, root, e, 0, false, false, Id(home), Sim.Position(em, home)); Sim.Set(em, e, new Soldier { Garrison = Id(home), Slot = MilitaryOps.FreeSlot(em, home), PopulationCost = 1 }); return e; }
                Force(3); var baseline = SnapshotCodec.Capture(em, root); var locked = em.GetComponentData<Session>(root);
                NightOps.Plan(em, root, false); Check(baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Same-turn plan call is entirely idempotent including RNG");
                var barracks = Garrison(); var soldier = Soldier(barracks); var power = MilitaryOps.Strength(em, root); var unit = em.GetComponentData<Soldier>(soldier); unit.Garrison = 0; em.SetComponentData(soldier, unit);
                Check(MilitaryOps.Strength(em, root) < power, "Unassigned troops excluded from effective combat strength"); unit.Garrison = Id(barracks); em.SetComponentData(soldier, unit);
                NightOps.Plan(em, root, false); Check(em.GetComponentData<Session>(root).StartCombatStrength == locked.StartCombatStrength && em.GetComponentData<Session>(root).Threat == locked.Threat, "Day recruitment cannot raise locked invasion budget");
                var waves = em.GetBuffer<NightWave>(root); float sum = 0;
                foreach (var w in waves) { Check(w.At >= 0 && w.At <= 1 && w.Count > 0, "Wave uses normalized time and positive count"); sum += w.Count * Sim.Definition(em, root, w.Definition).Value * w.PowerScale; }
                Check(math.abs(sum - locked.Threat) < .01f, "Composition normalized to exact threat budget");
                var gridData = em.GetComponentData<GridData>(root);
                foreach (var region in em.GetBuffer<SpawnRegion>(root))
                { Check(GridOps.Index(gridData, GridOps.Cell(gridData, region.Center)) >= 0, "TWC outside marker baked to edge inside actual grid"); Check(NightSpatialOps.Reserved(em, root, GridOps.Cell(gridData, region.Center)), "Entry strip and buffer excluded from placement"); }
                int legalRegion = -1; float3 point = default;
                for (int i = 0; i < em.GetBuffer<SpawnRegion>(root).Length; i++) if (NightSpatialOps.SpawnPoint(em, root, i, em.GetBuffer<SpawnRegion>(root)[i].Center, out point)) { legalRegion = i; break; }
                Check(legalRegion >= 0, "Map Test2 has a legal safe entry"); Check(NightSpatialOps.Inside(em.GetBuffer<SpawnRegion>(root)[legalRegion], point), "Resolved spawn point inside shared intelligence region");
                Check(!GridOps.CanPlace(em, root, em.GetComponentData<Identity>(barracks).Definition, GridOps.Cell(gridData, em.GetBuffer<SpawnRegion>(root)[legalRegion].Center), 0), "Building command placement cannot occupy invasion entry");
                var assault = NightSpatialOps.Target(em, root, Def("invader"), point, point, 0, false); Check(assault == Core(), "Core assault chooses reachable core");
                var repeat = NightSpatialOps.Target(em, root, Def("raider"), point, point, 0, false); Check(repeat != Entity.Null && repeat == NightSpatialOps.Target(em, root, Def("raider"), point, point, 0, false), "Harassment target randomization deterministic without consuming day RNG");
                PhaseTo(Phase.Night); var oldPosition = Sim.Position(em, barracks); BuildingOps.Ruin(em, root, barracks);
                var fallback = NightSpatialOps.Target(em, root, Def("raider"), point, oldPosition, Id(barracks), true); Check(fallback != Entity.Null && fallback != barracks && NightSpatialOps.ValidTarget(em, fallback), "Ruined original target replaced by reachable nearby site or core");
                // Restore this fixture's garrison before testing blocked deployment.
                var repaired = em.GetComponentData<Building>(barracks); repaired.Stage = LifeStage.Operational; repaired.RuinPending = 0; em.SetComponentData(barracks, repaired); GridOps.Occupy(em, root, barracks);
                using (var occupied = em.GetBuffer<Occupancy>(root).ToNativeArray(Allocator.Temp))
                {
                    var slots = em.GetBuffer<Occupancy>(root); for (int i = 0; i < slots.Length; i++) slots[i] = new Occupancy { Owner = 999999 };
                    Check(!NightSpatialOps.SpawnPoint(em, root, legalRegion, point, out _), "Blocked invasion region reports failure, no teleport");
                    Check(NightSpatialOps.Target(em, root, Def("invader"), point, point, 0, false) == Entity.Null, "Unreachable targets are rejected instead of attack-through-walls");
                    Check(!NavigationOps.TryNearestOpen(em, root, Sim.Position(em, barracks), 12, out _), "Blocked garrison exit reports failure");
                    PhaseTo(Phase.Night); MilitaryOps.PrepareNight(em, root); NightOps.Tick(em, root, .1f);
                    Check(em.GetComponentData<Combatant>(soldier).Deployed == 0 && em.GetComponentData<Combatant>(soldier).DeployAt == float.MaxValue, "Failed deployment stays home and does not retry spam");
                    slots = em.GetBuffer<Occupancy>(root); slots.CopyFrom(occupied);
                }
                Reset(); barracks = Garrison(); soldier = Soldier(barracks); var sid = Id(soldier); Soldier(barracks); Soldier(barracks); Soldier(barracks);
                NightPlanOps.Prepare(em, root); PhaseTo(Phase.Settlement); MilitaryOps.PrepareNight(em, root);
                using (var all = Sim.OrderedEntities<Soldier>(em)) { var first = em.GetComponentData<Combatant>(all[0]).DeployAt; var last = em.GetComponentData<Combatant>(all[all.Length - 1]).DeployAt; Check(last > first, "All soldiers receive deterministic garrison batches"); }
                var prep = Buffer<NightPreparation>(em, root); var snapshot = SnapshotCodec.Capture(em, root); // Settlement cannot be loaded; capture next at legal dusk.
                PhaseTo(Phase.Deployment); snapshot = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, snapshot)); soldier = Sim.Find(em, sid);
                Check(prep.SequenceEqual(Buffer<NightPreparation>(em, root)), "Dusk rebuild retains exact prepared modifiers");
                Check(em.GetComponentData<Combatant>(soldier).Damage == prep.First(p => p.Definition == em.GetComponentData<Identity>(soldier).Definition).Damage, "Rebuilt unit uses prepared attack");
                var modifierState = CourtOps.State(em, root); modifierState.TemporaryAttack = .5f; modifierState.TemporaryUntil = 10; Sim.Set(em, root, modifierState);
                var troopDef = em.GetComponentData<Identity>(soldier).Definition; var frozenDamage = em.GetComponentData<Combatant>(soldier).Damage;
                Check(MilitaryOps.CurrentStats(em, root, troopDef, false).Damage > frozenDamage, "Fixture changes live court modifier after preparation");
                MilitaryOps.ConfigureCombatant(em, root, soldier, 0, false, false, em.GetComponentData<Soldier>(soldier).Garrison, Sim.Position(em, soldier));
                Check(em.GetComponentData<Combatant>(soldier).Damage == frozenDamage, "Night reconfiguration cannot replace frozen combat modifiers");
                foreach (var fail in new[] { "root-reset", "record-created", "garrisons-prepared", "root-published", "before-retire" })
                {
                    var before = SnapshotCodec.Capture(em, root); bool threw = false;
                    try { SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, before), probe: stage => { if (stage == fail) throw new InvalidOperationException("injected"); }); } catch (InvalidOperationException) { threw = true; }
                    Check(threw && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Night state rolls back restore failure at " + fail);
                }
                Reset(); PhaseTo(Phase.Night); var peaceful = em.GetComponentData<Session>(root); peaceful.NightKind = NightKind.Peaceful; peaceful.NightDuration = 15; em.SetComponentData(root, peaceful); em.GetBuffer<NightWave>(root).Clear();
                NightOps.Tick(em, root, 9.9f); Check(em.GetComponentData<Session>(root).Turn == 1, "Peaceful patrol cannot end before ten seconds"); NightOps.Tick(em, root, 5.1f);
                Check(em.GetComponentData<Session>(root).Turn == 2 && em.GetComponentData<Session>(root).Phase == Phase.Day, "Peaceful total fifteen seconds without extra celebration");
                Reset(); PhaseTo(Phase.Night); Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.NightSpeed, Amount = 2 }) == ResultCode.Success, "Peaceful 2x accepted");
                NightOps.Tick(em, root, 1); Check(em.GetComponentData<Session>(root).PhaseTime == 2, "Peaceful speed scales simulated time");
                GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Pause }); var paused = em.GetComponentData<Session>(root).Time; NightOps.Tick(em, root, 10);
                Check(em.GetComponentData<Session>(root).Time == paused, "Pause freezes night time"); Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Bell, Target = Id(Core()) }) == ResultCode.Busy, "Pause rejects tactical commands at authority boundary");
                GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Pause });
                Reset(); Force(3); PhaseTo(Phase.Night); Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.NightSpeed, Amount = 2 }) == ResultCode.WrongPhase, "Battle fixed at 1x");
                NightOps.Tick(em, root, 1); Check(NightPlanOps.State(em, root).ClockStarted == 0, "Deployment lead excluded from battle clock");
                NightOps.Tick(em, root, 1); NightOps.Tick(em, root, 1);
                Check(NightPlanOps.State(em, root).AnySpawned == 1 && NightPlanOps.State(em, root).ClockStarted == 0, "First wave protection precedes battle clock");
                Entity enemy = Entity.Null; using (var all = Sim.Entities<Combatant>(em)) foreach (var e in all) if (em.GetComponentData<Combatant>(e).Faction == 1 && Sim.Alive(em, e)) { enemy = e; break; }
                Check(enemy != Entity.Null, "Actual lawful enemy instantiated"); var hp = em.GetComponentData<Health>(enemy).Current;
                CombatOps.ApplyDamage(em, root, new DamageRequest { Target = enemy, Amount = 1000 }); Check(em.GetComponentData<Health>(enemy).Current == hp, "Entry protection blocks incoming damage");
                NightOps.Tick(em, root, 1); Check(NightPlanOps.State(em, root).ClockStarted == 1, "First enemy can act before clock starts");
                var actor = em.GetComponentData<Combatant>(enemy); Check(NightSpatialOps.Inside(em.GetBuffer<SpawnRegion>(root)[em.GetBuffer<NightWave>(root)[0].Region], Sim.Position(em, enemy)), "Actual spawn did not drift outside intelligence region");
                var plan = NightPlanOps.State(em, root); plan.CombatElapsed = em.GetComponentData<Session>(root).NightDuration; Sim.Set(em, root, plan); NightOps.Tick(em, root, .1f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Retreat && NightOps.Progress(em, root) == 1, "Moon limit starts retreat, not victory/failure"); NightOps.Tick(em, root, 5);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Celebration, "Retreat bounded to five seconds"); NightOps.Tick(em, root, 9.9f); Check(em.GetComponentData<Session>(root).Phase == Phase.Celebration, "Combat celebration lasts full ten seconds"); NightOps.Tick(em, root, .1f);
                Check(em.GetComponentData<Session>(root).Phase == Phase.Report && em.GetComponentData<Session>(root).Turn == 3, "Report waits for user before dawn"); Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.WakeHero }) == ResultCode.WrongPhase, "Cannot wake hero after combat");
                Reset(); Force(20, true); var bossDefinition = NightPlanOps.State(em, root).BossDefinition; plan = NightPlanOps.State(em, root); plan.AnySpawned = 1; plan.BossEscaped = 1; Sim.Set(em, root, plan);
                Check(plan.BaseThreat <= NightPlanOps.Rules(em, root).ThreatFloor + em.GetComponentData<Session>(root).StartCombatStrength * NightPlanOps.Rules(em, root).ThreatPerStrengthCap, "Late turns obey surviving-force difficulty ceiling");
                NightPlanOps.Commit(em, root); Check(em.GetBuffer<UnresolvedBoss>(root).Length == 1 && em.GetBuffer<UnresolvedBoss>(root)[0].DueTurn == 25, "Escaped boss creates delayed same-identity aftermath"); NightPlanOps.Commit(em, root); Check(em.GetBuffer<UnresolvedBoss>(root).Length == 1 && em.GetBuffer<NightEventHistory>(root)[0].Count == 1, "Dawn event commit idempotent");
                Force(25); Check(NightPlanOps.State(em, root).Event.ToString() == "night.boss.return" && NightPlanOps.State(em, root).BossDefinition == bossDefinition, "Pending boss takes priority even outside periodic boss night");
                NightPlanOps.BossDeath(em, root, bossDefinition); plan = NightPlanOps.State(em, root); plan.AnySpawned = 1; Sim.Set(em, root, plan); NightPlanOps.Commit(em, root); Check(em.GetBuffer<UnresolvedBoss>(root).Length == 0, "Boss death clears unresolved threat at dawn");
                var evt = em.GetComponentData<ContentCatalog>(root).Value.Value.NightEvents[NightPlanOps.Find(em, root, new FixedString64Bytes("night.boss.return"))];
                Check(!NightPlanOps.Eligible(em, root, evt, false), "Return-only event excluded from normal draw"); evt.Once = 1; Check(!NightPlanOps.Eligible(em, root, evt, true), "Once flag respects completed history");
                Reset(); Force(3); snapshot = SnapshotCodec.Capture(em, root); var invalid = SnapshotCodec.Decode(em, root, snapshot); invalid.NightPlan.BaseThreat = -1;
                Reject(() => SnapshotCodec.Restore(em, root, invalid), "Invalid night plan rejected before destructive restore"); Check(snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected night snapshot leaves world intact");
                var archive = world.GetOrCreateSystemManaged<CheckpointSystem>(); archive.Update(); Check(NightOps.Begin(em, root) == ResultCode.Success, "Night entry transaction succeeds with new root buffers"); archive.Update();
                PhaseTo(Phase.Night); CombatOps.ApplyDamage(em, root, new DamageRequest { Target = Core(), Amount = 9999999 }); archive.Update(); var recovery = em.GetComponentData<RecoveryState>(root); var lostArchive = archive.Export(root); archive.Retry(root, false);
                var retry = em.GetComponentData<Session>(root); Check(retry.Threat < locked.Threat && retry.StartCombatStrength == locked.StartCombatStrength, "Core retry lowers only locked budget, not recomputed day strength");
                var composition = Buffer<NightWave>(em, root); var baseThreat = NightPlanOps.State(em, root).BaseThreat;
                archive.Import(root, lostArchive, false); archive.Retry(root, true); Check(composition.SequenceEqual(Buffer<NightWave>(em, root)) && baseThreat == NightPlanOps.State(em, root).BaseThreat, "Same recovery ticket yields identical day and dusk plan");
                Check(em.GetComponentData<RecoveryState>(root).Seed == recovery.Seed, "Choosing nodes does not create another seed");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
