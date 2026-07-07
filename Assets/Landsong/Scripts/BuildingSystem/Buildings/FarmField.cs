using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem.Buildings
{
    public sealed class FarmField : BuildingBase, IBuildingWorkforceFundingSource, IBuildingCropFieldSource, IBuildingCropFieldActions
    {
        private const string DefaultGoldItemId = "金币";
        private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
        private const string StatusInvalidGoldItem = BuildingRuntimeStatusCatalog.BS_金币配置异常;
        private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
        private const string StatusWorkerShortage = BuildingRuntimeStatusCatalog.BS_缺工;
        private const string StatusRecruitGoldMissing = BuildingRuntimeStatusCatalog.BS_招工金币不足;
        private const string StatusSubsidyGoldMissing = BuildingRuntimeStatusCatalog.BS_补贴金币不足;

        private static readonly IReadOnlyList<BuildingCropOption> EmptyCropOptions =
            Array.Empty<BuildingCropOption>();

        private static readonly IReadOnlyList<BuildingResourceChange> EmptyHarvestRewards =
            Array.Empty<BuildingResourceChange>();

        [TitleGroup("岗位")]
        [SerializeField, LabelText("最大岗位"), Min(1)]
        private int maxWorkers = 2;

        [TitleGroup("岗位")]
        [SerializeField, LabelText("建造完成初始工人"), Min(0)]
        private int initialWorkersOnPlaced;

        [TitleGroup("岗位")]
        [SerializeField, LabelText("基础吸引力"), Min(0f)]
        private float baseJobAttraction = 55f;

        [TitleGroup("岗位")]
        [SerializeField, LabelText("单人招工费用"), Min(0)]
        private int singleRecruitCost = 10;

        [TitleGroup("岗位补贴")]
        [SerializeField, LabelText("自动补贴满岗位")]
        private bool autoFullWorkerSubsidyEnabled;

        [TitleGroup("岗位补贴")]
        [SerializeField, LabelText("目标稳定工人"), Min(0)]
        private int targetStableWorkers;

        [TitleGroup("资源")]
        [SerializeField, LabelText("金币物品ID")]
        private string goldItemId = DefaultGoldItemId;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("当前工人")]
        private int currentWorkers;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("稳定工人")]
        private int stableWorkers;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("无补贴稳定工人")]
        private int stableWorkersWithoutSubsidy;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("原始岗位吸引力")]
        private float rawJobAttraction;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("岗位吸引力")]
        private float jobAttraction;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("无补贴岗位吸引力")]
        private float jobAttractionWithoutSubsidy;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("附近人口")]
        private int nearbyPopulation;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("人口搜索格数")]
        private int populationCellCount;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("上次人口搜索半径")]
        private int lastPopulationSearchRadius;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("上次附近每人口吸引力")]
        private float lastAttractionPerNearbyPopulation;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("人口密度")]
        private float populationDensity;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("附近人口吸引力加成")]
        private float populationAttractionBonus;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("全局吸引力修正合计")]
        private float globalAttractionModifierTotal;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("每金币补贴吸引力")]
        private float subsidyAttractionPerGold;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("本回合补贴吸引力")]
        private float subsidyAttractionBonus;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("目标补贴吸引力")]
        private float targetSubsidyAttractionBonus;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("目标补贴金币/回合")]
        private int targetSubsidyGoldPerTurn;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("本回合已支付补贴金币")]
        private int paidSubsidyGoldThisTurn;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("已发放初始工人")]
        private bool initialWorkersGranted;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("上回合招到工人")]
        private bool lastTurnRecruitedWorker;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("上回合补贴金币不足")]
        private bool lastTurnSubsidyGoldMissing;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("上回合可用人口不足")]
        private bool lastTurnNoAvailablePopulation;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("上次异常状态ID")]
        private string lastAbnormalStatusId = string.Empty;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("上次异常状态文本")]
        private string lastAbnormalStatusText = string.Empty;

        private readonly List<BuildingWorkforceAttractionFactor> workforceAttractionFactors =
            new List<BuildingWorkforceAttractionFactor>();

        private BuildingJobCalculation lastJobCalculation;
        private BuildingJobCalculation lastJobCalculationWithoutSubsidy;
        private bool hasCalculatedSubsidyTarget;

        public int CurrentWorkers => currentWorkers;
        public int MaxWorkers => maxWorkers;
        public int StableWorkers => stableWorkers;
        public float RawJobAttraction => rawJobAttraction;
        public float JobAttraction => jobAttraction;
        public bool AutoFullWorkerSubsidyEnabled => autoFullWorkerSubsidyEnabled;
        public int TargetStableWorkers => targetStableWorkers;
        public int TargetSubsidyGoldPerTurn => targetSubsidyGoldPerTurn;
        public int PaidSubsidyGoldThisTurn => paidSubsidyGoldThisTurn;
        public int MissingWorkersToFull => Mathf.Max(0, maxWorkers - currentWorkers);
        public int RecruitToFullWorkerCount => CalculateRecruitToFullWorkerCount();
        public int RecruitToFullCost => CalculateRecruitWorkerCost();
        public bool CanRecruitToFull => CalculateCanRecruitToFull();
        public float JobAttractionWithoutSubsidy => jobAttractionWithoutSubsidy;
        public float SubsidyAttractionPerGold => subsidyAttractionPerGold;
        public float SubsidyAttractionBonus => subsidyAttractionBonus;
        public float TargetSubsidyAttractionBonus => targetSubsidyAttractionBonus;
        public float PreviewJobAttractionWithTargetSubsidy =>
            BuildingJobSystem.CalculateAttractionWithSubsidy(
                jobAttractionWithoutSubsidy,
                targetSubsidyGoldPerTurn,
                maxWorkers);
        public float FullWorkerRequiredAttraction => BuildingJobSystem.CalculateFullWorkerRequiredAttraction(maxWorkers);
        public float JobAttractionGapToFullWorkers =>
            Mathf.Max(0f, FullWorkerRequiredAttraction - jobAttractionWithoutSubsidy);
        public IReadOnlyList<BuildingWorkforceAttractionFactor> WorkforceAttractionFactors =>
            BuildWorkforceAttractionFactors();

        public string PlantedCropId => TryGetCropGrowthModule(out var cropGrowth) ? cropGrowth.PlantedCropId : string.Empty;
        public string PlantedCropDisplayName => TryGetCropGrowthModule(out var cropGrowth) ? cropGrowth.PlantedCropDisplayName : string.Empty;
        public int GrowthProgressTurns => TryGetCropGrowthModule(out var cropGrowth) ? cropGrowth.GrowthProgressTurns : 0;
        public int RequiredGrowTurns => TryGetCropGrowthModule(out var cropGrowth) ? cropGrowth.RequiredGrowTurns : 0;
        public int RemainingGrowTurns => TryGetCropGrowthModule(out var cropGrowth) ? cropGrowth.RemainingGrowTurns : 0;
        public bool HasCrop => TryGetCropGrowthModule(out var cropGrowth) && cropGrowth.HasCrop;
        public bool IsMature => TryGetCropGrowthModule(out var cropGrowth) && cropGrowth.IsMature;
        public bool AutoHarvestEnabled => TryGetCropGrowthModule(out var cropGrowth) && cropGrowth.AutoHarvestEnabled;
        public IReadOnlyList<BuildingCropOption> CropOptions =>
            TryGetCropGrowthModule(out var cropGrowth) ? cropGrowth.CropOptions : EmptyCropOptions;
        public IReadOnlyList<BuildingResourceChange> LastHarvestRewards =>
            TryGetCropGrowthModule(out var cropGrowth) ? cropGrowth.LastHarvestRewards : EmptyHarvestRewards;

        [ShowInInspector, ReadOnly, LabelText("岗位调试信息")]
        public string JobDebugInfo => BuildingJobSystem.FormatDebugText(lastJobCalculation);

        protected override void Awake()
        {
            base.Awake();
            NormalizeConfiguration();
            RecalculateJobState(false);
        }

        protected override void OnInitialized()
        {
            NormalizeConfiguration();
            RecalculateJobState(false);
        }

        protected override void OnRegistered()
        {
            RecalculateJobState(false);
            RefreshDynastyEmployedPopulation();
        }

        protected override void OnPlaced()
        {
            NormalizeConfiguration();
            TryGrantInitialWorkersOnPlaced();
            RecalculateJobState(false);
            RefreshDynastyEmployedPopulation();
        }

        protected override bool OnTurn()
        {
            NormalizeConfiguration();
            ClearLastTurnState();

            var inventory = GameSystem == null ? null : GameSystem.Inventory;
            RecalculateJobState();
            TryPaySubsidyForCurrentTurn(inventory);
            RecalculateJobState();
            ProcessWorkerTurn();
            RecalculateJobState();

            var cropGrowth = EnsureCropGrowthModule();
            var succeeded = true;
            if (cropGrowth.HasCrop)
            {
                if (currentWorkers < maxWorkers)
                {
                    SetLastAbnormalStatus(StatusInsufficientWorkers, "工人不足");
                    succeeded = false;
                }
                else
                {
                    succeeded = cropGrowth.ProcessTurn(this, true, out _);
                    if (!succeeded && cropGrowth.TryGetLastRuntimeStatus(out var status))
                    {
                        SetLastAbnormalStatus(status.StatusId, status.DisplayName);
                    }
                }
            }

            RefreshDynastyEmployedPopulation();
            return succeeded;
        }

        protected override BuildingDataBase CaptureBuildingData()
        {
            return new FarmFieldData
            {
                CurrentWorkers = currentWorkers,
                AutoFullWorkerSubsidyEnabled = autoFullWorkerSubsidyEnabled,
                TargetStableWorkers = targetStableWorkers,
                InitialWorkersGranted = initialWorkersGranted,
                LastTurnRecruitedWorker = lastTurnRecruitedWorker,
                LastTurnSubsidyGoldMissing = lastTurnSubsidyGoldMissing,
                LastTurnNoAvailablePopulation = lastTurnNoAvailablePopulation,
                LastAbnormalStatusId = lastAbnormalStatusId,
                LastAbnormalStatusText = lastAbnormalStatusText
            };
        }

        protected override void RestoreBuildingData(BuildingDataBase data)
        {
            if (data is not FarmFieldData farmData)
            {
                return;
            }

            currentWorkers = farmData.CurrentWorkers;
            autoFullWorkerSubsidyEnabled = farmData.AutoFullWorkerSubsidyEnabled;
            targetStableWorkers = farmData.TargetStableWorkers;
            initialWorkersGranted = farmData.InitialWorkersGranted;
            lastTurnRecruitedWorker = farmData.LastTurnRecruitedWorker;
            lastTurnSubsidyGoldMissing = farmData.LastTurnSubsidyGoldMissing;
            lastTurnNoAvailablePopulation = farmData.LastTurnNoAvailablePopulation;
            lastAbnormalStatusId = string.IsNullOrWhiteSpace(farmData.LastAbnormalStatusId)
                ? string.Empty
                : farmData.LastAbnormalStatusId.Trim();
            lastAbnormalStatusText = string.IsNullOrWhiteSpace(farmData.LastAbnormalStatusText)
                ? lastAbnormalStatusId
                : farmData.LastAbnormalStatusText.Trim();

            NormalizeConfiguration();
            RecalculateJobState(false);
            RefreshDynastyEmployedPopulation();
        }

        protected override void OnReceiveReplacementState(BuildingBase sourceBuilding)
        {
            if (sourceBuilding is not FarmField sourceFarm)
            {
                return;
            }

            currentWorkers = Mathf.Clamp(sourceFarm.currentWorkers, 0, maxWorkers);
            autoFullWorkerSubsidyEnabled = sourceFarm.autoFullWorkerSubsidyEnabled;
            targetStableWorkers = Mathf.Clamp(sourceFarm.targetStableWorkers, 0, maxWorkers);
            initialWorkersGranted = true;
            ClearLastTurnState();
            NormalizeConfiguration();
            RecalculateJobState(false);
            RefreshDynastyEmployedPopulation();
        }

        protected override void OnUnregistered()
        {
            currentWorkers = 0;
            RefreshDynastyEmployedPopulation();
        }

        public override string GetOverviewInfo()
        {
            if (!TryGetCropGrowthModule(out var cropGrowth) || !cropGrowth.HasCrop)
            {
                return $"工人 {currentWorkers}/{maxWorkers}，未种植";
            }

            var cropStatus = cropGrowth.IsMature
                ? "可收获"
                : $"成熟剩余 {cropGrowth.RemainingGrowTurns} 回合";
            return $"工人 {currentWorkers}/{maxWorkers}，{cropStatus}";
        }

        public override IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
        {
            List<BuildingFunctionBlockEntry> entries = null;

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "需要工人",
                    maxWorkers,
                    new BuildingFunctionBlockSidebarRow("当前工人", $"{currentWorkers}/{maxWorkers}")));

            AppendBuildingModuleFunctionBlockEntries(ref entries);
            return entries ?? EmptyFunctionBlockEntries;
        }

        public override IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
        {
            List<BuildingRuntimeStatus> statuses = null;
            var hasCrop = TryGetCropGrowthModule(out var cropGrowth) && cropGrowth.HasCrop;

            AppendRuntimeStatus(
                ref statuses,
                hasCrop && currentWorkers < maxWorkers
                    ? new BuildingRuntimeStatus(StatusInsufficientWorkers, "工人不足", currentWorkers, maxWorkers)
                    : default);

            AppendRuntimeStatus(
                ref statuses,
                currentWorkers >= maxWorkers && currentWorkers < stableWorkers
                    ? new BuildingRuntimeStatus(StatusWorkerShortage, "缺工", currentWorkers, stableWorkers)
                    : default);

            AppendRuntimeStatus(
                ref statuses,
                lastTurnSubsidyGoldMissing
                    ? new BuildingRuntimeStatus(StatusSubsidyGoldMissing, "补贴金币不足", paidSubsidyGoldThisTurn, targetSubsidyGoldPerTurn)
                    : default);

            AppendRuntimeStatus(
                ref statuses,
                ShouldAddLastAbnormalStatus()
                    ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
                    : default);

            if (cropGrowth != null && cropGrowth.TryGetLastRuntimeStatus(out var cropStatus))
            {
                AppendRuntimeStatus(ref statuses, ShouldAppendCropRuntimeStatus(cropStatus) ? cropStatus : default);
            }

            AppendCommonRuntimeStatuses(ref statuses);
            return statuses ?? EmptyRuntimeStatuses;
        }

        [Button("立即招工")]
        public bool TryRecruitImmediately()
        {
            return TryRecruitToFull();
        }

        public void SetAutoFullWorkerSubsidyEnabled(bool enabled)
        {
            NormalizeConfiguration();
            autoFullWorkerSubsidyEnabled = enabled;
            if (autoFullWorkerSubsidyEnabled)
            {
                targetStableWorkers = maxWorkers;
            }

            RecalculateJobState(false);
            NotifyStateChanged();
        }

        public void SetTargetStableWorkers(int value)
        {
            NormalizeConfiguration();
            autoFullWorkerSubsidyEnabled = false;
            RecalculateJobState(false);
            targetStableWorkers = Mathf.Clamp(value, stableWorkersWithoutSubsidy, maxWorkers);
            RecalculateJobState(false);
            NotifyStateChanged();
        }

        public bool TryRecruitToFull()
        {
            NormalizeConfiguration();
            ClearRecruitAttemptState();

            var inventory = GameSystem == null ? null : GameSystem.Inventory;
            if (inventory == null)
            {
                SetLastAbnormalStatus(StatusMissingInventory, "库存服务缺失");
                NotifyStateChanged();
                return false;
            }

            if (!HasUsableItemId(goldItemId))
            {
                SetLastAbnormalStatus(StatusInvalidGoldItem, "金币配置异常");
                NotifyStateChanged();
                return false;
            }

            RecalculateJobState();
            if (currentWorkers >= maxWorkers || currentWorkers + 1 > stableWorkers)
            {
                NotifyStateChanged();
                return false;
            }

            if (GetAvailablePopulation() <= 0)
            {
                lastTurnNoAvailablePopulation = true;
                SendBuildingEvent(GameEventCatalog.GE_可用人口不足, "可用人口不足");
                NotifyStateChanged();
                return false;
            }

            const int recruitCount = 1;
            var recruitCost = CalculateImmediateRecruitCost(recruitCount);
            if (recruitCost > 0 && inventory.GetQuantity(goldItemId) < recruitCost)
            {
                SetLastAbnormalStatus(StatusRecruitGoldMissing, "招工金币不足");
                SendBuildingEvent(GameEventCatalog.GE_招工未完全补满, "招工金币不足，未能补满工人");
                NotifyStateChanged();
                return false;
            }

            if (recruitCost > 0 && !inventory.TryRemoveItem(goldItemId, recruitCost))
            {
                SetLastAbnormalStatus(StatusRecruitGoldMissing, "招工金币不足");
                NotifyStateChanged();
                return false;
            }

            currentWorkers += recruitCount;
            lastTurnRecruitedWorker = true;
            RecalculateJobState();
            RefreshDynastyEmployedPopulation();
            NotifyStateChanged();
            return true;
        }

        public bool CanPlant(string cropId)
        {
            return EnsureCropGrowthModule().CanPlant(this, cropId);
        }

        public bool CanHarvest()
        {
            return EnsureCropGrowthModule().CanHarvest();
        }

        public bool CanClearCrop()
        {
            return EnsureCropGrowthModule().CanClearCrop();
        }

        public bool TryPlant(string cropId)
        {
            var succeeded = EnsureCropGrowthModule().TryPlant(this, cropId, out var stateChanged);
            if (stateChanged)
            {
                NotifyStateChanged();
            }
            else if (!succeeded)
            {
                NotifyStateChanged();
            }

            return succeeded;
        }

        public bool TryHarvest()
        {
            var succeeded = EnsureCropGrowthModule().TryHarvest(this, out var stateChanged);
            if (stateChanged)
            {
                NotifyStateChanged();
            }
            else if (!succeeded)
            {
                NotifyStateChanged();
            }

            return succeeded;
        }

        public bool TryClearCrop()
        {
            var succeeded = EnsureCropGrowthModule().TryClearCrop(this, out var stateChanged);
            if (stateChanged)
            {
                NotifyStateChanged();
            }

            return succeeded;
        }

        public bool TrySetAutoHarvestEnabled(bool enabled)
        {
            var succeeded = EnsureCropGrowthModule().TrySetAutoHarvestEnabled(enabled, out var stateChanged);
            if (stateChanged)
            {
                NotifyStateChanged();
            }

            return succeeded;
        }

        private void ProcessWorkerTurn()
        {
            if (maxWorkers <= 0)
            {
                return;
            }

            if (currentWorkers < stableWorkers)
            {
                if (GetAvailablePopulation() <= 0)
                {
                    lastTurnNoAvailablePopulation = true;
                    SendBuildingEvent(GameEventCatalog.GE_可用人口不足, "可用人口不足");
                    return;
                }

                if (RollChance(lastJobCalculation.RecruitChancePercent))
                {
                    currentWorkers = Mathf.Min(maxWorkers, currentWorkers + 1);
                    lastTurnRecruitedWorker = true;
                }

                return;
            }

            if (currentWorkers <= stableWorkers)
            {
                return;
            }

            if (RollChance(lastJobCalculation.ResignChancePercent))
            {
                currentWorkers = Mathf.Max(0, currentWorkers - 1);
                SendBuildingEvent(GameEventCatalog.GE_工人离职, "工人离职");
            }
        }

        private bool TryPaySubsidyForCurrentTurn(InventoryService inventory)
        {
            paidSubsidyGoldThisTurn = 0;
            if (targetSubsidyGoldPerTurn <= 0)
            {
                return true;
            }

            if (!HasUsableItemId(goldItemId))
            {
                SetLastAbnormalStatus(StatusInvalidGoldItem, "金币配置异常");
                return false;
            }

            if (inventory == null || inventory.GetQuantity(goldItemId) < targetSubsidyGoldPerTurn)
            {
                lastTurnSubsidyGoldMissing = true;
                SetLastAbnormalStatus(StatusSubsidyGoldMissing, "补贴金币不足");
                SendBuildingEvent(
                    GameEventCatalog.GE_补贴金币不足,
                    $"补贴需要 {targetSubsidyGoldPerTurn} 金币/回合，金币不足，本回合补贴未生效");
                return false;
            }

            if (!inventory.TryRemoveItem(goldItemId, targetSubsidyGoldPerTurn))
            {
                lastTurnSubsidyGoldMissing = true;
                SetLastAbnormalStatus(StatusSubsidyGoldMissing, "补贴金币不足");
                SendBuildingEvent(GameEventCatalog.GE_补贴金币不足, "补贴金币不足，本回合补贴未生效");
                return false;
            }

            paidSubsidyGoldThisTurn = targetSubsidyGoldPerTurn;
            return true;
        }

        private void RecalculateJobState(bool allowAutoSubsidyEvent = true)
        {
            var buildings = GameSystem == null || GameSystem.Buildings == null
                ? null
                : GameSystem.Buildings.Buildings;

            var populationModule = TryGetModule<BM_附近人口岗位吸引>(out var module)
                ? module
                : null;
            lastPopulationSearchRadius = populationModule == null ? 0 : populationModule.PopulationSearchRadius;
            lastAttractionPerNearbyPopulation = populationModule == null
                ? 0f
                : populationModule.AttractionPerNearbyPopulation;

            populationCellCount = BuildingJobSystem.CountPopulationCells(this, lastPopulationSearchRadius);
            nearbyPopulation = BuildingJobSystem.CountNearbyPopulation(this, buildings, lastPopulationSearchRadius);
            var globalAttractionModifiers = BuildingJobSystem.ResolveGlobalAttractionModifiers(
                this,
                GameSystem);

            lastJobCalculationWithoutSubsidy = BuildingJobSystem.Calculate(new BuildingJobCalculationInput(
                maxWorkers,
                currentWorkers,
                baseJobAttraction,
                nearbyPopulation,
                populationCellCount,
                lastAttractionPerNearbyPopulation,
                globalAttractionModifiers,
                0f,
                singleRecruitCost));

            stableWorkersWithoutSubsidy = lastJobCalculationWithoutSubsidy.StableWorkers;
            jobAttractionWithoutSubsidy = lastJobCalculationWithoutSubsidy.Attraction;
            subsidyAttractionPerGold = BuildingJobSystem.CalculateSubsidyAttractionPerGold(maxWorkers);
            NormalizeTargetStableWorkers();
            RefreshTargetSubsidyGold(allowAutoSubsidyEvent);
            subsidyAttractionBonus = Mathf.Max(0, paidSubsidyGoldThisTurn) * subsidyAttractionPerGold;
            targetSubsidyAttractionBonus = Mathf.Max(0, targetSubsidyGoldPerTurn) * subsidyAttractionPerGold;

            lastJobCalculation = BuildingJobSystem.Calculate(new BuildingJobCalculationInput(
                maxWorkers,
                currentWorkers,
                baseJobAttraction,
                nearbyPopulation,
                populationCellCount,
                lastAttractionPerNearbyPopulation,
                globalAttractionModifiers,
                subsidyAttractionBonus,
                singleRecruitCost));

            stableWorkers = lastJobCalculation.StableWorkers;
            rawJobAttraction = lastJobCalculation.RawAttraction;
            jobAttraction = lastJobCalculation.Attraction;
            populationDensity = lastJobCalculation.PopulationDensity;
            populationAttractionBonus = lastJobCalculation.PopulationAttractionBonus;
            globalAttractionModifierTotal = lastJobCalculation.GlobalAttractionModifierTotal;
        }

        private void NormalizeTargetStableWorkers()
        {
            if (autoFullWorkerSubsidyEnabled)
            {
                targetStableWorkers = maxWorkers;
                return;
            }

            targetStableWorkers = Mathf.Clamp(targetStableWorkers, stableWorkersWithoutSubsidy, maxWorkers);
        }

        private void RefreshTargetSubsidyGold(bool allowAutoSubsidyEvent)
        {
            var previous = targetSubsidyGoldPerTurn;
            targetSubsidyGoldPerTurn = BuildingJobSystem.CalculateRequiredSubsidyGoldForTargetStableWorkers(
                maxWorkers,
                jobAttractionWithoutSubsidy,
                targetStableWorkers);

            if (!autoFullWorkerSubsidyEnabled)
            {
                hasCalculatedSubsidyTarget = true;
                return;
            }

            if (hasCalculatedSubsidyTarget && allowAutoSubsidyEvent && previous != targetSubsidyGoldPerTurn)
            {
                SendAutoSubsidyAdjustedEvent(previous, targetSubsidyGoldPerTurn);
            }

            hasCalculatedSubsidyTarget = true;
        }

        private void SendAutoSubsidyAdjustedEvent(int previousSubsidyGold, int newSubsidyGold)
        {
            if (previousSubsidyGold == newSubsidyGold)
            {
                return;
            }

            var eventType = newSubsidyGold > previousSubsidyGold
                ? GameEventCatalog.GE_自动补贴增加
                : GameEventCatalog.GE_自动补贴减少;
            SendBuildingEvent(
                eventType,
                $"自动补贴从 {previousSubsidyGold} 调整为 {newSubsidyGold} 金币/回合");
        }

        private int GetAvailablePopulation()
        {
            var buildings = GameSystem == null || GameSystem.Buildings == null
                ? null
                : GameSystem.Buildings.Buildings;

            return BuildingJobSystem.GetAvailablePopulation(GameSystem, buildings);
        }

        private void RefreshDynastyEmployedPopulation()
        {
            if (GameSystem == null || GameSystem.Dynasty == null || GameSystem.Buildings == null)
            {
                return;
            }

            GameSystem.Dynasty.SetEmployedPopulation(BuildingJobSystem.CountCurrentWorkers(GameSystem.Buildings.Buildings));
        }

        private void NormalizeConfiguration()
        {
            maxWorkers = Mathf.Max(1, maxWorkers);
            initialWorkersOnPlaced = Mathf.Clamp(initialWorkersOnPlaced, 0, maxWorkers);
            baseJobAttraction = Mathf.Max(0f, baseJobAttraction);
            singleRecruitCost = Mathf.Max(0, singleRecruitCost);
            currentWorkers = Mathf.Clamp(currentWorkers, 0, maxWorkers);
            stableWorkers = Mathf.Clamp(stableWorkers, 0, maxWorkers);
            stableWorkersWithoutSubsidy = Mathf.Clamp(stableWorkersWithoutSubsidy, 0, maxWorkers);
            targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
            goldItemId = NormalizeItemId(goldItemId, DefaultGoldItemId);
            NormalizeBuildingModules();
            EnsureCropGrowthModule();
        }

        private void TryGrantInitialWorkersOnPlaced()
        {
            if (initialWorkersGranted)
            {
                return;
            }

            initialWorkersGranted = true;
            if (initialWorkersOnPlaced <= 0)
            {
                return;
            }

            var workersToGrant = Mathf.Min(initialWorkersOnPlaced, maxWorkers - currentWorkers);
            if (workersToGrant <= 0)
            {
                return;
            }

            if (GetAvailablePopulation() < workersToGrant)
            {
                lastTurnNoAvailablePopulation = true;
                SendBuildingEvent(GameEventCatalog.GE_可用人口不足, "可用人口不足");
                return;
            }

            currentWorkers += workersToGrant;
            lastTurnRecruitedWorker = true;
        }

        private int CalculateImmediateRecruitCost(int recruitCount)
        {
            recruitCount = Mathf.Clamp(recruitCount, 0, Mathf.Max(0, maxWorkers - currentWorkers));
            return Mathf.CeilToInt(recruitCount * singleRecruitCost * (1f + (100f - jobAttraction) / 100f));
        }

        private int CalculateRecruitToFullWorkerCount()
        {
            if (currentWorkers >= maxWorkers || currentWorkers + 1 > stableWorkers)
            {
                return 0;
            }

            return GetAvailablePopulation() <= 0 ? 0 : 1;
        }

        private int CalculateRecruitWorkerCost()
        {
            return currentWorkers >= maxWorkers ? 0 : CalculateImmediateRecruitCost(1);
        }

        private bool CalculateCanRecruitToFull()
        {
            var recruitCount = CalculateRecruitToFullWorkerCount();
            if (recruitCount <= 0 || currentWorkers + recruitCount > stableWorkers)
            {
                return false;
            }

            var inventory = GameSystem == null ? null : GameSystem.Inventory;
            if (inventory == null || !HasUsableItemId(goldItemId))
            {
                return false;
            }

            var recruitCost = CalculateRecruitWorkerCost();
            return recruitCost <= 0 || inventory.GetQuantity(goldItemId) >= recruitCost;
        }

        private void ClearLastTurnState()
        {
            paidSubsidyGoldThisTurn = 0;
            lastTurnRecruitedWorker = false;
            lastTurnSubsidyGoldMissing = false;
            lastTurnNoAvailablePopulation = false;
            lastAbnormalStatusId = string.Empty;
            lastAbnormalStatusText = string.Empty;
            EnsureCropGrowthModule().ClearLastTurnState();
        }

        private void ClearRecruitAttemptState()
        {
            lastTurnRecruitedWorker = false;
            lastTurnNoAvailablePopulation = false;
            lastAbnormalStatusId = string.Empty;
            lastAbnormalStatusText = string.Empty;
        }

        private bool ShouldAddLastAbnormalStatus()
        {
            if (string.IsNullOrWhiteSpace(lastAbnormalStatusId))
            {
                return false;
            }

            if (lastTurnSubsidyGoldMissing
                && string.Equals(lastAbnormalStatusId, StatusSubsidyGoldMissing, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private bool ShouldAppendCropRuntimeStatus(BuildingRuntimeStatus cropStatus)
        {
            return cropStatus.IsValid
                   && !string.Equals(cropStatus.StatusId, lastAbnormalStatusId, StringComparison.Ordinal);
        }

        private void SetLastAbnormalStatus(string statusId, string statusText)
        {
            lastAbnormalStatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
            lastAbnormalStatusText = string.IsNullOrWhiteSpace(statusText) ? lastAbnormalStatusId : statusText.Trim();
        }

        private bool TryGetCropGrowthModule(out BuildingCropGrowthModule cropGrowth)
        {
            return TryGetModule(out cropGrowth);
        }

        private BuildingCropGrowthModule EnsureCropGrowthModule()
        {
            return EnsureBuildingModule<BuildingCropGrowthModule>();
        }

        private IReadOnlyList<BuildingWorkforceAttractionFactor> BuildWorkforceAttractionFactors()
        {
            workforceAttractionFactors.Clear();
            AddWorkforceAttractionFactor("基础吸引力", lastJobCalculationWithoutSubsidy.BaseAttraction);
            AddWorkforceAttractionFactor("附近人口", populationAttractionBonus);

            var modifiers = lastJobCalculationWithoutSubsidy.GlobalAttractionModifiers;
            if (modifiers != null)
            {
                for (var i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    if (!modifier.IsValid)
                    {
                        continue;
                    }

                    AddWorkforceAttractionFactor(
                        string.IsNullOrWhiteSpace(modifier.DisplayText) ? "未命名修正" : modifier.DisplayText,
                        modifier.Value);
                }
            }

            AddWorkforceAttractionFactor("补贴就业吸引力", subsidyAttractionBonus);
            return workforceAttractionFactors;
        }

        private void AddWorkforceAttractionFactor(string label, float value)
        {
            var factor = new BuildingWorkforceAttractionFactor(label, value);
            if (factor.IsValid)
            {
                workforceAttractionFactors.Add(factor);
            }
        }

        private static bool RollChance(float chancePercent)
        {
            return UnityEngine.Random.value * 100f < Mathf.Clamp(chancePercent, 0f, 100f);
        }

        private void OnValidate()
        {
            NormalizeConfiguration();
        }

        [Serializable]
        [BuildingDataTypeId("building.farm_field")]
        private sealed class FarmFieldData : BuildingDataBase
        {
            public int CurrentWorkers;
            public bool AutoFullWorkerSubsidyEnabled;
            public int TargetStableWorkers;
            public bool InitialWorkersGranted;
            public bool LastTurnRecruitedWorker;
            public bool LastTurnSubsidyGoldMissing;
            public bool LastTurnNoAvailablePopulation;
            public string LastAbnormalStatusId;
            public string LastAbnormalStatusText;
        }
    }
}
