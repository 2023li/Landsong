using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 可复用的岗位运营能力：岗位吸引、补贴、招工/离职、就业人口与模块存档。
    /// 生产、捕鱼、作物和市场收入仍由各自建筑处理。
    /// </summary>
    [Serializable]
    [BuildingModuleId("workforce")]
    public sealed class BM_岗位运营 : BuildingModuleBase,
        IBuildingWorkforceFundingSource,
        IBuildingModuleStateSerializer,
        IBuildingModuleInitialized,
        IBuildingModuleRegistered,
        IBuildingModulePlaced,
        IBuildingModuleConstructionCompleted,
        IBuildingModuleLevelApplied,
        IBuildingModuleUnregistered,
        IBuildingAutomaticTurnModule
    {
        private const int NewConstructionWorkerProtectionTurns = 2;
        private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
        private const string StatusInvalidGoldItem = BuildingRuntimeStatusCatalog.BS_金币配置异常;
        private const string StatusRecruitGoldMissing = BuildingRuntimeStatusCatalog.BS_招工金币不足;
        private const string StatusSubsidyGoldMissing = BuildingRuntimeStatusCatalog.BS_补贴金币不足;

        [Serializable]
        private sealed class WorkforceState
        {
            public int CurrentWorkers;
            public bool AutoFullWorkerSubsidyEnabled;
            public int TargetStableWorkers;
            public bool InitialWorkersGranted;
            public int WorkerProtectionTurnsRemaining;
        }

        [TitleGroup("岗位")]
        [SerializeField, LabelText("最大岗位"), Min(1)] private int maxWorkers = 3;
        [SerializeField, LabelText("直接运营放置初始工人"), Min(0)] private int initialWorkersOnPlaced;
        [SerializeField, LabelText("基础吸引力"), Min(0f)] private float baseJobAttraction = 55f;
        [SerializeField, LabelText("单人招工费用"), Min(0)] private int singleRecruitCost = 10;

        [TitleGroup("岗位补贴")]
        [SerializeField, LabelText("自动补贴满岗位")] private bool autoFullWorkerSubsidyEnabled;
        [SerializeField, LabelText("目标稳定工人"), Min(0)] private int targetStableWorkers;
        [SerializeField, AssetsOnly, LabelText("金币物品")] private ItemDefinition goldItemDefinition;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("当前工人")] private int currentWorkers;
        [SerializeField, ReadOnly, LabelText("稳定工人")] private int stableWorkers;
        [SerializeField, ReadOnly, LabelText("无补贴稳定工人")] private int stableWorkersWithoutSubsidy;
        [SerializeField, ReadOnly, LabelText("岗位吸引力")] private float jobAttraction;
        [SerializeField, ReadOnly, LabelText("无补贴岗位吸引力")] private float jobAttractionWithoutSubsidy;
        [SerializeField, ReadOnly, LabelText("目标每回合补贴金币")] private int targetSubsidyGoldPerTurn;
        [SerializeField, ReadOnly, LabelText("本回合已付补贴金币")] private int paidSubsidyGoldThisTurn;
        [SerializeField, ReadOnly, LabelText("已发放初始工人")] private bool initialWorkersGranted;
        [SerializeField, ReadOnly, LabelText("竣工保护剩余回合")] private int workerProtectionTurnsRemaining;

        [NonSerialized] private BuildingBase owner;
        [SerializeField, HideInInspector] private bool defaultsConfigured;
        private BuildingJobCalculation lastJobCalculation;
        private BuildingJobCalculation lastJobCalculationWithoutSubsidy;
        private readonly List<BuildingWorkforceAttractionFactor> factors = new List<BuildingWorkforceAttractionFactor>();

        public override string ModuleDescription => "处理岗位吸引、补贴、招工/离职、就业人口和岗位运行时状态。";
        public int CurrentWorkers => currentWorkers;
        public int MaxWorkers => maxWorkers;
        public int StableWorkers => stableWorkers;
        public bool AutoFullWorkerSubsidyEnabled => autoFullWorkerSubsidyEnabled;
        public int TargetStableWorkers => targetStableWorkers;
        public int TargetSubsidyGoldPerTurn => targetSubsidyGoldPerTurn;
        public int PaidSubsidyGoldThisTurn => paidSubsidyGoldThisTurn;
        public bool InitialWorkersGranted => initialWorkersGranted;
        public int WorkerProtectionTurnsRemaining => workerProtectionTurnsRemaining;
        public bool IsWorkerTurnoverProtected => workerProtectionTurnsRemaining > 0;
        public int MissingWorkersToFull => Mathf.Max(0, maxWorkers - currentWorkers);
        public int RecruitToFullWorkerCount => CalculateRecruitCount();
        public int RecruitToFullCost => CalculateRecruitCost();
        public bool CanRecruitToFull => CalculateCanRecruit();
        public float RawJobAttraction => lastJobCalculation.RawAttraction;
        public float JobAttraction => jobAttraction;
        public float JobAttractionWithoutSubsidy => jobAttractionWithoutSubsidy;
        public float SubsidyAttractionPerGold => BuildingJobSystem.CalculateSubsidyAttractionPerGold(maxWorkers);
        public float SubsidyAttractionBonus => paidSubsidyGoldThisTurn * SubsidyAttractionPerGold;
        public float TargetSubsidyAttractionBonus => targetSubsidyGoldPerTurn * SubsidyAttractionPerGold;
        public float PreviewJobAttractionWithTargetSubsidy => BuildingJobSystem.CalculateAttractionWithSubsidy(jobAttractionWithoutSubsidy, targetSubsidyGoldPerTurn, maxWorkers);
        public float FullWorkerRequiredAttraction => BuildingJobSystem.CalculateFullWorkerRequiredAttraction(maxWorkers);
        public float JobAttractionGapToFullWorkers => Mathf.Max(0f, FullWorkerRequiredAttraction - jobAttractionWithoutSubsidy);
        public IReadOnlyList<BuildingWorkforceAttractionFactor> WorkforceAttractionFactors => BuildFactors();
        public bool IsFullyStaffed => currentWorkers >= maxWorkers;
        public ItemDefinition GoldItemDefinition => goldItemDefinition;
        private string GoldItemId => goldItemDefinition == null ? string.Empty : goldItemDefinition.ItemId;

        public void Bind(BuildingBase building)
        {
            owner = building;
            Normalize();
            Recalculate(false);
        }

        public void OnBuildingInitialized(BuildingBase building) => Bind(building);
        public void OnBuildingRegistered(BuildingBase building) => Bind(building);
        public void OnBuildingPlaced(BuildingBase building) => OnPlaced(building);
        public void OnBuildingConstructionCompleted(BuildingBase building) => OnConstructionCompleted(building);

        public void OnBuildingLevelApplied(
            BuildingBase building,
            int previousLevel,
            int currentLevel) => Bind(building);

        public void OnBuildingUnregistered(BuildingBase building) => OnUnregistered(building);
        public bool ProcessAutomaticTurn(BuildingBase building) => ProcessTurn(building);

        public void ConfigureDefaultsIfUnset(int max, int initial, float attraction, int recruitCost, bool autoSubsidy, int targetWorkers, ItemDefinition goldItem)
        {
            if (defaultsConfigured)
            {
                return;
            }

            ApplyConfiguration(max, initial, attraction, recruitCost, autoSubsidy, targetWorkers, goldItem);
            defaultsConfigured = true;
        }

        public void ApplyConfiguration(
            int max,
            int initial,
            float attraction,
            int recruitCost,
            bool defaultAutoSubsidy,
            int defaultTargetWorkers,
            ItemDefinition goldItem)
        {
            var requestedMax = Mathf.Max(1, max);
            if (currentWorkers > requestedMax)
            {
                Debug.LogError(
                    $"岗位等级配置不能把最大岗位从当前工人数 {currentWorkers} 降到 {requestedMax}。已保留当前工人数以避免升级丢失状态。");
                requestedMax = currentWorkers;
            }

            maxWorkers = requestedMax;
            initialWorkersOnPlaced = Mathf.Clamp(initial, 0, maxWorkers);
            baseJobAttraction = Mathf.Max(0f, attraction);
            singleRecruitCost = Mathf.Max(0, recruitCost);
            goldItemDefinition = goldItem;

            if (!initialWorkersGranted)
            {
                autoFullWorkerSubsidyEnabled = defaultAutoSubsidy;
                targetStableWorkers = Mathf.Clamp(defaultTargetWorkers, 0, maxWorkers);
            }
            else
            {
                targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
            }

            defaultsConfigured = true;
            Normalize();
            Recalculate(false);
        }

        public void OnPlaced(BuildingBase building)
        {
            Bind(building);
            if (!initialWorkersGranted)
            {
                initialWorkersGranted = true;
                var granted = Mathf.Min(initialWorkersOnPlaced, maxWorkers - currentWorkers);
                if (granted > 0 && GetAvailablePopulation() >= granted)
                {
                    currentWorkers += granted;
                }
            }

            Recalculate(false);
            RefreshEmployedPopulation();
        }

        private void OnConstructionCompleted(BuildingBase building)
        {
            Bind(building);
            workerProtectionTurnsRemaining = NewConstructionWorkerProtectionTurns;
            FillVacantJobsFromAvailablePopulation();
            Recalculate(false);
            RefreshEmployedPopulation();
        }

        public bool ProcessTurn(BuildingBase building)
        {
            Bind(building);
            ClearTurnState();
            var inventory = owner?.GameSystem?.Services.Inventory;
            if (inventory == null)
            {
                return false;
            }

            Recalculate();
            TryPaySubsidy(inventory);
            Recalculate();
            ProcessWorkerTurn();
            AdvanceWorkerProtection();
            Recalculate();
            RefreshEmployedPopulation();
            return true;
        }

        public void OnUnregistered(BuildingBase building)
        {
            Bind(building);
            currentWorkers = 0;
            workerProtectionTurnsRemaining = 0;
            RefreshEmployedPopulation();
        }

        public void SetAutoFullWorkerSubsidyEnabled(bool enabled)
        {
            autoFullWorkerSubsidyEnabled = enabled;
            if (enabled)
            {
                targetStableWorkers = maxWorkers;
            }

            Recalculate(false);
            owner?.NotifyStateChanged();
        }

        public void SetTargetStableWorkers(int value)
        {
            autoFullWorkerSubsidyEnabled = false;
            Recalculate(false);
            targetStableWorkers = Mathf.Clamp(value, stableWorkersWithoutSubsidy, maxWorkers);
            Recalculate(false);
            owner?.NotifyStateChanged();
        }

        public bool TryRecruitToFull()
        {
            ClearRecruitState();
            var inventory = owner?.GameSystem?.Services.Inventory;
            if (inventory == null || string.IsNullOrWhiteSpace(GoldItemId))
            {
                return false;
            }

            Recalculate();
            if (CalculateRecruitCount() <= 0)
            {
                return false;
            }

            var cost = CalculateRecruitCost();
            if (cost > 0 && !inventory.TryRemoveItem(GoldItemId, cost))
            {
                return false;
            }

            currentWorkers++;
            Recalculate();
            RefreshEmployedPopulation();
            owner?.NotifyStateChanged();
            return true;
        }

        public override string GetOverviewFragment(BuildingBase building)
        {
            Bind(building);
            return IsWorkerTurnoverProtected
                ? Landsong.Localization.L10n.Gameplay(
                    "gameplay.building.overview.workers_protected",
                    "工人 {0}/{1}，保护 {2} 回合",
                    CurrentWorkers,
                    MaxWorkers,
                    WorkerProtectionTurnsRemaining)
                : Landsong.Localization.L10n.Gameplay("gameplay.building.overview.workers", "工人 {0}/{1}", CurrentWorkers, MaxWorkers);
        }

        public override void AppendRuntimeStatuses(
            BuildingBase building,
            ref List<BuildingRuntimeStatus> statuses)
        {
            Bind(building);
            var requiredWorkers = ResolveRequiredWorkers(building);
            AddRuntimeStatus(
                ref statuses,
                CurrentWorkers < requiredWorkers
                    ? new BuildingRuntimeStatus(
                        BuildingRuntimeStatusCatalog.BS_工人不足,
                        "工人不足",
                        CurrentWorkers,
                        requiredWorkers)
                    : default);
        }

        public override void Normalize()
        {
            maxWorkers = Mathf.Max(1, maxWorkers);
            initialWorkersOnPlaced = Mathf.Clamp(initialWorkersOnPlaced, 0, maxWorkers);
            baseJobAttraction = Mathf.Max(0f, baseJobAttraction);
            singleRecruitCost = Mathf.Max(0, singleRecruitCost);
            targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
            currentWorkers = Mathf.Clamp(currentWorkers, 0, maxWorkers);
            workerProtectionTurnsRemaining = Mathf.Clamp(
                workerProtectionTurnsRemaining,
                0,
                NewConstructionWorkerProtectionTurns);
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new WorkforceState
            {
                CurrentWorkers = currentWorkers,
                AutoFullWorkerSubsidyEnabled = autoFullWorkerSubsidyEnabled,
                TargetStableWorkers = targetStableWorkers,
                InitialWorkersGranted = initialWorkersGranted,
                WorkerProtectionTurnsRemaining = workerProtectionTurnsRemaining
            });
            return !string.IsNullOrWhiteSpace(json);
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<WorkforceState>(json);
            if (state == null)
            {
                return;
            }

            currentWorkers = Mathf.Clamp(state.CurrentWorkers, 0, maxWorkers);
            autoFullWorkerSubsidyEnabled = state.AutoFullWorkerSubsidyEnabled;
            targetStableWorkers = Mathf.Clamp(state.TargetStableWorkers, 0, maxWorkers);
            initialWorkersGranted = state.InitialWorkersGranted;
            workerProtectionTurnsRemaining = Mathf.Clamp(
                state.WorkerProtectionTurnsRemaining,
                0,
                NewConstructionWorkerProtectionTurns);
            Recalculate(false);
            owner?.GameSystem?.RefreshInventorySlotCapacity();
        }

        private void Recalculate(bool allowEvent = true)
        {
            if (owner == null)
            {
                return;
            }

            var module = owner.TryGetModule<BM_附近人口岗位吸引>(out var nearbyModule) ? nearbyModule : null;
            var radius = module == null ? 0 : module.PopulationSearchRadius;
            var perPopulation = module == null ? 0f : module.AttractionPerNearbyPopulation;
            var buildings = owner.GameSystem?.Services.Buildings?.Buildings;
            var modifiers = BuildingJobSystem.ResolveGlobalAttractionModifiers(owner, owner.GameSystem);
            lastJobCalculationWithoutSubsidy = BuildingJobSystem.Calculate(new BuildingJobCalculationInput(maxWorkers, currentWorkers, baseJobAttraction, BuildingJobSystem.CountNearbyPopulation(owner, buildings, radius), BuildingJobSystem.CountPopulationCells(owner, radius), perPopulation, modifiers, 0f, singleRecruitCost));
            stableWorkersWithoutSubsidy = lastJobCalculationWithoutSubsidy.StableWorkers;
            jobAttractionWithoutSubsidy = lastJobCalculationWithoutSubsidy.Attraction;
            targetStableWorkers = autoFullWorkerSubsidyEnabled ? maxWorkers : Mathf.Clamp(targetStableWorkers, stableWorkersWithoutSubsidy, maxWorkers);
            targetSubsidyGoldPerTurn = BuildingJobSystem.CalculateRequiredSubsidyGoldForTargetStableWorkers(maxWorkers, jobAttractionWithoutSubsidy, targetStableWorkers);
            lastJobCalculation = BuildingJobSystem.Calculate(new BuildingJobCalculationInput(maxWorkers, currentWorkers, baseJobAttraction, BuildingJobSystem.CountNearbyPopulation(owner, buildings, radius), BuildingJobSystem.CountPopulationCells(owner, radius), perPopulation, modifiers, SubsidyAttractionBonus, singleRecruitCost));
            stableWorkers = lastJobCalculation.StableWorkers;
            jobAttraction = lastJobCalculation.Attraction;
        }

        private bool TryPaySubsidy(InventoryService inventory)
        {
            paidSubsidyGoldThisTurn = 0;
            if (targetSubsidyGoldPerTurn <= 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(GoldItemId) || !inventory.TryRemoveItem(GoldItemId, targetSubsidyGoldPerTurn))
            {
                return false;
            }

            paidSubsidyGoldThisTurn = targetSubsidyGoldPerTurn;
            return true;
        }

        private void ProcessWorkerTurn()
        {
            if (currentWorkers < stableWorkers && GetAvailablePopulation() > 0 && RollChance(lastJobCalculation.RecruitChancePercent))
            {
                currentWorkers = Mathf.Min(maxWorkers, currentWorkers + 1);
            }
            else if (!IsWorkerTurnoverProtected
                     && currentWorkers > stableWorkers
                     && RollChance(lastJobCalculation.ResignChancePercent))
            {
                currentWorkers = Mathf.Max(0, currentWorkers - 1);
            }
        }

        private void FillVacantJobsFromAvailablePopulation()
        {
            var recruitedWorkers = Mathf.Min(
                Mathf.Max(0, maxWorkers - currentWorkers),
                GetAvailablePopulation());
            currentWorkers += recruitedWorkers;
        }

        private void AdvanceWorkerProtection()
        {
            if (workerProtectionTurnsRemaining > 0)
            {
                workerProtectionTurnsRemaining--;
            }
        }

        private int GetAvailablePopulation()
        {
            return owner == null ? 0 : BuildingJobSystem.GetAvailablePopulation(owner.GameSystem, owner.GameSystem?.Services.Buildings?.Buildings);
        }

        private void RefreshEmployedPopulation()
        {
            owner?.GameSystem?.RefreshInventorySlotCapacity();
            if (owner?.GameSystem?.Services.Dynasty == null || owner.GameSystem.Services.Buildings == null)
            {
                return;
            }

            owner.GameSystem.Services.Dynasty.SetEmployedPopulation(BuildingJobSystem.CountCurrentWorkers(owner.GameSystem.Services.Buildings.Buildings));
        }

        private int CalculateRecruitCount()
        {
            return currentWorkers < maxWorkers && currentWorkers + 1 <= stableWorkers && GetAvailablePopulation() > 0 ? 1 : 0;
        }

        private int CalculateRecruitCost()
        {
            return currentWorkers >= maxWorkers ? 0 : Mathf.CeilToInt(singleRecruitCost * (1f + (100f - jobAttraction) / 100f));
        }

        private bool CalculateCanRecruit()
        {
            var inventory = owner?.GameSystem?.Services.Inventory;
            var cost = CalculateRecruitCost();
            return CalculateRecruitCount() > 0 && inventory != null && (cost <= 0 || inventory.GetQuantity(GoldItemId) >= cost);
        }

        private IReadOnlyList<BuildingWorkforceAttractionFactor> BuildFactors()
        {
            factors.Clear();
            factors.Add(new BuildingWorkforceAttractionFactor("基础吸引力", lastJobCalculationWithoutSubsidy.BaseAttraction));
            factors.Add(new BuildingWorkforceAttractionFactor("补贴就业吸引力", SubsidyAttractionBonus));
            return factors;
        }

        private void ClearTurnState()
        {
            paidSubsidyGoldThisTurn = 0;
        }

        private void ClearRecruitState()
        {
        }

        private static bool RollChance(float chancePercent)
        {
            return UnityEngine.Random.value * 100f < Mathf.Clamp(chancePercent, 0f, 100f);
        }

        private int ResolveRequiredWorkers(BuildingBase building)
        {
            var requirements = building?.GetCapabilities<IBuildingWorkforceRequirementSource>();
            if (requirements == null || requirements.Count == 0)
            {
                return MaxWorkers;
            }

            var required = 0;
            for (var i = 0; i < requirements.Count; i++)
            {
                required = Mathf.Max(required, requirements[i].RequiredWorkers);
            }

            return Mathf.Clamp(required, 0, MaxWorkers);
        }
    }

    public static class BuildingWorkforceUtility
    {
        public static bool TryGetSource(BuildingBase building, out IBuildingWorkforceFundingSource source)
        {
            if (building != null
                && building.TryGetCapability<IBuildingWorkforceFundingSource>(out source))
            {
                if (source is BM_岗位运营 module)
                {
                    module.Bind(building);
                }
                return true;
            }

            source = null;
            return false;
        }
    }

    public interface IBuildingWorkforceModuleHost
    {
        BM_岗位运营 GetWorkforceModule();
    }
}
