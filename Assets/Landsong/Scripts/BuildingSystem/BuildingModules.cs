using System;
using System.Collections.Generic;
using Landsong.ConditionSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public abstract class BuildingModuleBase
    {
        [SerializeField, LabelText("启用")] private bool enabled = true;

        public bool IsEnabled => enabled;

        public virtual void Normalize()
        {
        }

        public virtual void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
        }

        protected static void AddFunctionBlockEntry(
            ref List<BuildingFunctionBlockEntry> entries,
            BuildingFunctionBlockEntry entry)
        {
            if (!entry.IsValid)
            {
                return;
            }

            entries ??= new List<BuildingFunctionBlockEntry>();
            entries.Add(entry);
        }
    }

    [Serializable]
    public sealed class BuildingNearbyPopulationJobAttractionModule : BuildingModuleBase
    {
        [SerializeField, LabelText("人口搜索半径"), Min(0)]
        [PropertyTooltip("单位：格。按曼哈顿距离统计附近人口建筑。")]
        private int populationSearchRadius = 10;

        [SerializeField, LabelText("附近每人口就业吸引力"), Min(0f)]
        [PropertyTooltip("单位：岗位吸引力点/人。附近每 1 人为该建筑提供多少岗位吸引力。")]
        private float attractionPerNearbyPopulation =
            BuildingJobSystem.DefaultAttractionPerNearbyPopulation;

        public int PopulationSearchRadius => Mathf.Max(0, populationSearchRadius);
        public float AttractionPerNearbyPopulation => Mathf.Max(0f, attractionPerNearbyPopulation);

        public override void Normalize()
        {
            populationSearchRadius = Mathf.Max(0, populationSearchRadius);
            attractionPerNearbyPopulation = Mathf.Max(0f, attractionPerNearbyPopulation);
        }

    }

    [Serializable]
    public sealed class BuildingInventorySlotCapacityModule : BuildingModuleBase
    {
        [SerializeField, LabelText("提供库存格数"), Min(0)]
        [PropertyTooltip("单位：格。该建筑存在时提供的额外库存格子数量。")]
        private int providedSlotCount = 5;

        public int ProvidedSlotCount => Mathf.Max(0, providedSlotCount);

        public void SetProvidedSlotCount(int slotCount)
        {
            providedSlotCount = Mathf.Max(0, slotCount);
        }

        public override void Normalize()
        {
            providedSlotCount = Mathf.Max(0, providedSlotCount);
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled)
            {
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "库存格",
                    ProvidedSlotCount));
        }
    }

    [Serializable]
    public sealed class BuildingTechnologyPointModule : BuildingModuleBase, IBuildingTechnologyPointSource
    {
        [SerializeField, LabelText("提供科技点/回合"), Min(0)]
        [PropertyTooltip("该建筑每次成功完成回合处理后，注入当前研究的科技点。")]
        private int providedTechnologyPointsPerTurn = 1;

        private int lastTechnologyPoints;

        public int CurrentTechnologyPointsPerTurn => Mathf.Max(0, providedTechnologyPointsPerTurn);
        public int LastTechnologyPoints => Mathf.Max(0, lastTechnologyPoints);

        public override void Normalize()
        {
            providedTechnologyPointsPerTurn = Mathf.Max(0, providedTechnologyPointsPerTurn);
            lastTechnologyPoints = Mathf.Max(0, lastTechnologyPoints);
        }

        public int ProvideTechnologyPointsForTurn()
        {
            lastTechnologyPoints = CurrentTechnologyPointsPerTurn;
            return lastTechnologyPoints;
        }

        public void ClearLastTechnologyPoints()
        {
            lastTechnologyPoints = 0;
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled || CurrentTechnologyPointsPerTurn <= 0)
            {
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "研究点/回合",
                    CurrentTechnologyPointsPerTurn));
        }
    }

    [Serializable]
    public sealed class BuildingLevelUpgradeModule : BuildingModuleBase
    {
        [SerializeField, LabelText("自动升级")]
        private bool autoUpgradeEnabled = true;

        [SerializeField, LabelText("当前经验"), Min(0)]
        private int currentExperience;

        [SerializeField, LabelText("升级所需经验"), Min(1)]
        private int requiredExperience = 10;

        [SerializeField, LabelText("升级目标预制体")]
        private BuildingBase upgradeTargetPrefab;

        [SerializeReference, LabelText("升级条件")]
        private GameCondition upgradeCondition;

        [SerializeField, LabelText("升级消耗")]
        private BuildingCost[] upgradeCosts = Array.Empty<BuildingCost>();

        public bool AutoUpgradeEnabled => autoUpgradeEnabled;
        public int CurrentExperience => Mathf.Max(0, currentExperience);
        public int RequiredExperience => Mathf.Max(1, requiredExperience);
        public BuildingBase UpgradeTargetPrefab => upgradeTargetPrefab;
        public GameCondition UpgradeCondition => upgradeCondition;
        public IReadOnlyList<BuildingCost> UpgradeCosts => upgradeCosts ?? Array.Empty<BuildingCost>();
        public float ExperienceProgress => Mathf.Clamp01(CurrentExperience / (float)RequiredExperience);
        public bool IsReadyToUpgrade => CurrentExperience >= RequiredExperience;
        public bool HasUpgradeCosts => HasAnyValidCost(UpgradeCosts);

        public override void Normalize()
        {
            currentExperience = Mathf.Max(0, currentExperience);
            requiredExperience = Mathf.Max(1, requiredExperience);
            NormalizeCosts();
        }

        public void SetAutoUpgradeEnabled(bool enabled)
        {
            autoUpgradeEnabled = enabled;
        }

        public void SetExperience(int experience)
        {
            currentExperience = Mathf.Clamp(experience, 0, RequiredExperience);
        }

        public void AddExperience(int experience)
        {
            if (experience <= 0)
            {
                return;
            }

            SetExperience(CurrentExperience + experience);
        }

        public bool CanUpgrade(BuildingBase building)
        {
            return building != null
                   && IsReadyToUpgrade
                   && upgradeTargetPrefab != null
                   && upgradeTargetPrefab.HasDefinition
                   && building.GameSystem != null
                   && building.GameSystem.Buildings != null
                   && IsUpgradeConditionMet(building)
                   && building.GameSystem.Buildings.CanReplace(building, upgradeTargetPrefab, false)
                   && CanAffordUpgradeCosts(building);
        }

        public bool TryUpgrade(BuildingBase building)
        {
            if (!CanUpgrade(building))
            {
                return false;
            }

            if (!TrySpendUpgradeCosts(building))
            {
                return false;
            }

            return building.GameSystem.Buildings.TryReplace(building, upgradeTargetPrefab, out _);
        }

        public bool TryAutoUpgrade(BuildingBase building)
        {
            return autoUpgradeEnabled && TryUpgrade(building);
        }

        public bool CanAffordUpgradeCosts(BuildingBase building)
        {
            if (!HasUpgradeCosts)
            {
                return true;
            }

            var inventory = building == null || building.GameSystem == null ? null : building.GameSystem.Inventory;
            return inventory != null && inventory.CanAffordBuildingCosts(UpgradeCosts);
        }

        private bool TrySpendUpgradeCosts(BuildingBase building)
        {
            if (!HasUpgradeCosts)
            {
                return true;
            }

            var inventory = building == null || building.GameSystem == null ? null : building.GameSystem.Inventory;
            return inventory != null && inventory.TrySpendBuildingCosts(UpgradeCosts);
        }

        private bool IsUpgradeConditionMet(BuildingBase building)
        {
            return upgradeCondition == null || upgradeCondition.IsMet(building.GameSystem);
        }

        private void NormalizeCosts()
        {
            if (upgradeCosts == null)
            {
                upgradeCosts = Array.Empty<BuildingCost>();
                return;
            }

            for (var i = 0; i < upgradeCosts.Length; i++)
            {
                upgradeCosts[i] = upgradeCosts[i].Normalized();
            }
        }

        private static bool HasAnyValidCost(IReadOnlyList<BuildingCost> costs)
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
    }
}
