using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class FishingHutBuilding :BuildingBase, IBuildingWorkforceFundingSource, IBuildingWorkforceModuleHost
{
    private const string DefaultFishItemId = "鱼";
    private const string DefaultGoldFishItemId = "黄金鱼";
    private const string DefaultGoldItemId = "金币";

    private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
    private const string StatusWorkerShortage = BuildingRuntimeStatusCatalog.BS_缺工;
    private const string StatusRecruitGoldMissing = BuildingRuntimeStatusCatalog.BS_招工金币不足;
    private const string StatusSubsidyGoldMissing = BuildingRuntimeStatusCatalog.BS_补贴金币不足;

    private const string StatusInvalidSpecialCatchItem = "invalid_special_catch_item";
    private const string StatusSpecialCatchStorageFailed = "special_catch_storage_failed";

    private static readonly IReadOnlyList<BuildingJobAttractionModifier> EmptyAttractionModifiers =
        Array.Empty<BuildingJobAttractionModifier>();

    [TitleGroup("资源")]
    [SerializeField, LabelText("黄金鱼物品ID")]
    private string goldFishItemId = DefaultGoldFishItemId;

    [TitleGroup("资源")]
    [SerializeField, LabelText("金币物品ID")]
    private string goldItemId = DefaultGoldItemId;

    [TitleGroup("岗位")]
    [SerializeField, LabelText("最大岗位"), Min(1)]
    private int maxWorkers = 3;

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

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("启用特殊捕获")]
    private bool enableSpecialCatch;

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("特殊捕获最低工人"), Min(1)]
    private int specialCatchMinimumWorkers = 5;

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("特殊捕获几率%"), Range(0f, 100f)]
    private float specialCatchChancePercent = 1f;

    [TitleGroup("特殊捕获")]
    [SerializeField, LabelText("特殊捕获数量"), Min(0)]
    private int specialCatchAmount = 1;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int currentWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int stableWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int stableWorkersWithoutSubsidy;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private float rawJobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private float jobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private float jobAttractionWithoutSubsidy;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int targetSubsidyGoldPerTurn;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int paidSubsidyGoldThisTurn;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private int lastProducedGoldFish;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool initialWorkersGranted;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnRecruitedWorker;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnSubsidyGoldMissing;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnNoAvailablePopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private bool lastTurnCaughtSpecial;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private string lastAbnormalStatusId = string.Empty;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly]
    private string lastAbnormalStatusText = string.Empty;

    private readonly List<BuildingWorkforceAttractionFactor> workforceAttractionFactors =
        new List<BuildingWorkforceAttractionFactor>();

    public BM_岗位运营 GetWorkforceModule()
    {
        var module = EnsureBuildingModule<BM_岗位运营>();
        module.ConfigureDefaultsIfUnset(maxWorkers, initialWorkersOnPlaced, baseJobAttraction, singleRecruitCost, autoFullWorkerSubsidyEnabled, targetStableWorkers, goldItemId);
        return module;
    }

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
    public int RecruitToFullCost => CalculateImmediateRecruitCost(RecruitToFullWorkerCount);
    public bool CanRecruitToFull => RecruitToFullWorkerCount > 0 && CanPayGold(RecruitToFullCost);
    public float JobAttractionWithoutSubsidy => jobAttractionWithoutSubsidy;
    public float SubsidyAttractionPerGold => BuildingJobSystem.CalculateSubsidyAttractionPerGold(maxWorkers);
    public float SubsidyAttractionBonus => Mathf.Max(0, paidSubsidyGoldThisTurn) * SubsidyAttractionPerGold;
    public float TargetSubsidyAttractionBonus => Mathf.Max(0, targetSubsidyGoldPerTurn) * SubsidyAttractionPerGold;
    public float PreviewJobAttractionWithTargetSubsidy =>
        BuildingJobSystem.CalculateAttractionWithSubsidy(
            jobAttractionWithoutSubsidy,
            targetSubsidyGoldPerTurn,
            maxWorkers);
    public float FullWorkerRequiredAttraction => BuildingJobSystem.CalculateFullWorkerRequiredAttraction(maxWorkers);
    public float JobAttractionGapToFullWorkers =>
        Mathf.Max(0f, FullWorkerRequiredAttraction - jobAttractionWithoutSubsidy);
    public IReadOnlyList<BuildingWorkforceAttractionFactor> WorkforceAttractionFactors =>
        workforceAttractionFactors;

    protected override void Awake()
    {
        base.Awake();
        NormalizeConfiguration();
        RecalculateWorkforce();
    }

    protected override void OnInitialized()
    {
        GetWorkforceModule().Bind(this);
    }

    protected override void OnRegistered()
    {
        GetWorkforceModule().Bind(this);
    }

    protected override void OnPlaced()
    {
        GetWorkforceModule().OnPlaced(this);
    }

    protected override bool OnTurn()
    {
        ClearLastTurnState();
        var workforce = GetWorkforceModule();
        if (!workforce.ProcessTurn(this)) return false;

        if (workforce.CurrentWorkers < EnsureResourceProductionModule().GetMinimumWorkersForProduction(workforce.MaxWorkers))
        {
            SetLastAbnormalStatus(StatusInsufficientWorkers, "工人不足");
            return false;
        }

        var inventory = GameSystem == null ? null : GameSystem.Inventory;
        var productionResult = EnsureResourceProductionModule()
            .TryAdvanceProductionCycle(this, inventory, workforce.CurrentWorkers, workforce.MaxWorkers);
        if (!productionResult.Succeeded)
        {
            SetLastAbnormalStatus(
                productionResult.FailureStatusId,
                productionResult.FailureStatusText);
            return false;
        }

        if (productionResult.ProducedResources)
        {
            TryProduceSpecialCatchIfNeeded(inventory);
            AddUpgradeExperience();
        }

        return true;
    }

    protected override BuildingDataBase CaptureBuildingData()
    {
        return new FishingHutData
        {
            CurrentWorkers = currentWorkers,
            AutoFullWorkerSubsidyEnabled = autoFullWorkerSubsidyEnabled,
            TargetStableWorkers = targetStableWorkers,
            InitialWorkersGranted = initialWorkersGranted,
            LastTurnSubsidyGoldMissing = lastTurnSubsidyGoldMissing,
            LastTurnNoAvailablePopulation = lastTurnNoAvailablePopulation,
            LastTurnCaughtSpecial = lastTurnCaughtSpecial,
            LastAbnormalStatusId = lastAbnormalStatusId,
            LastAbnormalStatusText = lastAbnormalStatusText
        };
    }

    protected override void RestoreBuildingData(BuildingDataBase data)
    {
        if (data is not FishingHutData fishingData)
        {
            return;
        }

        currentWorkers = fishingData.CurrentWorkers;
        autoFullWorkerSubsidyEnabled = fishingData.AutoFullWorkerSubsidyEnabled;
        targetStableWorkers = fishingData.TargetStableWorkers;
        initialWorkersGranted = fishingData.InitialWorkersGranted;
        lastTurnSubsidyGoldMissing = fishingData.LastTurnSubsidyGoldMissing;
        lastTurnNoAvailablePopulation = fishingData.LastTurnNoAvailablePopulation;
        lastTurnCaughtSpecial = fishingData.LastTurnCaughtSpecial;
        lastAbnormalStatusId = fishingData.LastAbnormalStatusId;
        lastAbnormalStatusText = fishingData.LastAbnormalStatusText;

        NormalizeConfiguration();
        RecalculateWorkforce();
        RefreshDynastyEmployedPopulation();
    }

    protected override void OnReceiveReplacementState(BuildingBase sourceBuilding)
    {
        if (sourceBuilding is not FishingHutBuilding sourceFishingHut)
        {
            return;
        }

        currentWorkers = Mathf.Clamp(sourceFishingHut.currentWorkers, 0, maxWorkers);
        autoFullWorkerSubsidyEnabled = sourceFishingHut.autoFullWorkerSubsidyEnabled;
        targetStableWorkers = Mathf.Clamp(sourceFishingHut.targetStableWorkers, 0, maxWorkers);
        initialWorkersGranted = true;

        NormalizeConfiguration();
        RecalculateWorkforce();
        RefreshDynastyEmployedPopulation();
    }

    protected override void OnUnregistered()
    {
        currentWorkers = 0;
        RefreshDynastyEmployedPopulation();
    }

    public override string GetOverviewInfo()
    {
        var productions = EnsureResourceProductionModule()
            .PreviewResourceProductions(currentWorkers, maxWorkers);
        return $"工人 {currentWorkers}/{maxWorkers}，产出 {FormatResourceChanges(productions)}";
    }

    public override IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
    {
        List<BuildingFunctionBlockEntry> entries = null;
        AppendBuildingModuleFunctionBlockEntries(ref entries);

        AddFunctionBlockEntry(
            ref entries,
            new BuildingFunctionBlockEntry(
                BuildingFunctionBlockGroup.功能性,
                "最低生产工人",
                GetMinimumWorkersForProduction()));

        if (enableSpecialCatch && specialCatchChancePercent > 0f && specialCatchAmount > 0)
        {
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    goldFishItemId,
                    specialCatchAmount,
                    new BuildingFunctionBlockSidebarRow(
                        "特殊捕获",
                        $"{specialCatchMinimumWorkers}工人时 {specialCatchChancePercent:0.##}%")));
        }

        AppendBuildingModuleFunctionBlockEntries(ref entries);
        return entries ?? EmptyFunctionBlockEntries;
    }

    public override IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
    {
        List<BuildingRuntimeStatus> statuses = null;

        var minimumWorkersForProduction = GetMinimumWorkersForProduction();
        AppendRuntimeStatus(
            ref statuses,
            currentWorkers < minimumWorkersForProduction
                ? new BuildingRuntimeStatus(
                    StatusInsufficientWorkers,
                    "工人不足",
                    currentWorkers,
                    minimumWorkersForProduction)
                : default);

        AppendRuntimeStatus(
            ref statuses,
            currentWorkers >= minimumWorkersForProduction && currentWorkers < stableWorkers
                ? new BuildingRuntimeStatus(
                    StatusWorkerShortage,
                    "缺工",
                    currentWorkers,
                    stableWorkers)
                : default);

        AppendRuntimeStatus(
            ref statuses,
            ShouldAddLastAbnormalStatus()
                ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
                : default);

        AppendCommonRuntimeStatuses(ref statuses);
        return statuses ?? EmptyRuntimeStatuses;
    }

    public void SetAutoFullWorkerSubsidyEnabled(bool enabled)
    {
        autoFullWorkerSubsidyEnabled = enabled;
        if (autoFullWorkerSubsidyEnabled)
        {
            targetStableWorkers = maxWorkers;
        }

        RecalculateWorkforceWithoutSubsidy();
        NotifyStateChanged();
    }

    public void SetTargetStableWorkers(int targetStableWorkers)
    {
        autoFullWorkerSubsidyEnabled = false;
        this.targetStableWorkers = Mathf.Clamp(targetStableWorkers, stableWorkersWithoutSubsidy, maxWorkers);
        RecalculateWorkforceWithoutSubsidy();
        NotifyStateChanged();
    }

    public bool TryRecruitToFull()
    {
        var recruitCount = RecruitToFullWorkerCount;
        if (recruitCount <= 0)
        {
            return false;
        }

        var recruitCost = CalculateImmediateRecruitCost(recruitCount);
        if (!TryPayGold(recruitCost))
        {
            SetLastAbnormalStatus(StatusRecruitGoldMissing, "招工金币不足");
            NotifyStateChanged();
            return false;
        }

        currentWorkers = Mathf.Clamp(currentWorkers + recruitCount, 0, maxWorkers);
        lastTurnRecruitedWorker = true;
        RecalculateWorkforce();
        RefreshDynastyEmployedPopulation();
        NotifyStateChanged();
        return true;
    }

    private void TryGrantInitialWorkersOnPlaced()
    {
        if (initialWorkersGranted)
        {
            return;
        }

        initialWorkersGranted = true;
        var workersToGrant = Mathf.Clamp(initialWorkersOnPlaced, 0, maxWorkers - currentWorkers);
        if (workersToGrant <= 0)
        {
            return;
        }

        var availablePopulation = GetAvailablePopulation();
        if (availablePopulation <= 0)
        {
            lastTurnNoAvailablePopulation = true;
            return;
        }

        workersToGrant = Mathf.Min(workersToGrant, availablePopulation);
        currentWorkers = Mathf.Clamp(currentWorkers + workersToGrant, 0, maxWorkers);
        lastTurnRecruitedWorker = workersToGrant > 0;
        RecalculateWorkforce();
    }

    private void ProcessWorkerTurn()
    {
        if (currentWorkers < stableWorkers)
        {
            if (GetAvailablePopulation() <= 0)
            {
                lastTurnNoAvailablePopulation = true;
                return;
            }

            currentWorkers = Mathf.Min(maxWorkers, currentWorkers + 1);
            lastTurnRecruitedWorker = true;
            RecalculateWorkforce();
            return;
        }

        if (currentWorkers > stableWorkers)
        {
            currentWorkers = Mathf.Max(0, currentWorkers - 1);
            RecalculateWorkforce();
        }
    }

    private void RecalculateWorkforceWithoutSubsidy()
    {
        var calculation = CalculateJob(0);
        stableWorkersWithoutSubsidy = calculation.StableWorkers;
        jobAttractionWithoutSubsidy = calculation.Attraction;

        NormalizeTargetStableWorkers();
        targetSubsidyGoldPerTurn =
            BuildingJobSystem.CalculateRequiredSubsidyGoldForTargetStableWorkers(
                maxWorkers,
                jobAttractionWithoutSubsidy,
                targetStableWorkers);
    }

    private void RecalculateWorkforce()
    {
        RecalculateWorkforceWithoutSubsidy();
        var calculation = CalculateJob(paidSubsidyGoldThisTurn);
        stableWorkers = calculation.StableWorkers;
        rawJobAttraction = calculation.RawAttraction;
        jobAttraction = calculation.Attraction;
        BuildWorkforceAttractionFactors();
    }

    private BuildingJobCalculation CalculateJob(int paidSubsidyGold)
    {
        return BuildingJobSystem.Calculate(
            new BuildingJobCalculationInput(
                maxWorkers,
                currentWorkers,
                baseJobAttraction,
                0,
                0,
                0f,
                EmptyAttractionModifiers,
                Mathf.Max(0, paidSubsidyGold) * SubsidyAttractionPerGold,
                singleRecruitCost));
    }

    private void NormalizeTargetStableWorkers()
    {
        targetStableWorkers = autoFullWorkerSubsidyEnabled
            ? maxWorkers
            : Mathf.Clamp(targetStableWorkers, stableWorkersWithoutSubsidy, maxWorkers);
    }

    private void TryPaySubsidyForCurrentTurn()
    {
        paidSubsidyGoldThisTurn = 0;
        if (targetSubsidyGoldPerTurn <= 0)
        {
            return;
        }

        if (TryPayGold(targetSubsidyGoldPerTurn))
        {
            paidSubsidyGoldThisTurn = targetSubsidyGoldPerTurn;
            return;
        }

        lastTurnSubsidyGoldMissing = true;
        SetLastAbnormalStatus(StatusSubsidyGoldMissing, "补贴金币不足");
    }

    private bool ShouldCatchSpecialFish()
    {
        return enableSpecialCatch
               && GetWorkforceModule().CurrentWorkers >= specialCatchMinimumWorkers
               && specialCatchAmount > 0
               && specialCatchChancePercent > 0f
               && UnityEngine.Random.value < Mathf.Clamp01(specialCatchChancePercent / 100f);
    }

    private void TryProduceSpecialCatchIfNeeded(InventoryService inventory)
    {
        if (ShouldCatchSpecialFish())
        {
            TryProduceSpecialCatch(inventory);
        }
    }

    private void TryProduceSpecialCatch(InventoryService inventory)
    {
        if (!HasUsableItemId(goldFishItemId))
        {
            SetLastAbnormalStatus(StatusInvalidSpecialCatchItem, "黄金鱼物品配置异常");
            return;
        }

        if (!inventory.TryAddItem(goldFishItemId, specialCatchAmount))
        {
            SetLastAbnormalStatus(StatusSpecialCatchStorageFailed, "黄金鱼存入失败");
            return;
        }

        lastProducedGoldFish = specialCatchAmount;
        lastTurnCaughtSpecial = true;
        EnsureResourceProductionModule().AppendLastResourceProduction(goldFishItemId, lastProducedGoldFish);
    }

    private void AddUpgradeExperience()
    {
        if (TryGetModule<BM_等级升级>(out var levelModule))
        {
            levelModule.AddExperience(1);
        }
    }

    private int CalculateRecruitToFullWorkerCount()
    {
        if (currentWorkers >= maxWorkers || stableWorkers < maxWorkers)
        {
            return 0;
        }

        return Mathf.Max(0, maxWorkers - currentWorkers);
    }

    private int CalculateImmediateRecruitCost(int recruitCount)
    {
        return Mathf.Max(0, recruitCount) * Mathf.Max(0, singleRecruitCost);
    }

    private bool CanPayGold(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        var inventory = GameSystem == null ? null : GameSystem.Inventory;
        return inventory != null
               && HasUsableItemId(goldItemId)
               && inventory.HasItem(goldItemId, amount);
    }

    private bool TryPayGold(int amount)
    {
        if (amount <= 0)
        {
            return true;
        }

        var inventory = GameSystem == null ? null : GameSystem.Inventory;
        return inventory != null
               && HasUsableItemId(goldItemId)
               && inventory.TryRemoveItem(goldItemId, amount);
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

        GameSystem.Dynasty.SetEmployedPopulation(
            BuildingJobSystem.CountCurrentWorkers(GameSystem.Buildings.Buildings));
    }

    private void BuildWorkforceAttractionFactors()
    {
        workforceAttractionFactors.Clear();
        AddWorkforceAttractionFactor("基础吸引力", baseJobAttraction);

        if (paidSubsidyGoldThisTurn > 0)
        {
            AddWorkforceAttractionFactor("本回合补贴", SubsidyAttractionBonus);
        }
    }

    private void AddWorkforceAttractionFactor(string label, float value)
    {
        var factor = new BuildingWorkforceAttractionFactor(label, value);
        if (factor.IsValid)
        {
            workforceAttractionFactors.Add(factor);
        }
    }

    private void ClearLastTurnState()
    {
        paidSubsidyGoldThisTurn = 0;
        lastProducedGoldFish = 0;
        lastTurnRecruitedWorker = false;
        lastTurnSubsidyGoldMissing = false;
        lastTurnNoAvailablePopulation = false;
        lastTurnCaughtSpecial = false;
        EnsureResourceProductionModule().ClearLastResourceProductions();
        lastAbnormalStatusId = string.Empty;
        lastAbnormalStatusText = string.Empty;
    }

    private bool ShouldAddLastAbnormalStatus()
    {
        return !string.IsNullOrWhiteSpace(lastAbnormalStatusId)
               && !string.Equals(lastAbnormalStatusId, StatusInsufficientWorkers, StringComparison.Ordinal)
               && !string.Equals(lastAbnormalStatusId, StatusWorkerShortage, StringComparison.Ordinal);
    }

    private void SetLastAbnormalStatus(string statusId, string statusText)
    {
        lastAbnormalStatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(statusText) ? lastAbnormalStatusId : statusText.Trim();
    }

    private void NormalizeConfiguration()
    {
        maxWorkers = Mathf.Max(1, maxWorkers);
        initialWorkersOnPlaced = Mathf.Clamp(initialWorkersOnPlaced, 0, maxWorkers);
        currentWorkers = Mathf.Clamp(currentWorkers, 0, maxWorkers);
        stableWorkers = Mathf.Clamp(stableWorkers, 0, maxWorkers);
        stableWorkersWithoutSubsidy = Mathf.Clamp(stableWorkersWithoutSubsidy, 0, maxWorkers);
        baseJobAttraction = Mathf.Max(0f, baseJobAttraction);
        singleRecruitCost = Mathf.Max(0, singleRecruitCost);
        specialCatchMinimumWorkers = Mathf.Clamp(specialCatchMinimumWorkers, 1, maxWorkers);
        specialCatchChancePercent = Mathf.Clamp(specialCatchChancePercent, 0f, 100f);
        specialCatchAmount = Mathf.Max(0, specialCatchAmount);
        targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
        goldFishItemId = NormalizeItemId(goldFishItemId, DefaultGoldFishItemId);
        goldItemId = NormalizeItemId(goldItemId, DefaultGoldItemId);
        EnsureResourceProductionModule();
    }

    private int GetMinimumWorkersForProduction()
    {
        return EnsureResourceProductionModule().GetMinimumWorkersForProduction(maxWorkers);
    }

    private BM_资源产出 EnsureResourceProductionModule()
    {
        var module = EnsureBuildingModule<BM_资源产出>();
        module.EnsureSingleOutput(
            DefaultFishItemId,
            1,
            new[]
            {
                new WorkerProductionTier(2, 1),
                new WorkerProductionTier(3, 2)
            });
        return module;
    }

    [Serializable]
    [BuildingDataTypeId("building.fishing_hut")]
    private sealed class FishingHutData : BuildingDataBase
    {
        public int CurrentWorkers;
        public bool AutoFullWorkerSubsidyEnabled;
        public int TargetStableWorkers;
        public bool InitialWorkersGranted;
        public bool LastTurnSubsidyGoldMissing;
        public bool LastTurnNoAvailablePopulation;
        public bool LastTurnCaughtSpecial;
        public string LastAbnormalStatusId;
        public string LastAbnormalStatusText;
    }
}
