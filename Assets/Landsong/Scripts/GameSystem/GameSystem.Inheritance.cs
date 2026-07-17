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

            AddInheritanceMessage(GameEventCatalog.GE_王子出生, $"王子出生：{prince.DisplayName}。");
            if (effects == null)
            {
                return;
            }

            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect.HasMessage)
                {
                    AddInheritanceMessage(
                        GameEventCatalog.GE_国王特性效果触发,
                        $"国王特性效果：{effect.Message}。");
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

            AddInheritanceMessage(
                GameEventCatalog.GE_王族后天特性获得,
                $"后天特性获得：{(character == null ? "未知角色" : character.DisplayName)} 获得 {trait.TraitName}。");
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
                    AddInheritanceMessage(
                        GameEventCatalog.GE_王子出生,
                        $"王子出生：{child.DisplayName}。",
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

                AddInheritanceMessage(
                    GameEventCatalog.GE_国王特性效果触发,
                    $"国王特性效果：{effect.Message}。",
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
                AddInheritanceMessage(
                    GameEventCatalog.GE_王族特性显现,
                    $"王族特性显现：{transition.Character.DisplayName} 的 {transition.Trait.Definition.TraitName}。",
                    turnNumber);
            }
            else if (transition.NewState == RoyalTraitState.Active)
            {
                AddInheritanceMessage(
                    GameEventCatalog.GE_王族特性激活,
                    $"王族特性激活：{transition.Character.DisplayName} 的 {transition.Trait.Definition.TraitName}。",
                    turnNumber);
            }
        }

        private void AddInheritanceLifetimeWarning(RoyalCharacterState king, int turnNumber)
        {
            if (king == null)
            {
                return;
            }

            AddInheritanceMessage(
                GameEventCatalog.GE_国王寿命预警,
                $"寿命预警：{king.DisplayName} 预计还剩 {king.RemainingLifespan} 回合。",
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
                AddInheritanceMessage(
                    GameEventCatalog.GE_王朝继承危机,
                    "王朝继承危机：没有可继承王位的继承人。",
                    turnNumber);
                return;
            }

            var reason = succession.Reason == RoyalSuccessionReason.Abdication ? "退位" : "死亡";
            AddInheritanceMessage(
                GameEventCatalog.GE_王位继承,
                $"王位继承：{succession.PreviousKing?.DisplayName ?? "前任国王"} 因{reason}离位，{succession.NewKing.DisplayName} 登基。",
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
    }
}
