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
    public sealed class UI_GamePanel_Expedition : Moyo.Unity.UIViewBase, IGameFeatureRenderer
    {
        public void BindFeature(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_RowRenderer rows)
        {
            if (session == null || commands == null || navigation == null || rows == null)
                throw new System.ArgumentException("功能展示器缺少会话服务。");
            sessionController = session;
            rowsController = rows;
            commandsController = commands;
        }

        public void Render() => Expeditions();
        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Court courtController;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
        internal int expeditionDestination = -1;
        internal int expeditionCrew = 10;
        internal int[] expeditionAmounts;
        internal ulong expeditionCaptain;
        internal void Expeditions()
        {
            if (!FeatureOps.Unlocked(sessionController.em, sessionController.root, "Expedition"))
            {
                rowsController.Row("远征许可尚未解锁");
                return;
            }

            rowsController.Row("远征所 · 选择出发建筑");
            using (var sites = Sim.OrderedEntities<Building>(sessionController.em))
                foreach (var e in sites)
                {
                    var id = sessionController.em.GetComponentData<Identity>(e);
                    var b = sessionController.em.GetComponentData<Building>(e);
                    if (Sim.Rule(sessionController.em, sessionController.root, id.Definition, RuleKind.ExpeditionSite, b.Level).Level < 0)
                        continue;
                    rowsController.Row((sessionController.selected == id.Id ? "▶ " : "") + id.Name + " LV" + b.Level + " · 工人 " + b.Workers + " / 稳定岗位 " + b.StableWorkers, () =>
                    {
                        sessionController.selected = id.Id;
                        sessionController.nextRefresh = 0;
                    });
                }

            var site = Sim.Find(sessionController.em, sessionController.selected);
            sessionController.ForDefinitions(ContentKind.Expedition, (i, d) =>
            {
                var quote = ExpeditionOps.Quote(sessionController.em, sessionController.root, site, i, expeditionCrew);
                if (!quote.Visible)
                    return;
                rowsController.Row((expeditionDestination == i ? "▶ " : "") + d.Name + " · 需驻地 LV" + d.Level + " · " + d.Duration + " 回合", () =>
                {
                    expeditionDestination = i;
                    expeditionAmounts = null;
                    sessionController.nextRefresh = 0;
                });
            });
            if (expeditionDestination >= 0 && site != Entity.Null && sessionController.em.HasComponent<Building>(site))
            {
                var q = ExpeditionOps.Quote(sessionController.em, sessionController.root, site, expeditionDestination, expeditionCrew, expeditionAmounts);
                if (expeditionAmounts == null)
                {
                    expeditionAmounts = new int[q.Options.Count];
                    for (var i = 0; i < expeditionAmounts.Length; i++)
                        expeditionAmounts[i] = q.Options[i].Amount;
                }

                rowsController.Row("出征人数 " + expeditionCrew + " · 允许 " + q.Minimum + "～" + q.Maximum + "（当前工人 " + q.Workers + "，稳定岗位 " + q.StableWorkers + "）");
                rowsController.Row("人数 −1", expeditionCrew > q.Minimum ? () =>
                {
                    expeditionCrew--;
                    sessionController.nextRefresh = 0;
                } : null);
                rowsController.Row("人数 +1", expeditionCrew < q.Maximum ? () =>
                {
                    expeditionCrew++;
                    sessionController.nextRefresh = 0;
                } : null);
                rowsController.Row("按可用人数填满", q.Maximum >= q.Minimum ? () =>
                {
                    expeditionCrew = q.Maximum;
                    sessionController.nextRefresh = 0;
                } : null);
                if (q.Options.Count == 0)
                    rowsController.Row("此目的地未要求补给");
                for (var i = 0; i < q.Options.Count; i++)
                {
                    var at = i;
                    var option = q.Options[i];
                    rowsController.Row(sessionController.Name(option.Target) + "：携带 " + expeditionAmounts[i] + " / 最低 " + option.Amount + " / 上限 " + ExpeditionOps.SupplyMaximum(option) + " / 持有 " + InventoryOps.Count(sessionController.em, sessionController.root, option.Target));
                    rowsController.Row("补给 −1：" + sessionController.Name(option.Target), expeditionAmounts[i] > option.Amount ? () =>
                    {
                        expeditionAmounts[at]--;
                        sessionController.nextRefresh = 0;
                    } : null);
                    rowsController.Row("补给 +1：" + sessionController.Name(option.Target), expeditionAmounts[i] < ExpeditionOps.SupplyMaximum(option) ? () =>
                    {
                        expeditionAmounts[at]++;
                        sessionController.nextRefresh = 0;
                    } : null);
                }

                rowsController.Row("成功率 " + q.SuccessChance.ToString("P0") + " · 奖励加成 " + q.RewardBonus.ToString("P0") + " · 抵达回合 " + q.Arrival);
                rowsController.Row("出发消耗：" + buildingController.CostText(q.Costs));
                rowsController.Row("成功物品奖励：" + buildingController.CostText(q.Rewards));
                foreach (var reward in ExpeditionNonItemRewards(expeditionDestination))
                    rowsController.Row(reward);
                rowsController.Row("失败预计伤亡 " + q.Casualties + " 人；抚恤 " + q.Subsidy + " 金币（按当前库存缺 " + q.MissingSubsidy + "，实际抵达时扣除）");
                rowsController.Row("抚恤不足每缺 10 金币计 1 层岗位吸引力惩罚；出发后人员、补给和加成锁定。");
                if (q.Code != ResultCode.Success)
                    rowsController.Row(q.Reason);
                var source = sessionController.selected;
                var destination = expeditionDestination;
                rowsController.Row("王室队长：" + courtController.PersonName(expeditionCaptain) + "（不占工人名额，普通失败不会战死）");
                rowsController.Row("不派队长", () =>
                {
                    expeditionCaptain = 0;
                    sessionController.nextRefresh = 0;
                });
                using (var royals = Sim.OrderedEntities<Royal>(sessionController.em))
                    foreach (var royal in royals)
                        if (CourtOps.AvailableCaptain(sessionController.em, sessionController.root, royal))
                        {
                            var person = sessionController.em.GetComponentData<Identity>(royal);
                            rowsController.Row("队长：" + person.Name, () =>
                            {
                                expeditionCaptain = person.Id;
                                sessionController.nextRefresh = 0;
                            });
                        }

                var captain = expeditionCaptain;
                rowsController.Row("确认派遣", q.Code == ResultCode.Success ? () => buildingController.ShowBuildingConfirmation("派遣：" + sessionController.Name(destination), new[] { "人数 " + q.Crew + "；出发消耗 " + buildingController.CostText(q.Costs), "王室队长：" + courtController.PersonName(captain) + "；成功影响力 +10，失败 −5，储君正收益提高。", "成功率 " + q.SuccessChance.ToString("P0") + "；奖励 " + buildingController.CostText(q.Rewards), "失败伤亡 " + q.Casualties + "；抚恤 " + q.Subsidy + " 金币。放弃不会返还出发物资。" }, () => commandsController.TryQueue(CommandRequests.StartExpedition(source, captain, destination, q))) : null);
            }

            rowsController.Row("队伍 / 待领取结果");
            var day = sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;
            using var all = Sim.OrderedEntities<Expedition>(sessionController.em);
            foreach (var e in all)
            {
                var id = sessionController.em.GetComponentData<Identity>(e);
                var j = sessionController.em.GetComponentData<Expedition>(e);
                var currentSource = Sim.Find(sessionController.em, j.Site);
                var sourceName = currentSource != Entity.Null ? sessionController.EntityName(j.Site) : j.SourceName.ToString() + "（原驻地）";
                rowsController.Row(id.Name + " · " + sourceName + " · " + j.Crew + " 人 · " + (j.Status == ExpeditionStatus.Travelling ? "在途，抵达回合 " + j.Arrival : j.Status == ExpeditionStatus.Success ? "成功，待领奖" : "失败，已结算"));
                rowsController.Row("出发驻地 LV" + j.SourceLevel + " · 出发回合 " + j.Departure + " · 成功率 " + j.SuccessChance.ToString("P0"));
                if (j.Captain != 0)
                    rowsController.Row("王室队长：" + courtController.PersonName(j.Captain));
                if (sessionController.em.HasBuffer<ExpeditionSupply>(e))
                    foreach (var supply in sessionController.em.GetBuffer<ExpeditionSupply>(e))
                        rowsController.Row("已投入 " + sessionController.Name(supply.Item) + " × " + supply.Amount);
                if (j.Status == ExpeditionStatus.Failure)
                    rowsController.Row("伤亡 " + j.Casualties + " / 幸存 " + (j.Crew - j.Casualties) + "；抚恤 " + j.SubsidyPaid + "/" + j.SubsidyRequired + "；惩罚 " + j.PenaltyStacks + " 层");
                if (j.Status == ExpeditionStatus.Success)
                {
                    rowsController.Row("物品奖励：" + buildingController.CostText(ExpeditionOps.Rewards(sessionController.em, sessionController.root, id.Definition, j.RewardBonus)) + "；加成 " + j.RewardBonus.ToString("P0") + "；正常库存放不下时整份保留。");
                    foreach (var reward in ExpeditionNonItemRewards(id.Definition))
                        rowsController.Row(reward);
                }

                if (j.Status != ExpeditionStatus.Travelling)
                    rowsController.Row(j.Status == ExpeditionStatus.Success ? "领取远征奖励" : "确认远征结果", !day ? null : () => buildingController.ShowBuildingConfirmation("确认：" + id.Name, new[] { j.Status == ExpeditionStatus.Success ? "领取后移除此结果，不会重复获得奖励。" : "伤亡和抚恤已在抵达时结算，确认不会重复扣除。" }, () => commandsController.Send(CommandKind.ClaimExpedition, id.Id)));
                rowsController.Row("放弃远征", !day ? null : () => buildingController.ShowBuildingConfirmation("放弃：" + id.Name, new[] { "携带物资、进度和未领取奖励将失去；不额外造成伤亡。" }, () => commandsController.Send(CommandKind.AbandonExpedition, id.Id)));
            }

            var penalty = ExpeditionOps.Penalty(sessionController.em, sessionController.root);
            if (penalty > 0)
                rowsController.Row("抚恤不足：全局岗位吸引力 −" + penalty + "，持续至回合 " + sessionController.em.GetComponentData<Session>(sessionController.root).ExpeditionPenaltyUntil);
        }

        internal IEnumerable<string> ExpeditionNonItemRewards(int definition)
        {
            var d = Sim.Definition(sessionController.em, sessionController.root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i);
                if (r.Kind == RuleKind.RewardBlueprint)
                    yield return "蓝图 " + sessionController.Name(r.Target) + " LV" + r.Amount;
                else if (r.Kind == RuleKind.RewardBuff)
                    yield return "增益 " + sessionController.Name(r.Target) + " × " + r.Amount;
                else if (r.Kind == RuleKind.RewardFeature)
                    yield return "解锁 " + sessionController.Name(r.Target);
            }
        }
    }
}
