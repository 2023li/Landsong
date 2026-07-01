using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GridSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

public class ResidentialHousingLV1 : BuildingBase, IBuildingResourceConsumptionSource, IBuildingTaxSource, IBuildingRuntimeStatusSource, IBuildingOverviewSource, IBuildingPopulationSource
{
    private const string DefaultFoodItemId = "蔬菜";
    private const string DefaultTaxItemId = "金币";
    private const string StatusAbandoned = "abandoned";
    private const string StatusConsumptionFailed = "consumption_failed";
    private const string StatusPopulationDecayed = "population_decayed";
    private const string StatusMissingInventory = "missing_inventory";
    private const string StatusInvalidFoodItem = "invalid_food_item";
    private const string StatusMissingResourceProvider = "missing_resource_provider";
    private const string StatusMissingFood = "missing_food";
    private const string StatusInvalidTaxItem = "invalid_tax_item";
    private const string StatusTaxRewardFailed = "tax_reward_failed";
    private const int ScaledRoadStepCost = 1;
    private const int ScaledNormalStepCost = 2;

    private static readonly IReadOnlyList<BuildingResourceChange> EmptyResourceChanges =
        Array.Empty<BuildingResourceChange>();

    private static readonly IReadOnlyList<BuildingRuntimeStatus> EmptyRuntimeStatuses =
        Array.Empty<BuildingRuntimeStatus>();

    [TitleGroup("人口")]
    [SerializeField, Min(1)] private int initialPopulationContribution = 2;

    [TitleGroup("人口")]
    [SerializeField, Min(1)] private int maxPopulationContribution = 5;

    [TitleGroup("运营消耗")]
    [SerializeField] private string foodItemId = DefaultFoodItemId;

    [TitleGroup("运营消耗")]
    [SerializeField, Min(0f)] private float resourceProviderSearchRange = 10f;

    [TitleGroup("运营消耗")]
    [SerializeField, Min(1)] private int growthIntervalTurns = 3;

    [TitleGroup("运营消耗")]
    [SerializeField, Min(1)] private int consumptionFailureDecayThreshold = 3;

    [TitleGroup("税收")]
    [SerializeField] private string taxItemId = DefaultTaxItemId;

    [TitleGroup("税收")]
    [SerializeField, Min(1)] private int taxIntervalTurns = 5;

    [TitleGroup("寻路")]
    [SerializeField] private string roadTerrainKey = GridTerrainKeys.Road;

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
    [SerializeField, ReadOnly] private float lastResourceProviderPathCost = -1f;

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
    public float LastResourceProviderPathCost => lastResourceProviderPathCost;
    public IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions =>
        isAbandoned ? EmptyResourceChanges : CreateResourceChanges(foodItemId, GetCurrentFoodConsumptionAmount());
    public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions => lastResourceConsumptions;
    public IReadOnlyList<BuildingResourceChange> CurrentTaxRewards =>
        HasReachedMaxPopulation ? CreateResourceChanges(taxItemId, currentPopulation) : EmptyResourceChanges;
    public IReadOnlyList<BuildingResourceChange> LastTaxRewards => lastTaxRewards;
    public IReadOnlyList<BuildingRuntimeStatus> RuntimeStatuses => CreateRuntimeStatuses();
    public string OverviewValueLabel => "人口";
    public string OverviewValueText => $"{currentPopulation}/{maxPopulationContribution}";

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

        if (!HasReachableResourceProvider())
        {
            return RegisterConsumptionFailure(StatusMissingResourceProvider, "无法连接资源");
        }

        lastTurnHadResourceProvider = true;

        var foodAmount = GetCurrentFoodConsumptionAmount();
        if (foodAmount > 0 && !inventory.TryRemoveItem(foodItemId, foodAmount))
        {
            return RegisterConsumptionFailure(StatusMissingFood, $"{foodItemId}不足");
        }

        ClearConsumptionFailure();
        lastTurnConsumedResources = foodAmount > 0;
        lastResourceConsumptions = CreateResourceChanges(foodItemId, foodAmount);

        return ProcessSuccessfulConsumption(inventory);
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

    private bool HasReachableResourceProvider()
    {
        lastResourceProviderPathCost = -1f;

        if (!HasPlacement || GridMap == null || GameSystem == null || GameSystem.Buildings == null)
        {
            return false;
        }

        var buildings = GameSystem.Buildings.Buildings;
        if (buildings == null || buildings.Count == 0)
        {
            return false;
        }

        var maxScaledCost = Mathf.CeilToInt(resourceProviderSearchRange * ScaledNormalStepCost);
        var bestScaledCost = int.MaxValue;

        for (var i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (!CanUseAsResourceProvider(building))
            {
                continue;
            }

            if (!TryGetScaledPathCostTo(building, maxScaledCost, out var scaledCost))
            {
                continue;
            }

            if (scaledCost < bestScaledCost)
            {
                bestScaledCost = scaledCost;
            }
        }

        if (bestScaledCost == int.MaxValue)
        {
            return false;
        }

        lastResourceProviderPathCost = bestScaledCost / (float)ScaledNormalStepCost;
        return true;
    }

    private bool CanUseAsResourceProvider(BuildingBase building)
    {
        if (building == null || building == this || !building.isActiveAndEnabled || building.IsDemolishing)
        {
            return false;
        }

        if (!building.HasPlacement || building.GridMap != GridMap)
        {
            return false;
        }

        return building is IResourceProviderPoint provider && provider.IsResourceProviderPoint;
    }

    private bool TryGetScaledPathCostTo(BuildingBase target, int maxScaledCost, out int scaledCost)
    {
        scaledCost = int.MaxValue;

        if (target == null || GridMap == null)
        {
            return false;
        }

        var targetCells = new HashSet<GridPosition>();
        foreach (var position in target.Footprint.Positions())
        {
            targetCells.Add(position);
        }

        if (targetCells.Count == 0)
        {
            return false;
        }

        var open = new List<PathNode>();
        var bestCosts = new Dictionary<GridPosition, int>();

        foreach (var position in Footprint.Positions())
        {
            bestCosts[position] = 0;
            open.Add(new PathNode(position, 0));
        }

        while (open.Count > 0)
        {
            var cheapestIndex = GetCheapestOpenNodeIndex(open);
            var current = open[cheapestIndex];
            open.RemoveAt(cheapestIndex);

            if (targetCells.Contains(current.Position))
            {
                scaledCost = current.ScaledCost;
                return true;
            }

            if (current.ScaledCost > maxScaledCost)
            {
                continue;
            }

            TryVisitNeighbor(target, targetCells, current, 1, 0, maxScaledCost, open, bestCosts);
            TryVisitNeighbor(target, targetCells, current, -1, 0, maxScaledCost, open, bestCosts);
            TryVisitNeighbor(target, targetCells, current, 0, 1, maxScaledCost, open, bestCosts);
            TryVisitNeighbor(target, targetCells, current, 0, -1, maxScaledCost, open, bestCosts);
        }

        return false;
    }

    private void TryVisitNeighbor(
        BuildingBase target,
        HashSet<GridPosition> targetCells,
        PathNode current,
        int offsetX,
        int offsetY,
        int maxScaledCost,
        List<PathNode> open,
        Dictionary<GridPosition, int> bestCosts)
    {
        var neighbor = new GridPosition(current.Position.X + offsetX, current.Position.Y + offsetY);
        if (!CanPathThrough(neighbor, target, targetCells))
        {
            return;
        }

        var nextScaledCost = current.ScaledCost + GetScaledStepCost(neighbor);
        if (nextScaledCost > maxScaledCost)
        {
            return;
        }

        if (bestCosts.TryGetValue(neighbor, out var knownCost) && knownCost <= nextScaledCost)
        {
            return;
        }

        bestCosts[neighbor] = nextScaledCost;
        open.Add(new PathNode(neighbor, nextScaledCost));
    }

    private bool CanPathThrough(
        GridPosition position,
        BuildingBase target,
        HashSet<GridPosition> targetCells)
    {
        if (GridMap == null || !GridMap.HasBaseTileAt(position))
        {
            return false;
        }

        if (targetCells.Contains(position))
        {
            return true;
        }

        if (!GridMap.TryGetOccupantId(position, out var occupantId))
        {
            return true;
        }

        return string.Equals(occupantId, GridOccupancyId, StringComparison.Ordinal)
               || string.Equals(occupantId, target.GridOccupancyId, StringComparison.Ordinal);
    }

    private int GetScaledStepCost(GridPosition position)
    {
        return !string.IsNullOrWhiteSpace(roadTerrainKey) && GridMap != null && GridMap.HasTerrainKey(position, roadTerrainKey)
            ? ScaledRoadStepCost
            : ScaledNormalStepCost;
    }

    private int GetCurrentFoodConsumptionAmount()
    {
        return Mathf.Max(0, currentPopulation);
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
        lastResourceProviderPathCost = -1f;
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
        resourceProviderSearchRange = Mathf.Max(0f, resourceProviderSearchRange);
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
        roadTerrainKey = GridTerrainKeys.Normalize(roadTerrainKey);
        if (string.IsNullOrEmpty(roadTerrainKey))
        {
            roadTerrainKey = GridTerrainKeys.Road;
        }
    }

    private void OnValidate()
    {
        NormalizeConfiguration();
    }

    private static int GetCheapestOpenNodeIndex(IReadOnlyList<PathNode> open)
    {
        var cheapestIndex = 0;
        var cheapestCost = open[0].ScaledCost;

        for (var i = 1; i < open.Count; i++)
        {
            if (open[i].ScaledCost >= cheapestCost)
            {
                continue;
            }

            cheapestIndex = i;
            cheapestCost = open[i].ScaledCost;
        }

        return cheapestIndex;
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

    private IReadOnlyList<BuildingRuntimeStatus> CreateRuntimeStatuses()
    {
        List<BuildingRuntimeStatus> statuses = null;

        AddRuntimeStatus(ref statuses, isAbandoned
            ? new BuildingRuntimeStatus(StatusAbandoned, "荒废")
            : default);

        AddRuntimeStatus(ref statuses, lastTurnPopulationDecayed
            ? new BuildingRuntimeStatus(
                StatusPopulationDecayed,
                "人口衰减",
                currentPopulation,
                maxPopulationContribution,
                "居民房人口衰减！")
            : default);

        AddRuntimeStatus(ref statuses, consecutiveConsumptionFailures > 0
            ? new BuildingRuntimeStatus(
                StatusConsumptionFailed,
                "消耗失败",
                consecutiveConsumptionFailures,
                consumptionFailureDecayThreshold)
            : default);

        AddRuntimeStatus(ref statuses, ShouldAddLastAbnormalStatus()
            ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
            : default);

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

        if (lastTurnPopulationDecayed && string.Equals(lastAbnormalStatusId, StatusPopulationDecayed, StringComparison.Ordinal))
        {
            return false;
        }

        return consecutiveConsumptionFailures <= 0
               || !string.Equals(lastAbnormalStatusId, StatusConsumptionFailed, StringComparison.Ordinal);
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

    private readonly struct PathNode
    {
        public PathNode(GridPosition position, int scaledCost)
        {
            Position = position;
            ScaledCost = scaledCost;
        }

        public GridPosition Position { get; }
        public int ScaledCost { get; }
    }
}
