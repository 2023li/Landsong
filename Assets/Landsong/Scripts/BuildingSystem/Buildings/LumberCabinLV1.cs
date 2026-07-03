using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

public class LumberCabinLV1 : BuildingBase, IBuildingJobSource, IBuildingResourceProductionSource
{
    private const string DefaultWoodItemId = "原木";
    private const string DefaultGoldItemId = "金币";
    private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
    private const string StatusInvalidWoodItem = BuildingRuntimeStatusCatalog.BS_原木配置异常;
    private const string StatusInvalidGoldItem = BuildingRuntimeStatusCatalog.BS_金币配置异常;
    private const string StatusWoodStorageFailed = BuildingRuntimeStatusCatalog.BS_原木存入失败;
    private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
    private const string StatusWorkerShortage = BuildingRuntimeStatusCatalog.BS_缺工;
    private const string StatusSubsidyPaymentFailed = BuildingRuntimeStatusCatalog.BS_补贴不足;
    private const string StatusRecruitGoldMissing = BuildingRuntimeStatusCatalog.BS_招工金币不足;

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
    [SerializeField, Min(0f)] private float populationDensityAttractionMultiplier =
        BuildingJobSystem.DefaultPopulationDensityAttractionMultiplier;

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
    [SerializeField, ReadOnly] private float populationAttractionBonus;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private float externalAttractionPenalty;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int lastPaidSubsidyGold;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int lastProducedWood;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnRecruitedWorker;

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
    public float PopulationAttractionBonus => populationAttractionBonus;
    public float ExternalAttractionPenalty => externalAttractionPenalty;
    public int LastPaidSubsidyGold => lastPaidSubsidyGold;
    public int LastProducedWood => lastProducedWood;
    public BuildingJobCalculation LastJobCalculation => lastJobCalculation;
    public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions =>
        CreateResourceChanges(woodItemId, GetCurrentWoodProductionAmount());
    public IReadOnlyList<BuildingResourceChange> LastResourceProductions => lastResourceProductions;

    public override string GetBaseInfo()
    {
        return $"工人 {currentWorkers}/{stableWorkers}";
    }

    public override BuildingDetailInfo GetDetailInfo()
    {
        return new BuildingDetailInfo(CreateDetailSections());
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

    protected override BuildingDataBase CaptureBuildingData()
    {
        return new LumberCabinLV1Data
        {
            CurrentWorkers = currentWorkers,
            SubsidyGoldPerTurn = subsidyGoldPerTurn,
            LastPaidSubsidyGold = lastPaidSubsidyGold,
            LastProducedWood = lastProducedWood,
            LastTurnRecruitedWorker = lastTurnRecruitedWorker,
            LastTurnSubsidyPaymentFailed = lastTurnSubsidyPaymentFailed,
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
        subsidyGoldPerTurn = cabinData.SubsidyGoldPerTurn;
        lastPaidSubsidyGold = cabinData.LastPaidSubsidyGold;
        lastProducedWood = cabinData.LastProducedWood;
        lastTurnRecruitedWorker = cabinData.LastTurnRecruitedWorker;
        lastTurnSubsidyPaymentFailed = cabinData.LastTurnSubsidyPaymentFailed;
        lastTurnNoAvailablePopulation = cabinData.LastTurnNoAvailablePopulation;
        lastAbnormalStatusId = string.IsNullOrWhiteSpace(cabinData.LastAbnormalStatusId)
            ? string.Empty
            : cabinData.LastAbnormalStatusId.Trim();
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(cabinData.LastAbnormalStatusText)
            ? lastAbnormalStatusId
            : cabinData.LastAbnormalStatusText.Trim();

        NormalizeConfiguration();
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
            SendBuildingEvent(GameEventCatalog.GE_可用人口不足, "可用人口不足");
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
            SendBuildingEvent(GameEventCatalog.GE_可用人口不足, "可用人口不足");
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
            SendBuildingEvent(GameEventCatalog.GE_补贴不足, "补贴不足");
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
        externalAttractionPenalty = BuildingJobSystem.ResolveExternalAttractionPenalty(
            this,
            GameSystem);

        lastJobCalculation = BuildingJobSystem.Calculate(new BuildingJobCalculationInput(
            maxWorkers,
            currentWorkers,
            baseJobAttraction,
            nearbyPopulation,
            populationCellCount,
            populationDensityAttractionMultiplier,
            Mathf.Max(0, effectiveSubsidyGoldPerTurn),
            externalAttractionPenalty,
            singleRecruitCost));

        stableWorkers = lastJobCalculation.StableWorkers;
        rawJobAttraction = lastJobCalculation.RawAttraction;
        jobAttraction = lastJobCalculation.Attraction;
        populationDensity = lastJobCalculation.PopulationDensity;
        populationAttractionBonus = lastJobCalculation.PopulationAttractionBonus;
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

        AddRuntimeStatus(ref statuses, lastTurnSubsidyPaymentFailed
            ? new BuildingRuntimeStatus(StatusSubsidyPaymentFailed, "补贴不足", lastPaidSubsidyGold, subsidyGoldPerTurn)
            : default);

        AddRuntimeStatus(ref statuses, ShouldAddLastAbnormalStatus()
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

        return true;
    }

    private void ClearLastTurnState()
    {
        lastPaidSubsidyGold = 0;
        lastProducedWood = 0;
        lastTurnRecruitedWorker = false;
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
        populationDensityAttractionMultiplier = Mathf.Max(0f, populationDensityAttractionMultiplier);
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

    private void SendBuildingEvent(string eventTypeId, string message)
    {
        GameSystem?.Events?.AddMessage(GameEventMessage.ForBuildingEvent(
            eventTypeId,
            this,
            message,
            GetEventTurnNumber()));
    }

    private int GetEventTurnNumber()
    {
        if (GameSystem == null)
        {
            return 0;
        }

        return GameSystem.IsAdvancingTurn ? GameSystem.CurrentTurn + 1 : GameSystem.CurrentTurn;
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

    private IReadOnlyList<BuildingDetailSection> CreateDetailSections()
    {
        BuildingDetailSection[] sections =
        {
            new BuildingDetailSection(
                "岗位",
                new[]
                {
                    new BuildingDetailRow("当前工人", $"{currentWorkers}/{maxWorkers}"),
                    new BuildingDetailRow("稳定工人", stableWorkers.ToString()),
                    new BuildingDetailRow("每回合补贴", subsidyGoldPerTurn.ToString()),
                    new BuildingDetailRow("上回合支付补贴", lastPaidSubsidyGold.ToString()),
                    new BuildingDetailRow("原始岗位吸引力", rawJobAttraction.ToString("0.##")),
                    new BuildingDetailRow("岗位吸引力", jobAttraction.ToString("0.##")),
                    new BuildingDetailRow("附近人口", nearbyPopulation.ToString()),
                    new BuildingDetailRow("人口密度", populationDensity.ToString("0.####")),
                    new BuildingDetailRow("人口吸引力加成", populationAttractionBonus.ToString("0.##")),
                    new BuildingDetailRow("外部吸引力惩罚", externalAttractionPenalty.ToString("0.##"))
                }),
            new BuildingDetailSection(
                "产出",
                new[]
                {
                    new BuildingDetailRow("产出物品", woodItemId),
                    new BuildingDetailRow("当前预计产出", $"{woodItemId} x{GetCurrentWoodProductionAmount()}"),
                    new BuildingDetailRow("上回合产出", FormatResourceChanges(lastResourceProductions)),
                    new BuildingDetailRow("最低生产工人", minimumWorkersForProduction.ToString()),
                    new BuildingDetailRow("满额生产工人", fullProductionWorkers.ToString())
                }),
            new BuildingDetailSection(
                "岗位调试",
                new[]
                {
                    new BuildingDetailRow("基础吸引力", lastJobCalculation.BaseAttraction.ToString("0.##")),
                    new BuildingDetailRow("人口密度倍率", lastJobCalculation.PopulationDensityAttractionMultiplier.ToString("0.##")),
                    new BuildingDetailRow("人均补贴", lastJobCalculation.PerWorkerSubsidy.ToString("0.##")),
                    new BuildingDetailRow("补贴加成", lastJobCalculation.SubsidyBonus.ToString("0.##")),
                    new BuildingDetailRow("缺工比例", lastJobCalculation.WorkerShortageRatio.ToString("0.##")),
                    new BuildingDetailRow("招工概率", $"{lastJobCalculation.RecruitChancePercent:0.##}%"),
                    new BuildingDetailRow("超员比例", lastJobCalculation.ExcessWorkerRatio.ToString("0.##")),
                    new BuildingDetailRow("离职概率", $"{lastJobCalculation.ResignChancePercent:0.##}%"),
                    new BuildingDetailRow("立即招工费用", lastJobCalculation.ImmediateRecruitCost.ToString())
                })
        };

        return sections;
    }

    private static string FormatResourceChanges(IReadOnlyList<BuildingResourceChange> changes)
    {
        if (changes == null || changes.Count == 0)
        {
            return "无";
        }

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < changes.Count; i++)
        {
            var change = changes[i];
            if (!change.IsValid)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("，");
            }

            builder.Append(change.ItemId);
            builder.Append(" x");
            builder.Append(change.Amount);
        }

        return builder.Length == 0 ? "无" : builder.ToString();
    }

    [Serializable]
    private sealed class LumberCabinLV1Data : BuildingDataBase
    {
        public int CurrentWorkers;
        public int SubsidyGoldPerTurn;
        public int LastPaidSubsidyGold;
        public int LastProducedWood;
        public bool LastTurnRecruitedWorker;
        public bool LastTurnSubsidyPaymentFailed;
        public bool LastTurnNoAvailablePopulation;
        public string LastAbnormalStatusId;
        public string LastAbnormalStatusText;
    }
}
