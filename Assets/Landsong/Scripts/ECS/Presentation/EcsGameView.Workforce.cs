using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        WorkforceScaleView workforceScale;
        bool showSupplySources, showAttractionSources, showSpatialSources;
        void WorkforceDetails(Entity entity, bool editable)
        {
            var id = em.GetComponentData<Identity>(entity); var q = WorkforceOps.Quote(em, root, entity);
            void Detail(string label, Action action = null) => Row(label, action, parent: BuildingDetailsRows);
            Detail($"岗位 · 实际 {q.Workers}/{q.Capacity} · 当前可稳定 {q.CurrentStable} · 目标 {q.Target} · 付款后预计稳定 {q.PlannedStable}");
            Detail($"每回合预计补贴：-{q.SubsidyCost} {Name(q.Gold)} · 当前正常库存 {q.Stock}" + (q.Stock < q.SubsidyCost ? "（不足，付款失败不会获得加成）" : ""));
            Detail(q.PaidTurn > 0 ? $"最近白天 {q.PaidTurn} 已付补贴：{q.Paid} {Name(q.Gold)}" : "尚未支付过岗位补贴");
            Detail("目标只设置未来补贴，不立即扣钱或招人。低于环境稳定人数时不补贴，也不会强制裁员。结算先维护再算补贴，实际费用可能变化。");
            var row = Row("", parent: BuildingDetailsRows); var child = row.transform.Find("WorkforceScale");
            if (child == null)
            {
                var go = new GameObject("WorkforceScale", typeof(RectTransform), typeof(WorkforceScaleView)); go.transform.SetParent(row.transform, false); child = go.transform;
                var rect = (RectTransform)child; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
                go.GetComponent<WorkforceScaleView>().Initialize(RowTemplate.GetComponentInChildren<Text>().font);
            }
            child.gameObject.SetActive(true); workforceScale = child.GetComponent<WorkforceScaleView>();
            row.GetComponent<LayoutElement>().preferredHeight = row.GetComponent<LayoutElement>().minHeight = 100;
            workforceScale.Bind(q, editable && !q.Locked, value => Send(CommandKind.WorkforceTarget, id.Id, amount: value));
            Detail("绿：环境稳定范围；黄：目标补贴；橙：整数金币产生的溢出；青线：实际工人；白柄/数字：目标人数。刻度按真实人数定位。");
            Detail($"当前招募 1 人：{q.RecruitCost} {Name(q.Gold)} · 空闲人口 {q.FreePopulation}");
            Detail("招募 1 名工人", editable && WorkforceOps.CanChange(q, 1) == ResultCode.Success ? () => Send(CommandKind.Workers, id.Id, amount: 1, argument: q.RecruitCost, text: "workforce-quote") : null);
            if (WorkforceOps.CanChange(q, 1) != ResultCode.Success) Detail("招工限制：" + WorkforceOps.Reason(q, 1));
            Detail("释放 1 名工人", editable && WorkforceOps.CanChange(q, -1) == ResultCode.Success ? () => Send(CommandKind.Workers, id.Id, amount: -1) : null);
            Detail(q.Locked ? "远征在途：人数与补贴目标锁定。" : $"自然招入/离职最多每次结算 1 人；当前离职保护剩余 {q.ProtectionTurns} 次结算。普通岗位和军事单位均占人口，不能挪用军事人口。");
            Detail(showAttractionSources ? "收起吸引力来源" : "展开吸引力来源", () => { showAttractionSources = !showAttractionSources; nextRefresh = 0; });
            if (showAttractionSources)
            {
                foreach (var source in q.Sources)
                {
                    var s = source; var label = s.Building != 0 ? EntityName(s.Building) : s.Definition >= 0 ? Name(s.Definition) : "";
                    Detail($"{s.Label} · {label}：{s.Value:+0.##;-0.##;0}", s.Building != 0 ? () => FocusBuilding(s.Building) : null);
                }
                Detail($"未截断合计 {q.Raw:0.##} → 环境吸引力 {q.Natural:0.##}（0～100）\n已付补贴加成 {q.Paid * q.PerGold:0.##} → 当前 {q.Current:0.##}；计划付款后 {q.Planned:0.##}");
            }
        }
        void RepairDetails(Entity entity, Action<string, Action> detail)
        {
            var q = BuildingCostOps.QuoteRepair(em, root, entity);
            detail((em.GetComponentData<Building>(entity).Stage == LifeStage.Repairing ? "已冻结修复总额：" : "拟定修复总额：") + CostText(q.Total) + " · 尚需 " + CostText(q.Remaining), null);
            detail($"下一期 {q.Step + 1}/{q.Duration}：{q.Reason}", null);
            foreach (var p in q.Payments) detail($"{Name(p.Item)}：本期需 {p.Required} = 待存放 {p.Pending} + 正常库存 {p.Normal}；正常可用 {p.Available}；缺口 {p.Missing}", null);
            detail("以上为当前资源预览，不预留材料；同回合较早结算的建筑仍可能先用这些物资。修复开始只冻结计划，不立即付款。", null);
        }
        void SupplyDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSupplySources ? "收起供给来源" : "展开供给来源", () => { showSupplySources = !showSupplySources; nextRefresh = 0; });
            if (!showSupplySources) return;
            var q = ResourceNetworkOps.Quote(em, root, entity);
            detail("先比较提供点优先级，再比较道路加权距离，同值按稳定建筑 ID。提供点用于连接/市场归因，材料仍从全城正常库存扣除。", null);
            foreach (var c in q.Candidates)
            {
                var candidate = c; detail($"{EntityName(c.Id)} · 优先级 {c.Priority} · 路径成本 {(float.IsInfinity(c.Cost) ? "不可达" : c.Cost.ToString("0.##"))} · {(q.Selected == c.Entity ? "已选中" : c.Reason)}", () => FocusBuilding(candidate.Id));
            }
            if (q.Candidates.Count == 0) detail("没有其他资源提供点。", null);
        }
        void SpatialDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSpatialSources ? "收起空间效果来源" : "展开空间效果来源", () => { showSpatialSources = !showSpatialSources; nextRefresh = 0; });
            if (!showSpatialSources) return;
            foreach (var kind in new[] { 10, 20, 30, 40 })
            {
                var q = SpatialOps.Quote(em, root, entity, kind); detail(EnvironmentName(kind) + " · 实际合计 " + q.Value, null);
                foreach (var source in q.Sources) { var s = source; detail($"{EntityName(s.Source)} · {s.Group} · 配置 {s.Amount} / 计入 {s.Applied} · {s.Reason}", () => FocusBuilding(s.Source)); }
            }
        }
    }
}
