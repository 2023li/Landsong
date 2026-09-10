using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        int expeditionDestination = -1, expeditionCrew = 10;
        int[] expeditionAmounts;
        ulong expeditionCaptain;
        readonly Dictionary<string, Button> featureButtons = new Dictionary<string, Button>();
        void InitializeFeatureButtons()
        {
            foreach (var button in GetComponentInParent<Canvas>().GetComponentsInChildren<Button>(true))
            {
                var label = button.GetComponentInChildren<Text>(true)?.text;
                if (label != "建筑" && label != "库存" && label != "远征") continue;
                for (var i = 0; i < button.onClick.GetPersistentEventCount(); i++) if (button.onClick.GetPersistentTarget(i) == this && button.onClick.GetPersistentMethodName(i) == "OpenPanel") featureButtons[label] = button;
            }
        }
        static string PanelFeature(string panel) => panel == "库存" ? "Inventory" : panel == "远征" ? "Expedition" : panel == "建筑" ? "Building" : null;
        void RefreshFeatureAccess()
        {
            foreach (var entry in featureButtons) entry.Value.interactable = FeatureOps.Unlocked(em, root, PanelFeature(entry.Key));
            if ((Panel == "库存" || Panel == "远征") && !FeatureOps.Unlocked(em, root, PanelFeature(Panel))) ClosePanel();
        }
        void Expeditions()
        {
            if (!FeatureOps.Unlocked(em, root, "Expedition")) { Row("远征许可尚未解锁"); return; }
            Row("远征所 · 选择出发建筑");
            using (var sites = Sim.OrderedEntities<Building>(em)) foreach (var e in sites)
            {
                var id = em.GetComponentData<Identity>(e); var b = em.GetComponentData<Building>(e);
                if (Sim.Rule(em, root, id.Definition, RuleKind.ExpeditionSite, b.Level).Level < 0) continue;
                Row((selected == id.Id ? "▶ " : "") + id.Name + " LV" + b.Level + " · 工人 " + b.Workers + " / 稳定岗位 " + b.StableWorkers, () => { selected = id.Id; nextRefresh = 0; });
            }
            var site = Sim.Find(em, selected);
            ForDefinitions(ContentKind.Expedition, (i, d) =>
            {
                var quote = ExpeditionOps.Quote(em, root, site, i, expeditionCrew); if (!quote.Visible) return;
                Row((expeditionDestination == i ? "▶ " : "") + d.Name + " · 需驻地 LV" + d.Level + " · " + d.Duration + " 回合", () => { expeditionDestination = i; expeditionAmounts = null; nextRefresh = 0; });
            });
            if (expeditionDestination >= 0 && site != Entity.Null && em.HasComponent<Building>(site))
            {
                var q = ExpeditionOps.Quote(em, root, site, expeditionDestination, expeditionCrew, expeditionAmounts);
                if (expeditionAmounts == null) { expeditionAmounts = new int[q.Options.Count]; for (var i = 0; i < expeditionAmounts.Length; i++) expeditionAmounts[i] = q.Options[i].Amount; }
                Row("出征人数 " + expeditionCrew + " · 允许 " + q.Minimum + "～" + q.Maximum + "（当前工人 " + q.Workers + "，稳定岗位 " + q.StableWorkers + "）");
                Row("人数 −1", expeditionCrew > q.Minimum ? () => { expeditionCrew--; nextRefresh = 0; } : null);
                Row("人数 +1", expeditionCrew < q.Maximum ? () => { expeditionCrew++; nextRefresh = 0; } : null);
                Row("按可用人数填满", q.Maximum >= q.Minimum ? () => { expeditionCrew = q.Maximum; nextRefresh = 0; } : null);
                if (q.Options.Count == 0) Row("此目的地未要求补给");
                for (var i = 0; i < q.Options.Count; i++)
                {
                    var at = i; var option = q.Options[i];
                    Row(Name(option.Target) + "：携带 " + expeditionAmounts[i] + " / 最低 " + option.Amount + " / 上限 " + ExpeditionOps.SupplyMaximum(option) + " / 持有 " + InventoryOps.Count(em, root, option.Target));
                    Row("补给 −1：" + Name(option.Target), expeditionAmounts[i] > option.Amount ? () => { expeditionAmounts[at]--; nextRefresh = 0; } : null);
                    Row("补给 +1：" + Name(option.Target), expeditionAmounts[i] < ExpeditionOps.SupplyMaximum(option) ? () => { expeditionAmounts[at]++; nextRefresh = 0; } : null);
                }
                Row("成功率 " + q.SuccessChance.ToString("P0") + " · 奖励加成 " + q.RewardBonus.ToString("P0") + " · 抵达回合 " + q.Arrival);
                Row("出发消耗：" + CostText(q.Costs)); Row("成功物品奖励：" + CostText(q.Rewards));
                foreach (var reward in ExpeditionNonItemRewards(expeditionDestination)) Row(reward);
                Row("失败预计伤亡 " + q.Casualties + " 人；抚恤 " + q.Subsidy + " 金币（按当前库存缺 " + q.MissingSubsidy + "，实际抵达时扣除）");
                Row("抚恤不足每缺 10 金币计 1 层岗位吸引力惩罚；出发后人员、补给和加成锁定。");
                if (q.Code != ResultCode.Success) Row(q.Reason);
                var source = selected; var destination = expeditionDestination;
                Row("王室队长：" + PersonName(expeditionCaptain) + "（不占工人名额，普通失败不会战死）");
                Row("不派队长", () => { expeditionCaptain = 0; nextRefresh = 0; });
                using (var royals = Sim.OrderedEntities<Royal>(em)) foreach (var royal in royals) if (CourtOps.AvailableCaptain(em, root, royal)) { var person = em.GetComponentData<Identity>(royal); Row("队长：" + person.Name, () => { expeditionCaptain = person.Id; nextRefresh = 0; }); }
                var captain = expeditionCaptain;
                Row("确认派遣", q.Code == ResultCode.Success ? () => ShowBuildingConfirmation("派遣：" + Name(destination), new[] { "人数 " + q.Crew + "；出发消耗 " + CostText(q.Costs), "王室队长：" + PersonName(captain) + "；成功影响力 +10，失败 −5，储君正收益提高。", "成功率 " + q.SuccessChance.ToString("P0") + "；奖励 " + CostText(q.Rewards), "失败伤亡 " + q.Casualties + "；抚恤 " + q.Subsidy + " 金币。放弃不会返还出发物资。" }, () => Send(CommandKind.StartExpedition, source, other: captain, definition: destination, amount: q.Crew, text: ExpeditionOps.Payload(q))) : null);
            }
            Row("队伍 / 待领取结果");
            var day = em.GetComponentData<Session>(root).Phase == Phase.Day;
            using var all = Sim.OrderedEntities<Expedition>(em);
            foreach (var e in all)
            {
                var id = em.GetComponentData<Identity>(e); var j = em.GetComponentData<Expedition>(e);
                var currentSource = Sim.Find(em, j.Site); var sourceName = currentSource != Entity.Null ? EntityName(j.Site) : j.SourceName.ToString() + "（原驻地）";
                Row(id.Name + " · " + sourceName + " · " + j.Crew + " 人 · " + (j.Status == ExpeditionStatus.Travelling ? "在途，抵达回合 " + j.Arrival : j.Status == ExpeditionStatus.Success ? "成功，待领奖" : "失败，已结算"));
                Row("出发驻地 LV" + j.SourceLevel + " · 出发回合 " + j.Departure + " · 成功率 " + j.SuccessChance.ToString("P0"));
                if (j.Captain != 0) Row("王室队长：" + PersonName(j.Captain));
                if (em.HasBuffer<ExpeditionSupply>(e)) foreach (var supply in em.GetBuffer<ExpeditionSupply>(e)) Row("已投入 " + Name(supply.Item) + " × " + supply.Amount);
                if (j.Status == ExpeditionStatus.Failure) Row("伤亡 " + j.Casualties + " / 幸存 " + (j.Crew - j.Casualties) + "；抚恤 " + j.SubsidyPaid + "/" + j.SubsidyRequired + "；惩罚 " + j.PenaltyStacks + " 层");
                if (j.Status == ExpeditionStatus.Success)
                {
                    Row("物品奖励：" + CostText(ExpeditionOps.Rewards(em, root, id.Definition, j.RewardBonus)) + "；加成 " + j.RewardBonus.ToString("P0") + "；正常库存放不下时整份保留。");
                    foreach (var reward in ExpeditionNonItemRewards(id.Definition)) Row(reward);
                }
                if (j.Status != ExpeditionStatus.Travelling) Row(j.Status == ExpeditionStatus.Success ? "领取远征奖励" : "确认远征结果", !day ? null : () => ShowBuildingConfirmation("确认：" + id.Name, new[] { j.Status == ExpeditionStatus.Success ? "领取后移除此结果，不会重复获得奖励。" : "伤亡和抚恤已在抵达时结算，确认不会重复扣除。" }, () => Send(CommandKind.ClaimExpedition, id.Id)));
                Row("放弃远征", !day ? null : () => ShowBuildingConfirmation("放弃：" + id.Name, new[] { "携带物资、进度和未领取奖励将失去；不额外造成伤亡。" }, () => Send(CommandKind.AbandonExpedition, id.Id)));
            }
            var penalty = ExpeditionOps.Penalty(em, root); if (penalty > 0) Row("抚恤不足：全局岗位吸引力 −" + penalty + "，持续至回合 " + em.GetComponentData<Session>(root).ExpeditionPenaltyUntil);
        }
        IEnumerable<string> ExpeditionNonItemRewards(int definition)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind == RuleKind.RewardBlueprint) yield return "蓝图 " + Name(r.Target) + " LV" + r.Amount;
                else if (r.Kind == RuleKind.RewardBuff) yield return "增益 " + Name(r.Target) + " × " + r.Amount;
                else if (r.Kind == RuleKind.RewardFeature) yield return "解锁 " + Name(r.Target);
            }
        }
    }
}
