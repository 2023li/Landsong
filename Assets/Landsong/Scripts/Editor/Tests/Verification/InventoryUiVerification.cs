#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Presentation;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class InventoryUiVerification
    {
        public static void PreparePreview(UI_GamePanel_Inventory view, string mode)
        {
            var woodIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Landsong/Art/Sprites/资源图标/ItemIcon_原木.png");
            bool buildings = mode == "InventoryBuildings";
            view.BuildingsScroll.gameObject.SetActive(buildings); view.ResourcesScroll.gameObject.SetActive(!buildings);
            view.SelectionDetails.text = "点击物资查看详情。建筑模式可跨建筑拖拽，或与待存区互相转移；夜晚只读。";
            view.PendingSummary.text = "待存区 · 2 种 / 25\n可拖入物资；入夜前清空。";
            for (int i = 0; i < 2; i++)
            {
                var pending = UnityEngine.Object.Instantiate(view.PendingTemplate, view.PendingContent);
                pending.Icon.sprite = woodIcon; pending.Label.text = i == 0 ? "原木 × 20" : "石头 × 5"; pending.gameObject.SetActive(true);
            }
            if (buildings)
                for (int i = 0; i < 3; i++)
                {
                    var building = UnityEngine.Object.Instantiate(view.BuildingTemplate, view.BuildingsScroll.content);
                    building.Information.text = (i == 0 ? "王宫" : "仓库 " + i) + "\n已用 3 / 8 格";
                    building.gameObject.SetActive(true);
                    for (int j = 0; j < 8; j++)
                    {
                        var slot = UnityEngine.Object.Instantiate(building.SlotTemplate, building.Slots);
                        slot.Icon.sprite = woodIcon; slot.Label.text = j < 3 ? (20 + j * 5).ToString() : "空"; slot.Icon.gameObject.SetActive(j < 3); slot.gameObject.SetActive(true);
                    }
                }
            else
                for (int i = 0; i < 4; i++)
                {
                    var row = UnityEngine.Object.Instantiate(view.ResourceTemplate, view.ResourcesScroll.content);
                    row.Icon.sprite = woodIcon; row.Information.text = new[] { "原木", "石头", "木板", "金币" }[i] + "  120（+10）";
                    row.gameObject.SetActive(true);
                }
            view.ResourceDetailsRoot.SetActive(mode == "InventoryDetails");
            view.ResourceDetailsTitle.text = "原木 · 第 352 回合资源详情";
            view.ForecastStatus.text = "本回合预计收支 · 随当前条件更新；不含随机收益、手动操作与入夜待存区清空。";
            view.IncomeBody.text = "合计 20\n\n伐木场 · 生产\n原木  +20";
            view.ExpenseBody.text = "合计 30\n\n木板工坊 · 生产\n原木  -18\n\n民居 · 维护\n原木  -10\n\n仓库 · 自然损耗\n原木  -2";

        }

        public static string Run()
        {
            var log = new StringBuilder(); int assertions = 0;
            void Check(bool value, string label)
            { if (!value) throw new InvalidOperationException(label); assertions++; log.AppendLine("PASS " + label); }
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryUiMigration.Path);
                var view = prefab.GetComponent<UI_GamePanel_Inventory>(); view.ValidateConfiguration();
                Check(view is UI_GamePanel_View && !typeof(UI_GamePanel_List).IsAssignableFrom(view.GetType()), "Dedicated inventory uses feature lifetime without generic list rows");
                Check(!view.BuildingTemplate.gameObject.activeSelf && !view.ResourceTemplate.gameObject.activeSelf
                    && !view.PendingTemplate.gameObject.activeSelf && !view.ResourceDetailsRoot.activeSelf, "Templates and ledger start hidden");
                Check(view.ResourceTemplate.Details != null && view.PendingRoot.GetComponent<UI_GamePanel_InventoryPendingDrop>().Owner == view, "Authored ledger and pending drop bindings");
                Check(view.BuildingsScroll.content != view.ResourcesScroll.content && view.PendingContent != view.BuildingsScroll.content, "Three independent content containers");
                var rootView = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab").GetComponent<UI_GamePanel>();
                rootView.InventoryWindow.ValidateConfiguration();
                Check(rootView.FeaturePanels.Count(p => p.PanelId == GamePanelId.Inventory) == 1, "Renamed nested prefab retains its registration");
                using var world = new World("Inventory read models"); var em = world.EntityManager; var root = em.CreateEntity();
                em.AddBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Index = 0, Item = 4, Count = 10 });
                em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 2, Index = 0, Item = 4, Count = 7 });
                em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 3, Index = 0, Item = 4, Count = 30, Unavailable = 1 });
                em.AddBuffer<PendingItem>(root).Add(new PendingItem { Item = 4, Amount = 5 });
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = 6, Amount = 2 });
                var resources = InventoryReadModel.Resources(em, root);
                Check(resources[4].Stored == 17 && resources[4].Pending == 5 && resources[6].Stored == 0 && resources[6].Pending == 2, "Resources combine providers, exclude unavailable stock, and retain pending-only kinds");
                em.AddBuffer<EconomyForecastEntry>(root);
                void Entry(int delta, EconomyReason reason, byte pending = 0, int item = 4, int turn = 9)
                    => em.GetBuffer<EconomyForecastEntry>(root).Add(new EconomyForecastEntry { Value = new EconomyEntry { Turn = turn, Item = item, Delta = delta, Pending = pending, Reason = reason } });
                Entry(20, EconomyReason.Production); Entry(-8, EconomyReason.Production); Entry(-2, EconomyReason.NaturalLoss);
                Entry(-3, EconomyReason.CapacityTransfer); Entry(3, EconomyReason.CapacityTransfer, 1);
                Entry(4, EconomyReason.Tax); Entry(100, EconomyReason.Production, item: 6); Entry(1000, EconomyReason.Production, turn: 8);
                var forecast = ResourceForecastReadModel.Read(em, root, 9);
                Check(forecast[4].Income == 24 && forecast[4].Expense == 10, "Current-turn forecast includes tax and loss but excludes transfers");
                Check(forecast[4].Incomes.Count == 2 && forecast[4].Expenses.Count == 2, "Forecast details partition positive and negative entries");
                Check(forecast.Count == 2 && !forecast.ContainsKey(9), "Forecast keeps different resources separate and absent resources empty");
                Check(ResourceForecastReadModel.Read(em, root, 7).Count == 0, "Other turns cannot leak into current prediction");
                var input = GameUiInputPolicy.Evaluate(true, true, GameUiInputOwner.InventoryDetails, false, false, GamePanelId.Inventory, true);
                Check(!input.CanNavigate && !input.CanWorldInput && !input.CanQueue(CommandKind.MoveInventoryToPending)
                    && input.BackOwner() == GameUiInputOwner.InventoryDetails, "Ledger owns back input and blocks gameplay underneath");
                log.AppendLine("PASS " + DateTime.Now.ToString("O") + " Assertions: " + assertions); return log.ToString();
            }
            catch (Exception error) { log.AppendLine("FAIL " + error); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/inventory-ui-configuration-verification.txt", log.ToString()); }
        }
    }
}
#endif
