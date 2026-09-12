using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Policy : Moyo.Unity.UIViewBase, IGameFeatureRenderer
    {
        public void BindFeature(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_RowRenderer rows)
        {
            if (session == null || commands == null || navigation == null || rows == null)
                throw new System.ArgumentException("功能展示器缺少会话服务。");
            sessionController = session;
            rowsController = rows;
            commandsController = commands;
            this.navigation = navigation;
        }

        public void Render() => PolicyRows();
        internal GameUiSession sessionController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_RowRenderer rowsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_Building buildingController;
        internal bool CourtDay => sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;

        [Sirenix.OdinInspector.LabelText("王室图视图")]
        public UI_GamePanel_CourtGraph CourtGraph;
        UI_GamePanel_CourtGraph courtGraph;
        internal int policyFocus = -1;
        internal void ResetSession()
        {
            policyFocus = -1;
            CourtGraph.ClearSession();
            CourtGraph.gameObject.SetActive(false);
        }

        internal void PolicyRows()
        {
            rowsController.Row("当前民意：" + sessionController.em.GetComponentData<Session>(sessionController.root).PublicOpinion + " / 100");
            rowsController.Row("同组同层只能选一项。民意是门槛，不扣除；不足时停用，恢复后自动生效。");
            sessionController.ForDefinitions(ContentKind.Policy, (i, d) =>
            {
                bool chosen = false;
                foreach (var p in sessionController.em.GetBuffer<PolicyChoice>(sessionController.root))
                    if (p.Definition == i)
                        chosen = true;
                rowsController.Row((policyFocus == i ? "▶ " : "") + d.Name + " · 民意需 " + d.Cost + " · " + (chosen ? (CourtOps.PolicyActive(sessionController.em, sessionController.root, i) ? "已生效" : "已选，条件不足暂停") : "未选"));
                for (int n = 0; n < d.RuleCount; n++)
                {
                    var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + n);
                    if (r.Kind == RuleKind.PlotRisk)
                        rowsController.Row("弑君风险修正 " + r.Value.ToString("P0"));
                    if (r.Kind == RuleKind.ProductionBonus)
                        rowsController.Row("生产修正 " + r.Value.ToString("P0"));
                }

                rowsController.Row("采用：" + d.Name, CourtDay && !chosen ? () => commandsController.TryQueue(CommandRequests.SelectPolicy(i, true)) : null);
                rowsController.Row("取消：" + d.Name, CourtDay && chosen ? () => commandsController.TryQueue(CommandRequests.SelectPolicy(i, false)) : null);
            });
        }

        internal void RefreshPresentation()
        {
            bool active = navigation.IsPanelOpen && !sessionController.intel && navigation.Panel == GamePanelId.Policy;
            CourtGraph.gameObject.SetActive(active);
            if (!active)
                return;
            if (courtGraph == null)
            {
                courtGraph = CourtGraph;
                courtGraph.BindActions(navigation.ClosePanel);
            }

            var cards = new List<CourtCard>();
            var rows = new Dictionary<int, int>();
            var catalog = PresentationRuntime.Instance?.Catalog;
            sessionController.ForDefinitions(ContentKind.Policy, (i, d) =>
            {
                bool chosen = false;
                foreach (var choice in sessionController.em.GetBuffer<PolicyChoice>(sessionController.root))
                    if (choice.Definition == i)
                        chosen = true;
                int col = Mathf.Max(0, d.Level - 1);
                rows.TryGetValue(col, out int row);
                rows[col] = row + 1;
                var source = buildingController.BuildingSource(i);
                var card = new CourtCard
                {
                    Id = (ulong)i + 1,
                    Column = col,
                    Row = row,
                    Title = d.Name.ToString(),
                    Detail = "民意需 " + d.Cost + "\n" + (chosen ? (CourtOps.PolicyActive(sessionController.em, sessionController.root, i) ? "已生效" : "条件不足，暂停") : "未采用") + "\n" + (!ConditionOps.Prerequisites(sessionController.em, sessionController.root, i) ? "缺少前置" : "点击定位操作"),
                    Portrait = source?.Icon,
                    Selected = policyFocus == i,
                    Click = () =>
                    {
                        policyFocus = i;
                        sessionController.nextRefresh = 0;
                        navigation.ActiveListPanel.PrimaryScroll.verticalNormalizedPosition = 1;
                    }
                };
                for (int n = 0; n < d.RuleCount; n++)
                {
                    var rule = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + n);
                    if (rule.Kind == RuleKind.Prerequisite && Sim.ValidDefinition(sessionController.em, sessionController.root, rule.Target) && Sim.Definition(sessionController.em, sessionController.root, rule.Target).Kind == ContentKind.Policy)
                    {
                        if (card.Parent == 0)
                            card.Parent = (ulong)rule.Target + 1;
                        else
                            card.SecondParent = (ulong)rule.Target + 1;
                    }
                }

                cards.Add(card);
            });
            courtGraph.Show("政策 · 分层展示；左侧操作沿用原规则", cards);
        }
    }
}
