using System.Collections.Generic;
using Landsong.InventorySystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public enum BuildingUpgradeFailure
    {
        None = 0,
        MissingBuilding = 10,
        UnderConstruction = 20,
        PresentationLocked = 30,
        MaxLevel = 40,
        LevelNotConfigured = 50,
        ConditionNotMet = 60,
        CannotAfford = 70,
        SpendFailed = 80,
        ApplyFailed = 90
    }

    public readonly struct BuildingUpgradeResult
    {
        public BuildingUpgradeResult(
            bool succeeded,
            BuildingUpgradeFailure failure,
            int targetLevel,
            string message)
        {
            Succeeded = succeeded;
            Failure = failure;
            TargetLevel = Mathf.Max(0, targetLevel);
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }

        public bool Succeeded { get; }
        public BuildingUpgradeFailure Failure { get; }
        public int TargetLevel { get; }
        public string Message { get; }
    }

    public sealed class BuildingUpgradeService
    {
        private readonly Landsong.GameSystem gameSystem;

        public BuildingUpgradeService(Landsong.GameSystem gameSystem)
        {
            this.gameSystem = gameSystem;
        }

        public BuildingUpgradeResult Evaluate(BuildingBase building)
        {
            if (building == null || building.FamilyDefinition == null)
            {
                return Fail(BuildingUpgradeFailure.MissingBuilding, 0, "建筑或家族定义缺失。");
            }

            if (!building.IsOperational)
            {
                return Fail(BuildingUpgradeFailure.UnderConstruction, 0, "施工阶段不能升级。");
            }

            if (building.PresentationController != null
                && building.PresentationController.InteractionLocked)
            {
                return Fail(
                    BuildingUpgradeFailure.PresentationLocked,
                    building.CurrentLevel + 1,
                    "当前建筑正在播放过渡表现。");
            }

            var targetLevel = building.CurrentLevel + 1;
            if (!building.FamilyDefinition.TryGetLevel(targetLevel, out var target))
            {
                return Fail(BuildingUpgradeFailure.MaxLevel, targetLevel, "已经达到最高等级。");
            }

            if (!target.IsConfigured)
            {
                return Fail(
                    BuildingUpgradeFailure.LevelNotConfigured,
                    targetLevel,
                    "目标等级数值尚未配置。");
            }

            if (!target.IsConditionMet(gameSystem))
            {
                return Fail(
                    BuildingUpgradeFailure.ConditionNotMet,
                    targetLevel,
                    "升级科技或其他条件尚未满足。");
            }

            var inventory = gameSystem?.Services.Inventory;
            if (HasAnyCost(target.UpgradeCosts)
                && (inventory == null
                    || !inventory.CanAffordBuildingCosts(target.UpgradeCosts)))
            {
                return Fail(
                    BuildingUpgradeFailure.CannotAfford,
                    targetLevel,
                    "升级金币或资源不足。");
            }

            return new BuildingUpgradeResult(true, BuildingUpgradeFailure.None, targetLevel, string.Empty);
        }

        public BuildingUpgradeResult TryUpgrade(BuildingBase building)
        {
            var evaluation = Evaluate(building);
            if (!evaluation.Succeeded)
            {
                return evaluation;
            }

            building.FamilyDefinition.TryGetLevel(evaluation.TargetLevel, out var target);
            var inventory = gameSystem?.Services.Inventory;
            if (HasAnyCost(target.UpgradeCosts)
                && (inventory == null
                    || !inventory.TrySpendBuildingCosts(target.UpgradeCosts)))
            {
                return Fail(
                    BuildingUpgradeFailure.SpendFailed,
                    evaluation.TargetLevel,
                    "升级扣费失败。");
            }

            if (building.TryApplyOperationalLevel(evaluation.TargetLevel))
            {
                return evaluation;
            }

            Refund(inventory, target.UpgradeCosts);
            return Fail(
                BuildingUpgradeFailure.ApplyFailed,
                evaluation.TargetLevel,
                "应用目标等级配置失败，升级费用已退回。");
        }

        private static void Refund(
            InventoryService inventory,
            IReadOnlyList<BuildingCost> costs)
        {
            if (inventory == null || costs == null)
            {
                return;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    inventory.TryAddItem(costs[i].ItemDefinition, costs[i].Amount);
                }
            }
        }

        private static bool HasAnyCost(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null)
            {
                return false;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static BuildingUpgradeResult Fail(
            BuildingUpgradeFailure failure,
            int targetLevel,
            string message)
        {
            return new BuildingUpgradeResult(false, failure, targetLevel, message);
        }
    }
}
