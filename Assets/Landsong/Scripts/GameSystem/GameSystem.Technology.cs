using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GameEventSystem;
using Landsong.TechnologySystem;
using Landsong.TurnSystem;
using UnityEngine;

namespace Landsong
{
    public sealed partial class GameSystem
    {
        internal void RebuildTechnologyUnlockContentRegistry()
        {
            TechnologyUnlockContents ??= new TechnologyUnlockContentRegistry();
            TechnologyUnlockContents.Clear();
            TechnologyUnlockContentRegistry.InjectCompletionEffects(
                Technology?.Catalog ?? technologyCatalog,
                TechnologyUnlockContents);
            buildingCatalog?.InjectTechnologyUnlockContents(TechnologyUnlockContents);
            technologyGlobalBuffCatalog?.InjectTechnologyUnlockContents(TechnologyUnlockContents);

            if (technologyUnlockContentProducerAssets == null)
            {
                return;
            }

            for (var i = 0; i < technologyUnlockContentProducerAssets.Length; i++)
            {
                if (technologyUnlockContentProducerAssets[i] is ITechnologyUnlockContentProducer producer)
                {
                    producer.InjectTechnologyUnlockContents(TechnologyUnlockContents);
                }
            }
        }

        private void CreateTechnologyService()
        {
            if (Technology != null)
            {
                Technology.StateChanged -= HandleTechnologyStateChanged;
            }

            Technology = new TechnologyService(
                technologyCatalog,
                startingUnlockedTechnologies,
                startingResearchTechnology == null ? null : startingResearchTechnology.TechnologyId,
                startingResearchProgress);
            Technology.StateChanged += HandleTechnologyStateChanged;
            GlobalBuffs = new TechnologyGlobalBuffService(this, technologyGlobalBuffCatalog);
            RebuildTechnologyUnlockContentRegistry();
            HandleTechnologyStateChanged(Technology);
        }

        private void HandleTechnologyStateChanged(TechnologyService changedTechnology)
        {
            SyncStartingResearchFromService();
            ReconcileBuildingBlueprintsFromAutomaticConditions();
            if (changedTechnology != null && changedTechnology.HasCurrentResearch)
            {
                ClearMissingResearchWarning();
            }
        }

        private void SyncStartingResearchFromService()
        {
            if (Technology == null)
            {
                startingResearchProgress = Mathf.Max(0, startingResearchProgress);
                return;
            }

            startingResearchTechnology = Technology.CurrentResearchDefinition;
            startingResearchProgress = Technology.CurrentResearchProgress;
        }

        private void HandleBuildingTechnologyPointsProvided(
            TurnService turn,
            BuildingTechnologyPointsProvidedEvent technologyPointsEvent)
        {
            if (!technologyPointsEvent.IsValid)
            {
                return;
            }

            pendingTechnologyPointsThisTurn += technologyPointsEvent.Points;
        }

        private void ApplyTechnologyResearchPoints(int turnNumber)
        {
            var points = pendingTechnologyPointsThisTurn;
            pendingTechnologyPointsThisTurn = 0;
            ApplyResearchPointsToTechnology(points, turnNumber);
        }

        internal TechnologyResearchAppliedResult ApplyExternalResearchPoints(
            int amount,
            int turnNumber,
            string sourceName)
        {
            return ApplyResearchPointsToTechnology(Mathf.Max(0, amount), turnNumber);
        }

        private TechnologyResearchAppliedResult ApplyResearchPointsToTechnology(int points, int turnNumber)
        {
            if (Technology == null)
            {
                CreateTechnologyService();
            }

            if (Technology == null)
            {
                return default;
            }

            var result = Technology.ApplyResearchPoints(Mathf.Max(0, points));
            SyncStartingResearchFromService();

            if (!result.Completed || result.Technology == null)
            {
                return result;
            }

            if (Events == null)
            {
                CreateGameEventService();
            }

            var effectMessage = ApplyTechnologyCompletionEffects(result.Technology);
            var completedTechnology = result.Technology;
            Events?.AddMessage(
                GameEventMessage.ForGameLocalized(
                    GameEventCatalog.GE_科技研究完成,
                    string.IsNullOrWhiteSpace(effectMessage)
                        ? "gameplay.technology.research_completed"
                        : "gameplay.technology.research_completed_with_effects",
                    string.IsNullOrWhiteSpace(effectMessage)
                        ? "科技研究完成：{0}"
                        : "科技研究完成：{0}（{1}）",
                    turnNumber,
                    () => new object[] { completedTechnology.DisplayName, effectMessage }));

            if (result.Technology.AllowRepeatResearch && Technology.IsCurrentResearch(result.Technology))
            {
                Events?.AddMessage(
                    GameEventMessage.ForGameLocalized(
                        GameEventCatalog.GE_科技自动重复研发,
                        "gameplay.technology.repeat_continued",
                        "科技已自动继续重复研发：{0}",
                        turnNumber,
                        () => new object[] { completedTechnology.DisplayName }));
            }

            return result;
        }

        private string ApplyTechnologyCompletionEffects(TechnologyDefinition technology)
        {
            if (technology == null)
            {
                return string.Empty;
            }

            var effects = technology.CompletionEffects;
            if (effects == null || effects.Count == 0)
            {
                return string.Empty;
            }

            var messages = new List<string>();
            for (var i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null)
                {
                    continue;
                }

                effect.Normalize();
                var result = effect.Apply(this, technology);
                if (result.Applied && result.HasMessage)
                {
                    messages.Add(result.Message);
                }
            }

            return messages.Count == 0 ? string.Empty : string.Join("，", messages);
        }

        private bool ShouldCancelNextTurnForMissingResearch()
        {
            if (Features == null || !Features.IsUnlocked(GameFeature.Technology))
            {
                ClearMissingResearchWarning();
                return false;
            }

            if (Technology == null)
            {
                CreateTechnologyService();
            }

            if (Technology != null && Technology.HasResearchPlan)
            {
                ClearMissingResearchWarning();
                return false;
            }

            if (turnWithMissingResearchWarning == CurrentTurn)
            {
                return false;
            }

            turnWithMissingResearchWarning = CurrentTurn;
            AddMissingResearchWarningEvent();
            return true;
        }

        private void AddMissingResearchWarningEvent()
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            Events?.AddMessage(
                GameEventMessage.ForGameLocalized(
                    GameEventCatalog.GE_未选择研发节点,
                    "gameplay.technology.no_research_selected",
                    "未选择研发节点：本次下回合已取消；再次点击下一回合将继续。",
                    CurrentTurn,
                    Array.Empty<object>()));
        }

        private void ClearMissingResearchWarning()
        {
            turnWithMissingResearchWarning = -1;
        }
    }
}
