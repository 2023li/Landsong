using Landsong.ECS.Definitions;
using Landsong.Content;
using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Policy : GameFeatureViewBase
    {
        [Sirenix.OdinInspector.LabelText("政策显示目录"), Sirenix.OdinInspector.Required]
        public PolicyDisplayCatalog Policies;
        internal IntelligenceViewState intelligence;
        public override void Render() => PolicyRows();
        internal UI_GamePanel_BuildingActionBar buildingController;
        internal bool CourtDay => sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;

        [Sirenix.OdinInspector.LabelText("王室图视图")]
        public UI_GamePanel_CourtGraph CourtGraph;
        UI_GamePanel_CourtGraph courtGraph;
        internal PolicyId policyFocus;
        internal void ResetSession()
        {
            policyFocus = default;
            CourtGraph.ClearSession();
            CourtGraph.gameObject.SetActive(false);
        }

        internal void PolicyRows()
        {
            rowsController.Row("当前民意：" + sessionController.em.GetComponentData<PublicOpinionState>(sessionController.root).Value + " / 100");
            rowsController.Row("同组同层只能选一项。民意是门槛，不扣除；不足时停用，恢复后自动生效。");
            for (int index = 0; index < PolicyDefinitions.Count(sessionController.em, sessionController.root); index++)
            {
                var i = PolicyId.FromIndex(index);
                ref var d = ref PolicyDefinitions.Get(sessionController.em, sessionController.root, i);
                bool chosen = false;
                foreach (var p in sessionController.em.GetBuffer<PolicyChoice>(sessionController.root))
                    if (p.Definition == i)
                        chosen = true;
                rowsController.Row((policyFocus == i ? "▶ " : "") + d.Metadata.Name + " · 民意需 " + d.RequiredPublicOpinion + " · " + (chosen ? (CourtOps.PolicyActive(sessionController.em, sessionController.root, i) ? "已生效" : "已选，条件不足暂停") : "未选"));
                var effects = new List<string>();
                DefinitionEffectText.Append(effects, sessionController.em, sessionController.root, ref d.Effects);
                foreach (var effect in effects)
                    rowsController.Row(effect);
                foreach (var prerequisite in DefinitionPrerequisiteText.Lines(sessionController.em, sessionController.root, ref d.Prerequisites))
                    rowsController.Row(prerequisite);
                rowsController.Row("采用：" + d.Metadata.Name, CourtDay && !chosen ? () => commandsController.TryQueue(new SelectPolicyRequest { Policy = i }) : null);
                rowsController.Row("取消：" + d.Metadata.Name, CourtDay && chosen ? () => commandsController.TryQueue(new CancelPolicyRequest { Policy = i }) : null);
            }
        }

        internal void RefreshPresentation()
        {
            bool active = navigation.IsPanelOpen && !intelligence.IsOpen && navigation.Panel == GamePanelId.Policy;
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
            for (int index = 0; index < PolicyDefinitions.Count(sessionController.em, sessionController.root); index++)
            {
                var i = PolicyId.FromIndex(index);
                ref var d = ref PolicyDefinitions.Get(sessionController.em, sessionController.root, i);
                bool chosen = false;
                foreach (var choice in sessionController.em.GetBuffer<PolicyChoice>(sessionController.root))
                    if (choice.Definition == i)
                        chosen = true;
                int col = Mathf.Max(0, d.PolicyTier - 1);
                rows.TryGetValue(col, out int row);
                rows[col] = row + 1;
                var source = Policies.Get(i);
                var card = new CourtCard
                {
                    Id = (ulong)i.Index + 1,
                    Column = col,
                    Row = row,
                    Title = d.Metadata.Name.ToString(),
                    Detail = "民意需 " + d.RequiredPublicOpinion + "\n" + (chosen ? (CourtOps.PolicyActive(sessionController.em, sessionController.root, i) ? "已生效" : "条件不足，暂停") : "未采用") + "\n" + (!PrerequisiteEvaluation.Satisfied(sessionController.em, sessionController.root, ref d.Prerequisites) ? "缺少前置" : "点击定位操作"),
                    Portrait = source?.Icon,
                    Selected = policyFocus == i,
                    Click = () =>
                    {
                        policyFocus = i;
                        refresh.NextPanel = 0;
                        rowsController.PrimaryScroll.verticalNormalizedPosition = 1;
                    }
                };
                cards.Add(card);
            }

            courtGraph.Show("政策 · 分层展示；左侧操作沿用原规则", cards);
        }
    }
}
