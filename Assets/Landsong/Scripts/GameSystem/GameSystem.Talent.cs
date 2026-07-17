using System;
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
            AddTalentMessageLocalized(
                GameEventCatalog.GE_人才刷新,
                "gameplay.talent.refreshed",
                "刷新人才：消耗 {0} 金币，获得 {1} 张候选卡。",
                () => new object[] { result.CostGold, result.Offers.Count });
        }

        private void HandleTalentOfferRecruited(TalentService service, TalentRecruitResult result)
        {
            if (result.Talent != null)
            {
                AddTalentMessageLocalized(
                    GameEventCatalog.GE_人才招募,
                    "gameplay.talent.recruited",
                    "招募人才：{0}。",
                    () => new object[] { result.Talent.DisplayName });
            }
        }

        private void HandleTalentAssigned(TalentService service, TalentAssignResult result)
        {
            if (result.Talent != null && result.Slot != null)
            {
                AddTalentMessageLocalized(
                    GameEventCatalog.GE_人才任命,
                    "gameplay.talent.assigned",
                    "任命人才：{0} -> {1}。",
                    () => new object[] { result.Talent.DisplayName, result.Slot.DisplayName });
            }
        }

        private void HandleTalentUnassigned(TalentService service, TalentAssignResult result)
        {
            if (result.Talent != null)
            {
                AddTalentMessageLocalized(
                    GameEventCatalog.GE_人才卸任,
                    "gameplay.talent.unassigned",
                    "卸任人才：{0}。",
                    () => new object[] { result.Talent.DisplayName });
            }
        }

        private void HandleTalentUpgraded(TalentService service, TalentUpgradeResult result)
        {
            if (result.Talent == null)
            {
                return;
            }

            AddTalentMessageLocalized(
                GameEventCatalog.GE_人才升级,
                "gameplay.talent.upgraded",
                "人才升级：{0} {1}->{2}。",
                () => new object[] { result.Talent.DisplayName, result.PreviousLevel, result.Talent.Level });
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
                AddTalentMessageLocalized(
                    eventType,
                    result.HasMissingSalary ? "gameplay.talent.salary_missing" : "gameplay.talent.salary_paid",
                    result.HasMissingSalary
                        ? "人才薪资不足：需要 {0} 金币，已支付 {1}，缺口 {2}。"
                        : "人才薪资支付：{1} 金币。",
                    () => new object[] { result.SalaryRequired, result.SalaryPaid, result.SalaryMissing },
                    result.TurnNumber);
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

                AddTalentMessageLocalized(
                    GameEventCatalog.GE_人才效果触发,
                    "gameplay.talent.effect_triggered",
                    "人才效果：{0}。",
                    () => new object[] { effect.Message },
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
                    AddTalentMessageLocalized(
                        GameEventCatalog.GE_人才特性发现,
                        "gameplay.talent.trait_discovered",
                        "人才特性发现：{0} 的 {1}。",
                        () => new object[] { transition.Talent.DisplayName, transition.Trait.Definition.DisplayName },
                        turnNumber);
                }
                else if (transition.NewState == TalentHiddenTraitState.Active)
                {
                    AddTalentMessageLocalized(
                        GameEventCatalog.GE_人才特性激活,
                        "gameplay.talent.trait_activated",
                        "人才特性激活：{0} 的 {1}。",
                        () => new object[] { transition.Talent.DisplayName, transition.Trait.Definition.DisplayName },
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

        private void AddTalentMessageLocalized(
            string eventTypeId,
            string textKey,
            string sourceMessage,
            Func<object[]> argumentsProvider,
            int? turnNumber = null)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGameLocalized(
                eventTypeId,
                textKey,
                sourceMessage,
                turnNumber ?? CurrentTurn,
                argumentsProvider));
        }
    }
}
