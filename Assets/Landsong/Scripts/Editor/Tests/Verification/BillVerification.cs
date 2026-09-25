#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
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
        static T[] Rows<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(root))
                return Array.Empty<T>();
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        public static string Run()
        {
            var log = new StringBuilder();
            int checks = 0;
            void Check(bool pass, string name)
            {
                if (!pass)
                    throw new InvalidOperationException(name);
                checks++;
                log.AppendLine("PASS " + name);
            }

            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Historical bills", WorldFlags.Game);
            try
            {
                using (var simple = new World("Bill accounting fixture"))
                {
                    var manager = simple.EntityManager;
                    var owner = manager.CreateEntity();
                    manager.AddComponentData(owner, new EconomyJournalState { Turn = 351 });
                    manager.AddBuffer<InventorySlot>(owner).Add(new InventorySlot { Provider = 1, Item = ItemId.FromIndex(0), Count = 500 });
                    manager.GetBuffer<InventorySlot>(owner).Add(new InventorySlot { Provider = 2, Item = ItemId.FromIndex(0), Count = 100, Unavailable = 1 });
                    manager.AddBuffer<PendingItem>(owner).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = 12 });
                    manager.AddBuffer<EconomyEntry>(owner);
                    void Entry(int delta, EconomyReason reason, byte pending = 0) => manager.GetBuffer<EconomyEntry>(owner).Add(new EconomyEntry { Turn = 351, Item = ItemId.FromIndex(0), Source = 1, Delta = delta, Reason = reason, Pending = pending });
                    Entry(20, EconomyReason.Production);
                    Entry(-23, EconomyReason.Production);
                    Entry(-2, EconomyReason.NaturalLoss);
                    Entry(-12, EconomyReason.CapacityTransfer);
                    Entry(12, EconomyReason.CapacityTransfer, 1);
                    EconomyBillOps.CaptureSettlement(manager, owner);
                    var row = Rows<EconomyBillEntry>(manager, owner).Single(b => b.Source == 0 && b.Item == ItemId.FromIndex(0));
                    Check(row.Income == 20 && row.Expense == 25 && row.Income - row.Expense == -5, "Example 20 output, 25 consumption yields minus 5 net; transfers excluded");
                    Check(row.Stored == 500 && row.Pending == 12, "Closing stock excludes locked stock and separates pending");
                    Check(Rows<EconomyBillEntry>(manager, owner).Single(b => b.Source == 1 && b.Item == ItemId.FromIndex(0)).Expense == 25, "Building bills retain resource cost attribution");
                    manager.SetComponentData(owner, new EconomyJournalState { Turn = 352, Forecast = 1 });
                    EconomyBillOps.CaptureSettlement(manager, owner);
                    Check(Rows<EconomyBillEntry>(manager, owner).All(b => b.Turn == 351), "Forecast and in-progress results are not archived");
                    manager.SetComponentData(owner, new EconomyJournalState { Turn = 352, Recording = 1 });
                    EconomyBillOps.CaptureSettlement(manager, owner);
                    Check(Rows<EconomyBillEntry>(manager, owner).All(b => b.Turn == 351), "Partial settlement cannot publish a bill");
                }

                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var item = em.GetComponentData<CurrencySettings>(root).Gold;
                var initial = SnapshotCodec.Capture(em, root);
                DailyEconomySettlement.Settle(em, root);
                var bills = Rows<EconomyBillEntry>(em, root);
                Check(bills.Any(b => b.Turn == 1 && b.Source == 0 && b.Item == ItemId.None), "Actual settlement captures turn marker");
                foreach (var bill in bills.Where(b => b.Item.IsValid && b.Source == 0))
                {
                    var journal = Rows<EconomyEntry>(em, root).Where(e => e.Item == bill.Item && e.Reason != EconomyReason.CapacityTransfer).ToArray();
                    Check(bill.Income == journal.Where(e => e.Delta > 0).Sum(e => (long)e.Delta) && bill.Expense == journal.Where(e => e.Delta < 0).Sum(e => -(long)e.Delta) && bill.Stored == InventoryOps.Count(em, root, bill.Item), "Bill agrees with authoritative settlement for " + bill.Item);
                }

                DailyEconomySettlement.Settle(em, root);
                Check(bills.SequenceEqual(Rows<EconomyBillEntry>(em, root)), "Repeated settlement cannot duplicate history");
                var state = em.GetComponentData<Session>(root);
                GameClock stateClock = em.GetComponentData<GameClock>(root);
                stateClock.Turn = 2;
                state.Phase = Phase.Day;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                }
                SeasonWeatherOps.Dawn(em, root);

                var bytes = SnapshotCodec.Capture(em, root);
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Forecast preserves all historical bills and live state");
                DailyEconomySettlement.Settle(em, root);
                Check(bills.SequenceEqual(Rows<EconomyBillEntry>(em, root).Where(b => b.Turn == 1)) && Rows<EconomyBillEntry>(em, root).Any(b => b.Turn == 2), "Second settlement retains first-turn closing balances");
                bytes = SnapshotCodec.Capture(em, root);
                var twoTurns = Rows<EconomyBillEntry>(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
                Check(twoTurns.SequenceEqual(Rows<EconomyBillEntry>(em, root)), "Save/load retains multiple turns and stock snapshots");
                try
                {
                    using var tx = new RestoreTransaction(em, root);
                    em.GetBuffer<EconomyBillEntry>(tx.Root).Clear();
                    tx.Commit(step =>
                    {
                        if (step == "root-published")
                            throw new IOException("owned probe");
                    });
                }
                catch (IOException)
                {
                }

                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Failed staged publication restores bill history atomically");
                var invalid = SnapshotCodec.Decode(em, root, bytes);
                invalid.Bills = twoTurns.Concat(new[] { twoTurns[0] }).ToArray();
                bool rejected = false;
                try
                {
                    SnapshotCodec.Restore(em, root, invalid);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }

                Check(rejected && bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Duplicate bill keys rejected before mutation");
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = item, Amount = 17 });
                long expense = twoTurns.Where(b => b.Source == 0 && b.Turn == 2 && b.Item == item).Sum(b => b.Expense);
                EconomyJournalOps.DiscardPending(em, root);
                Check(Rows<EconomyBillEntry>(em, root).Single(b => b.Source == 0 && b.Turn == 2 && b.Item == item).Expense == expense + 17, "Confirmed pending cleanup updates only that turn bill");
                Check(bills.SequenceEqual(Rows<EconomyBillEntry>(em, root).Where(b => b.Turn == 1)), "Later cleanup does not rewrite previous stock");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, initial));
                Check(Rows<EconomyBillEntry>(em, root).Length == 0, "Restoring earlier checkpoint removes future bills");
                var prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(BillUiAssets.GamePath).GetComponent<UI_GamePanel>();
                prefab.HistoryWindow.ValidateConfiguration();
                prefab.InventoryWindow.ValidateConfiguration();
                Check(prefab.HistoryWindow.name == "历史面板" && prefab.InventoryWindow.ResourceTemplate.Details.name == "btn_详情", "Rebuilt history panel and inventory retain explicit references");
                Check(prefab.FeaturePanels.Count(panel => panel.PanelId == GamePanelId.History) == 1 && prefab.FeaturePanels.All(panel => panel.PanelId != GamePanelId.Economy), "Old history list and standalone bill registration are removed");
                var turn = new UI_GamePanel_History.TurnRecord();
                turn.Economy.Add(("石头", new EconomyBillEntry { Income = 20, Expense = 25, Stored = 500 }));
                turn.Events.Add(new HistoryEntry { Text = new FixedString128Bytes("新君即位") , SourceName = new FixedString128Bytes("继承人") });
                turn.Battle.Add(new BattleReportEntry { Kind = EventKind.SoldierDeath, SourceName = new FixedString128Bytes("守城新兵"), Amount = 1 });
                turn.Battle.Add(new BattleReportEntry { Kind = EventKind.Ruin, SourceName = new FixedString128Bytes("城门"), Amount = 1 });
                var body = UI_GamePanel_History.FormatTurn(turn);
                Check(body.Contains("石头  本回合库存量 500  本回合变化量 -5") && body.Contains("继承人") && body.Contains("守城新兵") && body.Contains("城门"), "Turn history combines economy snapshots, events and named battle losses");
                var royalEvents = UI_GamePanel_History.EventLines(new[]
                {
                    new HistoryEntry { Text = new FixedString128Bytes("王室成员自然逝世"), SourceName = new FixedString128Bytes("王子") },
                    new HistoryEntry { Text = new FixedString128Bytes("君王自然逝世"), SourceName = new FixedString128Bytes("先王") },
                    new HistoryEntry { Text = new FixedString128Bytes("新君即位，继承结果已结算"), SourceName = new FixedString128Bytes("继承人") }
                });
                Check(royalEvents.Count == 2 && royalEvents[0].Contains("王子") && royalEvents[1] == "先王国王驾崩，由继承人继位", "Only a monarch death is paired with succession");
                Check(UI_GamePanel_History.BattleLines(new[] { new BattleReportEntry { Kind = EventKind.NightClosure } }).Single() == "是个平安夜", "Peaceful archived night has a concise report");
                Check(prefab.InventoryWindow.IncomeScroll != prefab.InventoryWindow.ExpenseScroll && prefab.InventoryWindow.IncomeBody != prefab.InventoryWindow.ExpenseBody, "Income and expenses have independent scroll content");
                var access = GameUiInputPolicy.Evaluate(true, true, GameUiInputOwner.InventoryDetails, false, false, GamePanelId.Inventory, true);
                Check(access.CanQueue(CommandKind.ForecastEconomy) && !access.CanQueue(CommandKind.MoveInventory), "Resource details permits forecast but blocks gameplay");
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception e)
            {
                log.AppendLine("FAIL " + e);
                throw;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                File.WriteAllText("Library/LandsongEcs/bill-verification.txt", log.ToString());
            }
        }
    }
}
#endif
