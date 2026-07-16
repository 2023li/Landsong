using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    [BuildingModuleId("warehouse.operation")]
    public sealed class BM_仓库运营 : BuildingModuleBase,
        IBuildingInventoryCapacitySource,
        IBuildingWorkforceRequirementSource,
        IBuildingJobAttractionModifierSource,
        IBuildingUpgradeRequirementSource,
        IBuildingResourceConsumptionSource,
        IBuildingModuleStateSerializer,
        IBuildingModuleInitialized,
        IBuildingModuleRegistered,
        IBuildingModulePlaced,
        IBuildingModuleLevelApplied,
        IBuildingModuleUnregistered,
        IBuildingAutomaticTurnModule
    {
        [Serializable]
        private sealed class WarehouseState
        {
            public int Experience;
            public bool MaintenanceFailed;
            public bool LastMaintenancePaid;
            public int LastExperienceGained;
        }

        [TitleGroup("运营要求")]
        [SerializeField, LabelText("容量所需工人"), Min(0)] private int requiredWorkers = 2;
        [SerializeField, LabelText("基础库存格"), Min(0)] private int baseProvidedSlots = 1;
        [SerializeField, AssetsOnly, LabelText("维护费物品")] private ItemDefinition maintenanceItemDefinition;
        [SerializeField, LabelText("每回合维护费"), Min(0)] private int maintenanceAmountPerTurn = 1;

        [TitleGroup("经验与升级")]
        [SerializeField, LabelText("获得经验所需工人"), Min(0)] private int experienceWorkerRequirement = 2;
        [SerializeField, LabelText("每回合经验"), Min(0)] private int experiencePerTurn = 1;
        [SerializeField, LabelText("升下一级所需经验"), Min(0)] private int experienceRequiredForNextLevel = 30;

        [TitleGroup("满员奖励")]
        [SerializeField, LabelText("奖励工人阈值"), Min(0)] private int bonusWorkerThreshold;
        [SerializeField, LabelText("奖励库存格"), Min(0)] private int bonusProvidedSlots;

        [TitleGroup("维护失败")]
        [SerializeField, LabelText("吸引力惩罚"), Min(0f)] private float maintenanceFailureAttractionPenalty = 10f;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("当前经验")] private int experience;
        [SerializeField, ReadOnly, LabelText("维护费不足")] private bool maintenanceFailed;
        [SerializeField, ReadOnly, LabelText("上回合已支付维护费")] private bool lastMaintenancePaid;
        [SerializeField, ReadOnly, LabelText("上回合获得经验")] private int lastExperienceGained;

        [NonSerialized] private BuildingBase owner;
        private IReadOnlyList<BuildingResourceChange> lastResourceConsumptions = Array.Empty<BuildingResourceChange>();

        public override string ModuleDescription => "处理仓库的岗位容量、固定维护费、经验升级门槛、维护失败吸引力惩罚与存档。";
        public int RequiredWorkers => Mathf.Max(0, requiredWorkers);
        public int Experience => Mathf.Max(0, experience);
        public int ExperienceRequiredForNextLevel => Mathf.Max(0, experienceRequiredForNextLevel);
        public ItemDefinition MaintenanceItemDefinition => maintenanceItemDefinition;
        public int CurrentProvidedSlotCount => CalculateProvidedSlots();
        public IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions =>
            owner != null && owner.IsOperational && maintenanceAmountPerTurn > 0
                ? OneChange(MaintenanceItemId, maintenanceAmountPerTurn)
                : Array.Empty<BuildingResourceChange>();
        public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions => lastResourceConsumptions;

        private string MaintenanceItemId => maintenanceItemDefinition == null
            ? string.Empty
            : maintenanceItemDefinition.ItemId;

        public void ApplyConfiguration(
            int capacityWorkers,
            int providedSlots,
            ItemDefinition maintenanceItem,
            int maintenanceAmount,
            int experienceWorkers,
            int experienceGain,
            int nextLevelExperience,
            int rewardWorkerThreshold,
            int rewardSlots,
            float failureAttractionPenalty)
        {
            requiredWorkers = capacityWorkers;
            baseProvidedSlots = providedSlots;
            maintenanceItemDefinition = maintenanceItem;
            maintenanceAmountPerTurn = maintenanceAmount;
            experienceWorkerRequirement = experienceWorkers;
            experiencePerTurn = experienceGain;
            experienceRequiredForNextLevel = nextLevelExperience;
            bonusWorkerThreshold = rewardWorkerThreshold;
            bonusProvidedSlots = rewardSlots;
            maintenanceFailureAttractionPenalty = failureAttractionPenalty;
            Normalize();
            RefreshCapacity();
        }

        public void OnBuildingInitialized(BuildingBase building) => Bind(building);
        public void OnBuildingRegistered(BuildingBase building) => Bind(building);
        public void OnBuildingPlaced(BuildingBase building) => Bind(building);
        public void OnBuildingLevelApplied(BuildingBase building, int previousLevel, int currentLevel) => Bind(building);

        public void OnBuildingUnregistered(BuildingBase building)
        {
            owner = building;
            RefreshCapacity();
            owner = null;
        }

        public bool ProcessAutomaticTurn(BuildingBase building)
        {
            Bind(building);
            lastMaintenancePaid = false;
            lastExperienceGained = 0;
            lastResourceConsumptions = Array.Empty<BuildingResourceChange>();

            var inventory = owner?.GameSystem?.Services.Inventory;
            if (inventory == null)
            {
                maintenanceFailed = true;
                RefreshAfterTurn();
                return false;
            }

            if (maintenanceAmountPerTurn > 0 && string.IsNullOrWhiteSpace(MaintenanceItemId))
            {
                maintenanceFailed = true;
                RefreshAfterTurn();
                return false;
            }

            if (maintenanceAmountPerTurn > 0
                && !inventory.TryRemoveItem(MaintenanceItemId, maintenanceAmountPerTurn))
            {
                maintenanceFailed = true;
                RefreshAfterTurn();
                return false;
            }

            maintenanceFailed = false;
            lastMaintenancePaid = true;
            lastResourceConsumptions = maintenanceAmountPerTurn > 0
                ? OneChange(MaintenanceItemId, maintenanceAmountPerTurn)
                : Array.Empty<BuildingResourceChange>();

            if (TryGetWorkers(out var workers) && workers >= experienceWorkerRequirement)
            {
                lastExperienceGained = experiencePerTurn;
                experience = Mathf.Max(0, experience + lastExperienceGained);
            }

            RefreshAfterTurn();
            return true;
        }

        public bool TryGetJobAttractionModifier(out BuildingJobAttractionModifier modifier)
        {
            if (maintenanceFailed && maintenanceFailureAttractionPenalty > 0f)
            {
                modifier = new BuildingJobAttractionModifier(
                    "warehouse_maintenance_penalty",
                    "仓库维护费不足",
                    -maintenanceFailureAttractionPenalty,
                    "Building",
                    "仓库上一回合未能支付维护费，降低本建筑的岗位吸引力。");
                return true;
            }

            modifier = default;
            return false;
        }

        public bool CanUpgradeToLevel(BuildingBase building, int targetLevel, out string failureMessage)
        {
            if (building == null || targetLevel != building.CurrentLevel + 1 || experienceRequiredForNextLevel <= 0)
            {
                failureMessage = string.Empty;
                return true;
            }

            if (experience >= experienceRequiredForNextLevel)
            {
                failureMessage = string.Empty;
                return true;
            }

            failureMessage = $"仓库经验不足：{experience}/{experienceRequiredForNextLevel}。";
            return false;
        }

        public override string GetOverviewFragment(BuildingBase building)
        {
            Bind(building);
            return ExperienceRequiredForNextLevel > 0
                ? $"库存格 {CurrentProvidedSlotCount} · 经验 {Experience}/{ExperienceRequiredForNextLevel}"
                : $"库存格 {CurrentProvidedSlotCount} · 最高等级";
        }

        public override void AppendFunctionBlockEntries(BuildingBase building, ref List<BuildingFunctionBlockEntry> entries)
        {
            Bind(building);
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "库存格",
                    CurrentProvidedSlotCount,
                    new[]
                    {
                        new BuildingFunctionBlockSidebarRow("工人要求", $"{GetCurrentWorkers()}/{RequiredWorkers}"),
                        new BuildingFunctionBlockSidebarRow(
                            "仓库经验",
                            ExperienceRequiredForNextLevel > 0
                                ? $"{Experience}/{ExperienceRequiredForNextLevel}"
                                : $"{Experience}（最高等级）"),
                        new BuildingFunctionBlockSidebarRow("维护费", $"{maintenanceAmountPerTurn} {MaintenanceItemId}/回合")
                    }));
        }

        public override void AppendRuntimeStatuses(BuildingBase building, ref List<BuildingRuntimeStatus> statuses)
        {
            Bind(building);
            if (maintenanceAmountPerTurn > 0 && string.IsNullOrWhiteSpace(MaintenanceItemId))
            {
                AddRuntimeStatus(
                    ref statuses,
                    new BuildingRuntimeStatus(
                        BuildingRuntimeStatusCatalog.BS_仓库维护配置异常,
                        "仓库维护费配置异常"));
            }
            else if (maintenanceFailed)
            {
                AddRuntimeStatus(
                    ref statuses,
                    new BuildingRuntimeStatus(
                        BuildingRuntimeStatusCatalog.BS_仓库维护费不足,
                        "仓库维护费不足"));
            }
        }

        public override void Normalize()
        {
            requiredWorkers = Mathf.Max(0, requiredWorkers);
            baseProvidedSlots = Mathf.Max(0, baseProvidedSlots);
            maintenanceAmountPerTurn = Mathf.Max(0, maintenanceAmountPerTurn);
            experienceWorkerRequirement = Mathf.Max(0, experienceWorkerRequirement);
            experiencePerTurn = Mathf.Max(0, experiencePerTurn);
            experienceRequiredForNextLevel = Mathf.Max(0, experienceRequiredForNextLevel);
            bonusWorkerThreshold = Mathf.Max(0, bonusWorkerThreshold);
            bonusProvidedSlots = Mathf.Max(0, bonusProvidedSlots);
            maintenanceFailureAttractionPenalty = Mathf.Max(0f, maintenanceFailureAttractionPenalty);
            experience = Mathf.Max(0, experience);
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new WarehouseState
            {
                Experience = experience,
                MaintenanceFailed = maintenanceFailed,
                LastMaintenancePaid = lastMaintenancePaid,
                LastExperienceGained = lastExperienceGained
            });
            return !string.IsNullOrWhiteSpace(json);
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<WarehouseState>(json);
            if (state == null)
            {
                return;
            }

            experience = Mathf.Max(0, state.Experience);
            maintenanceFailed = state.MaintenanceFailed;
            lastMaintenancePaid = state.LastMaintenancePaid;
            lastExperienceGained = Mathf.Max(0, state.LastExperienceGained);
            RefreshCapacity();
        }

        private void Bind(BuildingBase building)
        {
            owner = building;
            Normalize();
            RefreshCapacity();
        }

        private int CalculateProvidedSlots()
        {
            if (owner == null || !owner.IsOperational || !TryGetWorkers(out var workers) || workers < requiredWorkers)
            {
                return 0;
            }

            var result = baseProvidedSlots;
            if (bonusProvidedSlots > 0 && bonusWorkerThreshold > 0 && workers >= bonusWorkerThreshold)
            {
                result += bonusProvidedSlots;
            }

            return Mathf.Max(0, result);
        }

        private bool TryGetWorkers(out int workers)
        {
            if (owner != null && BuildingWorkforceUtility.TryGetSource(owner, out var workforce))
            {
                workers = workforce.CurrentWorkers;
                return true;
            }

            workers = 0;
            return false;
        }

        private int GetCurrentWorkers()
        {
            return TryGetWorkers(out var workers) ? workers : 0;
        }

        private void RefreshAfterTurn()
        {
            RefreshCapacity();
            owner?.NotifyStateChanged();
        }

        private void RefreshCapacity()
        {
            owner?.GameSystem?.RefreshInventorySlotCapacity();
        }

        private static IReadOnlyList<BuildingResourceChange> OneChange(string itemId, int amount)
        {
            return string.IsNullOrWhiteSpace(itemId) || amount <= 0
                ? Array.Empty<BuildingResourceChange>()
                : new[] { new BuildingResourceChange(itemId, amount) };
        }
    }
}
