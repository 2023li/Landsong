#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Landsong.ECS.Definitions;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        static IEnumerator InventoryUi(UI_GamePanel game, EntityManager em, Entity root, ItemId item, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            var view = game.InventoryWindow;
            try
            {
                var buffer = em.GetBuffer<InventorySlot>(root);
                for (int i = 0; i < buffer.Length; i++)
                {
                    var slot = buffer[i];
                    slot.Item = ItemId.None;
                    slot.Count = 0;
                    slot.LossRemainder = 0;
                    buffer[i] = slot;
                }

                em.GetBuffer<PendingItem>(root).Clear();
                var keys = buffer.ToNativeArray(Unity.Collections.Allocator.Temp);
                InventorySlot first, second;
                try
                {
                    var candidates = keys.ToArray().Where(s => s.Unavailable == 0 && InventoryStorage.Accepts(em, root, s.SlotType, item)).ToArray();
                    Require(candidates.Length >= 2, "Inventory UI has compatible real slots");
                    first = candidates[0];
                    second = candidates.FirstOrDefault(s => s.Provider != first.Provider);
                    if (second.Provider == 0)
                    {
                        using var buildings = WorldQueries.OrderedEntities<Building>(em);
                        foreach (var entity in buildings)
                        {
                            ulong provider = em.GetComponentData<Identity>(entity).Id;
                            if (provider == first.Provider)
                                continue;
                            second = new InventorySlot
                            {
                                Provider = provider,
                                Index = 5000,
                                Item = ItemId.None,
                                SlotType = first.SlotType
                            };
                            buffer = em.GetBuffer<InventorySlot>(root);
                            buffer.Add(second);
                            break;
                        }
                    }

                    Require(second.Provider != 0 && second.Provider != first.Provider, "Cross-building UI fixture uses two real building identities");
                }
                finally
                {
                    keys.Dispose();
                }

                first.Item = item;
                first.Count = 8;
                first.LossRemainder = .8f;
                buffer = em.GetBuffer<InventorySlot>(root);
                buffer[InventoryLayout.SlotIndex(em, root, first.Provider, first.Index)] = first;
                game.OpenPanel(GamePanelId.Inventory);
                yield return WaitFor(() => view.ResourceViews.Any(r => r.Item == item), "Resource mode is the default and aggregates inventory");
                Require(!view.ShowingBuildings && view.PendingRoot.gameObject.activeInHierarchy, "Pending remains visible in resource mode");
                view.BuildingsButton.onClick.Invoke();
                yield return WaitFor(() => SlotView(game, first)?.Count == 8, "Building mode binds stable provider/slot identities");
                var buildingView = view.BuildingViews.First(v => v.Provider == first.Provider);
                buildingView.Pin.isOn = true;
                yield return null;
                game.Refresh();
                Require(view.BuildingViews.OrderBy(v => v.transform.GetSiblingIndex()).First().Provider == first.Provider, "Pinned building sorts first");
                var originalSlot = SlotView(game, first);
                game.Refresh();
                Require(SlotView(game, first) == originalSlot, "Unchanged slots retain GameObject identity across refresh");
                DragSlot(SlotView(game, first), SlotView(game, second));
                yield return WaitFor(() => Slot(em, root, second).Count == 8 && SlotView(game, second)?.Count == 8, "Drag moves a stack between displayed building slots");
                Require(Slot(em, root, first).Count == 0 && Mathf.Abs(Slot(em, root, second).LossRemainder - .8f) < .0001f, "Drag conserves count and accrued loss");
                SlotView(game, second).Select.onClick.Invoke();
                view.Quantity.text = "3";
                view.MoveButton.onClick.Invoke();
                SlotView(game, first).Select.onClick.Invoke();
                yield return WaitFor(() => Slot(em, root, first).Count == 3 && SlotView(game, first)?.Count == 3, "Selected quantity splits a stack");
                Require(Slot(em, root, second).Count == 5 && Mathf.Abs(Slot(em, root, first).LossRemainder - .3f) < .0001f, "Split transfers proportional loss");
                var dragData = new PointerEventData(EventSystem.current)
                {
                    pointerDrag = SlotView(game, first).gameObject
                };
                ExecuteEvents.Execute(dragData.pointerDrag, dragData, ExecuteEvents.beginDragHandler);
                ExecuteEvents.Execute(view.PendingRoot.gameObject, dragData, ExecuteEvents.dropHandler);
                ExecuteEvents.Execute(dragData.pointerDrag, dragData, ExecuteEvents.endDragHandler);
                yield return WaitFor(() => InventoryOps.PendingCount(em, root, item) == 3 && view.PendingSlots.Any(s => s.Item == item), "Building drag accepts empty pending-area background");
                Require(Slot(em, root, first).Count == 0 && Mathf.Abs(em.GetBuffer<PendingItem>(root)[0].LossRemainder - .3f) < .0001f, "Outbound transfer retains loss in pending");
                DragSlot(view.PendingSlots.Single(s => s.Item == item), SlotView(game, first));
                yield return WaitFor(() => InventoryOps.PendingCount(em, root, item) == 0 && SlotView(game, first)?.Count == 3, "Pending drag returns to an explicit building slot");
                SlotView(game, first).Select.onClick.Invoke();
                view.Quantity.text = "1";
                view.PendingButton.onClick.Invoke();
                yield return WaitFor(() => InventoryOps.PendingCount(em, root, item) == 1, "Quantity action also supports outbound transfer");
                yield return null;
                game.Refresh();
                view.StoreAllButton.onClick.Invoke();
                yield return WaitFor(() => InventoryOps.PendingCount(em, root, item) == 0, "One-click storage returns pending stock");
                yield return null;
                game.Refresh();
                var occupied = view.StorageSlots.First(s => s.Count >= 2);
                occupied.Select.onClick.Invoke();
                view.Quantity.text = "2";
                view.DiscardButton.onClick.Invoke();
                var before = InventoryLayout.Fingerprint(em, root);
                Require(game.Buildings.BuildingConfirmPanel.activeSelf, "Discard opens a real confirmation");
                ClickIn(game.Buildings.BuildingConfirmRows, "取消");
                Require(before == InventoryLayout.Fingerprint(em, root), "Cancelling discard preserves inventory");
                occupied.Select.onClick.Invoke();
                view.DiscardButton.onClick.Invoke();
                var changedIndex = InventoryLayout.SlotIndex(em, root, occupied.Provider, occupied.Index);
                var changed = em.GetBuffer<InventorySlot>(root)[changedIndex];
                changed.Count++;
                buffer = em.GetBuffer<InventorySlot>(root);
                buffer[changedIndex] = changed;
                ClickIn(game.Buildings.BuildingConfirmRows, "确认");
                yield return WaitFor(() => em.GetBuffer<QueuedGameplayRequest>(root).Length == 0, "Stale discard command processed");
                Require(em.GetBuffer<InventorySlot>(root)[changedIndex].Count == changed.Count, "Changed inventory rejects the reviewed discard");
                yield return null;
                game.Refresh();
                view.StorageSlots.First(s => s.Provider == changed.Provider && s.Index == changed.Index).Select.onClick.Invoke();
                view.Quantity.text = "2";
                view.DiscardButton.onClick.Invoke();
                ClickIn(game.Buildings.BuildingConfirmRows, "确认");
                yield return WaitFor(() => em.GetBuffer<InventorySlot>(root)[changedIndex].Count == changed.Count - 2, "Confirmed discard removes precisely the requested amount");
                long stock = InventoryOps.Count(em, root, item);
                yield return null;
                game.Refresh();
                view.SortButton.onClick.Invoke();
                yield return WaitFor(() => em.GetBuffer<QueuedGameplayRequest>(root).Length == 0, "Sort action processed");
                Require(InventoryOps.Count(em, root, item) == stock, "Sort conserves total stock");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/inventory-buildings.png");
                    yield return new WaitForEndOfFrame();
                    yield return null;
                }

                view.ResourcesButton.onClick.Invoke();
                yield return null;
                game.Refresh();
                yield return WaitFor(() => em.HasComponent<EconomyForecastState>(root) && em.GetComponentData<EconomyForecastState>(root).Fingerprint.ToString() == EconomyForecastOps.Fingerprint(em, root), "Resource mode automatically computes the current day forecast");
                int forecastTurn = em.GetComponentData<GameClock>(root).Turn;
                var predictions = em.GetBuffer<EconomyForecastEntry>(root);
                predictions.Clear();
                predictions.Add(new EconomyForecastEntry { Value = new EconomyEntry { Turn = forecastTurn, Item = item, Delta = 20, Reason = EconomyReason.Production, SourceName = "生产建筑" } });
                predictions.Add(new EconomyForecastEntry { Value = new EconomyEntry { Turn = forecastTurn, Item = item, Delta = -8, Reason = EconomyReason.Production, SourceName = "加工建筑" } });
                predictions.Add(new EconomyForecastEntry { Value = new EconomyEntry { Turn = forecastTurn, Item = item, Delta = -2, Reason = EconomyReason.NaturalLoss, SourceName = "仓库" } });
                game.Refresh();
                Require(view.ResourceViews.First(r => r.Item == item).Information.text.EndsWith("（+10）"), "Resource row shows current stock with current-turn predicted net change");
                view.ResourceViews.First(r => r.Item == item).Details.onClick.Invoke();
                Require(view.ResourceDetailsOpen && view.IncomeBody.text.Contains("合计 20") && view.ExpenseBody.text.Contains("合计 10") && !view.IncomeBody.text.Contains("加工建筑") && view.ExpenseBody.text.Contains("自然损耗"), "Resource details separates current forecast income and expenses");
                Require(!game.InputPolicy.Capture().CanWorldActions && !game.InputPolicy.Capture().CanQueue(CommandKind.MoveInventoryToPending) && game.InputPolicy.Capture().CanQueue(CommandKind.ForecastEconomy), "Details allows forecasting and prevents background gameplay");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/inventory-details.png");
                    yield return new WaitForEndOfFrame();
                    yield return null;
                }

                string priorPrediction = em.GetComponentData<EconomyForecastState>(root).Fingerprint.ToString();
                int liveIndex = InventoryLayout.SlotIndex(em, root, view.StorageSlots.First(s => s.Count > 0).Provider, view.StorageSlots.First(s => s.Count > 0).Index);
                var changedStock = em.GetBuffer<InventorySlot>(root)[liveIndex];
                changedStock.Count++;
                buffer = em.GetBuffer<InventorySlot>(root);
                buffer[liveIndex] = changedStock;
                yield return WaitFor(() => em.GetComponentData<EconomyForecastState>(root).Fingerprint.ToString() != priorPrediction && em.GetComponentData<EconomyForecastState>(root).Fingerprint.ToString() == EconomyForecastOps.Fingerprint(em, root), "An open details popup refreshes after the underlying inventory changes");
                game.HandleBackInput();
                Require(!view.ResourceDetailsOpen && game.IsPanelOpen, "Escape closes only the resource ledger");
                game.Refresh();
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/inventory-resources.png");
                    yield return new WaitForEndOfFrame();
                    yield return null;
                }

                view.BuildingsButton.onClick.Invoke();
                yield return null;
                game.Refresh();
                Require(view.BuildingViews.First(v => v.Provider == first.Provider).Pin.isOn, "Pin survives mode changes");
                var state = em.GetComponentData<Session>(root);
                GameClock stateClock = em.GetComponentData<GameClock>(root);
                var previousPhase = state.Phase;
                state.Phase = Phase.Night;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                }

                try
                {
                    game.Refresh();
                    before = InventoryLayout.Fingerprint(em, root);
                    view.BeginInventoryDrag(view.StorageSlots.First(s => s.Count > 0));
                    view.DropInventoryToPending();
                    Require(!view.IsDragging && !view.StoreAllButton.interactable && before == InventoryLayout.Fingerprint(em, root), "Night disables dragging and storage");
                }
                finally
                {
                    state.Phase = previousPhase;
                    {
                        em.SetComponentData(root, state);
                        em.SetComponentData(root, stateClock);
                    }
                }

                game.ClosePanel();
                Require(!view.ResourceDetailsOpen && !view.IsDragging, "Closing inventory releases popup and drag state");
                {
                    state = em.GetComponentData<Session>(root);
                    stateClock = em.GetComponentData<GameClock>(root);
                }

                stateClock.Turn = 2;
                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateClock);
                }

                EntityState.Buffer<EconomyBillEntry>(em, root);
                var bills = em.GetBuffer<EconomyBillEntry>(root);
                bills.Clear();
                bills.Add(new EconomyBillEntry { Turn = 1, Item = item, Income = 20, Expense = 25, Stored = 500 });
                bills.Add(new EconomyBillEntry { Turn = 2, Item = item, Income = 30, Expense = 21, Stored = 509 });
                game.OpenEconomy();
                yield return WaitFor(() => game.EconomyWindow.TurnViews.Count() == 2, "Bill navigation displays two separate turn tables");
                Require(game.EconomyWindow.ResourceRows.Any(r => r.Net.text == "-5" && r.Stored.text == "500") && game.EconomyWindow.ResourceRows.Any(r => r.Net.text == "+9" && r.Stored.text == "509"), "Bill rows use each turn closing stock rather than today's inventory");
                var billRows = game.EconomyWindow.ResourceRows.ToArray();
                game.Refresh();
                Require(billRows.SequenceEqual(game.EconomyWindow.ResourceRows), "Bill rows remain stable during refresh");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/bill-panel.png");
                    yield return new WaitForEndOfFrame();
                    yield return null;
                }

                game.ClosePanel();
            }
            finally
            {
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            }

            Require(original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Inventory UI fixture restores the entire session");
        }
    }
}
#endif
