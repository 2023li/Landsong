#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class WorkforceVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); checks++; log.AppendLine("PASS " + label); }
        [MenuItem("Landsong/ECS/Verification/Workforce")]
        public static string Run()
        {
            checks = 0; log = new StringBuilder();
            try
            {
                Formulas(); AuthoringValidation(); Sources();
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes/EntityMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) Map(path);
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/workforce-verification.txt", log.ToString()); }
        }
        static void Formulas()
        {
            for (var cap = 1; cap <= 60; cap++)
            {
                var valid = true;
                foreach (var attraction in new[] { 0f, .1f, 20f, 33.3333f, 50f, 90f, 100f })
                    for (var target = 0; target <= cap; target++)
                    {
                        var cost = WorkforceOps.SubsidyCost(cap, attraction, target);
                        valid &= WorkforceOps.Stable(cap, attraction + cost * (100f / cap)) >= target;
                        if (cost > 0) valid &= WorkforceOps.Stable(cap, attraction + (cost - 1) * (100f / cap)) < target;
                    }
                Check(valid, "Minimal integer subsidy reaches exact worker target, capacity " + cap);
            }
            foreach (var cap in new[] { 1, 3, 10, 30, 100, 10000 })
            {
                var ticks = UI_GamePanel_WorkforceScale.TickValues(cap, cap / 3, cap / 2, cap - 1, 12);
                Check(ticks.Count <= 12 && ticks.Distinct().Count() == ticks.Count && ticks.Contains(0) && ticks.Contains(cap) && ticks.Contains(cap / 3) && ticks.Contains(cap / 2) && ticks.Contains(cap - 1), "Bounded genuine tick values retain important points, capacity " + cap);
            }
            Check(WorkforceOps.Stable(0, 100) == 0 && WorkforceOps.SubsidyCost(0, 0, 3) == 0 && WorkforceOps.Stable(9, 20) == 2, "No jobs and integer threshold boundaries");
        }
        static BlobAssetReference<ContentBlob> Blob(ContentDefinition[] definitions, Dictionary<int, Rule[]> rules, BlobAssetReference<ContentBlob> source = default)
        {
            using var builder = new BlobBuilder(Allocator.Temp); ref var result = ref builder.ConstructRoot<ContentBlob>();
            NightPlanningVerification.CopyNight(builder, ref result, source);
            var defs = builder.Allocate(ref result.Definitions, definitions.Length); var terms = builder.Allocate(ref result.Rules, rules.Sum(p => p.Value.Length)); var at = 0;
            for (var i = 0; i < definitions.Length; i++) { var d = definitions[i]; d.RuleStart = at; d.RuleCount = rules.TryGetValue(i, out var list) ? list.Length : 0; if (list != null) foreach (var r in list) terms[at++] = r; defs[i] = d; }
            return builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
        }
        static void AuthoringValidation()
        {
            var catalog = UnityEngine.ScriptableObject.CreateInstance<Authoring.GameCatalogAsset>();
            var coin = UnityEngine.ScriptableObject.CreateInstance<Authoring.GameDefinitionAsset>();
            var building = UnityEngine.ScriptableObject.CreateInstance<Authoring.GameDefinitionAsset>();
            try
            {
                coin.Data = new Authoring.ContentSource { Id = "coin", Kind = ContentKind.Item };
                building.Data = new Authoring.ContentSource { Id = "building", Kind = ContentKind.Building }; catalog.Definitions = new[] { coin, building };
                void Use(Authoring.WorkforceLevel row) { building.Data.Modules=new Authoring.BuildingModules();building.Data.Modules.Workforce.Enabled=true;building.Data.Modules.Workforce.Levels=new[]{row}; }
                void Effect(Authoring.SpatialEffectEntry row) { building.Data.Modules=new Authoring.BuildingModules();building.Data.Modules.Effects.Enabled=true;building.Data.Modules.Effects.Spatial=new[]{row}; }
                void Reject(Action configure,string label) { configure();var failed=false;try { Authoring.GameWorldAuthoring.ValidateWorkforce(catalog); } catch(InvalidOperationException) { failed=true; } Check(failed,label); }
                Use(new Authoring.WorkforceLevel{Capacity=10,InitialWorkers=1,BaseAttraction=55,RecruitmentCost=10,Currency=coin});
                Authoring.GameWorldAuthoring.ValidateWorkforce(catalog); Check(true,"Authoring accepts ordinary workforce configuration");
                Reject(()=>Use(new Authoring.WorkforceLevel{Capacity=-1}),"Authoring rejects negative job capacity");
                Reject(()=>Use(new Authoring.WorkforceLevel{Capacity=10,InitialWorkers=11}),"Authoring rejects excess initial workers");
                Reject(()=>Use(new Authoring.WorkforceLevel{BaseAttraction=float.NaN}),"Authoring rejects non-finite attraction");
                Reject(()=>Use(new Authoring.WorkforceLevel{RecruitmentCost=float.PositiveInfinity}),"Authoring rejects infinite recruitment price");
                Reject(()=>Use(new Authoring.WorkforceLevel{RecruitmentCost=1073741824f}),"Authoring rejects float-rounded price that could overflow integer payment");
                Reject(()=>Use(new Authoring.WorkforceLevel{Currency=building}),"Authoring workforce payment must target an item");
                Reject(()=>Effect(new Authoring.SpatialEffectEntry{Stacking=(Authoring.EffectStacking)11}),"Authoring rejects unknown spatial stacking mode");
                Reject(()=>Effect(new Authoring.SpatialEffectEntry{Building=coin}),"Authoring spatial target must be a building");
                Reject(()=>Effect(new Authoring.SpatialEffectEntry{Radius=float.NaN}),"Authoring rejects non-finite spatial radius");
                Effect(new Authoring.SpatialEffectEntry{Radius=4,Magnitude=10,Stacking=Authoring.EffectStacking.同类最高});
                Authoring.GameWorldAuthoring.ValidateWorkforce(catalog); Check(true,"Authoring accepts unfiltered highest spatial effect");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(coin); UnityEngine.Object.DestroyImmediate(building); }
        }
        static Entity Building(EntityManager em, int definition, ulong id, int2 cell, bool provider = false)
        {
            var e = em.CreateEntity(); em.AddComponentData(e, new Identity { Definition = definition, Id = id, Name = "测试建筑" });
            em.AddComponentData(e, new Building { Stage = LifeStage.Operational, Cell = cell, Size = new int2(1), Maintained = 1, Level = 1, Crop = -1 });
            em.AddComponentData(e, new BuildingStats { JobCapacity = 10, ActionPower = 20, IsProvider = (byte)(provider ? 1 : 0) });
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(cell.x, 0, cell.y))); em.AddBuffer<RepairMaterial>(e); em.AddBuffer<BuildingInvestment>(e); return e;
        }
        static void Sources()
        {
            using var world = new World("Wave five isolated source fixture"); var em = world.EntityManager; var root = em.CreateEntity();
            em.AddComponentData(root, new Session { Turn = 1, Phase = Phase.Day, BasePopulation = 30, RandomState = 123 }); em.AddComponentData(root, new GameSettings { Gold = 0 });
            em.AddBuffer<InventorySlot>(root); em.AddBuffer<PendingItem>(root); em.AddBuffer<Entitlement>(root); em.AddBuffer<NightWave>(root); em.AddBuffer<GameEvent>(root);
            var definitions = new ContentDefinition[5];
            definitions[0] = new ContentDefinition { Kind = ContentKind.Item, Id = "coin", Group = -1, Value = 3, Capacity = 10000 };
            for (var i = 1; i < 5; i++) definitions[i] = new ContentDefinition { Kind = ContentKind.Building, Id = "b" + i, Size = new int2(1) };
            definitions[3].BuildingPolicy.ProviderPriority = 2;
            Rule Effect(int amount, string key, int stacking = 0, int workers = 0, int target = -1) => new Rule { Kind = RuleKind.SpatialEffect, B = 20, Amount = amount, Key = key, Extra = stacking, Value = 4, C = workers, Target = target };
            var rules = new Dictionary<int, Rule[]> {
                [1] = new[] { new Rule { Kind = RuleKind.Workforce, Amount = 10, Value = 20, Extra = 10, Target = 0 }, new Rule { Kind = RuleKind.StorageCondition, Value = 5, Target = -1 } },
                [2] = new[] { Effect(3, "a"), Effect(2, "add", 10), Effect(4, "highest", 20) },
                [3] = new[] { Effect(5, "a"), Effect(7, "highest", 20), Effect(99, "workers", 10, 2), Effect(99, "wrong", 10, 0, 2) },
                [4] = new[] { Effect(5, "a") }
            };
            using var blob = Blob(definitions, rules); em.AddComponentData(root, new ContentCatalog { Value = blob });
            using var gridBuilder = new BlobBuilder(Allocator.Temp); ref var g = ref gridBuilder.ConstructRoot<GridBlob>(); g.Size = new int2(8, 8);
            var cells = gridBuilder.Allocate(ref g.Cells, 64); for (var i = 0; i < 64; i++) cells[i] = new GridCell { Exists = 1, Traversable = 1, Buildable = 1 };
            using var grid = gridBuilder.CreateBlobAssetReference<GridBlob>(Allocator.Persistent); em.AddComponentData(root, new GridData { Value = grid, CellSize = 1 }); em.AddBuffer<Occupancy>(root).ResizeUninitialized(64);
            var occupancy = em.GetBuffer<Occupancy>(root); for (var i = 0; i < 64; i++) occupancy[i] = default;
            var target = Building(em, 1, 1, new int2(1, 1)); var first = Building(em, 2, 2, new int2(2, 1), true); var priority = Building(em, 3, 3, new int2(4, 1), true); var tie = Building(em, 4, 4, new int2(2, 1), true);
            void Workers(Entity entity, int count, byte maintained = 1) { var b = em.GetComponentData<Building>(entity); b.Workers = count; b.Maintained = maintained; em.SetComponentData(entity, b); }
            Workers(first, 1); Workers(priority, 1); Workers(tie, 1);
            var slots = em.GetBuffer<InventorySlot>(root); slots.Add(new InventorySlot { Provider = 2, Item = 0, Count = 100, SlotType = -1 });
            var q = WorkforceOps.Quote(em, root, target); Check(q.NaturalStable == 2 && q.CurrentStable == 2 && q.RecruitCost == 18 && q.Sources.Sum(s => s.Value) == q.Raw, "Workforce quote uses real source sum and current recruitment price");
            Check(GameLoopSystem.Execute(em,root,CommandRequests.SetWorkforceBudget(1,3))==ResultCode.Success&&InventoryOps.Count(em,root,0)==100,"Direct subsidy budget changes without immediate payment");
            q=WorkforceOps.Quote(em,root,target);Check(q.SubsidyCost==3&&q.Planned==50&&q.Current==20,"Budget preview separates unpaid and actual attraction");
            Workers(target,0,0);q=WorkforceOps.Quote(em,root,target);Check(q.Natural==15&&q.SubsidyCost==3,"Direct subsidy budget stays fixed when natural attraction changes");Workers(target,0);
            Check(WorkforceOps.SetBudget(em,root,target,11)==ResultCode.InvalidTarget&&WorkforceOps.SetBudget(em,root,target,-1)==ResultCode.InvalidTarget,"Budget rejects negative or excessive amounts");
            var step=CommandRequests.AdjustWorkforceBudget(1,1);
            Check(GameLoopSystem.Execute(em,root,step)==ResultCode.Success&&GameLoopSystem.Execute(em,root,step)==ResultCode.Success&&WorkforceOps.Quote(em,root,target).SubsidyCost==5,"Rapid arrow commands each apply one budget step against current state");
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.WorkforceTarget, Target = 1, Amount = 10 }) == ResultCode.Success && InventoryOps.Count(em, root, 0) == 100, "Target command is non-paying");
            q = WorkforceOps.Quote(em, root, target); Check(q.PlannedStable == 10 && q.CurrentStable == 2 && q.SubsidyCost == 8, "Unpaid target cannot enlarge current stable workforce");
            Check(EconomyOps.ChangeWorkers(em, root, target, 3) == ResultCode.NoCapacity && InventoryOps.Count(em, root, 0) == 100, "Unpaid subsidy cannot fund recruitment eligibility");
            Check(GameLoopSystem.Execute(em, root, CommandRequests.RecruitWorkers(1, 1, 17)) == ResultCode.Unavailable && InventoryOps.Count(em, root, 0) == 100, "Stale displayed recruitment price rejected without charge");
            Check(EconomyOps.ChangeWorkers(em, root, target, 1) == ResultCode.Success && InventoryOps.Count(em, root, 0) == 82, "Recruit charges exactly current quoted price");
            Check(WorkforceOps.SetTarget(em, root, target, 0) == ResultCode.Success && WorkforceOps.Quote(em, root, target).Target == 2 && em.GetComponentData<Building>(target).Workers == 1, "Low target clamps to natural stable count without layoffs");
            Workers(target, 1, 0); q = WorkforceOps.Quote(em, root, target); Check(q.Raw == 15 && q.Sources.Any(s => s.Value == -5), "Maintenance penalty included in source breakdown"); Workers(target, 1);
            var expedition = em.CreateEntity(); em.AddComponentData(expedition, new Identity { Id = 8 }); em.AddComponentData(expedition, new Expedition { Site = 1, Status = ExpeditionStatus.Travelling });
            Check(WorkforceOps.SetTarget(em, root, target, 10) == ResultCode.Busy && EconomyOps.ChangeWorkers(em, root, target, -1) == ResultCode.Busy, "Travelling expedition locks recruitment dismissal and target"); em.DestroyEntity(expedition);
            var session = em.GetComponentData<Session>(root); session.Phase = Phase.Night; em.SetComponentData(root, session);
            Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.WorkforceTarget, Target = 1, Amount = 10 }) == ResultCode.WrongPhase, "Night blocks target commands"); session.Phase = Phase.Day; em.SetComponentData(root, session);
            var soldier = em.CreateEntity(); em.AddComponentData(soldier, new Soldier { PopulationCost = 30 });
            Check(EconomyOps.ChangeWorkers(em, root, target, 1) == ResultCode.InsufficientPopulation, "Recruit cannot borrow population occupied by soldiers"); em.DestroyEntity(soldier);
            var network = ResourceNetworkOps.Quote(em, root, target); Check(network.Selected == priority && network.Candidates.Count == 3, "Higher provider priority wins over shorter distance");
            Workers(priority, 0); network = ResourceNetworkOps.Quote(em, root, target); Check(network.Selected == first && network.Candidates.Single(c => c.Entity == priority).Reason == "缺少工人", "Inactive provider explained; equal costs use stable ID");
            Workers(first, 1, 0); Check(ResourceNetworkOps.Provider(em, root, target) == tie, "Unmaintained provider is ineligible"); Workers(first, 1); Workers(priority, 1);
            var stats = em.GetComponentData<BuildingStats>(target); stats.ActionPower = 0; em.SetComponentData(target, stats);
            Check(ResourceNetworkOps.Provider(em, root, target) == Entity.Null, "Provider outside connection budget unavailable"); stats.ActionPower = 20; em.SetComponentData(target, stats);
            var spatial = SpatialOps.Quote(em, root, target, 20);
            Check(spatial.Value == 14 && spatial.Sources.Sum(s => s.Applied) == EconomyOps.SpatialValue(em, root, target, 20), "Spatial actual total equals additive plus per-group max plus global max explanation");
            Check(spatial.Sources.Any(s => s.Reason.Contains("工人")) && spatial.Sources.Any(s => s.Reason.Contains("不匹配")) && spatial.Sources.Single(s => s.Source == 4).Applied == 0, "Spatial explanations include staffing, target filter and stable-ID duplicate suppression");
            Workers(priority, 1, 0); Check(SpatialOps.Quote(em, root, target, 20).Value == 14, "Maintenance alone does not silently disable spatial effects");
            var ruined = em.GetComponentData<Building>(priority); ruined.Stage = LifeStage.Ruined; em.SetComponentData(priority, ruined);
            Check(SpatialOps.Quote(em, root, target, 20).Value == 11, "Ruined source contributes nothing while remaining group source takes over"); ruined.Stage = LifeStage.Operational; ruined.Maintained = 1; em.SetComponentData(priority, ruined);
            var far = em.GetComponentData<Building>(tie); far.Cell = new int2(4, 4); em.SetComponentData(tie, far);
            Check(!SpatialOps.Quote(em, root, target, 20).Sources.Any(s => s.Source == 4), "Diagonal Manhattan gap beyond radius excluded");
            far.Cell = new int2(4, 2); em.SetComponentData(tie, far); var large = em.GetComponentData<Building>(target); large.Size = new int2(2); em.SetComponentData(target, large);
            Check(SpatialOps.Quote(em, root, target, 20).Sources.Any(s => s.Source == 4), "Spatial range measures full target footprint");
            large.Stage = LifeStage.Repairing; large.Size = new int2(1); large.RepairDuration = 3; large.Progress = 0; em.SetComponentData(target, large);
            em.GetBuffer<RepairMaterial>(target).Add(new RepairMaterial { Item = 0, Amount = 8 }); em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = 0, Amount = 2 });
            var repair = BuildingCostOps.QuoteRepair(em, root, target);
            Check(repair.Total[0].Amount == 8 && repair.Remaining[0].Amount == 8 && repair.Payments[0].Required == 3 && repair.Payments[0].Pending == 2 && repair.Payments[0].Normal == 1 && repair.Provider == priority, "Frozen repair front-loads remainder and splits pending versus normal payment");
            var normal = InventoryOps.Count(em, root, 0);
            Check(BuildingCostOps.PayRepairStep(em, root, target) && InventoryOps.Count(em, root, 0) == normal - 1 && InventoryOps.PendingCount(em, root, 0) == 0 && em.GetComponentData<Building>(priority).MarketValue == 3, "Repair charges exact split and attributes only ordinary resource value");
            large.Progress = 1; em.SetComponentData(target, large); repair = BuildingCostOps.QuoteRepair(em, root, target); Check(repair.Remaining[0].Amount == 5 && repair.Costs[0].Amount == 3, "Second installment leaves exact unpaid remainder");
            large.Progress = 2; em.SetComponentData(target, large); Check(BuildingCostOps.QuoteRepair(em, root, target).Costs[0].Amount == 2, "Final repair installment conserves frozen total");
            stats.ActionPower = 0; em.SetComponentData(target, stats); em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = 0, Amount = 1 });
            normal = InventoryOps.Count(em, root, 0); Check(!BuildingCostOps.PayRepairStep(em, root, target) && InventoryOps.Count(em, root, 0) == normal && InventoryOps.PendingCount(em, root, 0) == 1, "Disconnected mixed repair cannot partially charge pending pool");
            var pending = em.GetBuffer<PendingItem>(root); var pendingGold = pending[0]; pendingGold.Amount = 2; pending[0] = pendingGold;
            Check(!BuildingCostOps.QuoteRepair(em, root, target).NeedsNetwork && BuildingCostOps.PayRepairStep(em, root, target) && InventoryOps.Count(em, root, 0) == normal, "Pending-only repair needs no provider and does not consume ordinary stock");
            stats.ActionPower = 20; em.SetComponentData(target, stats); em.GetBuffer<InventorySlot>(root).Clear();
            Check(BuildingCostOps.QuoteRepair(em, root, target).Payments[0].Missing == 2 && !BuildingCostOps.PayRepairStep(em, root, target), "Insufficient complete installment is explained and rejected");
        }
        static void Map(string path)
        {
            log.AppendLine("MAP " + path); var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128); using var world = new World("Wave five isolated map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var original = em.GetComponentData<ContentCatalog>(root).Value;
                var definitions = Enumerable.Range(0, original.Value.Definitions.Length).Select(i => original.Value.Definitions[i]).ToArray();
                var def = Array.FindIndex(definitions, d => d.Kind == ContentKind.Building && d.Id.ToString().Contains("伐木")); Check(def >= 0, "Real map includes workforce content");
                var grid = em.GetComponentData<GridData>(root); var cell = grid.Value.Value.Min; var found = false;
                for (var y = 0; y < grid.Value.Value.Size.y && !found; y++) for (var x = 0; x < grid.Value.Value.Size.x && !found; x++) { cell = grid.Value.Value.Min + new int2(x, y); found = GridOps.CanPlace(em, root, def, cell, 0); }
                Check(found, "Workforce fixture has legal footprint"); var building = BuildingOps.Create(em, root, def, cell, 0, 1, true); var id = em.GetComponentData<Identity>(building).Id;
                var gold = em.GetComponentData<GameSettings>(root).Gold;
                var rules = new Dictionary<int, Rule[]>();
                for (var i = 0; i < definitions.Length; i++) { var d = definitions[i]; rules[i] = Enumerable.Range(d.RuleStart, d.RuleCount).Select(r => original.Value.Rules[r]).ToArray(); if (d.Kind == ContentKind.Item) definitions[i].Loss = 0; }
                // Preserve rule offsets used by the already-created quest progress in this live fixture.
                rules[def] = Enumerable.Repeat(new Rule { Kind = RuleKind.Workforce, Level = 999, Target = gold }, rules[def].Length).ToArray();
                rules[def][0] = new Rule { Kind = RuleKind.Workforce, Amount = 10, Value = 20, Extra = 10, Target = gold };
                using var blob = Blob(definitions, rules, original);
                try
                {
                    em.SetComponentData(root, new ContentCatalog { Value = blob }); BuildingOps.ApplyLevel(em, root, building, false);
                    var b = em.GetComponentData<Building>(building); b.Workers = 0; b.Subsidy = 1; b.WorkerTarget = 10; b.SubsidyBudget = 8; b.PaidSubsidy = 0; em.SetComponentData(building, b);
                    var s = em.GetComponentData<Session>(root); s.BasePopulation = 100; s.CheckpointPending = 0; s.LastSettledTurn = 0; em.SetComponentData(root, s);
                    InventoryOps.Add(em, root, gold, 100);
                    var before = SnapshotCodec.Capture(em, root);
                    Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Real map forecast rolls back paid subsidy, workers, inventory and RNG");
                    EconomyOps.Settle(em, root); building = Sim.Find(em, id); b = em.GetComponentData<Building>(building);
                    Check(b.PaidSubsidy == 8 && b.PaidSubsidyTurn == s.Turn && b.StableWorkers == 10 && b.Workers <= 1, "Real settlement pays target and changes natural workforce at most once");
                    using (var journal = em.GetBuffer<EconomyEntry>(root).ToNativeArray(Allocator.Temp)) Check(journal.ToArray().Any(r => r.Source == id && r.Reason == EconomyReason.Workforce && r.Delta == -8), "Paid subsidy reconciles actual per-building journal");
                    var paid = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, paid)); building = Sim.Find(em, id);
                    Check(paid.SequenceEqual(SnapshotCodec.Capture(em, root)) && WorkforceOps.Quote(em, root, building).CurrentStable == 10, "Version-six snapshot preserves actual subsidy and stable workforce exactly");
                    Check(WorkforceOps.SetTarget(em, root, building, 0) == ResultCode.Success && WorkforceOps.Quote(em, root, building).CurrentStable == 10, "Changing next payment does not erase benefit already paid");
                    s = em.GetComponentData<Session>(root); s.LastSettledTurn = 0; em.SetComponentData(root, s); EconomyOps.Settle(em, root);
                    Check(em.GetComponentData<Building>(building).PaidSubsidy == 0 && WorkforceOps.Quote(em, root, building).CurrentStable == 2, "Next settlement replaces previously paid subsidy with current plan");
                    SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, paid)); building = Sim.Find(em, id); InventoryOps.Remove(em, root, gold, InventoryOps.Count(em, root, gold));
                    s = em.GetComponentData<Session>(root); s.LastSettledTurn = 0; em.SetComponentData(root, s); EconomyOps.Settle(em, root);
                    Check(em.GetComponentData<Building>(building).PaidSubsidy == 0 && em.GetComponentData<Building>(building).Subsidy == 1, "Unfunded payment removes bonus but retains requested future target");
                    var malformed = SnapshotCodec.Decode(em, root, paid); malformed.Records.Single(r => r.Identity.Id == id).Building.PaidSubsidy = -1;
                    before = SnapshotCodec.Capture(em, root); var rejected = false; try { SnapshotCodec.Restore(em, root, malformed); } catch (InvalidDataException) { rejected = true; }
                    Check(rejected && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Invalid persisted subsidy rejected atomically");
                }
                finally { em.SetComponentData(root, new ContentCatalog { Value = original }); }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
