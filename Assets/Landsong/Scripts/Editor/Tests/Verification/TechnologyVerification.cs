#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class TechnologyVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); checks++; log.AppendLine("PASS " + label); }
        [MenuItem("Landsong/ECS/Verification/Technology")]
        public static string Run()
        {
            checks = 0; log = new StringBuilder();
            try
            {
                Configuration(); Research();
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes/EntityMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) Map(path);
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/technology-verification.txt", log.ToString()); }
        }
        static GameCatalogAsset Fixture()
        {
            var c = ScriptableObject.CreateInstance<GameCatalogAsset>();
            c.NightEvents = new[] { NightEventSource.Defaults()[0] }; // Isolated research catalog has no battle assets.
            c.Definitions = Enumerable.Range(0, 9).Select(_ => ScriptableObject.CreateInstance<GameDefinitionAsset>()).ToArray();
            c.Definitions[0].Data = new ContentSource { Id = "coin", Name = "金币", Kind = ContentKind.Item, Capacity = 10 };
            c.Definitions[1].Data = new ContentSource { Id = "root", Name = "基础", Kind = ContentKind.Technology, Cost = 5 };
            c.Definitions[2].Data = new ContentSource { Id = "leaf", Name = "目标", Kind = ContentKind.Technology, Cost = 8 };
            c.Definitions[3].Data = new ContentSource { Id = "middle", Name = "中间", Kind = ContentKind.Technology, Cost = 3 };
            c.Definitions[4].Data = new ContentSource { Id = ResearchOps.FeatureId, Name = "科技", Kind = ContentKind.Feature };
            c.Definitions[5].Data = new ContentSource { Id = "repeat", Name = "可重复", Kind = ContentKind.Technology, Cost = 0, Flags = 1 };
            c.Definitions[6].Data = new ContentSource { Id = "building", Kind = ContentKind.Building, Level = 2 };
            c.Definitions[7].Data = new ContentSource { Id = "buff", Kind = ContentKind.Buff };
            c.Definitions[8].Data = new ContentSource { Id = "other", Kind = ContentKind.Feature };
            c.Definitions[2].Data.Configuration.Conditions=new ConditionsContentModule{Enabled=true,Completions=new[]{new CompletionsConfiguration{Content=c.Definitions[3],Count=1}}};
            c.Definitions[2].Data.Configuration.Rewards=new RewardsContentModule{Enabled=true,
                Items=new[]{new ItemsReward{Order=1,Item=c.Definitions[0],Quantity=2}},
                Blueprints=new[]{new BlueprintsReward{Order=2,Building=c.Definitions[6],GrantedLevel=2}},
                Buffs=new[]{new BuffsReward{Order=3,Buff=c.Definitions[7],GrantedLevel=1}},
                Features=new[]{new FeaturesReward{Order=4,Feature=c.Definitions[8],GrantedLevel=1}}};
            c.Definitions[3].Data.Configuration.Conditions=new ConditionsContentModule{Enabled=true,Completions=new[]{new CompletionsConfiguration{Content=c.Definitions[1],Count=1}}};
            c.Definitions[5].Data.Configuration.Rewards=new RewardsContentModule{Enabled=true,Items=new[]{new ItemsReward{Item=c.Definitions[0],Quantity=1}}};
            foreach (var d in c.Definitions) if (string.IsNullOrEmpty(d.Data.Name)) d.Data.Name = d.Data.Id;
            return c;
        }
        static void Destroy(GameCatalogAsset c) { foreach (var d in c.Definitions) UnityEngine.Object.DestroyImmediate(d); UnityEngine.Object.DestroyImmediate(c); }
        static void Configuration()
        {
            var c = Fixture();
            try
            {
                TechnologyContentValidation.Validate(c); Check(true, "Synthetic valid graph supports out-of-index-order dependencies");
                var initial = c.Definitions[1].Data;
                void Reject(ContentSource invalid, string label) { c.Definitions[1].Data = invalid; var failed = false; try { TechnologyContentValidation.Validate(c); } catch (InvalidOperationException) { failed = true; } finally { c.Definitions[1].Data = initial; } Check(failed, label); }
                ContentSource Bad(Action<ContentModules> configure){var result=new ContentSource{Id="root",Kind=ContentKind.Technology};configure(result.Configuration);return result;}
                ContentSource Parent(GameDefinitionAsset asset,int count=1)=>Bad(m=>m.Conditions=new ConditionsContentModule{Enabled=true,Completions=new[]{new CompletionsConfiguration{Content=asset,Count=count}}});
                Reject(new ContentSource { Id = "root", Kind = ContentKind.Technology, Cost = -1 }, "Reject negative technology cost");
                Reject(new ContentSource { Id = "root", Kind = ContentKind.Technology, Flags = 2 }, "Reject unsupported repeat flags");
                Reject(new ContentSource { Id = "root", Kind = ContentKind.Technology, TechnologyPosition = new Vector2(float.NaN, 0) }, "Reject nonfinite graph coordinates");
                Reject(Parent(c.Definitions[2]), "Reject dependency cycle");
                Reject(Parent(c.Definitions[0]), "Prerequisites must be technologies");
                Reject(Parent(null), "Reject missing prerequisites");
                Reject(Parent(c.Definitions[5],2), "Reject unsupported repeat-completion prerequisite count");
                Reject(Bad(m=>m.Rewards=new RewardsContentModule{Enabled=true,Items=new[]{new ItemsReward{Item=c.Definitions[0],Quantity=0}}}), "Reject nonpositive reward");
                Reject(Bad(m=>m.Rewards=new RewardsContentModule{Enabled=true,Blueprints=new[]{new BlueprintsReward{Building=c.Definitions[6],GrantedLevel=3}}}), "Blueprint cannot exceed authored building level");
                Reject(Bad(m=>m.Rewards=new RewardsContentModule{Enabled=true,Buffs=new[]{new BuffsReward{Buff=c.Definitions[0],GrantedLevel=1}}}), "Reward type matches target type");
                Reject(Bad(m=>m.Conditions=new ConditionsContentModule{Enabled=true,Completions=new[]{new CompletionsConfiguration{Content=c.Definitions[5]},new CompletionsConfiguration{Content=c.Definitions[5]}}}), "Duplicate prerequisites cannot silently merge");
                Reject(Bad(m=>m.UnitCosts=new UnitCostsContentModule{Enabled=true,Recruitment=new[]{new RecruitmentCost{Item=c.Definitions[0],Quantity=1}}}), "Recruitment costs cannot be configured on technology");
                c.Definitions[8].Data.Id = "limit.test";
                c.Definitions[2].Data.Configuration.Rewards.Features=Array.Empty<FeaturesReward>();
                Reject(Bad(m=>m.Rewards=new RewardsContentModule{Enabled=true,Features=new[]{new FeaturesReward{Feature=c.Definitions[8],GrantedLevel=1}}}), "Building limit groups are not feature rewards");
            }
            finally { Destroy(c); }
            var formal = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            using var blob = GameWorldAuthoring.BuildCatalog(formal); Check(blob.IsCreated, "Formal catalog bakes with strict technology validation");
            var nodes = formal.Content.Where(d => d.Kind == ContentKind.Technology).ToArray(); Check(nodes.Length == 56, "All 56 formal technology nodes retained");
            var costs = new[] { 5, 8, 12, 18, 26, 36, 48, 62, 78, 96, 116, 140, 168, 200, 240, 285, 335, 500 };
            foreach (var node in nodes)
            {
                var parts = node.Id.Split('_'); var row = int.Parse(parts[1]); var column = int.Parse(parts[2]);
                Check(node.Cost == costs[column - 1] && node.HasTechnologyPosition && Mathf.Abs(node.TechnologyPosition.x - (column - 1) * 220) < 2 && Mathf.Abs(node.TechnologyPosition.y - (row - 1) * 150) < 2, "Retained cost and legacy layout: " + node.Id);
            }
            Check(nodes.All(n => n.Flags == 0), "Formal content retains all 56 nonrepeatable flags, including future");
            Check(nodes.Sum(n => n.Configuration.Conditions.Completions.Length) == 104 && nodes.Sum(n => (n.Configuration.Rewards.Items.Length+n.Configuration.Rewards.Blueprints.Length+n.Configuration.Rewards.Buffs.Length+n.Configuration.Rewards.Features.Length)) == 24, "All 104 prerequisite edges and 24 reward mappings retained");
            Check(formal.Content.Count(d => d.Kind == ContentKind.Feature && !d.Id.StartsWith("limit.")) == 4, "Four real feature licenses distinct from limit keys");
            Check(formal.Content.Any(d => d.Kind == ContentKind.Quest && d.Configuration.Rewards.Features.Any(r => r.Feature.Data.Id == ResearchOps.FeatureId)), "Mainline can grant technology access");
        }
        static void Research()
        {
            var catalog = Fixture(); using var world = new World("Wave six isolated research"); var em = world.EntityManager; var root = em.CreateEntity();
            try
            {
                using var blob = GameWorldAuthoring.BuildCatalog(catalog); em.AddComponentData(root, new ContentCatalog { Value = blob });
                em.AddComponentData(root, new Session { Turn = 1, Phase = Phase.Day }); em.AddBuffer<ResearchEntry>(root); em.AddBuffer<Entitlement>(root); em.AddBuffer<InventorySlot>(root); em.AddBuffer<PendingItem>(root); em.AddBuffer<GameEvent>(root);
                void Points(int n) { var s = em.GetComponentData<Session>(root); s.ResearchPoints = n; em.SetComponentData(root, s); }
                int PointsNow() => em.GetComponentData<Session>(root).ResearchPoints;
                Check(!ResearchOps.Unlocked(em, root) && ResearchOps.Command(em, root, 1, false) == ResultCode.Unavailable && ResearchOps.Plan(em, root, 2) == ResultCode.Unavailable, "Locked feature blocks single and path commands");
                FeatureOps.Unlock(em, root, 4); Check(ResearchOps.Unlocked(em, root), "Feature entitlement opens research");
                Check(ResearchOps.Command(em, root, 2, false) == ResultCode.MissingResearch && ResearchOps.Command(em, root, 0, false) == ResultCode.InvalidContent, "Single enqueue rejects missing prerequisite and nontechnology");
                var path = ResearchOps.Path(em, root, 2); Check(path.Definitions.SequenceEqual(new[] { 1, 3, 2 }) && path.Remaining == 16, "Path topological order not catalog index order");
                Points(2); Check(ResearchOps.Plan(em, root, 2) == ResultCode.Success && PointsNow() == 2, "Planning does not charge points");
                ResearchOps.Settle(em, root); Check(ResearchOps.Entry(em, root, 1).Progress == 2 && PointsNow() == 0, "Partial funding spends only available points");
                Check(ResearchOps.Command(em, root, 1, true) == ResultCode.Success && ResearchOps.Entry(em, root, 1).Progress == 2 && ResearchOps.Queue(em, root)[0].QueueOrder == 1, "Cancel keeps progress and compacts queue");
                Points(30); ResearchOps.Settle(em, root); Check(PointsNow() == 30 && ResearchOps.Completed(em, root, 3) == 0, "Cancelled prerequisite pauses dependent queue without spending");
                var stale = ResearchOps.Fingerprint(em, root); ResearchOps.Command(em, root, 3, true); var before = ResearchOps.Fingerprint(em, root);
                Check(ResearchOps.Plan(em, root, 2, stale) == ResultCode.Unavailable && before == ResearchOps.Fingerprint(em, root), "Stale path consent leaves state untouched");
                Check(ResearchOps.Plan(em, root, 2) == ResultCode.Success && ResearchOps.Path(em, root, 2).Remaining == 14, "Rebuild path reuses retained investment");
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, 1) == 1 && ResearchOps.Completed(em, root, 3) == 1 && ResearchOps.Entry(em, root, 2).Progress == 8 && PointsNow() == 16, "One settlement completes funded prerequisites then holds full reward-blocked target");
                Check(ResearchOps.Completed(em, root, 2) == 0 && !ConditionOps.Satisfied(em, root, 6) && !ConditionOps.Satisfied(em, root, 7) && !ConditionOps.Satisfied(em, root, 8), "No completion or nonitem grants before all rewards fit");
                Check(ResearchOps.Quote(em, root, 2).Status == ResearchStatus.AwaitingRewards, "Awaiting-reward status explicit");
                em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Index = 0, Item = -1, SlotType = -1 });
                var testSlots = em.GetBuffer<InventorySlot>(root); var limited = testSlots[0]; limited.Item = 0; limited.Count = 9; testSlots[0] = limited;
                ResearchOps.Settle(em, root); Check(InventoryOps.Count(em, root, 0) == 9 && ResearchOps.Completed(em, root, 2) == 0 && !ConditionOps.Satisfied(em, root, 6), "Partially fitting reward rolls back items and grants together");
                limited.Item = -1; limited.Count = 0; testSlots = em.GetBuffer<InventorySlot>(root); testSlots[0] = limited;
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, 2) == 1 && PointsNow() == 16 && InventoryOps.Count(em, root, 0) == 2 && ConditionOps.Satisfied(em, root, 6, 2) && ConditionOps.Satisfied(em, root, 7) && ConditionOps.Satisfied(em, root, 8), "Deferred award commits all reward kinds once with no extra research charge");
                Check(ResearchOps.Command(em, root, 2, false) == ResultCode.Unavailable && ResearchOps.Queue(em, root).Count == 0, "Nonrepeatable completed node cannot enqueue again");
                ResearchOps.Command(em, root, 5, false); ResearchOps.Settle(em, root); ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, 5) == 1 && InventoryOps.Count(em, root, 0) == 3, "Zero-cost repeated technology completes only once per explicit enqueue");
                ResearchOps.Command(em, root, 5, false); Check(ResearchOps.Command(em, root, 5, false) == ResultCode.Unavailable, "Duplicate queue rejected"); ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, 5) == 2 && InventoryOps.Count(em, root, 0) == 3 && PointsNow() == 16, "Repeat count increments without repeating first reward");
                var s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; em.SetComponentData(root, s);
                Check(ResearchOps.Command(em, root, 5, false) == ResultCode.WrongPhase && ResearchOps.Plan(em, root, 5) == ResultCode.WrongPhase && ResearchOps.Command(em, root, 5, true) == ResultCode.WrongPhase, "Night permits viewing but rejects all research writes");
                s.Phase = Phase.Day; s.CheckpointPending = 1; em.SetComponentData(root, s); Check(ResearchOps.Plan(em, root, 5) == ResultCode.Busy, "Checkpoint preparation locks planning");
                using var entries = em.GetBuffer<ResearchEntry>(root).ToNativeArray(Allocator.Temp); using var grants = em.GetBuffer<Entitlement>(root).ToNativeArray(Allocator.Temp);
                ResearchOps.ValidateState(em, root, 16, entries.ToArray(), grants.ToArray()); Check(true, "Completed and repeated research passes restore validation");
                var failed = false; try { ResearchOps.ValidateState(em, root, -1, entries.ToArray(), grants.ToArray()); } catch (InvalidDataException) { failed = true; } Check(failed, "Reject negative saved points");
            }
            finally { Destroy(catalog); }
        }
        static void Map(string path)
        {
            log.AppendLine("MAP " + path); var scene = EditorSceneManager.OpenPreviewScene(path); using var store = new BlobAssetStore(128); using var world = new World("Technology baked map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset"); var target = catalog.Find("TN_4_2_木工术"); var first = catalog.Find("TN_3_1_启蒙");
                Check(!ResearchOps.Unlocked(em, root) && GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Research, Definition = first }) == ResultCode.Unavailable, "Real map starts with locked technology command");
                FeatureOps.Unlock(em, root, catalog.Find(ResearchOps.FeatureId));
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.PlanResearch, Definition = target }) == ResultCode.Success, "Real processor dispatches research path");
                var s = em.GetComponentData<Session>(root); s.ResearchPoints = 2; em.SetComponentData(root, s); ResearchOps.Settle(em, root);
                var original = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                Check(original.SequenceEqual(SnapshotCodec.Capture(em, root)) && ResearchOps.Entry(em, root, first).Progress == 2 && ResearchOps.Queue(em, root).Count == 2, "Version six roundtrip retains partial progress and path order");
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Forecast clones research without point, queue, grant or RNG mutation");
                void Reject(Action<SnapshotCodec.Snapshot> mutate, string label)
                {
                    var invalid = SnapshotCodec.Decode(em, root, original); mutate(invalid); var failed = false;
                    try { SnapshotCodec.Restore(em, root, invalid); } catch (InvalidDataException) { failed = true; }
                    Check(failed && original.SequenceEqual(SnapshotCodec.Capture(em, root)), label);
                }
                Reject(x => x.Research[1].QueueOrder = x.Research[0].QueueOrder, "Duplicate saved queue order rejected atomically");
                Reject(x => x.Research[1].Definition = x.Research[0].Definition, "Duplicate saved research definition rejected atomically");
                Reject(x => x.Research[0].Progress = 99999, "Excess progress rejected atomically");
                Reject(x => x.Research[0].Completions = 1, "Completed nonrepeatable node cannot still be queued");
                Reject(x => x.Session.ResearchPoints = -1, "Negative saved research pool rejected atomically");
                s = em.GetComponentData<Session>(root); s.ResearchPoints = 100; em.SetComponentData(root, s); ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, target) == 1 && ResearchOps.Queue(em, root).Count == 0, "Real woodwork path completes in one funded settlement");
                var d = Sim.Definition(em, root, target); for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == RuleKind.RewardBlueprint) Check(ConditionOps.Satisfied(em, root, r.Target, r.Amount), "Woodwork building unlock retained: " + r.Target); }
                var completed = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, completed)); ResearchOps.Settle(em, root);
                Check(completed.SequenceEqual(SnapshotCodec.Capture(em, root)), "Load cannot reaward completed technology");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
