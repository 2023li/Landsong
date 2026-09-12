#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class EconomyVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); checks++; log.AppendLine("PASS " + label); }
        static T[] Rows<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData
        { if (!em.HasBuffer<T>(root)) return Array.Empty<T>(); using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); return rows.ToArray(); }
        static Entity[] Entities(EntityManager em)
        { using var q = em.CreateEntityQuery(new EntityQueryDesc { Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab }); using var a = q.ToEntityArray(Allocator.Temp); return a.ToArray().OrderBy(e => e.Index).ThenBy(e => e.Version).ToArray(); }
        [MenuItem("Landsong/ECS/Verification/Economy")]
        public static string Run()
        {
            log = new StringBuilder(); checks = 0;
            try
            {
                Food();
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes/EntityMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) Map(path);
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/economy-verification.txt", log.ToString()); }
        }
        static BlobAssetReference<ContentBlob> Blob(ContentDefinition[] definitions, Dictionary<int, Rule[]> rules, BlobAssetReference<ContentBlob> source = default)
        {
            using var builder = new BlobBuilder(Allocator.Temp); ref var result = ref builder.ConstructRoot<ContentBlob>();
            NightPlanningVerification.CopyNight(builder, ref result, source);
            var defs = builder.Allocate(ref result.Definitions, definitions.Length); var terms = builder.Allocate(ref result.Rules, rules.Sum(p => p.Value.Length)); var at = 0;
            for (var i = 0; i < definitions.Length; i++) { var d = definitions[i]; d.RuleStart = at; d.RuleCount = rules.TryGetValue(i, out var list) ? list.Length : 0; if (list != null) foreach (var r in list) terms[at++] = r; defs[i] = d; }
            return builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
        }
        static void Food()
        {
            using var world = new World("Food matching fixture"); var em = world.EntityManager; var root = em.CreateEntity();
            em.AddComponentData(root, new Session { Turn = 1 }); em.AddBuffer<InventorySlot>(root); em.AddBuffer<PendingItem>(root);
            var definitions = new ContentDefinition[6];
            for (var i = 0; i < 3; i++) definitions[i] = new ContentDefinition { Id = i == 0 ? "z-food" : i == 1 ? "a-food" : "c-food", Kind = ContentKind.Item, Group = 3, Capacity = 100 };
            definitions[3] = new ContentDefinition { Id = "group", Kind = ContentKind.ItemGroup, Group = -1 };
            definitions[4] = new ContentDefinition { Id = "house", Kind = ContentKind.Building };
            definitions[5] = new ContentDefinition { Id = "reward", Kind = ContentKind.Technology };
            var rules = new Dictionary<int, Rule[]> { [4] = new[] { new Rule { Kind = RuleKind.Food, Target = 3, Amount = 1, B = 1 }, new Rule { Kind = RuleKind.Food, Target = 0, Amount = 1, B = 1 } }, [5] = new[] { new Rule { Kind = RuleKind.RewardItem, Target = 0, Amount = 1 }, new Rule { Kind = RuleKind.RewardItem, Target = 2, Amount = 100 } } };
            using var blob = Blob(definitions, rules); em.AddComponentData(root, new ContentCatalog { Value = blob });
            var house = em.CreateEntity(); em.AddComponentData(house, new Identity { Id = 1, Definition = 4, Name = "测试住宅" }); em.AddComponentData(house, new Building { Population = 2, Level = 1 }); em.AddBuffer<FoodSelection>(house);
            void Stocks(int a, int b) { var slots = em.GetBuffer<InventorySlot>(root); slots.Clear(); slots.Add(new InventorySlot { Item = 0, Count = a, SlotType = -1 }); slots.Add(new InventorySlot { Item = 1, Count = b, SlotType = -1 }); }
            Stocks(10, 5); EconomyJournalOps.Begin(em, root, false);
            Check(ResidentialFoodOps.Plan(em, root, house, out var plan) && plan[0].Item == 1 && plan[1].Item == 0, "Overlapping groups find complete meal despite narrow group competing for highest stock");
            Check(ResidentialFoodOps.Pay(em, root, house) && InventoryOps.Count(em, root, 0) == 8 && InventoryOps.Count(em, root, 1) == 3, "Complete recipe consumes population-scaled quantities once");
            Check(Rows<FoodSelection>(em, house).Length == 2 && Rows<EconomyEntry>(em, root).Sum(r => r.Delta) == -4, "Actual food selections and actual journal agree");
            Check(ResidentialFoodOps.Pay(em, root, house) && !ResidentialFoodOps.Pay(em, root, house), "Consecutive houses compete against shared remaining stock");
            var before = Rows<InventorySlot>(em, root); var journal = Rows<EconomyEntry>(em, root);
            Check(!ResidentialFoodOps.Pay(em, root, house) && before.SequenceEqual(Rows<InventorySlot>(em, root)) && journal.SequenceEqual(Rows<EconomyEntry>(em, root)) && Rows<FoodSelection>(em, house).Length == 0, "Incomplete meal leaves no partial charges or stale selections");
            rules[4] = new[] { new Rule { Kind = RuleKind.Food, Target = 3, Amount = 1, B = 1 } };
            using var broad = Blob(definitions, rules); em.SetComponentData(root, new ContentCatalog { Value = broad }); Stocks(10, 10);
            Check(ResidentialFoodOps.Plan(em, root, house, out plan) && plan[0].Item == 1, "Equal stocks use stable item ID, not catalog order");
            Stocks(11, 10); Check(ResidentialFoodOps.Plan(em, root, house, out plan) && plan[0].Item == 0, "Higher total inventory has first preference");
            Stocks(3, 100); before = Rows<InventorySlot>(em, root); journal = Rows<EconomyEntry>(em, root);
            Check(!RewardOps.ApplyDefinition(em, root, 5) && before.SequenceEqual(Rows<InventorySlot>(em, root)), "Partial reward capacity failure rolls back every item");
            Check(Rows<EconomyEntry>(em, root).Count(r => r.Delta != 0) == journal.Count(r => r.Delta != 0), "Rolled-back reward has no phantom monetary rows");
            using (var payment = new InventoryTransaction(em, root)) { InventoryOps.Remove(em, root, 0, 2); payment.Reject("测试回滚"); }
            Check(before.SequenceEqual(Rows<InventorySlot>(em, root)), "Rejected transaction restores resources and keeps diagnostic only");
            EconomyJournalOps.End(em, root); var length = Rows<EconomyEntry>(em, root).Length; InventoryOps.Remove(em, root, 0, 1);
            Check(Rows<EconomyEntry>(em, root).Length == length, "Manual day actions do not contaminate last-settlement journal");
        }
        static void Map(string path)
        {
            log.AppendLine("MAP " + path); var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128); using var world = new World("Wave three isolated map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var initial = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                var bytes = SnapshotCodec.Capture(em, root); var entities = Entities(em); var state = em.GetComponentData<Session>(root);
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.ForecastEconomy }) == ResultCode.Success, "Forecast command succeeds in day");
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)) && state.Equals(em.GetComponentData<Session>(root)), "Forecast preserves all original entities, inventory, RNG and session");
                var model = Rows<EconomyForecastEntry>(em, root); var modelState = em.GetComponentData<EconomyForecastState>(root);
                Check(modelState.Fingerprint.ToString() == EconomyForecastOps.Fingerprint(em, root), "Forecast is bound to captured day fingerprint");
                var failed = false; try { EconomyForecastOps.Create(em, root, step => throw new IOException("Owned forecast probe")); } catch (IOException) { failed = true; }
                Check(failed && bytes.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)) && model.SequenceEqual(Rows<EconomyForecastEntry>(em, root)), "Forecast failure restores root buffers and prior forecast, no candidate leak");
                var gold = em.GetComponentData<GameSettings>(root).Gold; em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 1 });
                Check(modelState.Fingerprint.ToString() != EconomyForecastOps.Fingerprint(em, root), "Changed day invalidates cached forecast");
                SnapshotCodec.Restore(em, root, initial);
                Check(em.GetComponentData<EconomyForecastState>(root).Fingerprint.IsEmpty, "Loading a node clears transient forecast");
                var itemCount = em.GetComponentData<ContentCatalog>(root).Value.Value.Definitions.Length;
                var counts = Enumerable.Range(0, itemCount).Select(i => InventoryOps.Count(em, root, i) + InventoryOps.PendingCount(em, root, i)).ToArray();
                EconomyOps.Settle(em, root); var entries = Rows<EconomyEntry>(em, root);
                for (var i = 0; i < itemCount; i++) if (Sim.Definition(em, root, i).Kind == ContentKind.Item)
                    Check(entries.Where(r => r.Item == i).Sum(r => (long)r.Delta) == (long)InventoryOps.Count(em, root, i) + InventoryOps.PendingCount(em, root, i) - counts[i], "Actual journal reconciles item " + Sim.Definition(em, root, i).Id);
                Check(em.GetComponentData<EconomyJournalState>(root).Recording == 0 && em.GetComponentData<Session>(root).Turn == state.Turn, "Recording closes and dusk does not increment turn");
                bytes = SnapshotCodec.Capture(em, root); EconomyOps.Settle(em, root);
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Settlement and journal are idempotent");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
                Check(entries.SequenceEqual(Rows<EconomyEntry>(em, root)), "Version-four snapshot preserves actual journal");
                var pending = em.GetBuffer<PendingItem>(root); pending.Add(new PendingItem { Item = gold, Amount = 17 }); EconomyJournalOps.DiscardPending(em, root);
                Check(InventoryOps.PendingCount(em, root, gold) == 0 && Rows<EconomyEntry>(em, root).Any(r => r.Reason == EconomyReason.NightDiscard && r.Delta == -17 && r.Pending == 1), "Confirmed night discard recorded separately from ordinary spending");
                SnapshotCodec.Restore(em, root, initial); Vertical(em, root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void Vertical(EntityManager em, Entity root)
        {
            var original = em.GetComponentData<ContentCatalog>(root).Value;
            var definitions = Enumerable.Range(0, original.Value.Definitions.Length).Select(i => original.Value.Definitions[i]).ToArray();
            var core = Entity.Null; using (var all = Sim.OrderedEntities<Building>(em)) foreach (var e in all) if (em.GetComponentData<BuildingStats>(e).IsCore != 0) core = e;
            var coreDef = em.GetComponentData<Identity>(core).Definition;
            var otherDef = Array.FindIndex(definitions, d => d.Kind == ContentKind.Building && d.Id != definitions[coreDef].Id && d.Size.x > 0 && d.Size.y > 0);
            var grid = em.GetComponentData<GridData>(root); var cell = grid.Value.Value.Min; var found = false;
            for (var y = 0; y < grid.Value.Value.Size.y && !found; y++) for (var x = 0; x < grid.Value.Value.Size.x && !found; x++) { cell = grid.Value.Value.Min + new int2(x, y); found = GridOps.CanPlace(em, root, otherDef, cell, 0); }
            Check(found, "Vertical fixture finds a legal second footprint");
            var second = BuildingOps.Create(em, root, otherDef, cell, 0, 1, true);
            var rules = new Dictionary<int, Rule[]>();
            for (var i = 0; i < definitions.Length; i++) { var d = definitions[i]; rules[i] = Enumerable.Range(d.RuleStart, d.RuleCount).Select(r => original.Value.Rules[r]).ToArray(); }
            var gold = em.GetComponentData<GameSettings>(root).Gold; var wood = Array.FindIndex(definitions, d => d.Kind == ContentKind.Item && d.Id != definitions[gold].Id);
            rules[coreDef] = rules[coreDef].Where(r => r.Kind == RuleKind.Population || r.Kind == RuleKind.Warehouse || r.Kind == RuleKind.Provider || r.Kind == RuleKind.QuestCapacity || r.Kind == RuleKind.Garrison).Concat(new[] { new Rule { Kind = RuleKind.Production, Amount = 1 }, new Rule { Kind = RuleKind.ProductionTier, Target = wood, Amount = 7 } }).ToArray();
            rules[otherDef] = new[] { new Rule { Kind = RuleKind.Maintenance, Target = wood, Amount = 7 }, new Rule { Kind = RuleKind.Production, Amount = 1 }, new Rule { Kind = RuleKind.ProductionTier, Target = gold, Amount = 10 } };
            definitions[otherDef].Duration = 1; foreach (var i in Enumerable.Range(0, definitions.Length)) if (definitions[i].Kind == ContentKind.Item) definitions[i].Loss = 0;
            using var blob = Blob(definitions, rules, original);
            try
            {
                em.SetComponentData(root, new ContentCatalog { Value = blob });
                using (var all = Sim.OrderedEntities<Building>(em)) foreach (var e in all) { var b = em.GetComponentData<Building>(e); b.Workers = b.Population = 0; b.Subsidy = 0; if (e != core && e != second) b.Stage = LifeStage.Ruined; em.SetComponentData(e, b); BuildingOps.ApplyLevel(em, root, e, false); }
                var slots = em.GetBuffer<InventorySlot>(root); for (var i = 0; i < slots.Length; i++) { var s = slots[i]; s.Item = -1; s.Count = 0; s.LossRemainder = 0; slots[i] = s; }
                var day = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success, "Deterministic vertical fixture forecasts");
                var forecast = Rows<EconomyForecastEntry>(em, root).Select(r => r.Value).Where(r => r.Delta != 0).ToArray();
                EconomyOps.Settle(em, root);
                Check(em.GetComponentData<Building>(second).Maintained == 1 && InventoryOps.Count(em, root, wood) == 0 && InventoryOps.Count(em, root, gold) == 10, "Earlier building output funds later maintenance in the same settlement");
                Check(forecast.SequenceEqual(Rows<EconomyEntry>(em, root).Where(r => r.Delta != 0)), "Deterministic forecast monetary rows match actual vertical settlement");
                var secondId = em.GetComponentData<Identity>(second).Id;
                SnapshotCodec.Restore(em, root, day); second = Sim.Find(em, secondId); var construction = em.GetComponentData<Building>(second); construction.Stage = LifeStage.Construction; construction.Progress = 0; em.SetComponentData(second, construction);
                EconomyOps.Settle(em, root);
                Check(em.GetComponentData<Building>(second).Stage == LifeStage.Operational && InventoryOps.Count(em, root, gold) == 0, "Newly completed construction cannot produce until next settlement");
                SnapshotCodec.Restore(em, root, day); second = Sim.Find(em, secondId);
                rules[otherDef] = new[] { new Rule { Kind = RuleKind.Residence, Amount = 2, C = 2, Value = 1 }, new Rule { Kind = RuleKind.Food, Target = wood, Amount = 1, B = 1 }, new Rule { Kind = RuleKind.Environment, B = 20, Amount = 100 }, new Rule { Kind = RuleKind.Tax, Target = gold, Amount = 1, B = 1 } };
                rules[coreDef] = rules[coreDef].Concat(new[] { new Rule { Kind = RuleKind.RareOutput, Target = gold, Amount = 33, Value = .5f, B = 0 } }).ToArray();
                using var residenceBlob = Blob(definitions, rules, original); em.SetComponentData(root, new ContentCatalog { Value = residenceBlob });
                var residents = em.GetComponentData<Building>(second); residents.Population = 1; em.SetComponentData(second, residents); BuildingOps.ApplyLevel(em, root, second, false);
                // Reference mode deliberately excludes rare output; environment blocks growth, not meals.
                EconomyOps.Settle(em, root, true);
                Check(InventoryOps.Count(em, root, wood) == 6 && em.GetComponentData<Building>(second).Population == 1, "Bad environment consumes full meal but blocks population growth");
                var state = em.GetComponentData<Session>(root); state.LastSettledTurn = 0; em.SetComponentData(root, state);
                residents = em.GetComponentData<Building>(second); residents.Population = 2; em.SetComponentData(second, residents);
                EconomyOps.Settle(em, root, true);
                Check(InventoryOps.Count(em, root, gold) == 2 && Rows<EconomyEntry>(em, root).Where(r => r.Reason == EconomyReason.Food).Sum(r => r.Delta) == -2, "Full house taxes after feeding even when environment is insufficient");
                state = em.GetComponentData<Session>(root); state.LastSettledTurn = 0; state.RandomState = 111; em.SetComponentData(root, state);
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success, "Reference forecast supports probabilistic rules");
                var first = Rows<EconomyForecastEntry>(em, root);
                state.RandomState = 222; em.SetComponentData(root, state); EconomyForecastOps.Create(em, root);
                Check(first.SequenceEqual(Rows<EconomyForecastEntry>(em, root)), "Reference output does not disclose the hidden next RNG roll");
                Check(first.Any(r => r.Value.Item == gold && r.Value.Delta == 0 && r.Value.Note.ToString().Contains("概率")), "Rare output is shown as probability rather than guaranteed income");
            }
            finally { em.SetComponentData(root, new ContentCatalog { Value = original }); }
        }
    }
}
#endif
