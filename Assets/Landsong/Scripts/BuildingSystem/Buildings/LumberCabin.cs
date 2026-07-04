using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

public class LumberCabin : BuildingBase, IBuildingWorkforceFundingSource, IBuildingResourceProductionSource
{
    private const string DefaultWoodItemId = "原木";
    private const string DefaultGoldItemId = "金币";
    private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
    private const string StatusInvalidWoodItem = BuildingRuntimeStatusCatalog.BS_原木配置异常;
    private const string StatusInvalidGoldItem = BuildingRuntimeStatusCatalog.BS_金币配置异常;
    private const string StatusWoodStorageFailed = BuildingRuntimeStatusCatalog.BS_原木存入失败;
    private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
    private const string StatusWorkerShortage = BuildingRuntimeStatusCatalog.BS_缺工;
    private const string StatusRecruitGoldMissing = BuildingRuntimeStatusCatalog.BS_招工金币不足;
    private const string StatusSubsidyGoldMissing = BuildingRuntimeStatusCatalog.BS_补贴金币不足;

    [Serializable]
    private struct WorkerProductionTier
    {
        [SerializeField, LabelText("最低工人数"), Min(1)]
        private int minimumWorkers;

        [SerializeField, LabelText("原木产量"), Min(0)]
        private int productionAmount;

        public WorkerProductionTier(int minimumWorkers, int productionAmount)
        {
            this.minimumWorkers = minimumWorkers;
            this.productionAmount = productionAmount;
        }

        public int MinimumWorkers => Mathf.Max(1, minimumWorkers);
        public int ProductionAmount => Mathf.Max(0, productionAmount);
        public bool IsValid => ProductionAmount > 0;

        public WorkerProductionTier Normalize(int maxWorkers)
        {
            return new WorkerProductionTier(
                Mathf.Clamp(minimumWorkers, 1, Mathf.Max(1, maxWorkers)),
                Mathf.Max(0, productionAmount));
        }
    }

    [TitleGroup("岗位")]
    [SerializeField, LabelText("最大岗位"), Min(1)] private int maxWorkers = 3;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("建造完成初始工人"), Min(0)] private int initialWorkersOnPlaced = 2;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("基础吸引力"), Min(0f)]
    [PropertyTooltip("单位：岗位吸引力点。没有人口增益和全局修正时使用该值。")]
    private float baseJobAttraction = 55f;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("单人招工费用"), Min(0)] private int singleRecruitCost = 10;

    [TitleGroup("岗位补贴")]
    [SerializeField, LabelText("自动补贴满岗位")] private bool autoFullWorkerSubsidyEnabled;

    [TitleGroup("岗位补贴")]
    [SerializeField, LabelText("目标稳定工人"), Min(0)] private int targetStableWorkers;

    [TitleGroup("产出")]
    [SerializeField] private string woodItemId = DefaultWoodItemId;

    [TitleGroup("产出")]
    [SerializeField] private string goldItemId = DefaultGoldItemId;

    [TitleGroup("产出")]
    [SerializeField, LabelText("生产周期回合"), Min(1)] private int productionIntervalTurns = 3;

    [TitleGroup("产出")]
    [SerializeField, LabelText("工人数产量表")]
    private WorkerProductionTier[] workerProductionTiers =
    {
        new WorkerProductionTier(2, 1),
        new WorkerProductionTier(3, 2)
    };

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int currentWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int stableWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int stableWorkersWithoutSubsidy;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float rawJobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float jobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float jobAttractionWithoutSubsidy;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int nearbyPopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int populationCellCount;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int lastPopulationSearchRadius;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float lastAttractionPerNearbyPopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float populationDensity;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float populationAttractionBonus;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float globalAttractionModifierTotal;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float subsidyAttractionPerGold;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float subsidyAttractionBonus;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float targetSubsidyAttractionBonus;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int targetSubsidyGoldPerTurn;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int paidSubsidyGoldThisTurn;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int lastProducedWood;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int productionProgress;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool initialWorkersGranted;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnRecruitedWorker;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnSubsidyGoldMissing;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnNoAvailablePopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private string lastAbnormalStatusId = string.Empty;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private string lastAbnormalStatusText = string.Empty;

    private BuildingJobCalculation lastJobCalculation;
    private BuildingJobCalculation lastJobCalculationWithoutSubsidy;
    private readonly List<BuildingWorkforceAttractionFactor> workforceAttractionFactors =
        new List<BuildingWorkforceAttractionFactor>();
    private IReadOnlyList<BuildingResourceChange> lastResourceProductions = EmptyResourceChanges;
    private bool hasCalculatedSubsidyTarget;

    public int CurrentWorkers => currentWorkers;
    public int MaxWorkers => maxWorkers;
    public int StableWorkers => stableWorkers;
    public bool AutoFullWorkerSubsidyEnabled => autoFullWorkerSubsidyEnabled;
    public int TargetStableWorkers => targetStableWorkers;
    public int TargetSubsidyGoldPerTurn => targetSubsidyGoldPerTurn;
    public int PaidSubsidyGoldThisTurn => paidSubsidyGoldThisTurn;
    public int MissingWorkersToFull => Mathf.Max(0, maxWorkers - currentWorkers);
    public int RecruitToFullWorkerCount => CalculateRecruitToFullWorkerCount();
    public int RecruitToFullCost => CalculateRecruitWorkerCost();
    public bool CanRecruitToFull => CalculateCanRecruitToFull();
    public float RawJobAttraction => rawJobAttraction;
    public float JobAttraction => jobAttraction;
    public float JobAttractionWithoutSubsidy => jobAttractionWithoutSubsidy;
    public int NearbyPopulation => nearbyPopulation;
    public int PopulationCellCount => populationCellCount;
    public int LastPopulationSearchRadius => lastPopulationSearchRadius;
    public float LastAttractionPerNearbyPopulation => lastAttractionPerNearbyPopulation;
    public float PopulationDensity => populationDensity;
    public float PopulationAttractionBonus => populationAttractionBonus;
    public float GlobalAttractionModifierTotal => globalAttractionModifierTotal;
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
    public int LastProducedWood => lastProducedWood;
    public int ProductionIntervalTurns => productionIntervalTurns;
    public int ProductionProgress => productionProgress;
    public BuildingJobCalculation LastJobCalculation => lastJobCalculation;
    public IReadOnlyList<BuildingWorkforceAttractionFactor> WorkforceAttractionFactors =>
        BuildWorkforceAttractionFactors();
    public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions =>
        CreateResourceChanges(woodItemId, GetCurrentWoodProductionAmount());
    public IReadOnlyList<BuildingResourceChange> LastResourceProductions => lastResourceProductions;

    public override string GetOverviewInfo()
    {
        return $"工人 {currentWorkers}/{maxWorkers}（{stableWorkers}）";
    }

    public override IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
    {
        List<BuildingFunctionBlockEntry> entries = null;
        var productionAmount = GetCurrentWoodProductionAmount();
        if (productionAmount > 0)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.资源组,
                    woodItemId,
                    productionAmount,
                    BuildProductionSidebarRows()));
        }

        AppendBuildingModuleFunctionBlockEntries(ref entries);
        return entries == null ? EmptyFunctionBlockEntries : entries;
    }

    public override IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
    {
        return CreateRuntimeStatuses();
    }

    [ShowInInspector, ReadOnly, LabelText("岗位调试信息")]
    public string JobDebugInfo => BuildingJobSystem.FormatDebugText(lastJobCalculation);

    protected override void OnInitialized()
    {
        NormalizeConfiguration();
        RecalculateJobState();
    }

    protected override void OnPlaced()
    {
        NormalizeConfiguration();
        TryGrantInitialWorkersOnPlaced();
        RecalculateJobState();
        RefreshDynastyEmployedPopulation();
    }

    protected override void OnRegistered()
    {
        RecalculateJobState();
        RefreshDynastyEmployedPopulation();
    }

    protected override bool OnTurn()
    {
        NormalizeConfiguration();
        ClearLastTurnState();

        var inventory = GameSystem == null ? null : GameSystem.Inventory;
        if (inventory == null)
        {
            SetLastAbnormalStatus(StatusMissingInventory, "库存服务缺失");
            return false;
        }

        RecalculateJobState();
        TryPaySubsidyForCurrentTurn(inventory);
        RecalculateJobState();
        ProcessWorkerTurn();
        RecalculateJobState();

        var produced = TryAdvanceProductionCycle(inventory);
        RefreshDynastyEmployedPopulation();
        return produced;
    }

    protected override BuildingDataBase CaptureBuildingData()
    {
        return new LumberCabinLV1Data
        {
            CurrentWorkers = currentWorkers,
            AutoFullWorkerSubsidyEnabled = autoFullWorkerSubsidyEnabled,
            TargetStableWorkers = targetStableWorkers,
            LastProducedWood = lastProducedWood,
            ProductionProgress = productionProgress,
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
        if (data is not LumberCabinLV1Data cabinData)
        {
            return;
        }

        currentWorkers = cabinData.CurrentWorkers;
        autoFullWorkerSubsidyEnabled = cabinData.AutoFullWorkerSubsidyEnabled;
        targetStableWorkers = cabinData.TargetStableWorkers;
        lastProducedWood = cabinData.LastProducedWood;
        productionProgress = cabinData.ProductionProgress;
        initialWorkersGranted = true;
        lastTurnRecruitedWorker = cabinData.LastTurnRecruitedWorker;
        lastTurnSubsidyGoldMissing = cabinData.LastTurnSubsidyGoldMissing;
        lastTurnNoAvailablePopulation = cabinData.LastTurnNoAvailablePopulation;
        lastAbnormalStatusId = string.IsNullOrWhiteSpace(cabinData.LastAbnormalStatusId)
            ? string.Empty
            : cabinData.LastAbnormalStatusId.Trim();
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(cabinData.LastAbnormalStatusText)
            ? lastAbnormalStatusId
            : cabinData.LastAbnormalStatusText.Trim();

        NormalizeConfiguration();
        RecalculateJobState(false);
    }

    protected override void OnUnregistered()
    {
        currentWorkers = 0;
        RefreshDynastyEmployedPopulation();
    }

    protected override void OnReceiveReplacementState(BuildingBase sourceBuilding)
    {
        if (sourceBuilding is not LumberCabin sourceCabin)
        {
            return;
        }

        currentWorkers = Mathf.Clamp(sourceCabin.currentWorkers, 0, maxWorkers);
        autoFullWorkerSubsidyEnabled = sourceCabin.autoFullWorkerSubsidyEnabled;
        targetStableWorkers = Mathf.Clamp(sourceCabin.targetStableWorkers, 0, maxWorkers);
        productionProgress = 0;
        initialWorkersGranted = true;
        ClearLastTurnState();
        NormalizeConfiguration();
        RecalculateJobState(false);
        RefreshDynastyEmployedPopulation();
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
        if (currentWorkers >= maxWorkers)
        {
            NotifyStateChanged();
            return false;
        }

        if (currentWorkers + 1 > stableWorkers)
        {
            NotifyStateChanged();
            return false;
        }

        var availablePopulation = GetAvailablePopulation();
        if (availablePopulation <= 0)
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
        return recruitCount > 0;
    }

    [Button("输出岗位调试信息")]
    public void LogJobDebugInfo()
    {
        NormalizeConfiguration();
        RecalculateJobState();
        Debug.Log($"{name} 岗位调试信息：\n{JobDebugInfo}", this);
    }

    [Button("刷新岗位计算")]
    public void RefreshJobCalculation()
    {
        NormalizeConfiguration();
        RecalculateJobState();
        NotifyStateChanged();
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

    private bool TryAdvanceProductionCycle(InventoryService inventory)
    {
        var productionAmount = GetCurrentWoodProductionAmount();
        if (productionAmount <= 0)
        {
            SetLastAbnormalStatus(StatusInsufficientWorkers, "工人不足");
            return false;
        }

        productionProgress = Mathf.Min(productionIntervalTurns, productionProgress + 1);
        if (productionProgress < productionIntervalTurns)
        {
            return true;
        }

        if (!TryProduceWood(inventory, productionAmount))
        {
            return false;
        }

        productionProgress = 0;
        AddProductionExperience();
        return true;
    }

    private bool TryProduceWood(InventoryService inventory, int productionAmount)
    {
        if (!HasUsableItemId(woodItemId))
        {
            SetLastAbnormalStatus(StatusInvalidWoodItem, "原木配置异常");
            return false;
        }

        if (!inventory.TryAddItem(woodItemId, productionAmount))
        {
            SetLastAbnormalStatus(StatusWoodStorageFailed, "原木存入失败");
            return false;
        }

        lastProducedWood = productionAmount;
        lastResourceProductions = CreateResourceChanges(woodItemId, productionAmount);
        return true;
    }

    private void AddProductionExperience()
    {
        if (TryGetModule<BuildingLevelUpgradeModule>(out var levelModule))
        {
            levelModule.AddExperience(1);
        }
    }

    private void RecalculateJobState(bool allowAutoSubsidyEvent = true)
    {
        var buildings = GameSystem == null || GameSystem.Buildings == null
            ? null
            : GameSystem.Buildings.Buildings;

        var populationModule = TryGetModule<BuildingNearbyPopulationJobAttractionModule>(out var module)
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

    private int CalculateImmediateRecruitCost(int recruitCount)
    {
        recruitCount = Mathf.Clamp(recruitCount, 0, Mathf.Max(0, maxWorkers - currentWorkers));
        return Mathf.CeilToInt(recruitCount * singleRecruitCost * (1f + (100f - jobAttraction) / 100f));
    }

    private int CalculateRecruitToFullWorkerCount()
    {
        if (currentWorkers >= maxWorkers)
        {
            return 0;
        }

        if (currentWorkers + 1 > stableWorkers)
        {
            return 0;
        }

        var availablePopulation = GetAvailablePopulation();
        if (availablePopulation <= 0)
        {
            return 0;
        }

        return 1;
    }

    private int CalculateRecruitWorkerCost()
    {
        return currentWorkers >= maxWorkers ? 0 : CalculateImmediateRecruitCost(1);
    }

    private bool CalculateCanRecruitToFull()
    {
        var recruitCount = CalculateRecruitToFullWorkerCount();
        if (recruitCount <= 0)
        {
            return false;
        }

        if (currentWorkers + recruitCount > stableWorkers)
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

    private static string CreatePartialRecruitMessage(
        int recruitedWorkers,
        bool populationLimited,
        bool goldLimited)
    {
        var reasons = new List<string>();
        if (populationLimited)
        {
            reasons.Add("可用人口不足");
        }

        if (goldLimited)
        {
            reasons.Add("金币不足");
        }

        var reasonText = reasons.Count == 0 ? "未达到满岗位" : string.Join("，", reasons);
        return $"只招到 {recruitedWorkers} 名工人：{reasonText}";
    }

    private int GetCurrentWoodProductionAmount()
    {
        return GetProductionAmountForWorkers(currentWorkers);
    }

    private IReadOnlyList<BuildingRuntimeStatus> CreateRuntimeStatuses()
    {
        List<BuildingRuntimeStatus> statuses = null;
        var minimumWorkersForProduction = GetMinimumWorkersForProduction();

        AppendRuntimeStatus(ref statuses, currentWorkers < minimumWorkersForProduction
            ? new BuildingRuntimeStatus(StatusInsufficientWorkers, "工人不足", currentWorkers, minimumWorkersForProduction)
            : default);

        AppendRuntimeStatus(ref statuses, currentWorkers >= minimumWorkersForProduction && currentWorkers < stableWorkers
            ? new BuildingRuntimeStatus(StatusWorkerShortage, "缺工", currentWorkers, stableWorkers)
            : default);

        AppendRuntimeStatus(ref statuses, lastTurnSubsidyGoldMissing
            ? new BuildingRuntimeStatus(StatusSubsidyGoldMissing, "补贴金币不足", paidSubsidyGoldThisTurn, targetSubsidyGoldPerTurn)
            : default);

        AppendRuntimeStatus(ref statuses, ShouldAddLastAbnormalStatus()
            ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
            : default);

        AppendCommonRuntimeStatuses(ref statuses);
        return statuses ?? EmptyRuntimeStatuses;
    }

    private bool ShouldAddLastAbnormalStatus()
    {
        if (string.IsNullOrWhiteSpace(lastAbnormalStatusId))
        {
            return false;
        }

        if (currentWorkers < GetMinimumWorkersForProduction()
            && string.Equals(lastAbnormalStatusId, StatusInsufficientWorkers, StringComparison.Ordinal))
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

    private void ClearLastTurnState()
    {
        paidSubsidyGoldThisTurn = 0;
        lastProducedWood = 0;
        lastTurnRecruitedWorker = false;
        lastTurnSubsidyGoldMissing = false;
        lastTurnNoAvailablePopulation = false;
        lastAbnormalStatusId = string.Empty;
        lastAbnormalStatusText = string.Empty;
        lastResourceProductions = EmptyResourceChanges;
    }

    private void ClearRecruitAttemptState()
    {
        lastTurnRecruitedWorker = false;
        lastTurnNoAvailablePopulation = false;
        lastAbnormalStatusId = string.Empty;
        lastAbnormalStatusText = string.Empty;
    }

    private void NormalizeConfiguration()
    {
        maxWorkers = Mathf.Max(1, maxWorkers);
        initialWorkersOnPlaced = Mathf.Clamp(initialWorkersOnPlaced, 0, maxWorkers);
        baseJobAttraction = Mathf.Max(0f, baseJobAttraction);
        singleRecruitCost = Mathf.Max(0, singleRecruitCost);
        NormalizeBuildingModules();
        targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
        productionIntervalTurns = Mathf.Max(1, productionIntervalTurns);
        productionProgress = Mathf.Clamp(productionProgress, 0, productionIntervalTurns);
        NormalizeWorkerProductionTiers();
        currentWorkers = Mathf.Clamp(currentWorkers, 0, maxWorkers);
        woodItemId = NormalizeItemId(woodItemId, DefaultWoodItemId);
        goldItemId = NormalizeItemId(goldItemId, DefaultGoldItemId);
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

    private void NormalizeWorkerProductionTiers()
    {
        if (workerProductionTiers == null || workerProductionTiers.Length == 0)
        {
            workerProductionTiers = new[]
            {
                new WorkerProductionTier(2, 1),
                new WorkerProductionTier(Mathf.Min(3, maxWorkers), 2)
            };
        }

        for (var i = 0; i < workerProductionTiers.Length; i++)
        {
            workerProductionTiers[i] = workerProductionTiers[i].Normalize(maxWorkers);
        }

        Array.Sort(
            workerProductionTiers,
            (a, b) => a.MinimumWorkers == b.MinimumWorkers
                ? a.ProductionAmount.CompareTo(b.ProductionAmount)
                : a.MinimumWorkers.CompareTo(b.MinimumWorkers));
    }

    private int GetMinimumWorkersForProduction()
    {
        var minimumWorkers = maxWorkers;
        var hasValidTier = false;
        for (var i = 0; i < workerProductionTiers.Length; i++)
        {
            var tier = workerProductionTiers[i];
            if (!tier.IsValid)
            {
                continue;
            }

            minimumWorkers = Mathf.Min(minimumWorkers, tier.MinimumWorkers);
            hasValidTier = true;
        }

        return hasValidTier ? minimumWorkers : maxWorkers;
    }

    private int GetProductionAmountForWorkers(int workers)
    {
        var amount = 0;
        for (var i = 0; i < workerProductionTiers.Length; i++)
        {
            var tier = workerProductionTiers[i];
            if (!tier.IsValid || workers < tier.MinimumWorkers)
            {
                continue;
            }

            amount = tier.ProductionAmount;
        }

        return amount;
    }

    private IReadOnlyList<BuildingFunctionBlockSidebarRow> BuildProductionSidebarRows()
    {
        return new[]
        {
            new BuildingFunctionBlockSidebarRow(
                "生产周期",
                $"{productionProgress}/{productionIntervalTurns}"),
            new BuildingFunctionBlockSidebarRow(
                "产量规则",
                FormatProductionTiers())
        };
    }

    private string FormatProductionTiers()
    {
        if (workerProductionTiers == null || workerProductionTiers.Length == 0)
        {
            return "无";
        }

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < workerProductionTiers.Length; i++)
        {
            var tier = workerProductionTiers[i];
            if (!tier.IsValid)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("，");
            }

            builder.Append(tier.MinimumWorkers);
            builder.Append("工人=");
            builder.Append(tier.ProductionAmount);
            builder.Append(woodItemId);
        }

        return builder.Length == 0 ? "无" : builder.ToString();
    }

    protected virtual void OnValidate()
    {
        NormalizeConfiguration();
    }

    private void SetLastAbnormalStatus(string statusId, string statusText)
    {
        lastAbnormalStatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(statusText) ? lastAbnormalStatusId : statusText.Trim();
    }

    private static bool RollChance(float chancePercent)
    {
        return UnityEngine.Random.value * 100f < Mathf.Clamp(chancePercent, 0f, 100f);
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

    [Serializable]
    private sealed class LumberCabinLV1Data : BuildingDataBase
    {
        public int CurrentWorkers;
        public bool AutoFullWorkerSubsidyEnabled;
        public int TargetStableWorkers;
        public int LastProducedWood;
        public int ProductionProgress;
        public bool InitialWorkersGranted;
        public bool LastTurnRecruitedWorker;
        public bool LastTurnSubsidyGoldMissing;
        public bool LastTurnNoAvailablePopulation;
        public string LastAbnormalStatusId;
        public string LastAbnormalStatusText;
    }
}


