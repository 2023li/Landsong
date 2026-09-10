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
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class InvitationExpeditionVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); checks++; log.AppendLine("PASS " + label); }
        public static void FixturePermissions(EntityManager em, Entity root)
        {
            foreach (var name in new[] { "Building", "Inventory", "Expedition" }) { var d = Sim.FindDefinition(em, root, new FixedString128Bytes("feature." + name)); if (d >= 0) Sim.Grant(em, root, d); }
        }
        [MenuItem("Landsong/ECS/Verification/InvitationExpedition")]
        public static string Run()
        {
            checks = 0; log = new StringBuilder();
            try
            {
                Configuration();
                // Map_Test01 is an intentionally incomplete test map, not an onboarding acceptance target.
                Map("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception e) { log.AppendLine(e.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/invitations-expeditions-verification.txt", log.ToString()); }
        }
        static void Configuration()
        {
            var c = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            InvitationExpeditionValidation.Validate(c); using var blob = GameWorldAuthoring.BuildCatalog(c);
            Check(blob.Value.Quests.MarketValuePerStrength == 100 && blob.Value.Quests.StrengthStep == 10, "Legacy source conversion preserved");
            Check(blob.Value.Expeditions.PenaltyTurns == 5 && blob.Value.Expeditions.AttractionPerStack == 5, "Legacy five-turn / five-attraction subsidy penalty");
            foreach (var entry in new[] { ("random_supply_stone", 1, 2f, 10, 16, 8), ("random_supply_soil", 2, 3f, 30, 45, 15), ("random_supply_wood", 0, 1f, 10, 15, 3) })
            {
                var d = blob.Value.Definitions[c.Find(entry.Item1)]; var rules = Enumerable.Range(d.RuleStart, d.RuleCount).Select(i => blob.Value.Rules[i]).ToArray();
                Check(d.QuestIntensity == entry.Item2 && d.ItemQuantityScale == entry.Item3 && d.QuestWeight == 100, "Authored intensity/weight/scale " + entry.Item1);
                Check(rules.Single(r => r.Kind == RuleKind.SubmitItem).Amount == entry.Item4 && rules.Single(r => r.Kind == RuleKind.RewardItem).Amount == entry.Item5 && rules.Single(r => r.Kind == RuleKind.FailureItem).Amount == entry.Item6, "Requirements/rewards/penalties scaled once " + entry.Item1);
            }
            var low = blob.Value.Definitions[c.Find("random_supply_wood")]; var high = blob.Value.Definitions[c.Find("random_supply_soil")];
            Check(QuestOfferOps.Weight(blob.Value.Quests, low, 0) > QuestOfferOps.Weight(blob.Value.Quests, high, 0), "Weak source favours low intensity");
            Check(QuestOfferOps.Weight(blob.Value.Quests, low, 30) < QuestOfferOps.Weight(blob.Value.Quests, high, 30), "Strong source favours higher intensity without modifying amounts");
            foreach (var entry in new[] { ("expedition.nearby_recon", 1, 3, .4f, .03f, .2f, 5, 1, 2), ("expedition.abandoned_fort", 2, 5, .25f, .035f, .35f, 10, 2, 3) })
            {
                var d = c.Content[c.Find(entry.Item1)];
                Check(d.Level == entry.Item2 && d.Duration == entry.Item3 && d.Population == 10 && d.Capacity == 15 && d.Flags == 1, "Legacy destination level/duration/crew/repeat " + d.Id);
                Check(d.Chance == entry.Item4 && d.Interval == entry.Item5 && d.Range == .9f && d.Loss == entry.Item6 && d.Cost == entry.Item7 && d.Value == entry.Item8, "Legacy destination probability/casualties/pension " + d.Id);
                Check(d.Rules.Count(r => r.Kind == RuleKind.RewardItem) == entry.Item9 && !d.Rules.Any(r => r.Kind == RuleKind.Supply), "Original reward count and intentionally empty supplies " + d.Id);
            }
            var clone = ScriptableObject.CreateInstance<GameCatalogAsset>(); clone.Definitions = c.Definitions.Select(UnityEngine.Object.Instantiate).ToArray();
            try
            {
                var q = clone.Definitions[clone.Find("random_supply_soil")].Data;
                void Invalid(Action change, string name) { change(); var rejected = false; try { InvitationExpeditionValidation.Validate(clone); } catch (InvalidOperationException) { rejected = true; } Check(rejected, name); }
                Invalid(() => q.ItemQuantityScale = float.NaN, "NaN task scaling rejected"); q.ItemQuantityScale = 3;
                Invalid(() => q.QuestWeight = -1, "Negative weight rejected"); q.QuestWeight = 100;
                Invalid(() => q.QuestIntensity = 4, "Unknown intensity rejected"); q.QuestIntensity = 2;
                var destination = clone.Definitions[clone.Find("expedition.nearby_recon")].Data;
                destination.Rules = destination.Rules.Concat(new[] { new RuleSource { Kind = RuleKind.Supply, Target = "原木", Amount = 10, Value = .01f, Extra = .02f } }).ToArray();
                InvitationExpeditionValidation.Validate(clone); Check(true, "Optional authored supplies accepted");
                Invalid(() => destination.Rules.Last().B = 6, "Extra supply cannot exceed 50 percent minimum");
            }
            finally { foreach (var d in clone.Definitions) UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(clone); }
        }
        static void Map(string path)
        {
            log.AppendLine("MAP " + path); var scene = EditorSceneManager.OpenPreviewScene(path); using var store = new BlobAssetStore(128); using var world = new World("Wave eight map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                int Def(string id) => Sim.FindDefinition(em, root, new FixedString128Bytes(id));
                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                void Turn(int turn) { var s = em.GetComponentData<Session>(root); s.Turn = turn; s.Phase = Phase.Day; s.CheckpointPending = 0; em.SetComponentData(root, s); }
                void Stock(int item, int amount) { InventoryOps.Remove(em, root, item, InventoryOps.Count(em, root, item)); Check(InventoryOps.Add(em, root, item, amount) == amount, "Fixture stock " + amount); }
                Entity NewBuilding(string name, int level = 1)
                {
                    var def = Def(name); var grid = em.GetComponentData<GridData>(root);
                    for (var i = 0; i < grid.Value.Value.Cells.Length; i++) { var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x); if (GridOps.CanPlace(em, root, def, cell, 0)) return BuildingOps.Create(em, root, def, cell, 0, level, true); }
                    throw new InvalidOperationException("No fixture location");
                }
                Entity Quest(string name) { using var all = Sim.OrderedEntities<Quest>(em); foreach (var e in all) if (em.GetComponentData<Identity>(e).Definition == Def(name)) return e; return Entity.Null; }
                var original = SnapshotCodec.Capture(em, root);
                foreach (var feature in new[] { "Building", "Inventory", "Expedition" }) Check(!FeatureOps.Unlocked(em, root, feature), "Formal new dynasty does not grant " + feature);
                foreach (var kind in new[] { CommandKind.Build, CommandKind.BuildRoad, CommandKind.MoveInventory, CommandKind.SortInventory, CommandKind.StorePending, CommandKind.Discard, CommandKind.StartExpedition, CommandKind.ClaimExpedition, CommandKind.AbandonExpedition })
                    Check(GameLoopSystem.Execute(em, root, new Command { Kind = kind }) == ResultCode.Unavailable && original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Locked command is mutation free " + kind);
                GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.CameraMoved }); GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.CameraZoomed });
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.ClaimQuest, Target = Id(Quest("main_camera_survey")) }) == ResultCode.Success && FeatureOps.Unlocked(em, root, "Inventory") && !FeatureOps.Unlocked(em, root, "Building"), "First actual mainline unlocks only inventory");
                foreach (var source in new[] { "b树木1", "b小土堆", "b小石堆" })
                {
                    using var buildings = Sim.OrderedEntities<Building>(em); var pile = Entity.Null;
                    foreach (var candidate in buildings) if (em.GetComponentData<Identity>(candidate).Definition == Def(source)) { pile = candidate; break; }
                    Check(pile != Entity.Null, "Authored starting map supplies tutorial harvest source " + source);
                    Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Harvest, Target = Id(pile) }) == ResultCode.Success && !FeatureOps.Unlocked(em, root, "Building"), "Initial harvest works before building permission " + source);
                }
                foreach (var item in new[] { "原木", "泥土", "石头" }) Check(InventoryOps.Count(em, root, Def(item)) >= 10, "Tutorial materials earned by harvesting, not fixture stock " + item);
                ProgressionOps.EvaluateQuests(em, root);
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.ClaimQuest, Target = Id(Quest("main_collect_building_materials")) }) == ResultCode.Success && FeatureOps.Unlocked(em, root, "Building") && !FeatureOps.Unlocked(em, root, "Expedition"), "Second actual mainline unlocks construction without early expedition grant");
                FixturePermissions(em, root); var clean = SnapshotCodec.Capture(em, root); var gold = Def("金币");
                var market = NewBuilding("b市场"); var marketId = Id(market); var b = em.GetComponentData<Building>(market); b.Workers = em.GetComponentData<BuildingStats>(market).JobCapacity; b.StableWorkers = b.Workers; em.SetComponentData(market, b);
                var before = SnapshotCodec.Capture(em, root); var quote = QuestOfferOps.Quote(em, root, market, 0);
                Check(quote.Code == ResultCode.Success && quote.Candidates.Count == 3 && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Source preview filters candidates and never consumes RNG");
                Stock(gold, 0); before = SnapshotCodec.Capture(em, root);
                Check(ProgressionOps.Offer(em, root, market, 0, true) == ResultCode.InsufficientResources && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Paid recruit failure preserves inventory RNG cooldown and IDs");
                Stock(gold, 30); Check(ProgressionOps.Offer(em, root, market, 0, true) == ResultCode.Success && InventoryOps.Count(em, root, gold) == 20, "Paid recruit consumes exact fee once");
                var offer = QuestOfferOps.Offered(em, marketId, 0); Check(offer != Entity.Null && em.GetComponentData<Quest>(offer).Status == QuestStatus.Offered, "Invitation is not automatically accepted");
                before = SnapshotCodec.Capture(em, root); Check(ProgressionOps.Offer(em, root, market, 0, true) == ResultCode.Busy && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Full slot cannot recruit or pay again");
                b = em.GetComponentData<Building>(market); b.Workers = 0; em.SetComponentData(market, b); ProgressionOps.EvaluateQuests(em, root);
                Check(em.Exists(offer) && !QuestOfferOps.Available(em, root, market, out _), "Workforce stop retains existing offer");
                var next = em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn; QuestOfferOps.Settle(em, root);
                Check(em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn == next + 1, "Unavailable source pauses countdown");
                Check(ProgressionOps.QuestCommand(em, root, new Command { Kind = CommandKind.AcceptQuest, Target = Id(offer) }) == ResultCode.Success, "Retained offer can be accepted while source paused");
                var acceptedId = Id(offer); BuildingOps.Demolish(em, root, market); Check(em.Exists(offer) && em.GetComponentData<Quest>(offer).Status == QuestStatus.Active, "Demolition preserves accepted task");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                market = NewBuilding("b市场"); b = em.GetComponentData<Building>(market); b.Workers = em.GetComponentData<BuildingStats>(market).JobCapacity; em.SetComponentData(market, b);
                ResourceNetworkOps.Record(em, root, market, Def("原木"), 100); var lifetime = em.GetComponentData<Building>(market).MarketLifetimeValue; ResourceNetworkOps.SettleMarkets(em, root);
                Check(lifetime > 0 && em.GetComponentData<Building>(market).MarketLifetimeValue == lifetime && em.GetComponentData<Building>(market).MarketValue == 0, "Lifetime source strength survives per-turn market clearing");
                QuestOfferOps.Settle(em, root); next = em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn; Check(next >= 31 && next <= 51, "Initial automatic cooldown uses authored random range");
                Turn(next - 1); QuestOfferOps.Settle(em, root); Check(QuestOfferOps.Offered(em, Id(market), 0) != Entity.Null, "Automatic cooldown fills one invitation");
                before = SnapshotCodec.Capture(em, root); var random = em.GetComponentData<Session>(root).RandomState; QuestOfferOps.Settle(em, root); Check(em.GetComponentData<Session>(root).RandomState == random, "Full source does not roll meaningless cooldowns");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, before)); Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "v8 roundtrip preserves source lifetime and cooldown");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                var post = NewBuilding("b陆上远征所", 2); var postId = Id(post); b = em.GetComponentData<Building>(post); b.Workers = b.StableWorkers = 15; em.SetComponentData(post, b); var session = em.GetComponentData<Session>(root); session.BasePopulation = 100; em.SetComponentData(root, session);
                var destination = Def("expedition.nearby_recon"); var departure = ExpeditionOps.Quote(em, root, post, destination, 15);
                Check(departure.Code == ResultCode.Success && departure.Minimum == 10 && departure.Maximum == 15 && math.abs(departure.SuccessChance - .85f) < .00001f && departure.RewardBonus == .25f, "Crew bounds, success and full-crew bonus preview");
                Check(ExpeditionOps.Quote(em, root, post, destination, 9).Code == ResultCode.InsufficientPopulation, "Understaffed departure refused");
                var command = new Command { Kind = CommandKind.StartExpedition, Target = postId, Definition = destination, Amount = 15, Text = ExpeditionOps.Payload(departure) };
                InventoryOps.Add(em, root, gold, 1); before = SnapshotCodec.Capture(em, root); Check(GameLoopSystem.Execute(em, root, command) == ResultCode.Unavailable && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Stale departure confirmation cannot spend");
                departure = ExpeditionOps.Quote(em, root, post, destination, 15); command.Text = ExpeditionOps.Payload(departure);
                Check(GameLoopSystem.Execute(em, root, command) == ResultCode.Success && EconomyOps.WorkforceLocked(em, postId), "Departure locks current building workforce");
                Check(!QuestOfferOps.Available(em, root, post, out _), "Travelling site cannot produce invitations");
                before = SnapshotCodec.Capture(em, root); Check(GameLoopSystem.Execute(em, root, command) == ResultCode.Busy && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Only one travelling party per site");
                Entity Journey() { using var all = Sim.OrderedEntities<Expedition>(em); return all[all.Length - 1]; }
                var journey = Journey(); var journeyId = Id(journey); var j = em.GetComponentData<Expedition>(journey);
                var grants = em.GetBuffer<Entitlement>(root); for (var i = grants.Length - 1; i >= 0; i--) if (grants[i].Definition == Def("feature.Expedition")) grants.RemoveAt(i);
                var oldArrival = j.Arrival; var frozenRandom = em.GetComponentData<Session>(root).RandomState; ExpeditionOps.Settle(em, root);
                Check(em.GetComponentData<Expedition>(journey).Arrival == oldArrival + 1 && em.GetComponentData<Session>(root).RandomState == frozenRandom, "Locked expedition pauses travel duration without rolling outcome");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.AbandonExpedition, Target = journeyId }) == ResultCode.Unavailable && EconomyOps.WorkforceLocked(em, postId), "Locked action cannot abandon frozen party");
                Sim.Grant(em, root, Def("feature.Expedition")); j = em.GetComponentData<Expedition>(journey); j.SuccessChance = 1; em.SetComponentData(journey, j); Turn(j.Arrival - 1);
                before = SnapshotCodec.Capture(em, root); Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Arrival forecast preserves departure, supplies and RNG");
                ExpeditionOps.Settle(em, root); Check(em.GetComponentData<Expedition>(journey).Status == ExpeditionStatus.Success && !EconomyOps.WorkforceLocked(em, postId), "Successful arrival frees workforce and awaits manual claim");
                var exp = em.GetComponentData<Building>(post).Experience; ExpeditionOps.Settle(em, root); Check(em.GetComponentData<Building>(post).Experience == exp, "Arrival experience cannot settle twice");
                var resultBytes = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, resultBytes)); Check(resultBytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "v8 departure/result snapshot roundtrip");
                post = Sim.Find(em, postId); journey = Sim.Find(em, journeyId); BuildingOps.Demolish(em, root, post); Check(em.Exists(journey), "Completed result survives source demolition");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.ClaimExpedition, Target = journeyId }) == ResultCode.Success && !em.Exists(journey), "Result claim works after source demolition");
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.ClaimExpedition, Target = journeyId }) == ResultCode.InvalidTarget, "Result cannot grant twice");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                post = NewBuilding("b陆上远征所", 2); postId = Id(post); b = em.GetComponentData<Building>(post); b.Workers = b.StableWorkers = 15; em.SetComponentData(post, b); session = em.GetComponentData<Session>(root); session.BasePopulation = 100; em.SetComponentData(root, session); Stock(gold, 3);
                departure = ExpeditionOps.Quote(em, root, post, destination, 10); Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.StartExpedition, Target = postId, Definition = destination, Amount = 10, Text = ExpeditionOps.Payload(departure) }) == ResultCode.Success, "Failure fixture departure");
                journey = Journey(); j = em.GetComponentData<Expedition>(journey); j.SuccessChance = 0; em.SetComponentData(journey, j); Turn(j.Arrival - 1); var population = Sim.Population(em, root);
                ExpeditionOps.Settle(em, root); j = em.GetComponentData<Expedition>(journey);
                Check(j.Status == ExpeditionStatus.Failure && j.Casualties == 2 && Sim.Population(em, root) == population - 2 && em.GetComponentData<Building>(post).Workers == 13, "Failure removes actual crew casualties and population");
                Check(j.SubsidyRequired == 15 && j.SubsidyPaid == 3 && InventoryOps.Count(em, root, gold) == 0 && j.PenaltyStacks == 2 && ExpeditionOps.Penalty(em, root) == 10, "Partial pension pays available gold and applies attraction stacks");
                before = SnapshotCodec.Capture(em, root); ExpeditionOps.Settle(em, root); Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Failure casualties/pension/penalty are one-shot");
                var invalid = SnapshotCodec.Decode(em, root, before); invalid.Records.Single(r => (r.Mask & 32) != 0).Expedition.SuccessChance = float.NaN;
                var rejected = false; try { SnapshotCodec.Restore(em, root, invalid); } catch (InvalidDataException) { rejected = true; } Check(rejected && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Invalid expedition snapshot rejected atomically");
                Turn(em.GetComponentData<Session>(root).ExpeditionPenaltyUntil + 1); Check(ExpeditionOps.Penalty(em, root) == 0, "Pension penalty expires by configured turn");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                post = NewBuilding("b陆上远征所", 2); postId = Id(post); b = em.GetComponentData<Building>(post); b.Workers = b.StableWorkers = 15; em.SetComponentData(post, b);
                departure = ExpeditionOps.Quote(em, root, post, destination, 10); Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.StartExpedition, Target = postId, Definition = destination, Amount = 10, Text = ExpeditionOps.Payload(departure) }) == ResultCode.Success, "Ruin fixture departs");
                population = Sim.Population(em, root); BuildingOps.Ruin(em, root, post);
                using (var all = Sim.OrderedEntities<Expedition>(em)) Check(all.Length == 0 && !EconomyOps.WorkforceLocked(em, postId) && Sim.Population(em, root) == population, "Ruined source withdraws travel immediately without extra casualties");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                SupplyAndCapacity(em, root, NewBuilding, Def, Turn, Stock);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void SupplyAndCapacity(EntityManager em, Entity root, Func<string,int,Entity> building, Func<string,int> def, Action<int> turn, Action<int,int> stock)
        {
            var originalCatalog = em.GetComponentData<ContentCatalog>(root); var c = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset")); c.Definitions = c.Definitions.Select(UnityEngine.Object.Instantiate).ToArray();
            try
            {
                var destination = c.Definitions[c.Find("expedition.nearby_recon")].Data;
                destination.Flags = 0;
                destination.Rules = destination.Rules.Concat(new[] { new RuleSource { Kind = RuleKind.Supply, Target = "原木", Amount = 10, Value = .01f, Extra = .02f } }).ToArray();
                var marketData = c.Definitions[c.Find("b市场")].Data; marketData.Rules.Single(r => r.Kind == RuleKind.QuestSource).Amount = 2;
                marketData.Level = 2;
                marketData.Rules = marketData.Rules.Concat(new[] { new RuleSource { Kind = RuleKind.QuestSource, Level = 2, Amount = 1, B = 0, C = 30, Value = 50 }, new RuleSource { Kind = RuleKind.QuestCapacity, Amount = 4 }, new RuleSource { Kind = RuleKind.Maintenance, Target = "金币", Amount = 1000000 } }).ToArray();
                marketData.Rules.First(r => r.Kind == RuleKind.QuestSource).Level = 1;
                using var blob = GameWorldAuthoring.BuildCatalog(c); em.SetComponentData(root, new ContentCatalog { Value = blob });
                // This isolated fixture adds rules to earlier definitions; rebind existing stable keys to its new blob offsets.
                using (var tasks = Sim.OrderedEntities<Quest>(em)) foreach (var e in tasks)
                {
                    var d = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition); var progress = em.GetBuffer<QuestProgress>(e);
                    for (var n = 0; n < progress.Length; n++) { var p = progress[n]; for (var k = 0; k < d.RuleCount; k++) { var rule = Sim.GetRule(em, root, d.RuleStart + k); if (QuestOps.Requirement(rule.Kind) && QuestOps.Key(rule, k) == p.Key) p.RuleIndex = d.RuleStart + k; } progress[n] = p; }
                }
                var post = building("b陆上远征所", 2); var b = em.GetComponentData<Building>(post); b.Workers = b.StableWorkers = 15; em.SetComponentData(post, b); stock(def("原木"), 15);
                var q = ExpeditionOps.Quote(em, root, post, def("expedition.nearby_recon"), 15, new[] { 15 });
                Check(q.Code == ResultCode.Success && math.abs(q.SuccessChance - .9f) < .0001 && math.abs(q.RewardBonus - .35f) < .0001 && q.Rewards.Single(r => r.Item == def("原木")).Amount == 16, "Extra supply succeeds with capped chance and frozen reward bonus");
                Check(ExpeditionOps.Quote(em, root, post, def("expedition.nearby_recon"), 15, new[] { 16 }).Code == ResultCode.InvalidContent, "Extra supply limit enforced in domain");
                var command = new Command { Kind = CommandKind.StartExpedition, Target = em.GetComponentData<Identity>(post).Id, Definition = def("expedition.nearby_recon"), Amount = 15, Text = ExpeditionOps.Payload(q) };
                Check(GameLoopSystem.Execute(em, root, command) == ResultCode.Success && InventoryOps.Count(em, root, def("原木")) == 0, "Supply deducted atomically at departure");
                using var journeys = Sim.OrderedEntities<Expedition>(em); var journey = journeys[0]; Check(em.GetBuffer<ExpeditionSupply>(journey)[0].Amount == 15, "Assigned supplies stored on persistent expedition");
                var bytes = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes)); Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Supply departure persists through rebuild");
                using (var all = Sim.OrderedEntities<Expedition>(em)) foreach (var e in all) Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.AbandonExpedition, Target = em.GetComponentData<Identity>(e).Id }) == ResultCode.Success, "Explicit abandon clears travelling party");
                Check(InventoryOps.Count(em, root, def("原木")) == 0, "Abandon does not refund supplies");
                stock(def("原木"), 15); post = Sim.Find(em, command.Target); q = ExpeditionOps.Quote(em, root, post, command.Definition, 15, new[] { 15 }); command.Text = ExpeditionOps.Payload(q);
                Check(GameLoopSystem.Execute(em, root, command) == ResultCode.Success, "Abandoned nonrepeatable destination can still be retried");
                using (var all = Sim.OrderedEntities<Expedition>(em)) foreach (var e in all) { var state = em.GetComponentData<Expedition>(e); state.SuccessChance = 1; em.SetComponentData(e, state); turn(state.Arrival - 1); }
                ExpeditionOps.Settle(em, root);
                Check(ExpeditionOps.CompletedAt(em, post, command.Definition) && ExpeditionOps.Quote(em, root, post, command.Definition, 15).Code == ResultCode.Unavailable, "Successful nonrepeatable destination locks this source before reward claim");
                bytes = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes)); post = Sim.Find(em, command.Target);
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)) && ExpeditionOps.CompletedAt(em, post, command.Definition), "Per-site nonrepeatable history persists");
                using (var all = Sim.OrderedEntities<Expedition>(em)) foreach (var e in all) Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.ClaimExpedition, Target = em.GetComponentData<Identity>(e).Id }) == ResultCode.Success, "Claim removes result without removing nonrepeatable history");
                Check(ExpeditionOps.Quote(em, root, post, command.Definition, 15).Code == ResultCode.Unavailable, "Claim does not reopen completed nonrepeatable source");
                var otherPost = building("b陆上远征所", 1); var otherState = em.GetComponentData<Building>(otherPost); otherState.Workers = otherState.StableWorkers = 15; em.SetComponentData(otherPost, otherState); stock(def("原木"), 15);
                Check(ExpeditionOps.Quote(em, root, otherPost, command.Definition, 10).Code == ResultCode.Success, "Other source can complete the same nonrepeatable destination as in legacy rules");
                var market = building("b市场", 1); b = em.GetComponentData<Building>(market); b.Workers = em.GetComponentData<BuildingStats>(market).JobCapacity; em.SetComponentData(market, b); QuestOfferOps.Settle(em, root);
                var next = em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn; turn(next - 1); QuestOfferOps.Settle(em, root);
                var id = em.GetComponentData<Identity>(market).Id;
                Check(QuestOfferOps.Offered(em, id, 0) != Entity.Null && QuestOfferOps.Offered(em, id, 1) == Entity.Null, "Multi-slot source fills only one vacancy per cooldown");
                var random = em.GetComponentData<Session>(root).RandomState; QuestOfferOps.Settle(em, root); Check(em.GetComponentData<Session>(root).RandomState == random, "Other vacancies wait for the restarted source cooldown");
                stock(def("金币"), 30); Check(ProgressionOps.Offer(em, root, market, 1, true) == ResultCode.Success, "Second stable slot can be filled by paid recruit");
                var retained = QuestOfferOps.Offered(em, id, 0); var removed = QuestOfferOps.Offered(em, id, 1);
                Check(ProgressionOps.QuestCommand(em, root, new Command { Kind = CommandKind.AcceptQuest, Target = em.GetComponentData<Identity>(retained).Id }) == ResultCode.Success, "Accept first slot before source capacity shrink");
                b = em.GetComponentData<Building>(market); b.Level = 2; em.SetComponentData(market, b); BuildingOps.ApplyLevel(em, root, market, false);
                Check(em.Exists(retained) && !em.Exists(removed) && em.GetBuffer<QuestOfferSlot>(market).Length == 2, "Capacity shrink removes only excess offered tasks and retains stable slot identities");
                b = em.GetComponentData<Building>(market); b.Level = 1; b.Maintained = 1; em.SetComponentData(market, b); BuildingOps.ApplyLevel(em, root, market, false);
                using (var all = Sim.OrderedEntities<Quest>(em)) foreach (var e in all) if (em.GetComponentData<Quest>(e).Mainline == 0) em.DestroyEntity(e); QuestOps.RefreshTracking(em, root);
                b=em.GetComponentData<Building>(market);b.Workers=b.StableWorkers=em.GetComponentData<BuildingStats>(market).JobCapacity;em.SetComponentData(market,b);
                for (var i = 0; i < 4; i++) { var task = ProgressionOps.CreateQuest(em, root, def("random_supply_wood"), id, 100 + i); Check(ProgressionOps.QuestCommand(em, root, new Command { Kind = CommandKind.AcceptQuest, Target = em.GetComponentData<Identity>(task).Id }) == ResultCode.Success, "Capacity-loss fixture accepted task " + i); }
                Check(ProgressionOps.QuestCount(em) == 5 && ProgressionOps.QuestCapacity(em) == 8, "Pre-settlement capacity is sufficient");
                var held = new System.Collections.Generic.List<ulong>();
                using(var all=Sim.OrderedEntities<Quest>(em))foreach(var e in all)if(em.GetComponentData<Quest>(e).Container==id)held.Add(em.GetComponentData<Identity>(e).Id);
                Check(held.Count==4,"Accepted tasks bind actual distinct market slots before using other providers");
                var completed=Sim.Find(em,held[0]);var cq=em.GetComponentData<Quest>(completed);cq.Status=QuestStatus.Completed;em.SetComponentData(completed,cq);
                var failure=ProgressionOps.FailureCosts(em,root,def("random_supply_wood"))[0];stock(failure.Item,failure.Amount*4);
                var available=InventoryOps.Count(em,root,failure.Item); var baseline=SnapshotCodec.Capture(em,root);
                SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,baseline));
                Check(baseline.SequenceEqual(SnapshotCodec.Capture(em,root)),"Concrete task containers and completed occupancy round trip");
                var invalid=SnapshotCodec.Decode(em,root,baseline);var first=System.Array.Find(invalid.Records,r=>r.Identity.Id==held[0]);var second=System.Array.Find(invalid.Records,r=>r.Identity.Id==held[1]);second.Quest.ContainerSlot=first.Quest.ContainerSlot;
                var rejected=false;try{SnapshotCodec.Restore(em,root,invalid);}catch(System.IO.InvalidDataException){rejected=true;}
                Check(rejected && baseline.SequenceEqual(SnapshotCodec.Capture(em,root)),"Duplicate task occupancy rejected without mutating live world");
                foreach(var loss in new[]{"maintenance","workers","stage","shrink","demolish"})
                {
                    SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,baseline));market=Sim.Find(em,id);b=em.GetComponentData<Building>(market);
                    if(loss=="maintenance"){b.Maintained=0;em.SetComponentData(market,b);}
                    else if(loss=="workers"){b.Workers=0;em.SetComponentData(market,b);}
                    else if(loss=="stage"){b.Stage=LifeStage.Repairing;em.SetComponentData(market,b);}
                    else if(loss=="shrink"){var stats=em.GetComponentData<BuildingStats>(market);stats.QuestCapacity=0;em.SetComponentData(market,stats);}
                    else BuildingOps.Demolish(em,root,market);
                    ProgressionOps.ReconcileQuestContainers(em,root);
                    foreach(var taskId in held)Check(Sim.Find(em,taskId)==Entity.Null,loss+" removes the bound task including completed rewards");
                    Check(InventoryOps.Count(em,root,failure.Item)==math.max(0,available-failure.Amount*4),loss+" charges normal abandonment penalties once");
                    var count=InventoryOps.Count(em,root,failure.Item);ProgressionOps.ReconcileQuestContainers(em,root);
                    Check(count==InventoryOps.Count(em,root,failure.Item),loss+" reconciliation cannot charge twice");
                    Check(ProgressionOps.QuestCount(em)==1,"Only the unaffected palace mainline remains after "+loss);
                }
                SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,baseline));
                var injected=false;
                try{NightEntryOps.Begin(em,root,false,0,stage=>{if(stage=="day-settled"){injected=true;foreach(var taskId in held)Check(Sim.Find(em,taskId)==Entity.Null,"Day settlement loses tasks whose containers fail maintenance");throw new InvalidOperationException("quest-container-rollback-probe");}});}
                catch(InvalidOperationException error)when(error.Message=="quest-container-rollback-probe"){}
                Check(injected && baseline.SequenceEqual(SnapshotCodec.Capture(em,root)),"Failed night transaction restores removed quests, containers and penalties");
            }
            finally { em.SetComponentData(root, originalCatalog); foreach (var d in c.Definitions) UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(c); }
        }
    }
}
#endif
