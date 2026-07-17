using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.ExpeditionSystem;
using Landsong.GameEventSystem;
using UnityEngine;

namespace Landsong
{
    public sealed partial class GameSystem
    {
        public IReadOnlyList<BuildingJobAttractionModifier> GetJobAttractionModifiers(BuildingBase building)
        {
            activeJobAttractionModifiers.Clear();
            if (Expeditions != null && Expeditions.IsSubsidyPenaltyActive && Expeditions.SubsidyPenaltyStacks > 0)
            {
                var value = -Mathf.Max(0f, expeditionSubsidyPenaltyAttractionPerStack)
                            * Expeditions.SubsidyPenaltyStacks;
                activeJobAttractionModifiers.Add(
                    new BuildingJobAttractionModifier(
                        "expedition_subsidy_penalty",
                        "远征补贴不足",
                        value,
                        "Expedition",
                        "远征失败补贴不足造成的全局岗位吸引力惩罚。"));
            }

            localJobAttractionModifierSources.Clear();
            building?.GetCapabilities(localJobAttractionModifierSources);
            for (var i = 0; i < localJobAttractionModifierSources.Count; i++)
            {
                if (localJobAttractionModifierSources[i].TryGetJobAttractionModifier(out var modifier)
                    && modifier.IsValid)
                {
                    activeJobAttractionModifiers.Add(modifier);
                }
            }
            localJobAttractionModifierSources.Clear();

            return activeJobAttractionModifiers.Count == 0
                ? EmptyJobAttractionModifiers
                : activeJobAttractionModifiers.ToArray();
        }

        internal float CalculateExpeditionRewardYieldBonus()
        {
            var buildings = Buildings == null ? null : Buildings.Buildings;
            if (buildings == null || buildings.Count == 0)
            {
                return 0f;
            }

            var total = 0f;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || !building.isActiveAndEnabled || building.IsDemolishing)
                {
                    continue;
                }

                var modules = building.BuildingModules;
                for (var j = 0; j < modules.Count; j++)
                {
                    if (modules[j] is IBuildingExpeditionRewardYieldSource source
                        && modules[j].IsEnabled)
                    {
                        total += Mathf.Max(0f, source.ExpeditionRewardYieldBonus);
                    }
                }
            }

            return Mathf.Max(0f, total);
        }

        private void CreateExpeditionService(ExpeditionSaveData saveData = null)
        {
            UnsubscribeExpeditionService();
            Expeditions = new ExpeditionService(
                this,
                expeditionDestinationCatalog,
                expeditionSubsidyGoldItemDefinition,
                expeditionTeamCapacity,
                saveData);
            Expeditions.StateChanged += HandleExpeditionsChanged;
            Expeditions.ExpeditionStarted += HandleExpeditionStarted;
            Expeditions.RewardsClaimed += HandleExpeditionRewardsClaimed;
            SyncExpeditionPopulationEmployment();
        }

        private void UnsubscribeExpeditionService()
        {
            if (Expeditions == null)
            {
                return;
            }

            Expeditions.StateChanged -= HandleExpeditionsChanged;
            Expeditions.ExpeditionStarted -= HandleExpeditionStarted;
            Expeditions.RewardsClaimed -= HandleExpeditionRewardsClaimed;
        }

        private void HandleExpeditionsChanged(ExpeditionService changedExpeditions)
        {
            SyncExpeditionPopulationEmployment();
        }

        private void HandleExpeditionStarted(ExpeditionService service, ExpeditionStartResult result)
        {
            if (result.Expedition == null)
            {
                return;
            }

            AddExpeditionMessage(
                GameEventCatalog.GE_远征出发,
                $"远征出发：{FormatExpeditionDestinationName(result.Expedition)}，预计第 {result.Expedition.ReturnTurn} 回合归来，成功率 {FormatPercent(result.SuccessChance)}。");
        }

        private void HandleExpeditionRewardsClaimed(ExpeditionService service, ExpeditionClaimResult result)
        {
            if (result.Expedition == null)
            {
                return;
            }

            AddExpeditionMessage(
                GameEventCatalog.GE_远征奖励领取,
                $"领取远征奖励：{FormatExpeditionDestinationName(result.Expedition)}。");
        }

        private void SyncExpeditionPopulationEmployment()
        {
            if (Dynasty == null)
            {
                return;
            }

            Dynasty.SetExternalEmployedPopulation(
                "expedition",
                Expeditions == null ? 0 : Expeditions.ActiveAssignedPopulation);
        }

        private void SettleExpeditionsForTurn(int turnNumber)
        {
            if (Expeditions == null)
            {
                CreateExpeditionService();
            }

            if (Expeditions == null)
            {
                return;
            }

            var results = Expeditions.SettleDueExpeditions(turnNumber);
            if (results == null || results.Count == 0)
            {
                SyncExpeditionPopulationEmployment();
                return;
            }

            for (var i = 0; i < results.Count; i++)
            {
                AddExpeditionSettlementMessage(results[i], turnNumber);
            }

            SyncExpeditionPopulationEmployment();
        }

        private void AddExpeditionSettlementMessage(ExpeditionSettlementResult result, int turnNumber)
        {
            var expedition = result.Expedition;
            if (expedition == null)
            {
                return;
            }

            if (result.Succeeded)
            {
                var rewardYieldText = $"收益率 {FormatRewardYieldPercent(expedition.RewardYieldMultiplier)}，";
                var message = result.RewardsPending
                    ? $"远征归来：{FormatExpeditionDestinationName(expedition)} 成功，人口已返还，{rewardYieldText}仓库空间不足，奖励待领取。"
                    : $"远征归来：{FormatExpeditionDestinationName(expedition)} 成功，人口已返还，{rewardYieldText}奖励已结算。";
                AddExpeditionMessage(
                    result.RewardsPending
                        ? GameEventCatalog.GE_远征奖励待领取
                        : GameEventCatalog.GE_远征成功,
                    message,
                    turnNumber);
                return;
            }

            var failureMessage = result.SubsidyMissing > 0
                ? $"远征失败：{FormatExpeditionDestinationName(expedition)}，损失人口 {expedition.AssignedPopulation}，补贴需 {result.SubsidyRequired} 金币，已支付 {result.SubsidyPaid}，缺口 {result.SubsidyMissing}，触发全局惩罚 {result.PenaltyStacksApplied} 层。"
                : $"远征失败：{FormatExpeditionDestinationName(expedition)}，损失人口 {expedition.AssignedPopulation}，已支付补贴 {result.SubsidyPaid} 金币。";
            AddExpeditionMessage(
                result.SubsidyMissing > 0
                    ? GameEventCatalog.GE_远征补贴不足
                    : GameEventCatalog.GE_远征失败,
                failureMessage,
                turnNumber);

            if (result.SubsidyMissing > 0)
            {
                Expeditions.ExtendSubsidyPenalty(turnNumber, expeditionSubsidyPenaltyDurationTurns);
            }
        }

        private void AddExpeditionMessage(string eventTypeId, string message)
        {
            AddExpeditionMessage(eventTypeId, message, CurrentTurn);
        }

        private void AddExpeditionMessage(string eventTypeId, string message, int turnNumber)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGame(eventTypeId, message, turnNumber));
        }

        private static string FormatExpeditionDestinationName(ExpeditionState expedition)
        {
            if (expedition == null)
            {
                return "未知目的地";
            }

            if (expedition.Definition != null)
            {
                return expedition.Definition.DisplayName;
            }

            return string.IsNullOrWhiteSpace(expedition.DestinationId) ? "未知目的地" : expedition.DestinationId;
        }

        private static string FormatPercent(float value)
        {
            return $"{Mathf.Clamp01(value) * 100f:0.#}%";
        }

        private static string FormatRewardYieldPercent(float value)
        {
            return $"{Mathf.Max(0f, value) * 100f:0.#}%";
        }
    }
}
