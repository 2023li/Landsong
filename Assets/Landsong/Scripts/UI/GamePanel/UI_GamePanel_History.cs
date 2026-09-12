using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Text = TMPro.TextMeshProUGUI;
using System.Text;
using Unity.Transforms;
using UnityEngine.EventSystems;
using InputField = TMPro.TMP_InputField;
using Landsong.ECS.Persistence;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_History : Moyo.Unity.UIViewBase, IGameFeatureRenderer
    {
        public void BindFeature(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_RowRenderer rows)
        {
            if (session == null || commands == null || navigation == null || rows == null)
                throw new System.ArgumentException("功能展示器缺少会话服务。");
            sessionController = session;
            rowsController = rows;
            this.navigation = navigation;
        }

        public void Render() => HistoryRows();
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
        internal UI_GamePanel_WorldInteraction worldController;
        [Sirenix.OdinInspector.LabelText("导航面板")]
        public UI_GamePanel_Navigation NavigationPanel;
        [Sirenix.OdinInspector.LabelText("历史工具")]
        public RectTransform HistoryTools;
        [Sirenix.OdinInspector.LabelText("历史筛选")]
        public TMP_InputField HistoryFilter;
        internal int historyCategory = -1;
        internal int historyPage;
        internal int historyFilterTurn;
        internal string historySearch = "";
        internal TMP_InputField historyFilter;
        internal RectTransform historyTools;
        internal void InitializeInterface()
        {
            if (worldController.WorldPresentation == null || NavigationPanel == null || HistoryTools == null || HistoryFilter == null || navigation.InterfaceGroup == null || navigation.InterfaceScaler == null)
                throw new InvalidOperationException("游戏界面通用检查器引用不完整。");
            worldController.worldPresentation = worldController.WorldPresentation;
            NavigationPanel.ValidateConfiguration();
            NavigationPanel.Back.onClick.AddListener(navigation.BackPanel);
            NavigationPanel.History.onClick.AddListener(() => navigation.OpenPanel(GamePanelId.History));
            historyTools = HistoryTools;
            historyFilter = HistoryFilter;
            historyFilter.onValueChanged.AddListener(value =>
            {
                historySearch = value;
                historyPage = 0;
                sessionController.nextRefresh = 0;
            });
            historyTools.gameObject.SetActive(false);
        }

        internal void HistoryRows()
        {
            rowsController.Row("王朝历史 · 按来源快照记录；筛选不删除记录，也不重新结算收益。");
            rowsController.Row("分类：" + (historyCategory < 0 ? "全部" : new[] { "一般消息", "经济收支", "已结算夜战", "重要消息" }[historyCategory]), () =>
            {
                historyCategory = historyCategory == 3 ? -1 : historyCategory + 1;
                historyPage = 0;
                sessionController.nextRefresh = 0;
            });
            rowsController.Row(historyFilterTurn == 0 ? "回合：全部（点击仅看当前回合）" : "回合：" + historyFilterTurn + "（点击查看全部）", () =>
            {
                historyFilterTurn = historyFilterTurn == 0 ? sessionController.em.GetComponentData<Session>(sessionController.root).Turn : 0;
                historyPage = 0;
                sessionController.nextRefresh = 0;
            });
            historyTools.gameObject.SetActive(true);
            var entries = new List<(int turn, string text, Action locate)>();
            var totals = new SortedDictionary<int, (long income, long expense)>();
            if (sessionController.em.HasBuffer<HistoryEntry>(sessionController.root))
                foreach (var h in sessionController.em.GetBuffer<HistoryEntry>(sessionController.root))
                {
                    if (historyCategory >= 0 && (int)h.Category != historyCategory || historyFilterTurn != 0 && h.Turn != historyFilterTurn)
                        continue;
                    var line = $"回合 {h.Turn} · {h.SourceName} · {h.Text}";
                    if (h.Item >= 0)
                        line += $" · {sessionController.Name(h.Item)} {(h.Delta > 0 ? "+" : "")}{h.Delta}（{(h.Pending != 0 ? "待存放" : "库存")}）";
                    if (h.Count > 1)
                        line += " × " + h.Count;
                    if (!string.IsNullOrWhiteSpace(historySearch) && line.IndexOf(historySearch, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (h.Item >= 0 && h.Transfer == 0)
                    {
                        totals.TryGetValue(h.Item, out var total);
                        if (h.Delta > 0)
                            total.income += h.Delta;
                        else
                            total.expense -= (long)h.Delta;
                        totals[h.Item] = total;
                    }

                    var position = (Vector3)h.Position;
                    var source = h.Source;
                    bool locate = h.HasPosition != 0;
                    entries.Add((h.Turn, line, locate ? () => worldController.LocateHistory(source, position) : null));
                }

            if ((historyCategory == -1 || historyCategory == 2) && sessionController.em.HasBuffer<BattleHistoryEntry>(sessionController.root))
            {
                var reports = new SortedDictionary<int, List<BattleReportEntry>>();
                foreach (var h in sessionController.em.GetBuffer<BattleHistoryEntry>(sessionController.root))
                {
                    if (historyFilterTurn != 0 && h.Turn != historyFilterTurn)
                        continue;
                    if (!reports.TryGetValue(h.Turn, out var list))
                        reports.Add(h.Turn, list = new List<BattleReportEntry>());
                    list.Add(h.Entry);
                }

                foreach (var pair in reports)
                    foreach (var line in NightReportOps.Lines(sessionController.em, sessionController.root, pair.Value))
                    {
                        var label = "夜晚 " + pair.Key + " · " + line;
                        if (string.IsNullOrWhiteSpace(historySearch) || label.IndexOf(historySearch, StringComparison.OrdinalIgnoreCase) >= 0)
                            entries.Add((pair.Key, label, null));
                    }
            }

            entries = entries.OrderByDescending(e => e.turn).ToList();
            int pages = Math.Max(1, (entries.Count + 39) / 40);
            historyPage = Mathf.Clamp(historyPage, 0, pages - 1);
            if (totals.Count > 0)
            {
                rowsController.Row("当前筛选的经济历史汇总（转库不作收入/支出；夜战另按完整战报显示）");
                foreach (var t in totals)
                    rowsController.Row(sessionController.Name(t.Key) + " · 收入 +" + t.Value.income + " / 支出 -" + t.Value.expense + " / 净额 " + UI_GamePanel_Economy.Signed(t.Value.income - t.Value.expense));
            }

            rowsController.Row($"第 {historyPage + 1}/{pages} 页 · {entries.Count} 条；一般/经济历史保留最近 {HistoryOps.Limit} 条，夜战按完整回合保留。");
            if (historyPage > 0)
                rowsController.Row("上一页", () =>
                {
                    historyPage--;
                    sessionController.nextRefresh = 0;
                });
            if (historyPage + 1 < pages)
                rowsController.Row("下一页", () =>
                {
                    historyPage++;
                    sessionController.nextRefresh = 0;
                });
            foreach (var entry in entries.Skip(historyPage * 40).Take(40))
                rowsController.Row(entry.text, entry.locate);
            if (entries.Count == 0)
                rowsController.Row("没有匹配记录。");
        }
    }
}
