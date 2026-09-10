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
    public static class InventoryVerification
    {
        static StringBuilder log; static int assertions;
        static void Check(bool value, string label) { if (!value) throw new InvalidOperationException("FAIL " + label); assertions++; log.AppendLine("PASS " + label); }
        static T[] Rows<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData { using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); return rows.ToArray(); }
        [MenuItem("Landsong/ECS/Verification/Inventory")]
        public static string Run()
        {
            log = new StringBuilder(); assertions = 0;
            try { Fixture(); foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes/EntityMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) Map(path); log.AppendLine("Assertions: " + assertions); return log.ToString(); }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/inventory-verification.txt", log.ToString()); }
        }
        static void Fixture()
        {
            var catalog = ScriptableObject.CreateInstance<GameCatalogAsset>();
            catalog.NightEvents = new[] { NightEventSource.Defaults()[0] }; // Isolated inventory catalog has no battle assets.
            var data = new[] {
                new ContentSource { Id = "a-food", Kind = ContentKind.Item, Group = "food", Capacity = 10, Loss = .1f },
                new ContentSource { Id = "b-wood", Kind = ContentKind.Item, Capacity = 10, Loss = .2f },
                new ContentSource { Id = "food", Kind = ContentKind.ItemGroup, Group = "parent" },
                new ContentSource { Id = "parent", Kind = ContentKind.ItemGroup },
                new ContentSource { Id = "normal", Kind = ContentKind.SlotType, Loss = 1 },
                new ContentSource { Id = "cold", Kind = ContentKind.SlotType, Loss = .5f, Rules = new[] { new RuleSource { Kind = RuleKind.SlotAccept, Target = "parent" } } },
                new ContentSource { Id = "warehouse", Kind = ContentKind.Building, Rules = new[] { new RuleSource { Kind = RuleKind.Warehouse, Target = "cold", Amount = 1 }, new RuleSource { Kind = RuleKind.Warehouse, Target = "normal", Amount = 1, B = 2 }, new RuleSource { Kind = RuleKind.StorageCondition, Amount = 2, Extra = 2, B = 300 } } },
                new ContentSource { Id = "buff", Kind = ContentKind.Buff, Rules = new[] { new RuleSource { Kind = RuleKind.LossModifier, Value = .5f } } },
                new ContentSource { Id = "feature.Inventory", Kind = ContentKind.Feature }
            };
            catalog.Definitions = data.Select(d => { d.Name = d.Id; var asset = ScriptableObject.CreateInstance<GameDefinitionAsset>(); asset.Data = d; return asset; }).ToArray();
            try
            {
                using var blob = GameWorldAuthoring.BuildCatalog(catalog); using var world = new World("Inventory fixture"); var em = world.EntityManager; var root = em.CreateEntity();
                em.AddComponentData(root, new Session { Turn = 1 }); em.AddComponentData(root, new ContentCatalog { Value = blob }); em.AddBuffer<InventorySlot>(root); em.AddBuffer<PendingItem>(root); em.AddBuffer<Entitlement>(root);
                InvitationExpeditionVerification.FixturePermissions(em, root);
                InventorySlot Slot(int index, int item = -1, int count = 0, float debt = 0, int type = 4, byte locked = 0) => new InventorySlot { Provider = 7, Index = index, SlotType = type, Item = item, Count = count, LossRemainder = debt, Unavailable = locked };
                void Reset(params InventorySlot[] values) { em.GetBuffer<InventorySlot>(root).CopyFrom(values); em.GetBuffer<PendingItem>(root).Clear(); }
                ResultCode Move(int from, int to, int item, int amount, string stamp = null) => InventoryOps.LayoutCommand(em, root, new Command { Kind = CommandKind.MoveInventory, Target = 7, Other = 7, SourceSlot = from, DestinationSlot = to, Definition = item, Amount = amount, Text = stamp ?? InventoryOps.Fingerprint(em, root) });
                Check(InventoryOps.Accepts(em, root, 5, 0) && !InventoryOps.Accepts(em, root, 5, 1) && InventoryOps.Accepts(em, root, 4, 1), "Optional slot acceptance matches parent group; default type remains unrestricted");
                Reset(Slot(0, 0, 1), Slot(1, type: 5)); InventoryOps.Add(em, root, 0, 3);
                Check(InventoryOps.Count(em, root, 0) == 4 && em.GetBuffer<InventorySlot>(root)[1].Count == 3, "Better-loss empty slot precedes worse-loss existing stack");
                InventoryOps.Remove(em, root, 0, 1); Check(em.GetBuffer<InventorySlot>(root)[0].Count == 0, "Consumption drains high-loss slot first");
                Reset(Slot(9), Slot(3)); InventoryOps.Add(em, root, 0, 2); Check(em.GetBuffer<InventorySlot>(root)[1].Count == 2, "Equal storage priority uses stable provider/index rather than buffer order");
                Reset(Slot(0, 0, 8, .8f), Slot(1)); Check(Move(0, 1, 0, 3) == ResultCode.Success, "Partial move splits stack");
                Check(math.abs(em.GetBuffer<InventorySlot>(root)[0].LossRemainder - .5f) < .00001f && math.abs(em.GetBuffer<InventorySlot>(root)[1].LossRemainder - .3f) < .00001f, "Split apportions debt with quantities");
                for (var i = 0; i < 100; i++) { Move(1, 0, 0, 1); Move(0, 1, 0, 1); }
                Check(InventoryOps.Count(em, root, 0) == 8 && math.abs(Rows<InventorySlot>(em, root).Sum(s => s.LossRemainder) - .8f) < .0001f, "Repeated movement conserves count and accrued loss");
                Reset(Slot(0, 0, 8, .8f), Slot(1, 0, 8, .4f)); var before = InventoryOps.Fingerprint(em, root);
                Check(Move(0, 1, 0, 3) == ResultCode.NoCapacity && before == InventoryOps.Fingerprint(em, root), "Full destination rejects whole requested transfer without partial mutation");
                Reset(Slot(0, 0, 8, .8f), Slot(1, 1, 2, .2f));
                Check(Move(0, 1, 0, 2) != ResultCode.Success, "Partial cross-item swap rejected");
                Check(Move(0, 1, 0, 8) == ResultCode.Success && em.GetBuffer<InventorySlot>(root)[0].Item == 1 && em.GetBuffer<InventorySlot>(root)[1].LossRemainder == .8f, "Full swap transfers both identities, quantities and debt");
                Reset(Slot(0, 0, 8, .8f, 5), Slot(1, 1, 2)); before = InventoryOps.Fingerprint(em, root);
                Check(Move(0, 1, 0, 8) != ResultCode.Success && before == InventoryOps.Fingerprint(em, root), "Swap checks reverse acceptance before any write");
                Reset(Slot(0, 0, 8), Slot(1, locked: 1)); Check(Move(0, 1, 0, 2) != ResultCode.Success, "Ruined slot refuses transfer");
                Reset(Slot(0, 0, 8), Slot(1)); var stale = InventoryOps.Fingerprint(em, root); InventoryOps.Remove(em, root, 0, 1); before = InventoryOps.Fingerprint(em, root);
                Check(Move(0, 1, 0, 2, stale) != ResultCode.Success && before == InventoryOps.Fingerprint(em, root), "Stale drag cannot move a changed inventory");
                var state = em.GetComponentData<Session>(root); state.Phase = Phase.Night; em.SetComponentData(root, state); Check(Move(0, 1, 0, 2) == ResultCode.WrongPhase, "Layout commands cannot mutate at night"); state.Phase = Phase.Day; em.SetComponentData(root, state);
                Reset(Slot(0, 0, 8, .8f), Slot(1, 0, 2, .2f), Slot(2, type: 5));
                Check(InventoryOps.LayoutCommand(em, root, new Command { Kind = CommandKind.SortInventory, Text = InventoryOps.Fingerprint(em, root) }) == ResultCode.Success, "One-click sort commits");
                Check(em.GetBuffer<InventorySlot>(root)[2].Count == 10 && math.abs(Rows<InventorySlot>(em, root).Sum(s => s.LossRemainder) - 1) < .00001f, "Sort consolidates into lowest-loss storage without erasing debt");
                var sorted = InventoryOps.Fingerprint(em, root); InventoryOps.LayoutCommand(em, root, new Command { Kind = CommandKind.SortInventory, Text = sorted }); Check(sorted == InventoryOps.Fingerprint(em, root), "Sort is idempotent");
                Reset(Slot(0, 0, 8), Slot(1, 1, 10)); em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = 0, Amount = 6, LossRemainder = .6f });
                var command = new Command { Kind = CommandKind.StorePendingSlot, Other = 7, DestinationSlot = 0, Definition = 0, Amount = 3, Text = InventoryOps.Fingerprint(em, root) }; before = InventoryOps.Fingerprint(em, root);
                Check(InventoryOps.LayoutCommand(em, root, command) == ResultCode.NoCapacity && before == InventoryOps.Fingerprint(em, root), "Explicit pending transfer is atomic on insufficient room");
                command.Amount = 2; Check(InventoryOps.LayoutCommand(em, root, command) == ResultCode.Success && InventoryOps.PendingCount(em, root, 0) == 4, "Selected pending quantity enters chosen slot");
                Check(math.abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .4f) < .00001f && math.abs(em.GetBuffer<InventorySlot>(root)[0].LossRemainder - .2f) < .00001f, "Pending partial transfer conserves both debt portions");
                Reset(Slot(0, 0, 8)); em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = 0, Amount = 6, LossRemainder = .6f }); InventoryOps.StoreAllPending(em, root);
                Check(InventoryOps.Count(em, root, 0) == 10 && InventoryOps.PendingCount(em, root, 0) == 4, "Store-all fills only available space and keeps overflow");
                var discarded = new Command { Kind = CommandKind.DiscardPending, Definition = 0, Amount = 1, Text = InventoryOps.Fingerprint(em, root) };
                Check(InventoryOps.LayoutCommand(em, root, discarded) == ResultCode.Success && InventoryOps.LayoutCommand(em, root, discarded) != ResultCode.Success, "Discard confirmation cannot replay against modified pool");
                Reset(Slot(0, 1, 3, .3f)); var warehouse = em.CreateEntity(); em.AddComponentData(warehouse, new Identity { Id = 7, Definition = 6 }); em.AddComponentData(warehouse, new Building { Level = 1, Stage = LifeStage.Operational });
                InventoryOps.Provision(em, root, warehouse);
                Check(em.GetBuffer<InventorySlot>(root)[0].SlotType == 5 && em.GetBuffer<InventorySlot>(root)[0].Count == 0 && InventoryOps.PendingCount(em, root, 1) == 3, "Changed slot type relocates newly incompatible contents to pending");
                Check(math.abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .3f) < .00001f, "Capacity/type reconciliation retains debt");
                var staffing = em.GetComponentData<Building>(warehouse); staffing.Workers = 2; staffing.Maintained = 1; em.SetComponentData(warehouse, staffing);
                Reset(Slot(0, 0, 5, .2f, 5)); em.GetBuffer<Entitlement>(root).Add(new Entitlement { Definition = 7, Level = 1 });
                Check(math.abs(InventoryOps.LossRate(em, root, em.GetBuffer<InventorySlot>(root)[0], 0) - .025f) < .00001f, "Displayed/automatic loss rate shares slot and buff modifiers");
                InventoryOps.ApplyLoss(em, root); Check(math.abs(em.GetBuffer<InventorySlot>(root)[0].LossRemainder - .325f) < .00001f, "Actual loss uses shared quoted rate");
                staffing.Workers = 1; em.SetComponentData(warehouse, staffing);
                Check(math.abs(InventoryOps.LossRate(em, root, em.GetBuffer<InventorySlot>(root)[0], 0) - .05f) < .00001f, "Worker shortage immediately affects shared loss rate");
                staffing.Maintained = 0; em.SetComponentData(warehouse, staffing);
                Check(math.abs(InventoryOps.LossRate(em, root, em.GetBuffer<InventorySlot>(root)[0], 0) - .15f) < .00001f, "Maintenance failure combines with workforce and buff loss modifiers");
                staffing.Workers = 2; staffing.Maintained = 1; em.SetComponentData(warehouse, staffing);
                em.AddComponentData(warehouse, new BuildingStats { JobCapacity = 2 }); em.AddBuffer<NightWave>(root); em.AddComponentData(root, new GameSettings { Gold = 0 });
                Reset(Slot(0, 0, 5, .2f, 5), Slot(1, 1, 6, .6f));
                Check(GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.Workers, Target = 7, Amount = -1 }) == ResultCode.Success && InventoryOps.SlotIndex(em, root, 7, 1) < 0, "Actual worker command immediately removes threshold-dependent slot");
                Check(em.GetBuffer<InventorySlot>(root)[0].Index == 0 && em.GetBuffer<InventorySlot>(root)[0].LossRemainder == .2f && InventoryOps.PendingCount(em, root, 1) == 6 && math.abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .6f) < .00001f, "Capacity shrink preserves surviving key and pending quantity/debt");
                staffing.Workers = 2; em.SetComponentData(warehouse, staffing); InventoryOps.Provision(em, root, warehouse);
                Check(InventoryOps.SlotIndex(em, root, 7, 1) >= 0 && em.GetBuffer<InventorySlot>(root)[1].Count == 0 && InventoryOps.PendingCount(em, root, 1) == 6, "Restored capacity reuses stable key without silently consuming pending pool");
                InventoryOps.StoreAllPending(em, root);
                Check(InventoryOps.PendingCount(em, root, 1) == 0 && math.abs(em.GetBuffer<InventorySlot>(root)[1].LossRemainder - .6f) < .00001f, "Regained capacity can explicitly store overflow with retained debt");
                var lockedSlot = em.GetBuffer<InventorySlot>(root)[0]; lockedSlot.Unavailable = 1; var lockedBuffer = em.GetBuffer<InventorySlot>(root); lockedBuffer[0] = lockedSlot;
                staffing.RuinPending = 1; staffing.Stage = LifeStage.Ruined; em.SetComponentData(warehouse, staffing); before = InventoryOps.Fingerprint(em, root); InventoryOps.Provision(em, root, warehouse);
                Check(before == InventoryOps.Fingerprint(em, root), "Pending night ruin does not relocate already lost stock into safety");
                Reset(Slot(0)); em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = 0, Amount = 0 }); InventoryOps.StoreAllPending(em, root);
                Check(em.GetBuffer<PendingItem>(root).Length == 0, "Empty pending row is safely removed without nonfinite debt");
                Reset(Slot(0, 0, 1)); em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = 0, Amount = 0 });
                Check(InventoryOps.RemoveWithPending(em, root, 0, 1) && em.GetBuffer<PendingItem>(root).Length == 0 && InventoryOps.Count(em, root, 0) == 0, "Repair payment tolerates empty pending row without division by zero");
                data[2].Group = "food"; var rejected = false; try { GameWorldAuthoring.ValidateInventory(catalog); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Baking validation rejects cyclic item groups");
            }
            finally { foreach (var asset in catalog.Definitions) UnityEngine.Object.DestroyImmediate(asset); UnityEngine.Object.DestroyImmediate(catalog); }
        }
        static void Map(string path)
        {
            log.AppendLine("MAP " + path); var scene = EditorSceneManager.OpenPreviewScene(path); using var store = new BlobAssetStore(128); using var world = new World("Inventory baked map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root); InvitationExpeditionVerification.FixturePermissions(em, root);
                var gold = em.GetComponentData<GameSettings>(root).Gold; var slots = em.GetBuffer<InventorySlot>(root); Check(slots.Length >= 2, "Map supplies multiple real inventory slots");
                for (var i = 0; i < slots.Length; i++) { var s = slots[i]; s.Item = -1; s.Count = 0; s.LossRemainder = 0; slots[i] = s; }
                var first = slots[0]; first.Item = gold; first.Count = 8; first.LossRemainder = .8f; slots[0] = first; var second = slots[1];
                var command = new Command { Kind = CommandKind.MoveInventory, Target = first.Provider, SourceSlot = first.Index, Other = second.Provider, DestinationSlot = second.Index, Definition = gold, Amount = 3, Text = InventoryOps.Fingerprint(em, root) };
                Check(GameLoopSystem.Execute(em, root, command) == ResultCode.Success, "Real command processor dispatches stable-key transfer");
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 5, LossRemainder = .5f });
                var snapshot = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root)); var before = InventoryOps.Fingerprint(em, root);
                SnapshotCodec.Restore(em, root, snapshot); Check(before == InventoryOps.Fingerprint(em, root), "Version-five restore retains slot layout and pending debt");
                var invalid = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root)); invalid.Inventory[0].Count = int.MaxValue; var failed = false;
                try { SnapshotCodec.Restore(em, root, invalid); } catch (InvalidDataException) { failed = true; }
                Check(failed && before == InventoryOps.Fingerprint(em, root), "Overfull snapshot is rejected without changing live inventory");
                invalid = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root)); invalid.Pending[0].LossRemainder = float.NaN; failed = false;
                try { SnapshotCodec.Restore(em, root, invalid); } catch (InvalidDataException) { failed = true; }
                Check(failed && before == InventoryOps.Fingerprint(em, root), "Nonfinite pending loss is rejected atomically");
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && before == InventoryOps.Fingerprint(em, root), "New inventory schema works inside forecast transaction");
                var stale = new Command { Kind = CommandKind.DiscardSlot, Target = first.Provider, SourceSlot = first.Index, Definition = gold, Amount = 1, Text = InventoryOps.Fingerprint(em, root) };
                GameLoopSystem.Execute(em, root, new Command { Kind = CommandKind.StorePending }); before = InventoryOps.Fingerprint(em, root);
                Check(GameLoopSystem.Execute(em, root, stale) != ResultCode.Success && before == InventoryOps.Fingerprint(em, root), "Old discard authorization cannot apply after pool storage");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
