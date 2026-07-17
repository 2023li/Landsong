using System;
using System.Collections.Generic;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    [BuildingModuleId("residential.operation")]
    public sealed class BM_居民运营 : BuildingModuleBase,
        IBuildingPopulationSource,
        IBuildingResourceConsumptionSource,
        IBuildingTaxSource,
        IBuildingConnectionConsumerModule,
        IBuildingModuleStateSerializer,
        IBuildingModuleInitialized,
        IBuildingModuleRegistered,
        IBuildingModuleConstructionCompleted,
        IBuildingModuleLevelApplied,
        IBuildingModuleUnregistered,
        IBuildingModuleDemolished,
        IBuildingAutomaticTurnModule
    {
        private const string StatusAbandoned = "housing_abandoned";
        private const string StatusMissingResourceProvider = BuildingRuntimeStatusCatalog.BS_无法连接资源点;
        private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
        private const string StatusMissingFood = "housing_food_missing";
        private const string StatusInvalidFoodItem = "housing_invalid_food";
        private const string StatusInvalidTaxItem = "housing_invalid_tax";
        private const string StatusTaxRewardFailed = "housing_tax_store_failed";

        [Serializable]
        private sealed class ResidentialState
        {
            public int CurrentPopulation;
            public int GrowthProgress;
            public int TaxProgress;
            public int ConsecutiveFailures;
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
            public float CurrentLifeQuality;
            public float TargetLifeQuality;
            public float LastDietScore;
            public int LastDietDistinctItemCount;
        }

        [TitleGroup("人口")]
        [SerializeField, LabelText("初始人口"), Min(1)] private int initialPopulation = 2;
        [SerializeField, LabelText("最大人口"), Min(1)] private int maxPopulation = 5;

        [TitleGroup("消耗与增长")]
        [SerializeField, AssetsOnly, LabelText("食物物品")] private ItemDefinition foodItemDefinition;
        [SerializeField, AssetsOnly, LabelText("食物分类")] private ItemGroupDefinition foodItemGroup;
        [SerializeField, LabelText("饮食选择策略")]
        private ItemRequirementSelectionPolicy foodSelectionPolicy =
            ItemRequirementSelectionPolicy.PreferVariety;
        [SerializeField, LabelText("目标饮食种类"), Min(1)] private int targetDietVariety = 2;
        [SerializeField, LabelText("增长间隔回合"), Min(1)] private int growthIntervalTurns = 3;
        [SerializeField, LabelText("失败衰减阈值"), Min(1)] private int failureDecayThreshold = 3;
        [SerializeField, LabelText("每回合生活质量最大变化"), Min(0f)]
        private float maxLifeQualityChangePerTurn = 10f;
        [SerializeField, LabelText("高质量增长阈值"), Range(0f, 100f)]
        private float highQualityGrowthThreshold = 80f;

        [TitleGroup("税收")]
        [SerializeField, AssetsOnly, LabelText("税收物品")] private ItemDefinition taxItemDefinition;
        [SerializeField, LabelText("税收间隔回合"), Min(1)] private int taxIntervalTurns = 5;

        [TitleGroup("运行时")]
        [SerializeField, ReadOnly, LabelText("当前人口")] private int currentPopulation;
        [SerializeField, ReadOnly, LabelText("增长进度")] private int growthProgress;
        [SerializeField, ReadOnly, LabelText("税收进度")] private int taxProgress;
        [SerializeField, ReadOnly, LabelText("连续失败次数")] private int consecutiveFailures;
        [SerializeField, ReadOnly, LabelText("已荒废")] private bool isAbandoned;
        [SerializeField, ReadOnly, LabelText("上回合存在资源提供点")] private bool lastTurnHadResourceProvider;
        [SerializeField, ReadOnly, LabelText("上回合消耗失败")] private bool lastTurnConsumptionFailed;
        [SerializeField, ReadOnly, LabelText("上回合已消耗资源")] private bool lastTurnConsumedResources;
        [SerializeField, ReadOnly, LabelText("上回合人口衰减")] private bool lastTurnPopulationDecayed;
        [SerializeField, ReadOnly, LabelText("上回合人口增长")] private bool lastTurnGrewPopulation;
        [SerializeField, ReadOnly, LabelText("上回合已提供税收")] private bool lastTurnProvidedTax;
        [SerializeField, ReadOnly, LabelText("上次资源提供点行动力消耗")] private int lastResourceProviderActionCost;
        [SerializeField, ReadOnly, LabelText("当前生活质量"), Range(0f, 100f)]
        private float currentLifeQuality = 50f;
        [SerializeField, ReadOnly, LabelText("目标生活质量"), Range(0f, 100f)]
        private float targetLifeQuality = 50f;
        [SerializeField, ReadOnly, LabelText("上回合饮食评分"), Range(0f, 100f)]
        private float lastDietScore;
        [SerializeField, ReadOnly, LabelText("上回合饮食种类")]
        private int lastDietDistinctItemCount;

        private string lastAbnormalStatusId = string.Empty;
        private string lastAbnormalStatusText = string.Empty;
        private IReadOnlyList<BuildingResourceChange> lastResourceConsumptions = Array.Empty<BuildingResourceChange>();
        private IReadOnlyList<BuildingResourceChange> lastTaxRewards = Array.Empty<BuildingResourceChange>();
        [NonSerialized] private BuildingBase owner;

        private static readonly IReadOnlyList<string> RequiredConnections =
            new[] { BuildingConnectionTypes.Resource };

        public override string ModuleDescription => "完整持有居民人口、食物消耗、增长、税收、失败衰减、荒废、UI与存档。";
        public int CurrentPopulation => owner != null && owner.IsOperational && !isAbandoned ? currentPopulation : 0;
        public ItemDefinition FoodItemDefinition => foodItemDefinition;
        public ItemGroupDefinition FoodItemGroup => foodItemGroup;
        public ItemDefinition TaxItemDefinition => taxItemDefinition;
        public int MaximumPopulation => Mathf.Max(1, maxPopulation);
        public int GrowthProgress => Mathf.Clamp(growthProgress, 0, GrowthIntervalTurns);
        public int GrowthIntervalTurns => Mathf.Max(1, growthIntervalTurns);
        public int TaxProgress => Mathf.Clamp(taxProgress, 0, TaxIntervalTurns);
        public int TaxIntervalTurns => Mathf.Max(1, taxIntervalTurns);
        public int ConsecutiveFailures => Mathf.Max(0, consecutiveFailures);
        public int FailureDecayThreshold => Mathf.Max(1, failureDecayThreshold);
        public bool IsAbandoned => isAbandoned;
        public float MaxLifeQualityChangePerTurn => Mathf.Max(0f, maxLifeQualityChangePerTurn);
        public float HighQualityGrowthThreshold => Mathf.Clamp(highQualityGrowthThreshold, 0f, 100f);
        public float CurrentLifeQuality => Mathf.Clamp(currentLifeQuality, 0f, 100f);
        public float TargetLifeQuality => Mathf.Clamp(targetLifeQuality, 0f, 100f);
        public float LastDietScore => Mathf.Clamp(lastDietScore, 0f, 100f);
        public int LastDietDistinctItemCount => Mathf.Max(0, lastDietDistinctItemCount);
        private string FoodItemId => foodItemDefinition == null ? string.Empty : foodItemDefinition.ItemId;
        private string FoodRequirementId => foodItemGroup == null ? FoodItemId : foodItemGroup.GroupId;
        private string TaxItemId => taxItemDefinition == null ? string.Empty : taxItemDefinition.ItemId;
        public IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions =>
            isAbandoned || currentPopulation <= 0
                ? Array.Empty<BuildingResourceChange>()
                : OneChange(FoodRequirementId, currentPopulation);
        public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions => lastResourceConsumptions;
        public IReadOnlyList<BuildingResourceChange> CurrentTaxRewards =>
            !isAbandoned && currentPopulation >= maxPopulation
                ? OneChange(TaxItemId, currentPopulation)
                : Array.Empty<BuildingResourceChange>();
        public IReadOnlyList<BuildingResourceChange> LastTaxRewards => lastTaxRewards;
        public IReadOnlyList<string> RequiredConnectionTypeIds => RequiredConnections;

        public bool TryForecastFoodConsumption(
            Inventory inventory,
            out ItemConsumptionReceipt receipt)
        {
            return TryForecastFoodConsumption(inventory, currentPopulation, out receipt);
        }

        public bool TryForecastFoodConsumption(
            Inventory inventory,
            int population,
            out ItemConsumptionReceipt receipt)
        {
            receipt = null;
            population = Mathf.Max(0, population);
            if (inventory == null || isAbandoned || population <= 0)
            {
                return true;
            }

            if (foodItemGroup == null && string.IsNullOrWhiteSpace(FoodItemId))
            {
                return false;
            }

            return inventory.TryConsumeRequirements(
                new[] { CreateFoodRequirement(population) },
                out receipt);
        }

        public float CalculateDietScore(
            ItemConsumptionReceipt receipt,
            ItemCatalog catalog,
            int requiredAmount)
        {
            if (requiredAmount <= 0 || receipt == null)
            {
                return requiredAmount <= 0 ? 100f : 0f;
            }

            var satietyScore = Mathf.Clamp01((float)receipt.TotalConsumed / requiredAmount) * 100f;
            var varietyScore = Mathf.Clamp01(
                (float)receipt.DistinctItemCount / Mathf.Max(1, targetDietVariety)) * 100f;
            var qualityTotal = 0f;
            var qualityWeight = 0;
            for (var i = 0; i < receipt.Lines.Count; i++)
            {
                var line = receipt.Lines[i];
                if (catalog == null
                    || !catalog.TryGetDefinition(line.ItemId, out var definition)
                    || line.Amount <= 0)
                {
                    continue;
                }

                qualityTotal += definition.FoodProfile.DietQuality * line.Amount;
                qualityWeight += line.Amount;
            }

            var qualityScore = qualityWeight <= 0 ? 50f : qualityTotal / qualityWeight;
            return Mathf.Clamp(
                satietyScore * 0.5f + varietyScore * 0.3f + qualityScore * 0.2f,
                0f,
                100f);
        }

        public void ApplyConfiguration(
            int initial,
            int maximum,
            ItemDefinition foodItem,
            int growthTurns,
            int decayThreshold,
            ItemDefinition taxItem,
            int taxTurns,
            ItemGroupDefinition foodGroup = null,
            ItemRequirementSelectionPolicy selectionPolicy =
                ItemRequirementSelectionPolicy.PreferVariety,
            int dietVarietyTarget = 2,
            float lifeQualityChangePerTurn = 10f,
            float qualityGrowthThreshold = 80f)
        {
            initialPopulation = Mathf.Max(1, initial);
            maxPopulation = Mathf.Max(initialPopulation, maximum);
            if (currentPopulation > maxPopulation)
            {
                maxPopulation = currentPopulation;
            }

            foodItemDefinition = foodItem;
            foodItemGroup = foodGroup;
            foodSelectionPolicy = selectionPolicy;
            targetDietVariety = dietVarietyTarget;
            growthIntervalTurns = growthTurns;
            failureDecayThreshold = decayThreshold;
            taxItemDefinition = taxItem;
            taxIntervalTurns = taxTurns;
            maxLifeQualityChangePerTurn = lifeQualityChangePerTurn;
            highQualityGrowthThreshold = qualityGrowthThreshold;
            Normalize();
            if (!isAbandoned && currentPopulation <= 0)
            {
                currentPopulation = initialPopulation;
            }

            RefreshPopulationContribution();
        }

        public override void Normalize()
        {
            initialPopulation = Mathf.Max(1, initialPopulation);
            maxPopulation = Mathf.Max(Mathf.Max(initialPopulation, currentPopulation), maxPopulation);
            growthIntervalTurns = Mathf.Max(1, growthIntervalTurns);
            failureDecayThreshold = Mathf.Max(1, failureDecayThreshold);
            taxIntervalTurns = Mathf.Max(1, taxIntervalTurns);
            targetDietVariety = Mathf.Max(1, targetDietVariety);
            maxLifeQualityChangePerTurn = Mathf.Max(0f, maxLifeQualityChangePerTurn);
            highQualityGrowthThreshold = Mathf.Clamp(highQualityGrowthThreshold, 0f, 100f);
            currentLifeQuality = Mathf.Clamp(currentLifeQuality, 0f, 100f);
            targetLifeQuality = Mathf.Clamp(targetLifeQuality, 0f, 100f);
            lastDietScore = Mathf.Clamp(lastDietScore, 0f, 100f);
            lastDietDistinctItemCount = Mathf.Max(0, lastDietDistinctItemCount);
            currentPopulation = Mathf.Clamp(currentPopulation, 0, maxPopulation);
            growthProgress = Mathf.Clamp(growthProgress, 0, growthIntervalTurns);
            taxProgress = Mathf.Clamp(taxProgress, 0, taxIntervalTurns);
            consecutiveFailures = Mathf.Max(0, consecutiveFailures);
        }

        public void OnBuildingInitialized(BuildingBase building)
        {
            owner = building;
            Normalize();
            if (!isAbandoned && currentPopulation <= 0)
            {
                currentPopulation = initialPopulation;
            }
        }

        public void OnBuildingRegistered(BuildingBase building)
        {
            owner = building;
            RefreshPopulationContribution();
        }

        public void OnBuildingConstructionCompleted(BuildingBase building)
        {
            owner = building;
            RefreshPopulationContribution();
        }

        public void OnBuildingLevelApplied(BuildingBase building, int previousLevel, int currentLevel)
        {
            owner = building;
            Normalize();
            RefreshPopulationContribution();
        }

        public void OnBuildingUnregistered(BuildingBase building) => RemovePopulationContribution(building);
        public void OnBuildingDemolished(BuildingBase building) => RemovePopulationContribution(building);

        public bool ProcessAutomaticTurn(BuildingBase building)
        {
            owner = building;
            Normalize();
            ClearLastTurnState();

            if (isAbandoned || currentPopulation <= 0)
            {
                EnterAbandonedState();
                SetStatus(StatusAbandoned, "建筑荒废");
                return false;
            }

            var inventory = building?.GameSystem?.Services?.Inventory;
            if (inventory == null)
            {
                return RegisterConsumptionFailure(StatusMissingInventory, "库存服务缺失");
            }

            if (foodItemGroup == null && string.IsNullOrWhiteSpace(FoodItemId))
            {
                return RegisterConsumptionFailure(StatusInvalidFoodItem, "食物配置异常");
            }

            if (!BuildingResourceProviderSystem.TrySelectProvider(building, out var providerSelection))
            {
                return RegisterConsumptionFailure(StatusMissingResourceProvider, "无法连接资源");
            }

            lastTurnHadResourceProvider = true;
            lastResourceProviderActionCost = providerSelection.ActionCost;
            var foodAmount = Mathf.Max(0, currentPopulation);
            var foodRequirement = CreateFoodRequirement(foodAmount);
            ItemConsumptionReceipt consumptionReceipt = null;
            if (foodAmount > 0
                && !inventory.TryConsumeRequirements(
                    new[] { foodRequirement },
                    out consumptionReceipt))
            {
                return RegisterConsumptionFailure(StatusMissingFood, $"{FoodRequirementId}不足");
            }

            consecutiveFailures = 0;
            ClearStatus();
            lastTurnConsumedResources = foodAmount > 0;
            lastResourceConsumptions = ToResourceChanges(consumptionReceipt);
            UpdateDietAndLifeQuality(consumptionReceipt, inventory.ItemCatalog, foodAmount);
            for (var i = 0; i < lastResourceConsumptions.Count; i++)
            {
                BuildingResourceProviderSystem.RecordProvidedResource(
                    providerSelection,
                    building,
                    lastResourceConsumptions[i]);
            }

            return ProcessSuccessfulConsumption(inventory);
        }

        public override string GetOverviewFragment(BuildingBase building)
        {
            return isAbandoned ? "已荒废" : $"人口 {currentPopulation}/{maxPopulation}";
        }

        public override void AppendRuntimeStatuses(
            BuildingBase building,
            ref List<BuildingRuntimeStatus> statuses)
        {
            AddRuntimeStatus(
                ref statuses,
                isAbandoned
                    ? new BuildingRuntimeStatus(StatusAbandoned, "建筑荒废")
                    : default);
            AddRuntimeStatus(
                ref statuses,
                consecutiveFailures > 0
                    ? new BuildingRuntimeStatus(
                        lastAbnormalStatusId,
                        lastAbnormalStatusText,
                        consecutiveFailures,
                        failureDecayThreshold)
                    : default);
            AddRuntimeStatus(
                ref statuses,
                consecutiveFailures <= 0 && !string.IsNullOrWhiteSpace(lastAbnormalStatusId)
                    ? new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText)
                    : default);
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!isAbandoned && currentPopulation > 0)
            {
                AddFunctionBlockEntry(
                    ref entries,
                    new BuildingFunctionBlockEntry(
                        BuildingFunctionBlockGroup.资源组,
                        FoodRequirementId,
                        -currentPopulation,
                        new[]
                        {
                            new BuildingFunctionBlockSidebarRow("当前人口", currentPopulation.ToString()),
                            new BuildingFunctionBlockSidebarRow("增长进度", $"{growthProgress}/{growthIntervalTurns}"),
                            new BuildingFunctionBlockSidebarRow("资源路径行动力", lastResourceProviderActionCost.ToString()),
                            new BuildingFunctionBlockSidebarRow("饮食评分", $"{LastDietScore:0.#}"),
                            new BuildingFunctionBlockSidebarRow("饮食种类", $"{LastDietDistinctItemCount}/{targetDietVariety}")
                        }));
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "人口",
                    CurrentPopulation,
                    new[]
                    {
                        new BuildingFunctionBlockSidebarRow("人口上限", maxPopulation.ToString()),
                        new BuildingFunctionBlockSidebarRow("税收进度", $"{taxProgress}/{taxIntervalTurns}"),
                        new BuildingFunctionBlockSidebarRow("失败衰减", $"{consecutiveFailures}/{failureDecayThreshold}"),
                        new BuildingFunctionBlockSidebarRow("生活质量", $"{CurrentLifeQuality:0.#}/100")
                    }));
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new ResidentialState
            {
                CurrentPopulation = currentPopulation,
                GrowthProgress = growthProgress,
                TaxProgress = taxProgress,
                ConsecutiveFailures = consecutiveFailures,
                IsAbandoned = isAbandoned,
                LastTurnHadResourceProvider = lastTurnHadResourceProvider,
                LastTurnConsumptionFailed = lastTurnConsumptionFailed,
                LastTurnConsumedResources = lastTurnConsumedResources,
                LastTurnPopulationDecayed = lastTurnPopulationDecayed,
                LastTurnGrewPopulation = lastTurnGrewPopulation,
                LastTurnProvidedTax = lastTurnProvidedTax,
                LastResourceProviderActionCost = lastResourceProviderActionCost,
                LastAbnormalStatusId = lastAbnormalStatusId,
                LastAbnormalStatusText = lastAbnormalStatusText,
                CurrentLifeQuality = currentLifeQuality,
                TargetLifeQuality = targetLifeQuality,
                LastDietScore = lastDietScore,
                LastDietDistinctItemCount = lastDietDistinctItemCount
            });
            return true;
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json) ? null : JsonUtility.FromJson<ResidentialState>(json);
            if (state == null)
            {
                return;
            }

            currentPopulation = state.CurrentPopulation;
            growthProgress = state.GrowthProgress;
            taxProgress = state.TaxProgress;
            consecutiveFailures = state.ConsecutiveFailures;
            isAbandoned = state.IsAbandoned;
            lastTurnHadResourceProvider = state.LastTurnHadResourceProvider;
            lastTurnConsumptionFailed = state.LastTurnConsumptionFailed;
            lastTurnConsumedResources = state.LastTurnConsumedResources;
            lastTurnPopulationDecayed = state.LastTurnPopulationDecayed;
            lastTurnGrewPopulation = state.LastTurnGrewPopulation;
            lastTurnProvidedTax = state.LastTurnProvidedTax;
            lastResourceProviderActionCost = state.LastResourceProviderActionCost;
            lastAbnormalStatusId = NormalizeText(state.LastAbnormalStatusId);
            lastAbnormalStatusText = NormalizeText(state.LastAbnormalStatusText);
            currentLifeQuality = state.CurrentLifeQuality;
            targetLifeQuality = state.TargetLifeQuality;
            lastDietScore = state.LastDietScore;
            lastDietDistinctItemCount = state.LastDietDistinctItemCount;
            Normalize();
            RefreshPopulationContribution();
        }

        private bool ProcessSuccessfulConsumption(InventoryService inventory)
        {
            if (currentPopulation >= maxPopulation)
            {
                growthProgress = 0;
                taxProgress++;
                return taxProgress < taxIntervalTurns || TryProvideTax(inventory);
            }

            taxProgress = 0;
            growthProgress += CurrentLifeQuality >= highQualityGrowthThreshold ? 2 : 1;
            if (growthProgress < growthIntervalTurns)
            {
                return true;
            }

            growthProgress = 0;
            var previous = currentPopulation;
            currentPopulation = Mathf.Min(maxPopulation, currentPopulation + 1);
            lastTurnGrewPopulation = currentPopulation > previous;
            if (lastTurnGrewPopulation)
            {
                RefreshPopulationContribution();
            }

            return true;
        }

        private bool TryProvideTax(InventoryService inventory)
        {
            if (string.IsNullOrWhiteSpace(TaxItemId))
            {
                taxProgress = taxIntervalTurns;
                SetStatus(StatusInvalidTaxItem, "税收配置异常");
                return false;
            }

            var amount = Mathf.Max(0, currentPopulation);
            if (amount <= 0)
            {
                taxProgress = 0;
                return true;
            }

            if (!inventory.TryAddItem(TaxItemId, amount))
            {
                taxProgress = taxIntervalTurns;
                SetStatus(StatusTaxRewardFailed, "税收存入失败");
                return false;
            }

            taxProgress = 0;
            lastTurnProvidedTax = true;
            lastTaxRewards = OneChange(TaxItemId, amount);
            return true;
        }

        private bool RegisterConsumptionFailure(string statusId, string statusText)
        {
            lastTurnConsumptionFailed = true;
            SetStatus(statusId, statusText);
            lastDietScore = 0f;
            lastDietDistinctItemCount = 0;
            UpdateLifeQuality(0f);
            growthProgress = 0;
            taxProgress = 0;
            consecutiveFailures++;
            if (consecutiveFailures >= failureDecayThreshold)
            {
                DecayPopulation();
            }

            return false;
        }

        private void DecayPopulation()
        {
            consecutiveFailures = 0;
            var previous = currentPopulation;
            currentPopulation = Mathf.Max(0, currentPopulation - 1);
            lastTurnPopulationDecayed = currentPopulation < previous;
            if (lastTurnPopulationDecayed)
            {
                RefreshPopulationContribution();
                owner?.SendBuildingEvent(GameEventCatalog.GE_人口衰减, "居民房人口衰减！");
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
            growthProgress = 0;
            taxProgress = 0;
            RefreshPopulationContribution();
        }

        private void RefreshPopulationContribution()
        {
            if (owner == null || !owner.IsRegistered)
            {
                return;
            }

            owner.GameSystem?.Dynasty?.SetPopulationContribution(owner, CurrentPopulation);
        }

        private void RemovePopulationContribution(BuildingBase building)
        {
            var target = building == null ? owner : building;
            target?.GameSystem?.Dynasty?.RemovePopulationContribution(target);
        }

        private void ClearLastTurnState()
        {
            lastTurnHadResourceProvider = false;
            lastTurnConsumptionFailed = false;
            lastTurnConsumedResources = false;
            lastTurnPopulationDecayed = false;
            lastTurnGrewPopulation = false;
            lastTurnProvidedTax = false;
            lastResourceConsumptions = Array.Empty<BuildingResourceChange>();
            lastTaxRewards = Array.Empty<BuildingResourceChange>();
            lastResourceProviderActionCost = 0;
            lastDietDistinctItemCount = 0;
        }

        private void SetStatus(string id, string text)
        {
            lastAbnormalStatusId = NormalizeText(id);
            lastAbnormalStatusText = string.IsNullOrWhiteSpace(text) ? lastAbnormalStatusId : text.Trim();
        }

        private void ClearStatus()
        {
            lastAbnormalStatusId = string.Empty;
            lastAbnormalStatusText = string.Empty;
        }

        private static IReadOnlyList<BuildingResourceChange> OneChange(string itemId, int amount)
        {
            var change = new BuildingResourceChange(itemId, amount);
            return change.IsValid ? new[] { change } : Array.Empty<BuildingResourceChange>();
        }

        private static string NormalizeText(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private ItemRequirement CreateFoodRequirement(int amount)
        {
            return foodItemGroup != null
                ? new ItemRequirement(foodItemGroup, amount, foodSelectionPolicy)
                : new ItemRequirement(foodItemDefinition, amount, foodSelectionPolicy);
        }

        private void UpdateDietAndLifeQuality(
            ItemConsumptionReceipt receipt,
            ItemCatalog catalog,
            int requiredAmount)
        {
            if (requiredAmount <= 0 || receipt == null)
            {
                lastDietScore = 100f;
                lastDietDistinctItemCount = 0;
                UpdateLifeQuality(lastDietScore);
                return;
            }

            lastDietDistinctItemCount = receipt.DistinctItemCount;
            lastDietScore = CalculateDietScore(receipt, catalog, requiredAmount);
            UpdateLifeQuality(lastDietScore);
        }

        private void UpdateLifeQuality(float newTarget)
        {
            targetLifeQuality = Mathf.Clamp(newTarget, 0f, 100f);
            currentLifeQuality = Mathf.MoveTowards(
                currentLifeQuality,
                targetLifeQuality,
                maxLifeQualityChangePerTurn);
        }

        private static IReadOnlyList<BuildingResourceChange> ToResourceChanges(
            ItemConsumptionReceipt receipt)
        {
            if (receipt == null || receipt.Lines.Count == 0)
            {
                return Array.Empty<BuildingResourceChange>();
            }

            var amounts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < receipt.Lines.Count; i++)
            {
                var line = receipt.Lines[i];
                if (!line.IsValid)
                {
                    continue;
                }

                amounts.TryGetValue(line.ItemId, out var current);
                amounts[line.ItemId] = current + line.Amount;
            }

            var result = new List<BuildingResourceChange>(amounts.Count);
            foreach (var pair in amounts)
            {
                result.Add(new BuildingResourceChange(pair.Key, pair.Value));
            }

            return result;
        }
    }
}
