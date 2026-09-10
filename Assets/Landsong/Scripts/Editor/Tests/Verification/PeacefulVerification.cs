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
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class PeacefulVerification
    {
        static StringBuilder log; static int count;
        static void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException("FAIL " + name); count++; log.AppendLine("PASS " + name); }
        static void Reject(Action action, string name) { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } catch (InvalidDataException) { rejected = true; } Check(rejected, name); }
        [MenuItem("Landsong/ECS/Verification/Peaceful")]
        public static string Run()
        {
            log = new StringBuilder(); count = 0;
            try { Configuration(); Simulation(); log.AppendLine("Assertions: " + count); return log.ToString(); }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/peaceful-verification.txt", log.ToString()); }
        }
        static void Configuration()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            PeacefulContentValidation.Validate(catalog); Check(catalog.Find("night.fairy") >= 0, "Separate fairy registered in formal catalog");
            var copy = UnityEngine.Object.Instantiate(catalog); copy.Definitions = catalog.Definitions.Select(UnityEngine.Object.Instantiate).ToArray();
            try
            {
                var original = copy.Peaceful; var p = original; p.Interval = float.NaN; copy.Peaceful = p; Reject(() => PeacefulContentValidation.Validate(copy), "Nonfinite scheduling rejected"); copy.Peaceful = original;
                var visitor = copy.Definitions[copy.Find("night.visitor")].Data; var v = visitor.Opportunity; v.EndFraction = .9f; visitor.Opportunity = v; Reject(() => PeacefulContentValidation.Validate(copy), "Second half opportunity rejected"); visitor.Opportunity = OpportunityProfile.Default;
                var item = copy.Content.First(d => d.Kind == ContentKind.Item); var t = item.Theft; t.UnitValue = 0; item.Theft = t; Reject(() => PeacefulContentValidation.Validate(copy), "Free theft budget bypass rejected");
            }
            finally { foreach (var d in copy.Definitions) UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(copy); }
        }
        static BattleReportEntry[] Report(EntityManager em, Entity root) { using var rows = em.GetBuffer<BattleReportEntry>(root).ToNativeArray(Allocator.Temp); return rows.ToArray(); }
        static void Simulation()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");
            using var blobs = new BlobAssetStore(128); using var world = new World("Wave thirteen isolated", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var session = em.GetComponentData<Session>(root); session.CheckpointPending = 0; em.SetComponentData(root, session); var original = SnapshotCodec.Capture(em, root);
                int thief = Sim.FindDefinition(em, root, "night.visitor"), fairy = Sim.FindDefinition(em, root, "night.fairy"), soldier = Sim.FirstDefinition(em, root, ContentKind.Soldier), gold = em.GetComponentData<GameSettings>(root).Gold;
                Check(InventoryOps.Add(em, root, gold, 100) == 100, "Owned isolated fixture supplies stock through inventory API");
                Entity core = Entity.Null; using (var sites = Sim.OrderedEntities<Building>(em)) foreach (var site in sites) if (PeacefulOps.Items(em, root, em.GetComponentData<Identity>(site).Id).Count > 0) { core = site; break; }
                Check(core != Entity.Null, "Stocked storage source exists"); ulong provider = em.GetComponentData<Identity>(core).Id;
                Check(NavigationOps.TryNearestOpen(em, root, Sim.Position(em, core), 16, out var position), "Legal responder deployment");
                Entity Unit(bool hero = false)
                {
                    int definition = hero ? Sim.FirstDefinition(em, root, ContentKind.Hero) : soldier;
                    var e = Sim.Spawn(em, root, definition, position, false); MilitaryOps.ConfigureCombatant(em, root, e, 0, hero, true, provider, position);
                    if (hero) Sim.Set(em, e, new Hero { Recruited = 1, Sanctum = provider }); else Sim.Set(em, e, new Soldier()); return e;
                }
                var unit = Unit(); var heroUnit = Unit(true);
                session.Phase = Phase.Night; session.NightKind = NightKind.Peaceful; session.PhaseTime = 3; session.Time = 3; session.NightDuration = 15; session.SelectedHero = heroUnit; em.SetComponentData(root, session); NightResultOps.Reset(em, root);
                var scheduler = em.GetComponentData<PeacefulState>(root); scheduler.Next = 99; em.SetComponentData(root, scheduler);
                int stock = InventoryOps.Count(em, root, gold);
                Entity Spawn(int definition = -1) => PeacefulOps.TrySpawn(em, root, definition < 0 ? thief : definition, provider, 13579);
                log.AppendLine($"Fixture source items={PeacefulOps.Items(em, root, provider).Count}, operational={Sim.Operational(em, core)}, response={PeacefulOps.Eligible(em, unit, Sim.Definition(em, root, thief).Opportunity)}, route={PeacefulOps.Route(em, root, position, 8).Count}, position={position}, core={em.GetComponentData<Building>(core).Cell}");
                var visitor = Spawn(); Check(visitor != Entity.Null, "Valid thief spawns by stocked building");
                Check(InventoryOps.Count(em, root, gold) == stock, "Spawn neither reserves nor removes cargo");
                Check(Spawn() == Entity.Null, "At most one live visitor per source");
                var o = em.GetComponentData<Opportunity>(visitor); Check(o.Expires - session.Time >= 4, "Minimum response duration");
                var grid = em.GetComponentData<GridData>(root); var route = em.GetBuffer<VisitorPathPoint>(visitor);
                Check(route.Length > 1, "Nontrivial escape path"); foreach (var step in route) Check(GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, step.Position)), "Every escape step is legally walkable");
                Check(NightOps.PickUp(em, root, visitor) == ResultCode.Success && em.Exists(visitor), "Click assigns interception, does not instantly remove visitor");
                Check(em.GetComponentData<Opportunity>(visitor).Responder == heroUnit, "Selected eligible hero takes priority");
                Check(em.GetComponentData<UnitOrder>(heroUnit).Kind == OrderKind.Capture, "Explicit non-damaging ECS order");
                Check(NightOps.PickUp(em, root, visitor) == ResultCode.Busy, "Duplicate clicks do not assign duplicate responders");
                em.SetComponentData(heroUnit, LocalTransform.FromPosition(Sim.Position(em, visitor))); PeacefulOps.Tick(em, root, 0);
                Check(!em.Exists(visitor) && Report(em, root).Any(e => e.Kind == EventKind.TheftPrevented), "Proximity catches and reports prevention");
                Check(InventoryOps.Count(em, root, gold) == stock && em.GetComponentData<Combatant>(heroUnit).Participated == 0 && MilitaryOps.HeroBattleExperience(em, root, heroUnit) == 0, "Capture causes no damage, expense or combat XP");
                visitor = Spawn(fairy); Check(visitor != Entity.Null, "Fairy uses separate definition"); PeacefulOps.Finish(em, root, visitor, true);
                Check(InventoryOps.Count(em, root, gold) == stock && em.GetBuffer<NightReward>(root).Length == 1, "Fairy gift is journaled, not immediately deposited");
                visitor = Spawn(); Check(visitor != Entity.Null, "Same building supports later opportunity"); PeacefulOps.Finish(em, root, visitor, false);
                var theft = Report(em, root).Where(e => e.Kind == EventKind.Theft).ToArray(); Check(theft.Length == 1 && !theft[0].SourceName.IsEmpty && theft[0].Amount <= 5, "Escape only removes capped item and retains source name");
                using (var messages = em.GetBuffer<GameEvent>(root).ToNativeArray(Allocator.Temp)) Check(!messages.Any(e => e.Kind == EventKind.Theft), "Live event notifications do not disclose stolen items");
                int value = em.GetComponentData<PeacefulState>(root).StolenValue; Check(value > 0 && value <= PeacefulOps.Rules(em, root).TheftValueBudget, "Per-night stolen value budget consumed");
                var seeded = em.GetComponentData<PeacefulState>(root); uint global = em.GetComponentData<Session>(root).RandomState;
                for (int i = 0; i < 8; i++) { visitor = Spawn(); if (visitor != Entity.Null) PeacefulOps.Finish(em, root, visitor, false); }
                Check(em.GetComponentData<PeacefulState>(root).StolenValue <= PeacefulOps.Rules(em, root).TheftValueBudget && em.GetComponentData<Session>(root).RandomState == global, "Repeated escape respects budget without global RNG mutation");
                using var stockCopy = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp); var slots = em.GetBuffer<InventorySlot>(root); for (int i = 0; i < slots.Length; i++) { var slot = slots[i]; slot.Count = 0; slots[i] = slot; }
                Check(Spawn() == Entity.Null, "Empty source cannot spawn thief"); visitor = Spawn(fairy); Check(visitor != Entity.Null, "Fairy needs no inventory"); PeacefulOps.Finish(em, root, visitor, false);
                slots = em.GetBuffer<InventorySlot>(root); slots.CopyFrom(stockCopy);
                session = em.GetComponentData<Session>(root); session.Paused = 1; em.SetComponentData(root, session); var before = em.GetComponentData<PeacefulState>(root); PeacefulOps.Tick(em, root, 9); Check(em.GetComponentData<PeacefulState>(root).Equals(before) && Spawn() == Entity.Null, "Pause freezes scheduler and denies spawn");
                session.Paused = 0; session.IntelligenceMode = 1; em.SetComponentData(root, session); visitor = Spawn(); Check(visitor != Entity.Null && GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.PickUp, Target = em.GetComponentData<Identity>(visitor).Id }) == ResultCode.Busy, "Intel viewing does not suppress world but blocks capture input"); PeacefulOps.Finish(em, root, visitor, false, true);
                session.IntelligenceMode = 0; session.NightKind = NightKind.Invasion; em.SetComponentData(root, session); Check(Spawn() == Entity.Null, "Peaceful and battle mutually exclusive");
                Boundaries(em, root, provider, unit, heroUnit, thief, fairy);
                LootAndReport(em, root, original, position);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void Boundaries(EntityManager em, Entity root, ulong provider, Entity unit, Entity hero, int thief, int fairy)
        {
            var s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; s.NightKind = NightKind.Peaceful; s.PhaseTime = 3; s.Time = 3; s.SelectedHero = Entity.Null; em.SetComponentData(root, s);
            var e = PeacefulOps.TrySpawn(em, root, fairy, provider, 13579); Check(e != Entity.Null && PeacefulOps.Claim(em, root, e) == ResultCode.Success, "No selected hero falls back to qualified automatic unit"); PeacefulOps.Finish(em, root, e, false, true);
            s.PhaseTime = 8; em.SetComponentData(root, s); Check(PeacefulOps.TrySpawn(em, root, fairy, provider, 42) == Entity.Null, "No visitors after first half"); s.PhaseTime = 3; em.SetComponentData(root, s);
            var actor = em.GetComponentData<Combatant>(unit); var heroActor = em.GetComponentData<Combatant>(hero); var inactive = actor; inactive.Deployed = 0; em.SetComponentData(unit, inactive); inactive = heroActor; inactive.Deployed = 0; em.SetComponentData(hero, inactive);
            Check(PeacefulOps.TrySpawn(em, root, fairy, provider, 42) == Entity.Null, "No actionable friendly unit cancels opportunity before spawn");
            em.SetComponentData(unit, actor); em.SetComponentData(hero, heroActor);
            var bell = Sim.Find(em, provider); var originalStats = em.GetComponentData<BuildingStats>(bell); var bellStats = originalStats; bellStats.BellRadius = 30; em.SetComponentData(bell, bellStats);
            e = PeacefulOps.TrySpawn(em, root, fairy, provider, 13579); Check(e != Entity.Null, "Bell visitor fixture"); MilitaryOps.Bell(em, root, bell);
            Check(em.GetComponentData<Opportunity>(e).Responder != Entity.Null, "Bell dispatches a nearby eligible responder");
            var responder = em.GetComponentData<Opportunity>(e).Responder; MilitaryOps.Bell(em, root, bell); PeacefulOps.Tick(em, root, 0);
            Check(em.Exists(e) && em.GetComponentData<Opportunity>(e).Responder == Entity.Null && em.GetComponentData<UnitOrder>(responder).Kind != OrderKind.Capture, "Second bell click cancels capture assignment without deleting visitor");
            PeacefulOps.Finish(em, root, e, false, true); em.SetComponentData(bell, originalStats);
            var recalled = em.GetComponentData<Soldier>(unit); recalled.RecallState = 1; em.SetComponentData(unit, recalled); Check(!PeacefulOps.Eligible(em, unit, Sim.Definition(em, root, fairy).Opportunity), "Recalling soldiers cannot accept capture jobs"); recalled.RecallState = 0; em.SetComponentData(unit, recalled);
            e = PeacefulOps.TrySpawn(em, root, fairy, provider, 42); Check(e != Entity.Null, "Cancellation path fixture");
            var grid = em.GetComponentData<GridData>(root); var next = em.GetBuffer<VisitorPathPoint>(e)[0].Position; int at = GridOps.Index(grid, GridOps.Cell(grid, next)); var occupied = em.GetBuffer<Occupancy>(root); var old = occupied[at]; occupied[at] = new Occupancy { Owner = ulong.MaxValue, MovementCost = 0 };
            PeacefulOps.Tick(em, root, .1f); Check(!em.Exists(e) && Report(em, root).Any(r => r.Kind == EventKind.VisitorCancelled), "Newly blocked escape route cancels without theft"); occupied = em.GetBuffer<Occupancy>(root); occupied[at] = old;
            var source = Sim.Find(em, provider); var identity = em.GetComponentData<Identity>(source); var named = identity; named.Name = "国库"; em.SetComponentData(source, named);
            e = PeacefulOps.TrySpawn(em, root, thief, provider, 42); Check(e != Entity.Null, "Named source fixture"); em.SetComponentData(source, identity); PeacefulOps.Finish(em, root, e, false);
            Check(Report(em, root).Any(r => r.Kind == EventKind.VisitorEscaped && r.SourceName.ToString() == "国库"), "Source rename after spawn cannot rewrite captured report name");
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset"); var copy = UnityEngine.Object.Instantiate(catalog); copy.Definitions = catalog.Definitions.Select(UnityEngine.Object.Instantiate).ToArray(); var original = em.GetComponentData<ContentCatalog>(root);
            try
            {
                using var inventory = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp); var slot = inventory.First(x => x.Provider == provider && x.Count > 0); string itemId = Sim.Definition(em, root, slot.Item).Id.ToString();
                foreach (var protection in new[] { ItemProtection.Quest, ItemProtection.Unique, ItemProtection.Bound })
                {
                    var item = copy.Definitions[copy.Find(itemId)].Data; var t = item.Theft; t.Protection = protection; item.Theft = t;
                    using var blob = GameWorldAuthoring.BuildCatalog(copy); em.SetComponentData(root, new ContentCatalog { Value = blob }); Check(!PeacefulOps.Stealable(em, root, slot), protection + " resource excluded"); em.SetComponentData(root, original);
                }
            }
            finally { em.SetComponentData(root, original); foreach (var d in copy.Definitions) UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(copy); }
            string Schedule()
            {
                NightResultOps.Reset(em, root); em.GetBuffer<BattleReportEntry>(root).Clear(); var signature = new StringBuilder();
                foreach (float time in new[] { 2.5f, 5, 7.5f, 10, 14 })
                {
                    s.PhaseTime = time; s.Time = time; em.SetComponentData(root, s); PeacefulOps.Tick(em, root, 0);
                    using var visitors = Sim.OrderedEntities<Opportunity>(em); foreach (var v in visitors) { signature.Append(em.GetComponentData<Identity>(v).Definition).Append(':').Append(em.GetComponentData<Opportunity>(v).Provider).Append(';'); PeacefulOps.Finish(em, root, v, false, true); }
                }
                var state = em.GetComponentData<PeacefulState>(root); Check(state.Spawned <= PeacefulOps.Rules(em, root).MaximumPerNight, "Scheduler respects total budget");
                foreach (var c in em.GetBuffer<OpportunityCount>(root)) Check(c.Count <= Sim.Definition(em, root, c.Definition).Opportunity.MaximumPerNight, "Per-kind repeat cap enforced");
                return signature.ToString();
            }
            string first = Schedule(), again = Schedule(); Check(first == again && first.Length > 0, "Local seed replays same definitions and equal-weight source picks");
            Check(em.GetComponentData<PeacefulState>(root).Spawned > 1, "Multiple opportunities per quiet night remain possible");
            em.GetBuffer<BattleReportEntry>(root).Clear(); s.Phase = Phase.Retreat; em.SetComponentData(root, s); Check(NightReportOps.Prepare(em, root) == NightEndPose.Guard, "Timeout cleanup uses guard pose");
            s.Phase = Phase.Night; em.SetComponentData(root, s); em.GetBuffer<BattleReportEntry>(root).Clear(); Check(NightReportOps.Prepare(em, root) == NightEndPose.Celebrate, "Clean end uses cheering pose");
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.ResidentsLost, Definition = -1, Amount = 2, SourceName = "受损民居" }); Check(NightReportOps.Prepare(em, root) == NightEndPose.Aid, "Civilian loss overrides celebratory mood");
        }
        static void LootAndReport(EntityManager em, Entity root, byte[] original, float3 position)
        {
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            var s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; s.NightKind = NightKind.Invasion; s.CheckpointPending = 0; em.SetComponentData(root, s); NightResultOps.Reset(em, root);
            int raider = Sim.FindDefinition(em, root, "raider"), gold = em.GetComponentData<GameSettings>(root).Gold;
            int stock = InventoryOps.Count(em, root, gold);
            var enemy = Sim.Spawn(em, root, raider, position, false); MilitaryOps.ConfigureCombatant(em, root, enemy, 1, false, true, 0, position); CombatOps.Death(em, root, enemy, Entity.Null);
            using (var drops = Sim.Entities<Loot>(em)) Check(drops.Length == 0 && em.GetBuffer<NightReward>(root).Length > 0, "Ordinary kill rewards directly enter journal, no clickable drops");
            int countBefore = em.GetBuffer<NightReward>(root).Length; CombatOps.Death(em, root, enemy, Entity.Null); Check(em.GetBuffer<NightReward>(root).Length == countBefore && InventoryOps.Count(em, root, gold) == stock, "Repeated death does not duplicate gains or deposit early");
            int boss = -1; var blob = em.GetComponentData<ContentCatalog>(root).Value; for (int i = 0; i < blob.Value.Definitions.Length; i++) if (blob.Value.Definitions[i].Kind == ContentKind.Enemy && (blob.Value.Definitions[i].Flags & 1) != 0) boss = i;
            var e = Sim.Spawn(em, root, boss, position, false); MilitaryOps.ConfigureCombatant(em, root, e, 1, false, true, 0, position); CombatOps.Death(em, root, e, Entity.Null);
            Entity drop = Entity.Null; using (var drops = Sim.Entities<Loot>(em)) { Check(drops.Length > 0, "Boss special rule creates clickable guaranteed reward"); drop = drops[0]; }
            var dropValue = em.GetComponentData<Loot>(drop); Check(dropValue.Rarity == 3 && !dropValue.SourceName.IsEmpty, "Special rarity and stable source snapshot");
            Check(GridOps.Traversable(em.GetComponentData<GridData>(root), em.GetBuffer<Occupancy>(root), GridOps.Cell(em.GetComponentData<GridData>(root), Sim.Position(em, drop))), "Special drop lands on legal surface");
            NightOps.PickUp(em, root, drop); countBefore = em.GetBuffer<NightReward>(root).Length; Check(NightOps.PickUp(em, root, drop) == ResultCode.InvalidTarget && em.GetBuffer<NightReward>(root).Length == countBefore, "Drop identity prevents double claims");
            var more = Sim.Spawn(em, root, Sim.FirstDefinition(em, root, ContentKind.Loot), position, false); Sim.Set(em, more, new Loot { Item = gold, Count = 100000, Rarity = 2 }); Sim.Set(em, more, new VisualState { Visible = 1 });
            s = em.GetComponentData<Session>(root); s.Phase = Phase.Celebration; s.PhaseTime = 0; em.SetComponentData(root, s); NightOps.Tick(em, root, 9.9f); Check(em.Exists(more), "Unclicked loot does not expire in ending"); NightOps.Tick(em, root, .2f); Check(!em.Exists(more) && em.GetComponentData<Session>(root).Phase == Phase.Report, "Fixed ten seconds auto-collects all and waits for report confirmation");
            int historyBefore = em.HasBuffer<BattleHistoryEntry>(root) ? em.GetBuffer<BattleHistoryEntry>(root).Length : 0;
            InventoryOps.Add(em, root, gold, 100000000);
            NightOps.Dawn(em, root); Check(em.GetBuffer<PendingItem>(root).Length > 0 && Report(em, root).Any(r => r.Kind == EventKind.RewardOverflow), "Full warehouse moves rewards to pending without missing-loot penalty");
            int count = em.GetBuffer<BattleHistoryEntry>(root).Length; Check(count > historyBefore, "Committed complete report archived"); NightOps.Dawn(em, root); Check(em.GetBuffer<BattleHistoryEntry>(root).Length == count, "Duplicate dawn never duplicates history");
            var saved = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved)); Check(em.GetBuffer<BattleHistoryEntry>(root).Length == count, "Committed report survives save and restore");
            var seed = em.GetComponentData<PeacefulState>(root); seed.Random = 4444; seed.StolenValue = 7; em.SetComponentData(root, seed); em.GetBuffer<OpportunityCount>(root).Add(new OpportunityCount { Definition = Sim.FirstDefinition(em, root, ContentKind.Opportunity), Count = 2 });
            foreach (string point in new[] { "root-reset", "root-published" })
            {
                Reject(() => SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original), probe: stage => { if (stage == point) throw new InvalidOperationException("Owned wave13 injection"); }), "Restore failure rollback at " + point);
                Check(em.GetBuffer<BattleHistoryEntry>(root).Length == count && em.GetComponentData<PeacefulState>(root).Equals(seed), "History and local RNG rollback at " + point);
                Check(em.GetBuffer<OpportunityCount>(root).Length == 1 && em.GetBuffer<OpportunityCount>(root)[0].Count == 2, "Per-kind opportunity counts rollback at " + point);
            }
            var bad = SnapshotCodec.Decode(em, root, saved); if (bad.BattleHistory.Length > 0) { bad.BattleHistory[0].Entry.Value = float.NaN; Reject(() => SnapshotCodec.Restore(em, root, bad), "Nonfinite report history rejected before publication"); }
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); Check(em.GetBuffer<NightReward>(root).Length == 0 && em.GetBuffer<BattleHistoryEntry>(root).Length == historyBefore, "Node replay removes uncommitted rewards and future report history");
        }
    }
}
#endif
