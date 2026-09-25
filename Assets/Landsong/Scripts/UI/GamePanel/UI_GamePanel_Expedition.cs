using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.Content;
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
    public sealed class UI_GamePanel_Expedition : GameFeatureViewBase
    {
        internal WorldSelectionState worldSelection;
        public override void Render() => Expeditions();
        internal UI_GamePanel_BuildingActionBar buildingController;
        internal UI_GamePanel_Royal courtController;
        internal ExpeditionId expeditionDestination;
        internal int expeditionCrew = 10;
        internal int[] expeditionAmounts;
        internal ulong expeditionCaptain;
        internal void ResetSession()
        {
            expeditionDestination = default;
            expeditionCaptain = 0;
            expeditionCrew = 10;
            expeditionAmounts = null;
        }

        internal void Expeditions()
        {
            if (!FeatureUnlocks.Has(sessionController.em, sessionController.root, FeatureDefinitions.Find(sessionController.em, sessionController.root, new Unity.Collections.FixedString128Bytes("feature.Expedition"))))
            {
                rowsController.Row("远征许可尚未解锁");
                return;
            }

            rowsController.Row("远征所 · 选择出发建筑");
            using (var sites = WorldQueries.OrderedEntities<Building>(sessionController.em))
                foreach (var e in sites)
                {
                    var id = sessionController.em.GetComponentData<Identity>(e);
                    var b = sessionController.em.GetComponentData<Building>(e);
                    BuildingWorkforceState bWorkforce = sessionController.em.GetComponentData<BuildingWorkforceState>(e);
                    if (!IsExpeditionSite(e))
                        continue;
                    rowsController.Row((worldSelection.SelectedEntityId == id.Id ? "▶ " : "") + id.Name + " LV" + b.Level + " · 工人 " + bWorkforce.Workers + " / 稳定岗位 " + bWorkforce.StableWorkers, () =>
                    {
                        worldSelection.SelectedEntityId = id.Id;
                        refresh.NextPanel = 0;
                    });
                }

            var site = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            for (int index = 0; index < ExpeditionDefinitions.Count(sessionController.em, sessionController.root); index++)
            {
                var i = ExpeditionId.FromIndex(index);
                ref var d = ref ExpeditionDefinitions.Get(sessionController.em, sessionController.root, i);
                var quote = ExpeditionOps.Quote(sessionController.em, sessionController.root, site, i, expeditionCrew);
                if (!quote.Visible)
                    continue;
                rowsController.Row((expeditionDestination == i ? "▶ " : "") + d.Metadata.Name + " · 需驻地 LV" + d.MinimumSiteLevel + " · " + d.TravelTurns + " 回合", () =>
                {
                    expeditionDestination = i;
                    expeditionAmounts = null;
                    refresh.NextPanel = 0;
                });
            }

            if (expeditionDestination.IsValid && site != Entity.Null && sessionController.em.HasComponent<Building>(site))
            {
                var q = ExpeditionOps.Quote(sessionController.em, sessionController.root, site, expeditionDestination, expeditionCrew, expeditionAmounts);
                if (expeditionAmounts == null)
                {
                    expeditionAmounts = new int[q.Options.Count];
                    for (var i = 0; i < expeditionAmounts.Length; i++)
                        expeditionAmounts[i] = q.Options[i].MinimumQuantity;
                }

                rowsController.Row("出征人数 " + expeditionCrew + " · 允许 " + q.Minimum + "～" + q.Maximum + "（当前工人 " + q.Workers + "，稳定岗位 " + q.StableWorkers + "）");
                rowsController.Row("人数 −1", expeditionCrew > q.Minimum ? () =>
                {
                    expeditionCrew--;
                    refresh.NextPanel = 0;
                } : null);
                rowsController.Row("人数 +1", expeditionCrew < q.Maximum ? () =>
                {
                    expeditionCrew++;
                    refresh.NextPanel = 0;
                } : null);
                rowsController.Row("按可用人数填满", q.Maximum >= q.Minimum ? () =>
                {
                    expeditionCrew = q.Maximum;
                    refresh.NextPanel = 0;
                } : null);
                if (q.Options.Count == 0)
                    rowsController.Row("此目的地未要求补给");
                for (var i = 0; i < q.Options.Count; i++)
                {
                    var at = i;
                    var option = q.Options[i];
                    rowsController.Row(ItemDefinitions.Get(sessionController.em, sessionController.root, option.Item).Metadata.Name.ToString() + "：携带 " + expeditionAmounts[i] + " / 最低 " + option.MinimumQuantity + " / 上限 " + ExpeditionOps.SupplyMaximum(option) + " / 持有 " + InventoryOps.Count(sessionController.em, sessionController.root, option.Item));
                    rowsController.Row("补给 −1：" + ItemDefinitions.Get(sessionController.em, sessionController.root, option.Item).Metadata.Name.ToString(), expeditionAmounts[i] > option.MinimumQuantity ? () =>
                    {
                        expeditionAmounts[at]--;
                        refresh.NextPanel = 0;
                    } : null);
                    rowsController.Row("补给 +1：" + ItemDefinitions.Get(sessionController.em, sessionController.root, option.Item).Metadata.Name.ToString(), expeditionAmounts[i] < ExpeditionOps.SupplyMaximum(option) ? () =>
                    {
                        expeditionAmounts[at]++;
                        refresh.NextPanel = 0;
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
                var source = worldSelection.SelectedEntityId;
                var destination = expeditionDestination;
                rowsController.Row("王室队长：" + courtController.PersonName(expeditionCaptain) + "（不占工人名额，普通失败不会战死）");
                rowsController.Row("不派队长", () =>
                {
                    expeditionCaptain = 0;
                    refresh.NextPanel = 0;
                });
                using (var royals = WorldQueries.OrderedEntities<Royal>(sessionController.em))
                    foreach (var royal in royals)
                        if (CourtOps.AvailableCaptain(sessionController.em, sessionController.root, royal))
                        {
                            var person = sessionController.em.GetComponentData<Identity>(royal);
                            rowsController.Row("队长：" + person.Name, () =>
                            {
                                expeditionCaptain = person.Id;
                                refresh.NextPanel = 0;
                            });
                        }

                var captain = expeditionCaptain;
                rowsController.Row("确认派遣", q.Code == ResultCode.Success ? () => buildingController.ShowBuildingConfirmation("派遣：" + ExpeditionDefinitions.Get(sessionController.em, sessionController.root, destination).Metadata.Name.ToString(), new[] { "人数 " + q.Crew + "；出发消耗 " + buildingController.CostText(q.Costs), "王室队长：" + courtController.PersonName(captain) + "；成功影响力 +10，失败 −5，储君正收益提高。", "成功率 " + q.SuccessChance.ToString("P0") + "；奖励 " + buildingController.CostText(q.Rewards), "失败伤亡 " + q.Casualties + "；抚恤 " + q.Subsidy + " 金币。放弃不会返还出发物资。" }, () => commandsController.TryQueue(ExpeditionRequest(source, captain, destination, q))) : null);
            }

            rowsController.Row("队伍 / 待领取结果");
            var day = sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;
            using var all = WorldQueries.OrderedEntities<Expedition>(sessionController.em);
            foreach (var e in all)
            {
                var id = sessionController.em.GetComponentData<Identity>(e);
                var j = sessionController.em.GetComponentData<Expedition>(e);
                var currentSource = WorldQueries.Find(sessionController.em, j.Site);
                var sourceName = currentSource != Entity.Null ? sessionController.EntityName(j.Site) : j.SourceName.ToString() + "（原驻地）";
                rowsController.Row(id.Name + " · " + sourceName + " · " + j.Crew + " 人 · " + (j.Status == ExpeditionStatus.Travelling ? "在途，抵达回合 " + j.Arrival : j.Status == ExpeditionStatus.Success ? "成功，待领奖" : "失败，已结算"));
                rowsController.Row("出发驻地 LV" + j.SourceLevel + " · 出发回合 " + j.Departure + " · 成功率 " + j.SuccessChance.ToString("P0"));
                if (j.Captain != 0)
                    rowsController.Row("王室队长：" + courtController.PersonName(j.Captain));
                if (sessionController.em.HasBuffer<ExpeditionSupply>(e))
                    foreach (var supply in sessionController.em.GetBuffer<ExpeditionSupply>(e))
                        rowsController.Row("已投入 " + ItemDefinitions.Get(sessionController.em, sessionController.root, supply.Item).Metadata.Name.ToString() + " × " + supply.Amount);
                if (j.Status == ExpeditionStatus.Failure)
                    rowsController.Row("伤亡 " + j.Casualties + " / 幸存 " + (j.Crew - j.Casualties) + "；抚恤 " + j.SubsidyPaid + "/" + j.SubsidyRequired + "；惩罚 " + j.PenaltyStacks + " 层");
                if (j.Status == ExpeditionStatus.Success)
                {
                    rowsController.Row("物品奖励：" + buildingController.CostText(ExpeditionOps.Rewards(sessionController.em, sessionController.root, sessionController.em.GetComponentData<ExpeditionDefinitionRef>(e).Definition, j.RewardBonus)) + "；加成 " + j.RewardBonus.ToString("P0") + "；正常库存放不下时整份保留。");
                    foreach (var reward in ExpeditionNonItemRewards(sessionController.em.GetComponentData<ExpeditionDefinitionRef>(e).Definition))
                        rowsController.Row(reward);
                }

                if (j.Status != ExpeditionStatus.Travelling)
                    rowsController.Row(j.Status == ExpeditionStatus.Success ? "领取远征奖励" : "确认远征结果", !day ? null : () => buildingController.ShowBuildingConfirmation("确认：" + id.Name, new[] { j.Status == ExpeditionStatus.Success ? "领取后移除此结果，不会重复获得奖励。" : "伤亡和抚恤已在抵达时结算，确认不会重复扣除。" }, () => commandsController.TryQueue(new ClaimExpeditionRequest { Expedition = id.Id })));
                rowsController.Row("放弃远征", !day ? null : () => buildingController.ShowBuildingConfirmation("放弃：" + id.Name, new[] { "携带物资、进度和未领取奖励将失去；不额外造成伤亡。" }, () => commandsController.TryQueue(new AbandonExpeditionRequest { Expedition = id.Id })));
            }

            var penalty = ExpeditionOps.Penalty(sessionController.em, sessionController.root);
            if (penalty > 0)
                rowsController.Row("抚恤不足：全局岗位吸引力 −" + penalty + "，持续至回合 " + sessionController.em.GetComponentData<ExpeditionPenaltyState>(sessionController.root).UntilTurn);
        }

        bool IsExpeditionSite(Entity site)
        {
            var level = sessionController.em.GetComponentData<Building>(site).Level;
            ref var definition = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, sessionController.em.GetComponentData<BuildingDefinitionRef>(site).Definition);
            if (!definition.Capabilities.Expeditions.Enabled)
                return false;
            for (int i = 0; i < definition.Capabilities.Expeditions.Levels.Length; i++)
                if (definition.Capabilities.Expeditions.Levels[i].Level == 0 || definition.Capabilities.Expeditions.Levels[i].Level == level)
                    return true;
            return false;
        }

        static StartExpeditionRequest ExpeditionRequest(ulong site, ulong captain, ExpeditionId destination, ExpeditionQuote quote)
        {
            var request = new StartExpeditionRequest
            {
                Site = site,
                Captain = captain,
                Destination = destination,
                Crew = quote.Crew,
                ExpectedQuote = new FixedString128Bytes(quote.Stamp)
            };
            foreach (var supply in quote.Supplies)
                request.SupplyQuantities.Add(supply.Amount);
            return request;
        }

        internal IEnumerable<string> ExpeditionNonItemRewards(ExpeditionId definition)
        {
            ref var data = ref ExpeditionDefinitions.Get(sessionController.em, sessionController.root, definition);
            ref var rewards = ref data.Rewards;
            var lines = new List<(int Order, string Text)>();
            for (int i = 0; i < rewards.Blueprints.Length; i++)
            {
                var reward = rewards.Blueprints[i];
                lines.Add((reward.Order, "蓝图 " + BuildingDefinitions.Get(sessionController.em, sessionController.root, reward.Building).Metadata.Name + " LV" + reward.GrantedLevel));
            }

            for (int i = 0; i < rewards.Buffs.Length; i++)
            {
                var reward = rewards.Buffs[i];
                lines.Add((reward.Order, "增益 " + BuffDefinitions.Get(sessionController.em, sessionController.root, reward.Buff).Metadata.Name + " × " + reward.GrantedLevel));
            }

            for (int i = 0; i < rewards.Features.Length; i++)
            {
                var reward = rewards.Features[i];
                lines.Add((reward.Order, "解锁 " + FeatureDefinitions.Get(sessionController.em, sessionController.root, reward.Feature).Metadata.Name));
            }

            return lines.OrderBy(line => line.Order).Select(line => line.Text).ToList();
        }
    }
}
