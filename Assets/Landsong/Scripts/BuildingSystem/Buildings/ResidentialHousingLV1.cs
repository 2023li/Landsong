using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Landsong.UISystem;
using Sirenix.OdinInspector;
using UnityEngine;

public class ResidentialHousingLV1 : BuildingBase, IBuildingResourceConsumptionSource, IBuildingTaxSource, IBuildingPopulationSource, IBuildingConnectionConsumer
{
    private static readonly IReadOnlyList<string> ResourceConnectionTypes =
        new[] { BuildingConnectionTypes.Resource };
    private const string DefaultFoodItemId = "蔬菜";
    private const string DefaultTaxItemId = "金币";
    private const string StatusAbandoned = BuildingRuntimeStatusCatalog.BS_废弃;
    private const string StatusConsumptionFailed = BuildingRuntimeStatusCatalog.BS_消耗失败;
    private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
    private const string StatusInvalidFoodItem = BuildingRuntimeStatusCatalog.BS_食物配置异常;
    private const string StatusMissingResourceProvider = BuildingRuntimeStatusCatalog.BS_无法连接资源点;
    private const string StatusMissingFood = BuildingRuntimeStatusCatalog.BS_食物不足;
    private const string StatusInvalidTaxItem = BuildingRuntimeStatusCatalog.BS_税收配置异常;
    private const string StatusTaxRewardFailed = BuildingRuntimeStatusCatalog.BS_税收存入失败;

    public IReadOnlyList<string> RequiredConnectionTypeIds => ResourceConnectionTypes;

    [TitleGroup("人口")]
    [SerializeField, Min(1)] private int initialPopulationContribution = 2;

    [TitleGroup("人口")]
    [SerializeField, Min(1)] private int maxPopulationContribution = 5;

    [TitleGroup("运营消耗")]
    [SerializeField] private string foodItemId = DefaultFoodItemId;

    [TitleGroup("运营消耗")]
    [SerializeField, Min(1)] private int growthIntervalTurns = 3;

    [TitleGroup("运营消耗")]
    [SerializeField, Min(1)] private int consumptionFailureDecayThreshold = 3;

    [TitleGroup("税收")]
    [SerializeField] private string taxItemId = DefaultTaxItemId;

    [TitleGroup("税收")]
    [SerializeField, Min(1)] private int taxIntervalTurns = 5;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int currentPopulation = 2;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int growthConsumptionProgress;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int taxConsumptionProgress;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int consecutiveConsumptionFailures;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool isAbandoned;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnHadResourceProvider;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnConsumptionFailed;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnConsumedResources;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnPopulationDecayed;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnGrewPopulation;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private bool lastTurnProvidedTax;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private int lastResourceProviderActionCost = -1;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private string lastAbnormalStatusId = string.Empty;

    [TitleGroup("运行时")]
    [SerializeField, ReadOnly] private string lastAbnormalStatusText = string.Empty;

    private IReadOnlyList<BuildingResourceChange> lastResourceConsumptions = EmptyResourceChanges;
    private IReadOnlyList<BuildingResourceChange> lastTaxRewards = EmptyResourceChanges;

    public int CurrentPopulation => currentPopulation;
    public int MaxPopulation => maxPopulationContribution;
    public int GrowthConsumptionProgress => growthConsumptionProgress;
    public int GrowthIntervalTurns => growthIntervalTurns;
    public int TaxConsumptionProgress => taxConsumptionProgress;
    public int TaxIntervalTurns => taxIntervalTurns;
    public int ConsecutiveConsumptionFailures => consecutiveConsumptionFailures;
    public int ConsumptionFailureDecayThreshold => consumptionFailureDecayThreshold;
    public bool IsAbandoned => isAbandoned;
    public bool HasReachedMaxPopulation => !isAbandoned && currentPopulation >= maxPopulationContribution;
    public bool LastTurnHadResourceProvider => lastTurnHadResourceProvider;
    public bool LastTurnConsumptionFailed => lastTurnConsumptionFailed;
    public bool LastTurnConsumedResources => lastTurnConsumedResources;
    public bool LastTurnPopulationDecayed => lastTurnPopulationDecayed;
    public bool LastTurnGrewPopulation => lastTurnGrewPopulation;
    public bool LastTurnProvidedTax => lastTurnProvidedTax;
    public string LastAbnormalStatusId => lastAbnormalStatusId;
    public string LastAbnormalStatusText => lastAbnormalStatusText;
    public int LastResourceProviderActionCost => lastResourceProviderActionCost;
    public IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions =>
        isAbandoned ? EmptyResourceChanges : CreateResourceChanges(foodItemId, GetCurrentFoodConsumptionAmount());
    public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions => lastResourceConsumptions;
    public IReadOnlyList<BuildingResourceChange> CurrentTaxRewards =>
        HasReachedMaxPopulation ? CreateResourceChanges(taxItemId, currentPopulation) : EmptyResourceChanges;
    public IReadOnlyList<BuildingResourceChange> LastTaxRewards => lastTaxRewards;
    public override string GetOverviewInfo()
    {
        return $"人口 {currentPopulation}/{maxPopulationContribution}";
    }

    public override IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries()
    {
        List<BuildingFunctionBlockEntry> entries = null;
        var foodAmount = GetCurrentFoodConsumptionAmount();
        if (!isAbandoned && foodAmount > 0)
        {
            var foodConsumptionPerPopulation = GetFoodConsumptionPerPopulation();
            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.资源组,
                    foodItemId,
                    -foodAmount,
                    new BuildingFunctionBlockSidebarRow(
                        "每人口消耗",
                        $"{foodConsumptionPerPopulation}{foodItemId}",
                        -foodConsumptionPerPopulation,
                        true)));
        }

        AppendBuildingModuleFunctionBlockEntries(ref entries);
        return entries == null ? EmptyFunctionBlockEntries : entries;
    }

    public override IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses()
    {
        return CreateRuntimeStatuses();
    }

    protected override void OnInitialized()
    {
        NormalizeConfiguration();
        if (!isAbandoned && currentPopulation <= 0)
        {
            currentPopulation = initialPopulationContribution;
        }

        currentPopulation = Mathf.Clamp(currentPopulation, 0, maxPopulationContribution);
        growthConsumptionProgress = Mathf.Clamp(growthConsumptionProgress, 0, growthIntervalTurns);
        taxConsumptionProgress = Mathf.Clamp(taxConsumptionProgress, 0, taxIntervalTurns);
    }

    protected override void OnPlaced()
    {
    }

    protected override void OnRegistered()
    {
        UpdatePopulationContribution();
    }

    protected override bool OnTurn()
    {
        NormalizeConfiguration();
        ClearLastTurnState();

        if (isAbandoned || currentPopulation <= 0)
        {
            EnterAbandonedState();
            SetLastAbnormalStatus(StatusAbandoned, "建筑荒废");
            return false;
        }

        var inventory = GameSystem == null ? null : GameSystem.Inventory;

        if (inventory == null)
        {
            return RegisterConsumptionFailure(
                StatusMissingInventory,
                "库存服务缺失",
                $"ResidentialHousingLV1 '{name}' cannot operate because InventoryService is missing.");
        }

        if (!HasUsableItemId(foodItemId))
        {
            return RegisterConsumptionFailure(
                StatusInvalidFoodItem,
                "食物配置异常",
                $"ResidentialHousingLV1 '{name}' cannot consume food because food item id is empty.");
        }

        if (!BuildingResourceProviderSystem.TrySelectProvider(this, out var providerSelection))
        {
            return RegisterConsumptionFailure(StatusMissingResourceProvider, "无法连接资源");
        }

        lastTurnHadResourceProvider = true;
        lastResourceProviderActionCost = providerSelection.ActionCost;

        var foodAmount = GetCurrentFoodConsumptionAmount();
        if (foodAmount > 0 && !inventory.TryRemoveItem(foodItemId, foodAmount))
        {
            return RegisterConsumptionFailure(StatusMissingFood, $"{foodItemId}不足");
        }

        ClearConsumptionFailure();
        lastTurnConsumedResources = foodAmount > 0;
        lastResourceConsumptions = CreateResourceChanges(foodItemId, foodAmount);
        if (foodAmount > 0)
        {
            BuildingResourceProviderSystem.RecordProvidedResource(
                providerSelection,
                this,
                new BuildingResourceChange(foodItemId, foodAmount));
        }

        return ProcessSuccessfulConsumption(inventory);
    }

    protected override BuildingDataBase CaptureBuildingData()
    {
        return new ResidentialHousingLV1Data
        {
            CurrentPopulation = currentPopulation,
            GrowthConsumptionProgress = growthConsumptionProgress,
            TaxConsumptionProgress = taxConsumptionProgress,
            ConsecutiveConsumptionFailures = consecutiveConsumptionFailures,
            IsAbandoned = isAbandoned,
            LastTurnHadResourceProvider = lastTurnHadResourceProvider,
            LastTurnConsumptionFailed = lastTurnConsumptionFailed,
            LastTurnConsumedResources = lastTurnConsumedResources,
            LastTurnPopulationDecayed = lastTurnPopulationDecayed,
            LastTurnGrewPopulation = lastTurnGrewPopulation,
            LastTurnProvidedTax = lastTurnProvidedTax,
            LastResourceProviderActionCost = lastResourceProviderActionCost,
            LastAbnormalStatusId = lastAbnormalStatusId,
            LastAbnormalStatusText = lastAbnormalStatusText
        };
    }

    protected override void RestoreBuildingData(BuildingDataBase data)
    {
        if (data is not ResidentialHousingLV1Data housingData)
        {
            return;
        }

        currentPopulation = housingData.CurrentPopulation;
        growthConsumptionProgress = housingData.GrowthConsumptionProgress;
        taxConsumptionProgress = housingData.TaxConsumptionProgress;
        consecutiveConsumptionFailures = housingData.ConsecutiveConsumptionFailures;
        isAbandoned = housingData.IsAbandoned;
        lastTurnHadResourceProvider = housingData.LastTurnHadResourceProvider;
        lastTurnConsumptionFailed = housingData.LastTurnConsumptionFailed;
        lastTurnConsumedResources = housingData.LastTurnConsumedResources;
        lastTurnPopulationDecayed = housingData.LastTurnPopulationDecayed;
        lastTurnGrewPopulation = housingData.LastTurnGrewPopulation;
        lastTurnProvidedTax = housingData.LastTurnProvidedTax;
        lastResourceProviderActionCost = housingData.LastResourceProviderActionCost;
        lastAbnormalStatusId = string.IsNullOrWhiteSpace(housingData.LastAbnormalStatusId)
            ? string.Empty
            : housingData.LastAbnormalStatusId.Trim();
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(housingData.LastAbnormalStatusText)
            ? lastAbnormalStatusId
            : housingData.LastAbnormalStatusText.Trim();

        NormalizeConfiguration();
    }

    protected override void OnUnregistered()
    {
        GameSystem?.Dynasty?.RemovePopulationContribution(this);
    }

    private bool ProcessSuccessfulConsumption(InventoryService inventory)
    {
        if (HasReachedMaxPopulation)
        {
            growthConsumptionProgress = 0;
            taxConsumptionProgress++;

            if (taxConsumptionProgress < taxIntervalTurns)
            {
                return true;
            }

            return TryProvideTax(inventory);
        }

        taxConsumptionProgress = 0;
        growthConsumptionProgress++;

        if (growthConsumptionProgress < growthIntervalTurns)
        {
            return true;
        }

        growthConsumptionProgress = 0;
        var previousPopulation = currentPopulation;
        currentPopulation = Mathf.Min(maxPopulationContribution, currentPopulation + 1);
        lastTurnGrewPopulation = currentPopulation > previousPopulation;

        if (lastTurnGrewPopulation)
        {
            GameSystem?.Dynasty?.SetPopulationContribution(this, currentPopulation);
        }

        return true;
    }
    protected override void OnClicked()
    {
        base.OnClicked();
    }


    private bool TryProvideTax(InventoryService inventory)
    {
        if (!HasUsableItemId(taxItemId))
        {
            Debug.LogWarning($"ResidentialHousingLV1 '{name}' cannot provide tax because tax item id is empty.", this);
            taxConsumptionProgress = taxIntervalTurns;
            SetLastAbnormalStatus(StatusInvalidTaxItem, "税收配置异常");
            return false;
        }

        var taxAmount = Mathf.Max(0, currentPopulation);
        if (taxAmount <= 0)
        {
            taxConsumptionProgress = 0;
            return true;
        }

        if (!inventory.TryAddItem(taxItemId, taxAmount))
        {
            Debug.LogWarning(
                $"ResidentialHousingLV1 '{name}' could not add tax reward '{taxItemId}' x{taxAmount}.",
                this);
            taxConsumptionProgress = taxIntervalTurns;
            SetLastAbnormalStatus(StatusTaxRewardFailed, "税收存入失败");
            return false;
        }

        taxConsumptionProgress = 0;
        lastTurnProvidedTax = true;
        lastTaxRewards = CreateResourceChanges(taxItemId, taxAmount);
        return true;
    }

    private int GetCurrentFoodConsumptionAmount()
    {
        return Mathf.Max(0, currentPopulation);
    }

    private int GetFoodConsumptionPerPopulation()
    {
        return currentPopulation <= 0
            ? 0
            : Mathf.CeilToInt(GetCurrentFoodConsumptionAmount() / (float)currentPopulation);
    }

    private bool RegisterConsumptionFailure(string statusId, string statusText, string warningMessage = null)
    {
        if (!string.IsNullOrWhiteSpace(warningMessage))
        {
            Debug.LogWarning(warningMessage, this);
        }

        lastTurnConsumptionFailed = true;
        SetLastAbnormalStatus(statusId, statusText);
        ResetConsumptionProgress();

        consecutiveConsumptionFailures++;
        if (consecutiveConsumptionFailures >= consumptionFailureDecayThreshold)
        {
            DecayPopulation();
        }

        return false;
    }

    private void DecayPopulation()
    {
        consecutiveConsumptionFailures = 0;
        var previousPopulation = currentPopulation;
        currentPopulation = Mathf.Max(0, currentPopulation - 1);
        lastTurnPopulationDecayed = currentPopulation < previousPopulation;

        if (lastTurnPopulationDecayed)
        {
            UpdatePopulationContribution();
            SendBuildingEvent(GameEventCatalog.GE_人口衰减, "居民房人口衰减！");
        }

        if (currentPopulation <= 0)
        {
            EnterAbandonedState();
        }
    }

    private void EnterAbandonedState()
    {
        isAbandoned = true;
        currentPopulation = 0;
        consecutiveConsumptionFailures = 0;
        ResetConsumptionProgress();
        UpdatePopulationContribution();
    }

    private void ClearConsumptionFailure()
    {
        consecutiveConsumptionFailures = 0;
    }

    private void ResetConsumptionProgress()
    {
        growthConsumptionProgress = 0;
        taxConsumptionProgress = 0;
    }

    private void ClearLastTurnState()
    {
        lastTurnHadResourceProvider = false;
        lastTurnConsumptionFailed = false;
        lastTurnConsumedResources = false;
        lastTurnPopulationDecayed = false;
        lastTurnGrewPopulation = false;
        lastTurnProvidedTax = false;
        lastResourceProviderActionCost = -1;
        lastAbnormalStatusId = string.Empty;
        lastAbnormalStatusText = string.Empty;
        lastResourceConsumptions = EmptyResourceChanges;
        lastTaxRewards = EmptyResourceChanges;
    }

    private void NormalizeConfiguration()
    {
        initialPopulationContribution = Mathf.Max(1, initialPopulationContribution);
        maxPopulationContribution = Mathf.Max(initialPopulationContribution, maxPopulationContribution);
        currentPopulation = Mathf.Clamp(currentPopulation, 0, maxPopulationContribution);
        growthIntervalTurns = Mathf.Max(1, growthIntervalTurns);
        consumptionFailureDecayThreshold = Mathf.Max(1, consumptionFailureDecayThreshold);
        taxIntervalTurns = Mathf.Max(1, taxIntervalTurns);
        growthConsumptionProgress = Mathf.Clamp(growthConsumptionProgress, 0, growthIntervalTurns);
        taxConsumptionProgress = Mathf.Clamp(taxConsumptionProgress, 0, taxIntervalTurns);
        consecutiveConsumptionFailures = Mathf.Clamp(consecutiveConsumptionFailures, 0, consumptionFailureDecayThreshold);
        if (isAbandoned)
        {
            currentPopulation = 0;
            consecutiveConsumptionFailures = 0;
        }

        foodItemId = NormalizeItemId(foodItemId, DefaultFoodItemId);
        taxItemId = NormalizeItemId(taxItemId, DefaultTaxItemId);
    }

    private void OnValidate()
    {
        NormalizeConfiguration();
    }

    private IReadOnlyList<BuildingRuntimeStatus> CreateRuntimeStatuses()
    {
        List<BuildingRuntimeStatus> statuses = null;

        AppendRuntimeStatus(ref statuses, isAbandoned
            ? new BuildingRuntimeStatus(StatusAbandoned, "荒废")
            : default);

        AppendRuntimeStatus(ref statuses, consecutiveConsumptionFailures > 0
            ? new BuildingRuntimeStatus(
                StatusConsumptionFailed,
                "消耗失败",
                consecutiveConsumptionFailures,
                consumptionFailureDecayThreshold)
            : default);

        AppendRuntimeStatus(ref statuses, ShouldAddLastAbnormalStatus()
            ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
            : default);

        AppendCommonRuntimeStatuses(ref statuses);
        return statuses ?? EmptyRuntimeStatuses;
    }

    private void SetLastAbnormalStatus(string statusId, string statusText)
    {
        lastAbnormalStatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
        lastAbnormalStatusText = string.IsNullOrWhiteSpace(statusText) ? lastAbnormalStatusId : statusText.Trim();
    }

    private void UpdatePopulationContribution()
    {
        GameSystem?.Dynasty?.SetPopulationContribution(this, isAbandoned ? 0 : currentPopulation);
    }

    private bool ShouldAddLastAbnormalStatus()
    {
        if (string.IsNullOrWhiteSpace(lastAbnormalStatusId))
        {
            return false;
        }

        if (isAbandoned && string.Equals(lastAbnormalStatusId, StatusAbandoned, StringComparison.Ordinal))
        {
            return false;
        }

        return consecutiveConsumptionFailures <= 0
               || !string.Equals(lastAbnormalStatusId, StatusConsumptionFailed, StringComparison.Ordinal);
    }

    [Serializable]
    [BuildingDataTypeId("building.residential_housing.lv1")]
    private sealed class ResidentialHousingLV1Data : BuildingDataBase
    {
        public int CurrentPopulation;
        public int GrowthConsumptionProgress;
        public int TaxConsumptionProgress;
        public int ConsecutiveConsumptionFailures;
        public bool IsAbandoned;
        public bool LastTurnHadResourceProvider;
        public bool LastTurnConsumptionFailed;
        public bool LastTurnConsumedResources;
        public bool LastTurnPopulationDecayed;
        public bool LastTurnGrewPopulation;
        public bool LastTurnProvidedTax;
        public int LastResourceProviderActionCost;
        public string LastAbnormalStatusId;
        public string LastAbnormalStatusText;
    }

}
