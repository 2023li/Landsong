using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

public class LumberCabinLV1 : BuildingBase, IBuildingJobSource, IBuildingRuntimeStatusSource, IBuildingOverviewSource, IBuildingResourceProductionSource
{
    private const string DefaultWoodItemId = "原木";
    private const string DefaultGoldItemId = "金币";
    private const string StatusMissingInventory = "missing_inventory";
    private const string StatusInvalidWoodItem = "invalid_wood_item";
    private const string StatusInvalidGoldItem = "invalid_gold_item";
    private const string StatusWoodStorageFailed = "wood_storage_failed";
    private const string StatusInsufficientWorkers = "insufficient_workers";
    private const string StatusWorkerShortage = "worker_shortage";
    private const string StatusSubsidyPaymentFailed = "subsidy_payment_failed";
    private const string StatusRecruitGoldMissing = "recruit_gold_missing";
    private const string StatusNoAvailablePopulation = "no_available_population";
    private const string StatusWorkerResigned = "worker_resigned";

    private static readonly IReadOnlyList<BuildingResourceChange> EmptyResourceChanges =
        Array.Empty<BuildingResourceChange>();

    private static readonly IReadOnlyList<BuildingRuntimeStatus> EmptyRuntimeStatuses =
        Array.Empty<BuildingRuntimeStatus>();

    [TitleGroup("岗位")]
    [SerializeField, Min(1)] private int maxWorkers = 3;

    [TitleGroup("岗位")]
    [SerializeField, Min(0f)] private float baseJobAttraction = 35f;

    [TitleGroup("岗位")]
    [SerializeField, Min(0)] private int subsidyGoldPerTurn;

    [TitleGroup("岗位")]
    [SerializeField, Min(0)] private int singleRecruitCost = 10;

    [TitleGroup("岗位")]
    [SerializeField, Min(0)] private int populationSearchRadius = 10;

    [TitleGroup("岗位")]
    [SerializeField, Min(0)] private int jobCompetitionSearchRadius = 10;

    [TitleGroup("岗位")]
    [SerializeField, Min(0f)] private float competitionPenaltyPerCompetingJob = 2f;

    [TitleGroup("岗位")]
    [SerializeField, Min(0f)] private float maxCompetitionPenalty = 60f;

    [TitleGroup("产出")]
    [SerializeField] private string woodItemId = DefaultWoodItemId;

    [TitleGroup("产出")]
    [SerializeField] private string goldItemId = DefaultGoldItemId;

    [TitleGroup("产出")]
    [SerializeField, Min(1)] private int minimumWorkersForProduction = 2;

    [TitleGroup("产出")]
    [SerializeField, Min(1)] private int fullProductionWorkers = 3;

    [TitleGroup("产出")]
    [SerializeField, Min(0)] private int baseProductionAmount = 1;

    [TitleGroup("产出")]
    [SerializeField, Min(0)] private int fullProductionAmount = 2;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int currentWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int stableWorkers;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float rawJobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float jobAttraction;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int nearbyPopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int populationCellCount;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float populationDensity;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int nearbyCompetingJobs;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float competitionPenalty;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int lastPaidSubsidyGold;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int lastProducedWood;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnRecruitedWorker;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnWorkerResigned;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnSubsidyPaymentFailed;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnNoAvailablePopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private string lastAbnormalStatusId = string.Empty;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private string lastAbnormalStatusText = string.Empty;

    private BuildingJobCalculation lastJobCalculation;
    private IReadOnlyList<BuildingResourceChange> lastResourceProductions = EmptyResourceChanges;

    public int CurrentWorkers => currentWorkers;
    public int MaxWorkers => maxWorkers;
    public int StableWorkers => stableWorkers;
    public int SubsidyGoldPerTurn => subsidyGoldPerTurn;
    public float RawJobAttraction => rawJobAttraction;
    public float JobAttraction => jobAttraction;
    public int NearbyPopulation => nearbyPopulation;
    public int PopulationCellCount => populationCellCount;
    public float PopulationDensity => populationDensity;
    public int NearbyCompetingJobs => nearbyCompetingJobs;
    public float CompetitionPenalty => competitionPenalty;
    public int LastPaidSubsidyGold => lastPaidSubsidyGold;
    public int LastProducedWood => lastProducedWood;
    public BuildingJobCalculation LastJobCalculation => lastJobCalculation;
    public IReadOnlyList<BuildingRuntimeStatus> RuntimeStatuses => CreateRuntimeStatuses();
    public string OverviewValueLabel => "岗位";
    public string OverviewValueText =>
        $"补贴 {subsidyGoldPerTurn}，工人 {currentWorkers}/{stableWorkers}，吸引力 {rawJobAttraction:0.#}";
    public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions =>
        CreateResourceChanges(woodItemId, GetCurrentWoodProductionAmount());
    public IReadOnlyList<BuildingResourceChange> LastResourceProductions => lastResourceProductions;

    [ShowInInspector, ReadOnly, LabelText("岗位调试信息")]
    public string JobDebugInfo => BuildingJobSystem.FormatDebugText(lastJobCalculation);

    protected override void OnInitialized()
    {
        NormalizeConfiguration();
        RecalculateJobState(subsidyGoldPerTurn);
    }

    protected override void OnPlaced()
    {
        RecalculateJobState(subsidyGoldPerTurn);
    }

    protected override void OnRegistered()
    {
        RecalculateJobState(subsidyGoldPerTurn);
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

        var effectiveSubsidy = TryPaySubsidy(inventory) ? subsidyGoldPerTurn : 0;
        RecalculateJobState(effectiveSubsidy);
        ProcessWorkerTurn();
        RecalculateJobState(effectiveSubsidy);

        var produced = TryProduceWood(inventory);
        RefreshDynastyEmployedPopulation();
        return produced;
    }

    protected override void OnUnregistered()
    {
        currentWorkers = 0;
        RefreshDynastyEmployedPopulation();
    }

    public void SetSubsidyGoldPerTurn(int amount)
    {
        NormalizeConfiguration();
        subsidyGoldPerTurn = Mathf.Max(0, amount);
        RecalculateJobState(subsidyGoldPerTurn);
        NotifyStateChanged();
    }

    [Button("立即招工")]
    public bool TryRecruitImmediately()
    {
        NormalizeConfiguration();
        ClearLastTurnState();

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

        RecalculateJobState(subsidyGoldPerTurn);
        var missingWorkers = Mathf.Max(0, maxWorkers - currentWorkers);
        if (missingWorkers <= 0)
        {
            return true;
        }

        var availablePopulation = GetAvailablePopulation();
        if (availablePopulation <= 0)
        {
            lastTurnNoAvailablePopulation = true;
            SetLastAbnormalStatus(StatusNoAvailablePopulation, "可用人口不足");
            NotifyStateChanged();
            return false;
        }

        var recruitCount = Mathf.Min(missingWorkers, availablePopulation);
        var recruitCost = CalculateImmediateRecruitCost(recruitCount);
        if (recruitCost > 0 && !inventory.TryRemoveItem(goldItemId, recruitCost))
        {
            SetLastAbnormalStatus(StatusRecruitGoldMissing, "招工金币不足");
            NotifyStateChanged();
            return false;
        }

        currentWorkers += recruitCount;
        lastTurnRecruitedWorker = recruitCount > 0;
        lastTurnNoAvailablePopulation = recruitCount < missingWorkers;
        if (lastTurnNoAvailablePopulation)
        {
            SetLastAbnormalStatus(StatusNoAvailablePopulation, "可用人口不足");
        }

        RecalculateJobState(subsidyGoldPerTurn);
        RefreshDynastyEmployedPopulation();
        NotifyStateChanged();
        return recruitCount > 0;
    }

    [Button("输出岗位调试信息")]
    public void LogJobDebugInfo()
    {
        NormalizeConfiguration();
        RecalculateJobState(subsidyGoldPerTurn);
        Debug.Log($"{name} 岗位调试信息：\n{JobDebugInfo}", this);
    }

    [Button("刷新岗位计算")]
    public void RefreshJobCalculation()
    {
        NormalizeConfiguration();
        RecalculateJobState(subsidyGoldPerTurn);
        NotifyStateChanged();
    }

    private bool TryPaySubsidy(InventoryService inventory)
    {
        lastPaidSubsidyGold = 0;
        if (subsidyGoldPerTurn <= 0)
        {
            return true;
        }

        if (!HasUsableItemId(goldItemId))
        {
            SetLastAbnormalStatus(StatusInvalidGoldItem, "金币配置异常");
            return false;
        }

        if (!inventory.TryRemoveItem(goldItemId, subsidyGoldPerTurn))
        {
            lastTurnSubsidyPaymentFailed = true;
            SetLastAbnormalStatus(StatusSubsidyPaymentFailed, "补贴不足");
            return false;
        }

        lastPaidSubsidyGold = subsidyGoldPerTurn;
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
            lastTurnWorkerResigned = true;
        }
    }

    private bool TryProduceWood(InventoryService inventory)
    {
        var productionAmount = GetCurrentWoodProductionAmount();
        if (productionAmount <= 0)
        {
            SetLastAbnormalStatus(StatusInsufficientWorkers, "工人不足");
            return false;
        }

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

    private void RecalculateJobState(int effectiveSubsidyGoldPerTurn)
    {
        var buildings = GameSystem == null || GameSystem.Buildings == null
            ? null
            : GameSystem.Buildings.Buildings;

        populationCellCount = BuildingJobSystem.CountPopulationCells(this, populationSearchRadius);
        nearbyPopulation = BuildingJobSystem.CountNearbyPopulation(this, buildings, populationSearchRadius);
        nearbyCompetingJobs = BuildingJobSystem.CountNearbyCompetingJobs(this, buildings, jobCompetitionSearchRadius);
        competitionPenalty = BuildingJobSystem.CalculateCompetitionPenalty(
            nearbyCompetingJobs,
            competitionPenaltyPerCompetingJob,
            maxCompetitionPenalty);

        lastJobCalculation = BuildingJobSystem.Calculate(new BuildingJobCalculationInput(
            maxWorkers,
            currentWorkers,
            baseJobAttraction,
            nearbyPopulation,
            populationCellCount,
            Mathf.Max(0, effectiveSubsidyGoldPerTurn),
            competitionPenalty,
            singleRecruitCost));

        stableWorkers = lastJobCalculation.StableWorkers;
        rawJobAttraction = lastJobCalculation.RawAttraction;
        jobAttraction = lastJobCalculation.Attraction;
        populationDensity = lastJobCalculation.PopulationDensity;
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

    private int GetCurrentWoodProductionAmount()
    {
        if (currentWorkers < minimumWorkersForProduction)
        {
            return 0;
        }

        return currentWorkers >= fullProductionWorkers ? fullProductionAmount : baseProductionAmount;
    }

    private IReadOnlyList<BuildingRuntimeStatus> CreateRuntimeStatuses()
    {
        List<BuildingRuntimeStatus> statuses = null;

        AddRuntimeStatus(ref statuses, currentWorkers < minimumWorkersForProduction
            ? new BuildingRuntimeStatus(StatusInsufficientWorkers, "工人不足", currentWorkers, minimumWorkersForProduction)
            : default);

        AddRuntimeStatus(ref statuses, currentWorkers >= minimumWorkersForProduction && currentWorkers < stableWorkers
            ? new BuildingRuntimeStatus(StatusWorkerShortage, "缺工", currentWorkers, stableWorkers)
            : default);

        AddRuntimeStatus(ref statuses, lastTurnWorkerResigned
            ? new BuildingRuntimeStatus(StatusWorkerResigned, "工人离职", currentWorkers, stableWorkers)
            : default);

        AddRuntimeStatus(ref statuses, lastTurnSubsidyPaymentFailed
            ? new BuildingRuntimeStatus(StatusSubsidyPaymentFailed, "补贴不足", lastPaidSubsidyGold, subsidyGoldPerTurn)
            : default);

        AddRuntimeStatus(ref statuses, lastTurnNoAvailablePopulation
            ? new BuildingRuntimeStatus(StatusNoAvailablePopulation, "可用人口不足")
            : default);

        AddRuntimeStatus(ref statuses, ShouldAddLastAbnormalStatus()
            ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
            : default);

        return statuses ?? EmptyRuntimeStatuses;
    }

    private bool ShouldAddLastAbnormalStatus()
    {
        if (string.IsNullOrWhiteSpace(lastAbnormalStatusId))
        {
            return false;
        }

        if (currentWorkers < minimumWorkersForProduction
            && string.Equals(lastAbnormalStatusId, StatusInsufficientWorkers, StringComparison.Ordinal))
        {
            return false;
        }

        if (lastTurnSubsidyPaymentFailed
            && string.Equals(lastAbnormalStatusId, StatusSubsidyPaymentFailed, StringComparison.Ordinal))
        {
            return false;
        }

        if (lastTurnNoAvailablePopulation
            && string.Equals(lastAbnormalStatusId, StatusNoAvailablePopulation, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private void ClearLastTurnState()
    {
        lastPaidSubsidyGold = 0;
        lastProducedWood = 0;
        lastTurnRecruitedWorker = false;
        lastTurnWorkerResigned = false;
        lastTurnSubsidyPaymentFailed = false;
        lastTurnNoAvailablePopulation = false;
        lastAbnormalStatusId = string.Empty;
        lastAbnormalStatusText = string.Empty;
        lastResourceProductions = EmptyResourceChanges;
    }

    private void NormalizeConfiguration()
    {
        maxWorkers = Mathf.Max(1, maxWorkers);
        baseJobAttraction = Mathf.Max(0f, baseJobAttraction);
        subsidyGoldPerTurn = Mathf.Max(0, subsidyGoldPerTurn);
        singleRecruitCost = Mathf.Max(0, singleRecruitCost);
        populationSearchRadius = Mathf.Max(0, populationSearchRadius);
        jobCompetitionSearchRadius = Mathf.Max(0, jobCompetitionSearchRadius);
        competitionPenaltyPerCompetingJob = Mathf.Max(0f, competitionPenaltyPerCompetingJob);
        maxCompetitionPenalty = Mathf.Max(0f, maxCompetitionPenalty);
        minimumWorkersForProduction = Mathf.Clamp(minimumWorkersForProduction, 1, maxWorkers);
        fullProductionWorkers = Mathf.Clamp(fullProductionWorkers, minimumWorkersForProduction, maxWorkers);
        baseProductionAmount = Mathf.Max(0, baseProductionAmount);
        fullProductionAmount = Mathf.Max(baseProductionAmount, fullProductionAmount);
        currentWorkers = Mathf.Clamp(currentWorkers, 0, maxWorkers);
        woodItemId = NormalizeItemId(woodItemId, DefaultWoodItemId);
        goldItemId = NormalizeItemId(goldItemId, DefaultGoldItemId);
    }

    private void OnValidate()
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

    private static bool HasUsableItemId(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId);
    }

    private static string NormalizeItemId(string itemId, string fallback)
    {
        return string.IsNullOrWhiteSpace(itemId) ? fallback : itemId.Trim();
    }

    private static IReadOnlyList<BuildingResourceChange> CreateResourceChanges(string itemId, int amount)
    {
        var change = new BuildingResourceChange(itemId, amount);
        return change.IsValid ? new[] { change } : EmptyResourceChanges;
    }

    private static void AddRuntimeStatus(ref List<BuildingRuntimeStatus> statuses, BuildingRuntimeStatus status)
    {
        if (!status.IsValid)
        {
            return;
        }

        statuses ??= new List<BuildingRuntimeStatus>();
        statuses.Add(status);
    }
}
