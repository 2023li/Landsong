using System;
using System.Collections.Generic;
using Landsong.GameEventSystem;
using Landsong.InheritanceSystem;

namespace Landsong
{
    public sealed partial class GameSystem
    {
        private void CreateInheritanceService(RoyalInheritanceSaveData saveData = null)
        {
            UnsubscribeInheritanceService();
            royalInheritanceConfig ??= new RoyalInheritanceConfig();
            royalInheritanceConfig.Normalize();
            Inheritance = new RoyalInheritanceService(
                this,
                royalTraitCatalog,
                royalInheritanceConfig,
                saveData);
            Inheritance.PrinceBorn += HandlePrinceBorn;
            Inheritance.SuccessionOccurred += HandleSuccessionOccurred;
            Inheritance.AcquiredTraitAdded += HandleAcquiredRoyalTraitAdded;
        }

        private void UnsubscribeInheritanceService()
        {
            if (Inheritance == null)
            {
                return;
            }

            Inheritance.PrinceBorn -= HandlePrinceBorn;
            Inheritance.SuccessionOccurred -= HandleSuccessionOccurred;
            Inheritance.AcquiredTraitAdded -= HandleAcquiredRoyalTraitAdded;
        }

        private void HandlePrinceBorn(
            RoyalInheritanceService service,
            RoyalCharacterState prince,
            IReadOnlyList<RoyalEffectApplicationResult> effects)
        {
            if (prince == null)
            {
                return;
            }

            AddInheritanceMessageLocalized(
                GameEventCatalog.GE_王子出生,
                "gameplay.inheritance.prince_born",
                "王子出生：{0}。",
                () => new object[] { prince.DisplayName });
            if (effects == null)
            {
                return;
            }

            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect.HasMessage)
                {
                    AddInheritanceMessageLocalized(
                        GameEventCatalog.GE_国王特性效果触发,
                        "gameplay.inheritance.trait_effect_triggered",
                        "国王特性效果：{0}。",
                        () => new object[] { effect.Message });
                }
            }
        }

        private void HandleSuccessionOccurred(
            RoyalInheritanceService service,
            RoyalSuccessionResult succession)
        {
            AddInheritanceSuccessionMessage(succession, CurrentTurn);
        }

        private void HandleAcquiredRoyalTraitAdded(
            RoyalInheritanceService service,
            RoyalCharacterState character,
            RoyalTraitDefinition trait)
        {
            if (trait == null)
            {
                return;
            }

            AddInheritanceMessageLocalized(
                GameEventCatalog.GE_王族后天特性获得,
                "gameplay.inheritance.acquired_trait_added",
                "后天特性获得：{0} 获得 {1}。",
                () => new object[]
                {
                    character == null
                        ? Landsong.Localization.L10n.Gameplay("gameplay.common.unknown_character", "未知角色")
                        : character.DisplayName,
                    trait.TraitName
                });
        }

        private void SettleInheritanceForTurn(int turnNumber)
        {
            if (Inheritance == null)
            {
                CreateInheritanceService();
            }

            if (Inheritance == null)
            {
                return;
            }

            var result = Inheritance.SettleTurn(turnNumber);
            AddInheritanceSettlementMessages(result);
        }

        private void AddInheritanceSettlementMessages(RoyalInheritanceTurnResult result)
        {
            if (result == null)
            {
                return;
            }

            var bornChildren = result.BornChildren;
            for (var i = 0; i < bornChildren.Count; i++)
            {
                var child = bornChildren[i];
                if (child != null)
                {
                    AddInheritanceMessageLocalized(
                        GameEventCatalog.GE_王子出生,
                        "gameplay.inheritance.prince_born",
                        "王子出生：{0}。",
                        () => new object[] { child.DisplayName },
                        result.TurnNumber);
                }
            }

            var transitions = result.TraitTransitions;
            for (var i = 0; i < transitions.Count; i++)
            {
                AddInheritanceTraitTransitionMessage(transitions[i], result.TurnNumber);
            }

            var effects = result.Effects;
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (!effect.HasMessage)
                {
                    continue;
                }

                AddInheritanceMessageLocalized(
                    GameEventCatalog.GE_国王特性效果触发,
                    "gameplay.inheritance.trait_effect_triggered",
                    "国王特性效果：{0}。",
                    () => new object[] { effect.Message },
                    result.TurnNumber);
            }

            if (result.LifetimeWarningIssued && result.Succession.PreviousKing == null)
            {
                var king = Inheritance == null ? null : Inheritance.CurrentKing;
                if (king != null)
                {
                    AddInheritanceLifetimeWarning(king, result.TurnNumber);
                }
            }

            AddInheritanceSuccessionMessage(result.Succession, result.TurnNumber);
        }

        private void AddInheritanceTraitTransitionMessage(RoyalTraitTransition transition, int turnNumber)
        {
            if (transition.Character == null || transition.Trait == null || transition.Trait.Definition == null)
            {
                return;
            }

            if (transition.NewState == RoyalTraitState.Discovered)
            {
                AddInheritanceMessageLocalized(
                    GameEventCatalog.GE_王族特性显现,
                    "gameplay.inheritance.trait_discovered",
                    "王族特性显现：{0} 的 {1}。",
                    () => new object[] { transition.Character.DisplayName, transition.Trait.Definition.TraitName },
                    turnNumber);
            }
            else if (transition.NewState == RoyalTraitState.Active)
            {
                AddInheritanceMessageLocalized(
                    GameEventCatalog.GE_王族特性激活,
                    "gameplay.inheritance.trait_activated",
                    "王族特性激活：{0} 的 {1}。",
                    () => new object[] { transition.Character.DisplayName, transition.Trait.Definition.TraitName },
                    turnNumber);
            }
        }

        private void AddInheritanceLifetimeWarning(RoyalCharacterState king, int turnNumber)
        {
            if (king == null)
            {
                return;
            }

            AddInheritanceMessageLocalized(
                GameEventCatalog.GE_国王寿命预警,
                "gameplay.inheritance.lifetime_warning",
                "寿命预警：{0} 预计还剩 {1} 回合。",
                () => new object[] { king.DisplayName, king.RemainingLifespan },
                turnNumber);
        }

        private void AddInheritanceSuccessionMessage(RoyalSuccessionResult succession, int turnNumber)
        {
            if (!succession.Occurred)
            {
                return;
            }

            if (succession.Crisis || succession.NewKing == null)
            {
                AddInheritanceMessageLocalized(
                    GameEventCatalog.GE_王朝继承危机,
                    "gameplay.inheritance.succession_crisis",
                    "王朝继承危机：没有可继承王位的继承人。",
                    () => Array.Empty<object>(),
                    turnNumber);
                return;
            }

            AddInheritanceMessageLocalized(
                GameEventCatalog.GE_王位继承,
                "gameplay.inheritance.succession",
                "王位继承：{0} 因{1}离位，{2} 登基。",
                () => new object[]
                {
                    succession.PreviousKing?.DisplayName
                    ?? Landsong.Localization.L10n.Gameplay("gameplay.common.previous_king", "前任国王"),
                    succession.Reason == RoyalSuccessionReason.Abdication
                        ? Landsong.Localization.L10n.Gameplay("gameplay.inheritance.reason.abdication", "退位")
                        : Landsong.Localization.L10n.Gameplay("gameplay.inheritance.reason.death", "死亡"),
                    succession.NewKing.DisplayName
                },
                turnNumber);
        }

        private void AddInheritanceMessage(string eventTypeId, string message)
        {
            AddInheritanceMessage(eventTypeId, message, CurrentTurn);
        }

        private void AddInheritanceMessage(string eventTypeId, string message, int turnNumber)
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(GameEventMessage.ForGame(eventTypeId, message, turnNumber));
        }

        private void AddInheritanceMessageLocalized(
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
