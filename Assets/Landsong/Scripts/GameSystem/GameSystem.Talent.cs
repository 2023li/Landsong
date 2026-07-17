using System.Collections.Generic;
using Landsong.GameEventSystem;
using Landsong.TalentSystem;

namespace Landsong
{
    public sealed partial class GameSystem
    {
        private void CreateTalentService(TalentSaveData saveData = null)
        {
            UnsubscribeTalentService();
            Talents = new TalentService(
                this,
                talentCatalog,
                talentGoldItemDefinition,
                startingTalentSlots,
                talentRefreshGoldCost,
                talentRefreshCardCount,
                saveData);
            Talents.OffersRefreshed += HandleTalentOffersRefreshed;
            Talents.OfferRecruited += HandleTalentOfferRecruited;
            Talents.TalentAssigned += HandleTalentAssigned;
            Talents.TalentUnassigned += HandleTalentUnassigned;
            Talents.TalentUpgraded += HandleTalentUpgraded;
        }

        private void UnsubscribeTalentService()
        {
            if (Talents == null)
            {
                return;
            }

            Talents.OffersRefreshed -= HandleTalentOffersRefreshed;
            Talents.OfferRecruited -= HandleTalentOfferRecruited;
            Talents.TalentAssigned -= HandleTalentAssigned;
            Talents.TalentUnassigned -= HandleTalentUnassigned;
            Talents.TalentUpgraded -= HandleTalentUpgraded;
        }

        private void HandleTalentOffersRefreshed(TalentService service, TalentRefreshResult result)
        {
            AddTalentMessage(
                GameEventCatalog.GE_人才刷新,
                $"刷新人才：消耗 {result.CostGold} 金币，获得 {result.Offers.Count} 张候选卡。");
        }

        private void HandleTalentOfferRecruited(TalentService service, TalentRecruitResult result)
        {
            if (result.Talent != null)
            {
                AddTalentMessage(GameEventCatalog.GE_人才招募, $"招募人才：{result.Talent.DisplayName}。");
            }
        }

        private void HandleTalentAssigned(TalentService service, TalentAssignResult result)
        {
            if (result.Talent != null && result.Slot != null)
            {
                AddTalentMessage(
                    GameEventCatalog.GE_人才任命,
                    $"任命人才：{result.Talent.DisplayName} -> {result.Slot.DisplayName}。");
            }
        }

        private void HandleTalentUnassigned(TalentService service, TalentAssignResult result)
        {
            if (result.Talent != null)
            {
                AddTalentMessage(GameEventCatalog.GE_人才卸任, $"卸任人才：{result.Talent.DisplayName}。");
            }
        }

        private void HandleTalentUpgraded(TalentService service, TalentUpgradeResult result)
        {
            if (result.Talent == null)
            {
                return;
            }

            AddTalentMessage(
                GameEventCatalog.GE_人才升级,
                $"人才升级：{result.Talent.DisplayName} {result.PreviousLevel}->{result.Talent.Level}。");
            AddTalentTraitTransitionMessages(result.TraitTransitions, CurrentTurn);
        }

        private void SettleTalentsForTurn(int turnNumber)
        {
            if (Talents == null)
            {
                CreateTalentService();
            }

            if (Talents == null)
            {
                return;
            }

            var result = Talents.SettleTurn(turnNumber);
            AddTalentSettlementMessages(result);
        }

        private void AddTalentSettlementMessages(TalentTurnSettlementResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.HasSalary)
            {
                var eventType = result.HasMissingSalary
                    ? GameEventCatalog.GE_人才薪资不足
                    : GameEventCatalog.GE_人才薪资支付;
                var message = result.HasMissingSalary
                    ? $"人才薪资不足：需要 {result.SalaryRequired} 金币，已支付 {result.SalaryPaid}，缺口 {result.SalaryMissing}。"
                    : $"人才薪资支付：{result.SalaryPaid} 金币。";
                AddTalentMessage(eventType, message, result.TurnNumber);
            }

            AddTalentTraitTransitionMessages(result.TraitTransitions, result.TurnNumber);

            var effects = result.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect.HasMessage)
                {
                    continue;
                }

                AddTalentMessage(
                    GameEventCatalog.GE_人才效果触发,
                    $"人才效果：{effect.Message}。",
                    result.TurnNumber);
            }
        }

        private void AddTalentTraitTransitionMessages(
            IReadOnlyList<TalentHiddenTraitTransition> transitions,
            int turnNumber)
        {
            if (transitions == null)
            {
                return;
            }

            for (var i = 0; i < transitions.Count; i++)
            {
                var transition = transitions[i];
                if (transition.Talent == null || transition.Trait == null || transition.Trait.Definition == null)
                {
                    continue;
                }

                if (transition.NewState == TalentHiddenTraitState.Discovered)
                {
                    AddTalentMessage(
                        GameEventCatalog.GE_人才特性发现,
                        $"人才特性发现：{transition.Talent.DisplayName} 的 {transition.Trait.Definition.DisplayName}。",
                        turnNumber);
                }
                else if (transition.NewState == TalentHiddenTraitState.Active)
                {
                    AddTalentMessage(
                        GameEventCatalog.GE_人才特性激活,
                        $"人才特性激活：{transition.Talent.DisplayName} 的 {transition.Trait.Definition.DisplayName}。",
                        turnNumber);
                }
            }
        }

        private void AddTalentMessage(string eventTypeId, string message)
        {
            AddTalentMessage(eventTypeId, message, CurrentTurn);
        }

        private void AddTalentMessage(string eventTypeId, string message, int turnNumber)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGame(eventTypeId, message, turnNumber));
        }
    }
}
