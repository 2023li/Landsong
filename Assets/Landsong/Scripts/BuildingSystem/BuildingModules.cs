using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class BuildingModuleIdAttribute : Attribute
    {
        public BuildingModuleIdAttribute(string id)
        {
            Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        public string Id { get; }
    }

    public interface IBuildingModuleStateSerializer
    {
        bool TryCaptureState(out string json);
        void RestoreState(string json);
    }

    public interface IBuildingExpeditionRewardYieldSource
    {
        float ExpeditionRewardYieldBonus { get; }
    }

    [Serializable]
    public abstract class BuildingModuleBase
    {
        [ShowInInspector, PropertyOrder(-100), LabelText("模块作用"), DisplayAsString]
        private string InspectorModuleDescription => ModuleDescription;

        [SerializeField, LabelText("启用")] private bool enabled = true;

        public bool IsEnabled => enabled;
        public string ModuleId =>
            (Attribute.GetCustomAttribute(GetType(), typeof(BuildingModuleIdAttribute)) as BuildingModuleIdAttribute)?.Id
            ?? string.Empty;
        public virtual string ModuleDescription => "未配置模块作用说明。";

        public override string ToString()
        {
            return GetType().Name;
        }

        public virtual void Normalize()
        {
        }

        public virtual void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
        }

        /// <summary>
        /// 返回用于建筑列表的一段简短概览。多个启用模块的文本由统一运行时按顺序组合。
        /// </summary>
        public virtual string GetOverviewFragment(BuildingBase building)
        {
            return string.Empty;
        }

        /// <summary>
        /// 向统一运行时追加本模块产生的异常或进度状态。
        /// </summary>
        public virtual void AppendRuntimeStatuses(
            BuildingBase building,
            ref List<BuildingRuntimeStatus> statuses)
        {
        }

        protected static void AddFunctionBlockEntry(
            ref List<BuildingFunctionBlockEntry> entries,
            BuildingFunctionBlockEntry entry)
        {
            if (!entry.IsValid)
            {
                return;
            }

            entries ??= new List<BuildingFunctionBlockEntry>();
            entries.Add(entry);
        }

        protected static void AddRuntimeStatus(
            ref List<BuildingRuntimeStatus> statuses,
            BuildingRuntimeStatus status)
        {
            if (!status.IsValid)
            {
                return;
            }

            statuses ??= new List<BuildingRuntimeStatus>();
            for (var i = 0; i < statuses.Count; i++)
            {
                if (string.Equals(statuses[i].StatusId, status.StatusId, StringComparison.Ordinal))
                {
                    return;
                }
            }

            statuses.Add(status);
        }
    }

    [Serializable]
    public struct WorkerProductionTier
    {
        [SerializeField, LabelText("工人"), Min(1)]
        private int 工人;

        [SerializeField, LabelText("产量"), Min(0)]
        private int 产量;

        public WorkerProductionTier(int minimumWorkers, int amount)
        {
            工人 = minimumWorkers;
            产量 = amount;
        }

        public int MinimumWorkers => Mathf.Max(1, 工人);
        public int Amount => Mathf.Max(0, 产量);
        public bool IsValid => Amount > 0;

        public WorkerProductionTier Normalize(int maxWorkers)
        {
            return new WorkerProductionTier(
                Mathf.Clamp(工人, 1, Mathf.Max(1, maxWorkers)),
                Mathf.Max(0, 产量));
        }
    }

    public readonly struct BuildingResourceProductionResult
    {
        public BuildingResourceProductionResult(
            bool succeeded,
            bool producedResources,
            string failureStatusId = "",
            string failureStatusText = "")
        {
            Succeeded = succeeded;
            ProducedResources = producedResources;
            FailureStatusId = string.IsNullOrWhiteSpace(failureStatusId) ? string.Empty : failureStatusId.Trim();
            FailureStatusText = string.IsNullOrWhiteSpace(failureStatusText)
                ? FailureStatusId
                : failureStatusText.Trim();
        }

        public bool Succeeded { get; }
        public bool ProducedResources { get; }
        public string FailureStatusId { get; }
        public string FailureStatusText { get; }
    }

    [Serializable]
    [BuildingModuleId("production")]
    public sealed class BM_资源产出 : BuildingModuleBase,
        IBuildingResourceProductionSource,
        IBuildingModuleStateSerializer,
        IBuildingAutomaticTurnModule
    {
        private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
        private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
        private const string StatusInvalidProductionItem = "invalid_production_item";
        private const string StatusProductionStorageFailed = "production_storage_failed";

        [Serializable]
        private sealed class ProductionState
        {
            public int Progress;
            public string[] LastItemIds = Array.Empty<string>();
            public int[] LastAmounts = Array.Empty<int>();
        }

        [Serializable]
        private struct ResourceProductionOutput
        {
            [SerializeField, AssetsOnly, LabelText("产出物品")]
            private ItemDefinition itemDefinition;

            [SerializeField, LabelText("工人数产量表")]
            private WorkerProductionTier[] workerProductionTiers;

            public ResourceProductionOutput(ItemDefinition itemDefinition, WorkerProductionTier[] workerProductionTiers)
            {
                this.itemDefinition = itemDefinition;
                this.workerProductionTiers = workerProductionTiers ?? Array.Empty<WorkerProductionTier>();
            }

            public ItemDefinition ItemDefinition => itemDefinition;
            public string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;
            public bool HasUsableItemDefinition => itemDefinition != null && !string.IsNullOrWhiteSpace(ItemId);

            public ResourceProductionOutput Normalize(int maxWorkers)
            {
                if (workerProductionTiers == null)
                {
                    workerProductionTiers = Array.Empty<WorkerProductionTier>();
                }

                for (var i = 0; i < workerProductionTiers.Length; i++)
                {
                    workerProductionTiers[i] = workerProductionTiers[i].Normalize(maxWorkers);
                }

                return this;
            }

            public int GetMinimumWorkersForProduction(int maxWorkers)
            {
                if (workerProductionTiers == null || workerProductionTiers.Length == 0)
                {
                    return Mathf.Max(1, maxWorkers);
                }

                var minimumWorkers = Mathf.Max(1, maxWorkers);
                var hasValidTier = false;
                for (var i = 0; i < workerProductionTiers.Length; i++)
                {
                    var tier = workerProductionTiers[i].Normalize(maxWorkers);
                    if (!tier.IsValid)
                    {
                        continue;
                    }

                    minimumWorkers = Mathf.Min(minimumWorkers, tier.MinimumWorkers);
                    hasValidTier = true;
                }

                return hasValidTier ? minimumWorkers : Mathf.Max(1, maxWorkers);
            }

            public int GetAmountForWorkers(int workers, int maxWorkers)
            {
                if (workerProductionTiers == null || workerProductionTiers.Length == 0)
                {
                    return 0;
                }

                var selectedMinimumWorkers = 0;
                var amount = 0;
                workers = Mathf.Max(0, workers);
                for (var i = 0; i < workerProductionTiers.Length; i++)
                {
                    var tier = workerProductionTiers[i].Normalize(maxWorkers);
                    if (!tier.IsValid || workers < tier.MinimumWorkers)
                    {
                        continue;
                    }

                    if (tier.MinimumWorkers < selectedMinimumWorkers)
                    {
                        continue;
                    }

                    if (tier.MinimumWorkers == selectedMinimumWorkers && tier.Amount <= amount)
                    {
                        continue;
                    }

                    selectedMinimumWorkers = tier.MinimumWorkers;
                    amount = tier.Amount;
                }

                return amount;
            }

            public string FormatProductionTiers(int maxWorkers)
            {
                if (workerProductionTiers == null || workerProductionTiers.Length == 0)
                {
                    return "无";
                }

                var sortedTiers = new List<WorkerProductionTier>();
                for (var i = 0; i < workerProductionTiers.Length; i++)
                {
                    var tier = workerProductionTiers[i].Normalize(maxWorkers);
                    if (tier.IsValid)
                    {
                        sortedTiers.Add(tier);
                    }
                }

                sortedTiers.Sort(CompareWorkerProductionTiers);
                if (sortedTiers.Count == 0)
                {
                    return "无";
                }

                var builder = new System.Text.StringBuilder();
                for (var i = 0; i < sortedTiers.Count; i++)
                {
                    var tier = sortedTiers[i];
                    if (builder.Length > 0)
                    {
                        builder.Append("，");
                    }

                    builder.Append(tier.MinimumWorkers);
                    builder.Append("工人=");
                    builder.Append(tier.Amount);
                    builder.Append(ItemId);
                }

                return builder.Length == 0 ? "无" : builder.ToString();
            }

            private static int CompareWorkerProductionTiers(WorkerProductionTier a, WorkerProductionTier b)
            {
                return a.MinimumWorkers == b.MinimumWorkers
                    ? a.Amount.CompareTo(b.Amount)
                    : a.MinimumWorkers.CompareTo(b.MinimumWorkers);
            }
        }

        [SerializeField, LabelText("生产周期回合"), Min(1)]
        private int productionIntervalTurns = 1;

        [SerializeField, LabelText("产出资源")]
        private ResourceProductionOutput[] outputs = Array.Empty<ResourceProductionOutput>();

        [SerializeField, ReadOnly, LabelText("生产进度")]
        private int productionProgress;

        private IReadOnlyList<BuildingResourceChange> currentResourceProductions =
            Array.Empty<BuildingResourceChange>();

        private IReadOnlyList<BuildingResourceChange> lastResourceProductions =
            Array.Empty<BuildingResourceChange>();

        public override string ModuleDescription => "按当前工人数和产量表生产一种或多种资源，并维护生产周期、进度和上次产出。";
        public int ProductionIntervalTurns => Mathf.Max(1, productionIntervalTurns);
        public int ProductionProgress => Mathf.Clamp(productionProgress, 0, ProductionIntervalTurns);
        public IReadOnlyList<BuildingResourceChange> CurrentResourceProductions =>
            currentResourceProductions ?? Array.Empty<BuildingResourceChange>();
        public IReadOnlyList<BuildingResourceChange> LastResourceProductions =>
            lastResourceProductions ?? Array.Empty<BuildingResourceChange>();
        public bool HasOnlyValidConfiguredItemDefinitions
        {
            get
            {
                if (outputs == null)
                {
                    return true;
                }

                for (var i = 0; i < outputs.Length; i++)
                {
                    if (!outputs[i].HasUsableItemDefinition)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void ApplyConfiguration(
            int intervalTurns,
            IReadOnlyList<BuildingProductionOutputConfiguration> configurations)
        {
            productionIntervalTurns = Mathf.Max(1, intervalTurns);
            if (configurations == null || configurations.Count == 0)
            {
                outputs = Array.Empty<ResourceProductionOutput>();
                Normalize();
                return;
            }

            outputs = new ResourceProductionOutput[configurations.Count];
            for (var i = 0; i < configurations.Count; i++)
            {
                var configuration = configurations[i];
                var tiers = configuration.WorkerProductionTiers;
                var copiedTiers = new WorkerProductionTier[tiers.Count];
                for (var tierIndex = 0; tierIndex < tiers.Count; tierIndex++)
                {
                    copiedTiers[tierIndex] = tiers[tierIndex];
                }

                outputs[i] = new ResourceProductionOutput(configuration.ItemDefinition, copiedTiers);
            }

            Normalize();
        }

        public void EnsureSingleOutput(
            ItemDefinition itemDefinition,
            int defaultProductionIntervalTurns,
            WorkerProductionTier[] defaultWorkerProductionTiers)
        {
            if (outputs != null && outputs.Length > 0)
            {
                Normalize();
                return;
            }

            productionIntervalTurns = Mathf.Max(1, defaultProductionIntervalTurns);
            outputs = new[]
            {
                new ResourceProductionOutput(itemDefinition, defaultWorkerProductionTiers)
            };
            Normalize();
        }

        public override void Normalize()
        {
            productionIntervalTurns = Mathf.Max(1, productionIntervalTurns);
            productionProgress = Mathf.Clamp(productionProgress, 0, productionIntervalTurns);

            if (outputs == null)
            {
                outputs = Array.Empty<ResourceProductionOutput>();
                return;
            }

            for (var i = 0; i < outputs.Length; i++)
            {
                outputs[i] = outputs[i].Normalize(GetFallbackMaxWorkers());
            }
        }

        public void ClearLastResourceProductions()
        {
            lastResourceProductions = Array.Empty<BuildingResourceChange>();
        }

        public void AppendLastResourceProduction(string itemId, int amount)
        {
            var change = new BuildingResourceChange(itemId, amount);
            if (!change.IsValid)
            {
                return;
            }

            if (lastResourceProductions == null || lastResourceProductions.Count == 0)
            {
                lastResourceProductions = new[] { change };
                return;
            }

            var changes = new List<BuildingResourceChange>(lastResourceProductions.Count + 1);
            for (var i = 0; i < lastResourceProductions.Count; i++)
            {
                if (lastResourceProductions[i].IsValid)
                {
                    changes.Add(lastResourceProductions[i]);
                }
            }

            changes.Add(change);
            lastResourceProductions = changes;
        }

        public void RestoreProductionProgress(int value)
        {
            productionProgress = Mathf.Clamp(value, 0, ProductionIntervalTurns);
        }

        public bool TryCaptureState(out string json)
        {
            var count = LastResourceProductions.Count;
            var itemIds = new string[count];
            var amounts = new int[count];
            for (var i = 0; i < count; i++)
            {
                itemIds[i] = LastResourceProductions[i].ItemId;
                amounts[i] = LastResourceProductions[i].Amount;
            }

            json = JsonUtility.ToJson(new ProductionState
            {
                Progress = ProductionProgress,
                LastItemIds = itemIds,
                LastAmounts = amounts
            });
            return !string.IsNullOrWhiteSpace(json);
        }

        public void RestoreState(string json)
        {
            var state = string.IsNullOrWhiteSpace(json)
                ? null
                : JsonUtility.FromJson<ProductionState>(json);
            RestoreProductionProgress(state?.Progress ?? 0);
            ClearLastResourceProductions();
            if (state?.LastItemIds == null || state.LastAmounts == null)
            {
                return;
            }

            var count = Mathf.Min(state.LastItemIds.Length, state.LastAmounts.Length);
            for (var i = 0; i < count; i++)
            {
                AppendLastResourceProduction(state.LastItemIds[i], state.LastAmounts[i]);
            }
        }

        public IReadOnlyList<BuildingResourceChange> PreviewResourceProductions(
            BuildingBase building,
            int workers,
            int maxWorkers)
        {
            currentResourceProductions = CreateResourceProductions(building, workers, maxWorkers);
            return currentResourceProductions;
        }

        /// <summary>
        /// 返回当前工人数对应的周期产出，但不修改运行时模块状态。
        /// </summary>
        public IReadOnlyList<BuildingResourceChange> GetForecastResourceProductions(
            BuildingBase building,
            int workers,
            int maxWorkers) => CreateResourceProductions(building, workers, maxWorkers);

        public int GetMinimumWorkersForProduction(int maxWorkers)
        {
            Normalize();
            if (outputs == null || outputs.Length == 0)
            {
                return Mathf.Max(1, maxWorkers);
            }

            var minimumWorkers = Mathf.Max(1, maxWorkers);
            var hasValidOutput = false;
            for (var i = 0; i < outputs.Length; i++)
            {
                if (!outputs[i].HasUsableItemDefinition)
                {
                    continue;
                }

                minimumWorkers = Mathf.Min(
                    minimumWorkers,
                    outputs[i].GetMinimumWorkersForProduction(maxWorkers));
                hasValidOutput = true;
            }

            return hasValidOutput ? minimumWorkers : Mathf.Max(1, maxWorkers);
        }

        public BuildingResourceProductionResult TryAdvanceProductionCycle(
            BuildingBase building,
            InventoryService inventory,
            int workers,
            int maxWorkers)
        {
            ClearLastResourceProductions();
            var productionChanges = CreateResourceProductions(building, workers, maxWorkers);
            currentResourceProductions = productionChanges;
            if (productionChanges.Count == 0)
            {
                return new BuildingResourceProductionResult(
                    false,
                    false,
                    StatusInsufficientWorkers,
                    "工人不足");
            }

            productionProgress = Mathf.Min(ProductionIntervalTurns, productionProgress + 1);
            if (productionProgress < ProductionIntervalTurns)
            {
                return new BuildingResourceProductionResult(true, false);
            }

            if (inventory == null)
            {
                return new BuildingResourceProductionResult(
                    false,
                    false,
                    StatusMissingInventory,
                    "库存服务缺失");
            }

            for (var i = 0; i < productionChanges.Count; i++)
            {
                var change = productionChanges[i];
                if (!change.IsValid)
                {
                    return new BuildingResourceProductionResult(
                        false,
                        false,
                        StatusInvalidProductionItem,
                        "产出资源配置异常");
                }

                if (!inventory.TryAddItem(change.ItemId, change.Amount))
                {
                    return new BuildingResourceProductionResult(
                        false,
                        false,
                        StatusProductionStorageFailed,
                        "产出资源存入失败");
                }
            }

            productionProgress = 0;
            lastResourceProductions = productionChanges;
            return new BuildingResourceProductionResult(true, true);
        }

        public bool ProcessAutomaticTurn(BuildingBase building)
        {
            if (!TryResolveWorkforce(building, out var workers, out var maxWorkers))
            {
                ClearLastResourceProductions();
                return false;
            }

            return TryAdvanceProductionCycle(
                building,
                building?.GameSystem?.Services?.Inventory,
                workers,
                maxWorkers).Succeeded;
        }

        public override string GetOverviewFragment(BuildingBase building)
        {
            return $"生产 {ProductionProgress}/{ProductionIntervalTurns}";
        }

        public override void AppendRuntimeStatuses(
            BuildingBase building,
            ref List<BuildingRuntimeStatus> statuses)
        {
            if (!TryResolveWorkforce(building, out var workers, out var maxWorkers))
            {
                return;
            }

            var requiredWorkers = GetMinimumWorkersForProduction(maxWorkers);
            AddRuntimeStatus(
                ref statuses,
                workers < requiredWorkers
                    ? new BuildingRuntimeStatus(
                        StatusInsufficientWorkers,
                        "工人不足",
                        workers,
                        requiredWorkers)
                    : default);
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled || !TryResolveWorkforce(building, out var workers, out var maxWorkers))
            {
                return;
            }

            var changes = PreviewResourceProductions(building, workers, maxWorkers);
            if (changes == null)
            {
                return;
            }

            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                if (!change.IsValid)
                {
                    continue;
                }

                AddFunctionBlockEntry(
                    ref entries,
                    new BuildingFunctionBlockEntry(
                        BuildingFunctionBlockGroup.资源组,
                        change.ItemId,
                        change.Amount,
                        BuildProductionSidebarRows(change.ItemId, maxWorkers)));
            }
        }

        private IReadOnlyList<BuildingResourceChange> CreateResourceProductions(
            BuildingBase building,
            int workers,
            int maxWorkers)
        {
            Normalize();
            if (outputs == null || outputs.Length == 0)
            {
                return Array.Empty<BuildingResourceChange>();
            }

            List<BuildingResourceChange> changes = null;
            for (var i = 0; i < outputs.Length; i++)
            {
                var output = outputs[i];
                var amount = output.GetAmountForWorkers(workers, maxWorkers);
                if (amount > 0)
                {
                    amount = checked(amount + Mathf.Max(
                        0,
                        building?.GameSystem?.Services?.GlobalBuffs?
                            .GetBuildingResourceProductionFlatBonus(building, output.ItemId) ?? 0));
                }
                var change = new BuildingResourceChange(output.ItemId, amount);
                if (!change.IsValid)
                {
                    continue;
                }

                changes ??= new List<BuildingResourceChange>();
                changes.Add(change);
            }

            return changes == null ? Array.Empty<BuildingResourceChange>() : changes;
        }

        private IReadOnlyList<BuildingFunctionBlockSidebarRow> BuildProductionSidebarRows(
            string itemId,
            int maxWorkers)
        {
            return new[]
            {
                new BuildingFunctionBlockSidebarRow(
                    "生产周期",
                    $"{ProductionProgress}/{ProductionIntervalTurns}"),
                new BuildingFunctionBlockSidebarRow(
                    "产量规则",
                    FormatProductionTiers(itemId, maxWorkers))
            };
        }

        private string FormatProductionTiers(string itemId, int maxWorkers)
        {
            if (outputs == null || outputs.Length == 0)
            {
                return "无";
            }

            for (var i = 0; i < outputs.Length; i++)
            {
                if (string.Equals(outputs[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return outputs[i].FormatProductionTiers(maxWorkers);
                }
            }

            return "无";
        }

        private static bool TryResolveWorkforce(BuildingBase building, out int workers, out int maxWorkers)
        {
            if (BuildingWorkforceUtility.TryGetSource(building, out var workforce))
            {
                workers = workforce.CurrentWorkers;
                maxWorkers = workforce.MaxWorkers;
                return true;
            }

            workers = 0;
            maxWorkers = 1;
            return false;
        }

        private static int GetFallbackMaxWorkers()
        {
            return int.MaxValue;
        }
    }

    [Serializable]
    [BuildingModuleId("workforce.nearby_population")]
    public sealed class BM_附近人口岗位吸引 : BuildingModuleBase
    {
        [SerializeField, LabelText("人口搜索半径"), Min(0)]
        [PropertyTooltip("单位：格。按曼哈顿距离统计附近人口建筑。")]
        private int populationSearchRadius = 10;

        [SerializeField, LabelText("附近每人口就业吸引力"), Min(0f)]
        [PropertyTooltip("单位：岗位吸引力点/人。附近每 1 人为该建筑提供多少岗位吸引力。")]
        private float attractionPerNearbyPopulation =
            BuildingJobSystem.DefaultAttractionPerNearbyPopulation;

        public override string ModuleDescription => "按建筑周围人口数量为岗位建筑提供就业吸引力加成，用于影响稳定工人数。";
        public int PopulationSearchRadius => Mathf.Max(0, populationSearchRadius);
        public float AttractionPerNearbyPopulation => Mathf.Max(0f, attractionPerNearbyPopulation);

        public override void Normalize()
        {
            populationSearchRadius = Mathf.Max(0, populationSearchRadius);
            attractionPerNearbyPopulation = Mathf.Max(0f, attractionPerNearbyPopulation);
        }

    }

    [Serializable]
    [BuildingModuleId("inventory.capacity")]
    public sealed class BM_库存格容量 : BuildingModuleBase,
        IBuildingInventorySlotProvider,
        IBuildingModuleInitialized,
        IBuildingModuleRegistered,
        IBuildingModuleConstructionCompleted,
        IBuildingModuleLevelApplied,
        IBuildingModuleUnregistered
    {
        [SerializeField, LabelText("提供库存格数"), Min(0)]
        [PropertyTooltip("单位：格。建筑竣工后提供，每格都以建筑实例与本地槽位 ID 标识。")]
        private int providedSlotCount = 5;

        [SerializeField, LabelText("槽位类型")]
        [PropertyTooltip("建筑只声明提供哪种槽位；损耗倍率和物品组修正由库存槽位类型目录统一解析，自动入库始终选择该物品有效损耗率最低的可用槽位。")]
        private InventorySlotType slotType = InventorySlotType.简陋库存;

        [NonSerialized] private BuildingBase owner;

        public override string ModuleDescription => "建筑竣工后提供具有稳定来源与槽位类型的库存格。";
        public int ProvidedSlotCount => Mathf.Max(0, providedSlotCount);

        public void SetProvidedSlotCount(int slotCount)
        {
            providedSlotCount = Mathf.Max(0, slotCount);
            RefreshTopology();
        }

        public void ApplyStorageConfiguration(
            int slotCount,
            InventorySlotType configuredSlotType)
        {
            providedSlotCount = Mathf.Max(0, slotCount);
            slotType = configuredSlotType;
            RefreshTopology();
        }

        public IReadOnlyList<InventorySlotProvision> GetInventorySlotProvisions(BuildingBase building)
        {
            var target = building == null ? owner : building;
            if (!IsEnabled || target == null || !target.IsOperational || ProvidedSlotCount <= 0)
            {
                return Array.Empty<InventorySlotProvision>();
            }

            var result = new InventorySlotProvision[ProvidedSlotCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = new InventorySlotProvision(
                    target.InstanceId,
                    target.FamilyId,
                    target.Definition == null ? target.name : target.Definition.DisplayName,
                    $"capacity.{i + 1:D3}",
                    slotType);
            }

            return result;
        }

        public override void Normalize()
        {
            providedSlotCount = Mathf.Max(0, providedSlotCount);
        }

        public void OnBuildingInitialized(BuildingBase building) => Bind(building);
        public void OnBuildingRegistered(BuildingBase building) => Bind(building);

        public void OnBuildingConstructionCompleted(BuildingBase building)
        {
            Bind(building);
            RefreshTopology();
        }

        public void OnBuildingLevelApplied(BuildingBase building, int previousLevel, int currentLevel)
        {
            Bind(building);
            RefreshTopology();
        }

        public void OnBuildingUnregistered(BuildingBase building)
        {
            owner = null;
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled)
            {
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "库存格",
                    ProvidedSlotCount));
        }

        private void Bind(BuildingBase building)
        {
            owner = building;
            Normalize();
        }

        private void RefreshTopology()
        {
            owner?.GameSystem?.RefreshInventorySlotCapacity();
        }
    }

    [Serializable]
    [BuildingModuleId("expedition.reward_yield")]
    public sealed class BM_远征收益率 : BuildingModuleBase, IBuildingExpeditionRewardYieldSource
    {
        [SerializeField, LabelText("远征收益率加成"), Min(0f)]
        [PropertyTooltip("0.1 表示远征成功物品奖励 +10%。")]
        private float expeditionRewardYieldBonus = 0.1f;

        public override string ModuleDescription => "为成功远征的物品奖励提供收益率加成。";
        public float ExpeditionRewardYieldBonus => Mathf.Max(0f, expeditionRewardYieldBonus);

        public override void Normalize()
        {
            expeditionRewardYieldBonus = Mathf.Max(0f, expeditionRewardYieldBonus);
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            var percent = Mathf.RoundToInt(ExpeditionRewardYieldBonus * 100f);
            if (percent <= 0)
            {
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "远征收益率",
                    percent,
                    new BuildingFunctionBlockSidebarRow(
                        "远征收益率",
                        $"+{percent}%",
                        ExpeditionRewardYieldBonus,
                        true)));
        }
    }

    [Serializable]
    [BuildingModuleId("technology.points")]
    public sealed class BM_科技点产出 : BuildingModuleBase, IBuildingTechnologyPointSource, IBuildingModuleStateSerializer
    {
        [Serializable]
        private sealed class TechnologyPointState
        {
            public int LastTechnologyPoints;
        }

        [SerializeField, LabelText("提供科技点/回合"), Min(0)]
        [PropertyTooltip("该建筑每次成功完成回合处理后，注入当前研究的科技点。")]
        private int providedTechnologyPointsPerTurn = 1;

        private int lastTechnologyPoints;

        public override string ModuleDescription => "建筑成功完成回合后提供科技点，由回合系统汇总并注入当前研究。";
        public int CurrentTechnologyPointsPerTurn => Mathf.Max(0, providedTechnologyPointsPerTurn);
        public int LastTechnologyPoints => Mathf.Max(0, lastTechnologyPoints);

        public void SetProvidedTechnologyPointsPerTurn(int points)
        {
            providedTechnologyPointsPerTurn = Mathf.Max(0, points);
        }

        public override void Normalize()
        {
            providedTechnologyPointsPerTurn = Mathf.Max(0, providedTechnologyPointsPerTurn);
            lastTechnologyPoints = Mathf.Max(0, lastTechnologyPoints);
        }

        public int ProvideTechnologyPointsForTurn()
        {
            lastTechnologyPoints = CurrentTechnologyPointsPerTurn;
            return lastTechnologyPoints;
        }

        public void ClearLastTechnologyPoints()
        {
            lastTechnologyPoints = 0;
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new TechnologyPointState
            {
                LastTechnologyPoints = LastTechnologyPoints
            });
            return !string.IsNullOrWhiteSpace(json);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                ClearLastTechnologyPoints();
                return;
            }

            var state = JsonUtility.FromJson<TechnologyPointState>(json);
            lastTechnologyPoints = Mathf.Max(0, state == null ? 0 : state.LastTechnologyPoints);
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled || CurrentTechnologyPointsPerTurn <= 0)
            {
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "研究点/回合",
                    CurrentTechnologyPointsPerTurn));
        }
    }

}
