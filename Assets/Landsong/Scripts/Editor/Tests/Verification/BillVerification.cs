#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class BillVerification
    {
        static T[] Rows<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData
        { if (!em.HasBuffer<T>(root)) return Array.Empty<T>(); using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); return rows.ToArray(); }
        public static string Run()
        {
            var log = new StringBuilder(); int checks = 0;
            void Check(bool pass, string name) { if (!pass) throw new InvalidOperationException(name); checks++; log.AppendLine("PASS " + name); }
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/GameMaps/Map_Test01/Map_Test01Data/Generated/Map_Test01_Entities.unity");
            using var store = new BlobAssetStore(128); using var world = new World("Historical bills", WorldFlags.Game);
            try
            {
                using (var simple = new World("Bill accounting fixture"))
                {
                    var manager = simple.EntityManager; var owner = manager.CreateEntity();
                    manager.AddComponentData(owner, new EconomyJournalState { Turn = 351 });
                    manager.AddBuffer<InventorySlot>(owner).Add(new InventorySlot { Provider = 1, Item = 0, Count = 500 });
                    manager.GetBuffer<InventorySlot>(owner).Add(new InventorySlot { Provider = 2, Item = 0, Count = 100, Unavailable = 1 });
                    manager.AddBuffer<PendingItem>(owner).Add(new PendingItem { Item = 0, Amount = 12 });
                    manager.AddBuffer<EconomyEntry>(owner);
                    void Entry(int delta, EconomyReason reason, byte pending = 0) => manager.GetBuffer<EconomyEntry>(owner).Add(new EconomyEntry { Turn = 351, Item = 0, Source = 1, Delta = delta, Reason = reason, Pending = pending });
                    Entry(20, EconomyReason.Production); Entry(-23, EconomyReason.Production); Entry(-2, EconomyReason.NaturalLoss);
                    Entry(-12, EconomyReason.CapacityTransfer); Entry(12, EconomyReason.CapacityTransfer, 1);
                    EconomyBillOps.CaptureSettlement(manager, owner);
                    var row = Rows<EconomyBillEntry>(manager, owner).Single(b => b.Source == 0 && b.Item == 0);
                    Check(row.Income == 20 && row.Expense == 25 && row.Income - row.Expense == -5, "Example 20 output, 25 consumption yields minus 5 net; transfers excluded");
                    Check(row.Stored == 500 && row.Pending == 12, "Closing stock excludes locked stock and separates pending");
                    Check(Rows<EconomyBillEntry>(manager, owner).Single(b => b.Source == 1 && b.Item == 0).Expense == 25, "Building bills retain resource cost attribution");
                    manager.SetComponentData(owner, new EconomyJournalState { Turn = 352, Forecast = 1 }); EconomyBillOps.CaptureSettlement(manager, owner);
                    Check(Rows<EconomyBillEntry>(manager, owner).All(b => b.Turn == 351), "Forecast and in-progress results are not archived");
                    manager.SetComponentData(owner, new EconomyJournalState { Turn = 352, Recording = 1 }); EconomyBillOps.CaptureSettlement(manager, owner);
                    Check(Rows<EconomyBillEntry>(manager, owner).All(b => b.Turn == 351), "Partial settlement cannot publish a bill");
                }
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store); var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                int item = em.GetComponentData<GameSettings>(root).Gold;
                var initial = SnapshotCodec.Capture(em, root);
                EconomyOps.Settle(em, root);
                var bills = Rows<EconomyBillEntry>(em, root);
                Check(bills.Any(b => b.Turn == 1 && b.Source == 0 && b.Item == -1), "Actual settlement captures turn marker");
                foreach (var bill in bills.Where(b => b.Item >= 0 && b.Source == 0))
                {
                    var journal = Rows<EconomyEntry>(em, root).Where(e => e.Item == bill.Item && e.Reason != EconomyReason.CapacityTransfer).ToArray();
                    Check(bill.Income == journal.Where(e => e.Delta > 0).Sum(e => (long)e.Delta)
                        && bill.Expense == journal.Where(e => e.Delta < 0).Sum(e => -(long)e.Delta)
                        && bill.Stored == InventoryOps.Count(em, root, bill.Item), "Bill agrees with authoritative settlement for " + bill.Item);
                }
                EconomyOps.Settle(em, root); Check(bills.SequenceEqual(Rows<EconomyBillEntry>(em, root)), "Repeated settlement cannot duplicate history");
                var state = em.GetComponentData<Session>(root); state.Turn = 2; state.Phase = Phase.Day; em.SetComponentData(root, state);
                var bytes = SnapshotCodec.Capture(em, root);
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Forecast preserves all historical bills and live state");
                EconomyOps.Settle(em, root);
                Check(bills.SequenceEqual(Rows<EconomyBillEntry>(em, root).Where(b => b.Turn == 1)) && Rows<EconomyBillEntry>(em, root).Any(b => b.Turn == 2), "Second settlement retains first-turn closing balances");
                bytes = SnapshotCodec.Capture(em, root); var twoTurns = Rows<EconomyBillEntry>(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes)); Check(twoTurns.SequenceEqual(Rows<EconomyBillEntry>(em, root)), "Save/load retains multiple turns and stock snapshots");
                try { using var tx = new RestoreTransaction(em, root); em.GetBuffer<EconomyBillEntry>(tx.Root).Clear(); tx.Commit(step => { if (step == "root-published") throw new IOException("owned probe"); }); } catch (IOException) { }
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Failed staged publication restores bill history atomically");
                var invalid = SnapshotCodec.Decode(em, root, bytes); invalid.Bills = twoTurns.Concat(new[] { twoTurns[0] }).ToArray();
                bool rejected = false; try { SnapshotCodec.Restore(em, root, invalid); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Duplicate bill keys rejected before mutation");
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = item, Amount = 17 });
                long expense = twoTurns.Where(b => b.Source == 0 && b.Turn == 2 && b.Item == item).Sum(b => b.Expense);
                EconomyJournalOps.DiscardPending(em, root);
                Check(Rows<EconomyBillEntry>(em, root).Single(b => b.Source == 0 && b.Turn == 2 && b.Item == item).Expense == expense + 17, "Confirmed pending cleanup updates only that turn bill");
                Check(bills.SequenceEqual(Rows<EconomyBillEntry>(em, root).Where(b => b.Turn == 1)), "Later cleanup does not rewrite previous stock");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, initial)); Check(Rows<EconomyBillEntry>(em, root).Length == 0, "Restoring earlier checkpoint removes future bills");
                var legacy = SnapshotCodec.CaptureLegacyV24ForVerification(em, root);
                var upgraded = SnapshotCodec.Decode(em, root, legacy);
                Check(upgraded.Bills.Length == 0, "Version 24 loads without fabricated historical balances");
                var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BillUiMigration.GamePath).GetComponent<UI_GamePanel>();
                prefab.EconomyWindow.ValidateConfiguration(); prefab.InventoryWindow.ValidateConfiguration();
                Check(prefab.EconomyWindow.name == "账单面板" && prefab.InventoryWindow.ResourceTemplate.Details.name == "btn_详情", "Authored names and new explicit references are retained");
                Check(prefab.InventoryWindow.IncomeScroll != prefab.InventoryWindow.ExpenseScroll && prefab.InventoryWindow.IncomeBody != prefab.InventoryWindow.ExpenseBody, "Income and expenses have independent scroll content");
                var access = GameUiInputPolicy.Evaluate(true, true, GameUiInputOwner.InventoryDetails, false, false, GamePanelId.Inventory, true);
                Check(access.CanQueue(CommandKind.ForecastEconomy) && !access.CanQueue(CommandKind.MoveInventory), "Resource details permits forecast but blocks gameplay");
                log.AppendLine("Assertions: " + checks); return log.ToString();
            }
            catch (Exception e) { log.AppendLine("FAIL " + e); throw; }
            finally { EditorSceneManager.ClosePreviewScene(scene); File.WriteAllText("Library/LandsongEcs/bill-verification.txt", log.ToString()); }
        }
    }
}
#endif
