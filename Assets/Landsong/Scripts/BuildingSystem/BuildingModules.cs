using System;
using System.Collections.Generic;
using Landsong.ConditionSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
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
    public sealed class BM_资源产出 : BuildingModuleBase, IBuildingResourceProductionSource
    {
        private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
        private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
        private const string StatusInvalidProductionItem = "invalid_production_item";
        private const string StatusProductionStorageFailed = "production_storage_failed";

        [Serializable]
        private struct ResourceProductionOutput
        {
            [SerializeField, LabelText("资源ID")]
            private string itemId;

            [SerializeField, LabelText("工人数产量表")]
            private WorkerProductionTier[] workerProductionTiers;

            public ResourceProductionOutput(string itemId, WorkerProductionTier[] workerProductionTiers)
            {
                this.itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
                this.workerProductionTiers = workerProductionTiers ?? Array.Empty<WorkerProductionTier>();
            }

            public string ItemId => string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            public bool HasUsableItemId => !string.IsNullOrWhiteSpace(ItemId);

            public ResourceProductionOutput Normalize(int maxWorkers)
            {
                itemId = ItemId;
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

        public void EnsureSingleOutput(
            string itemId,
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
                new ResourceProductionOutput(itemId, defaultWorkerProductionTiers)
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

        public IReadOnlyList<BuildingResourceChange> PreviewResourceProductions(int workers, int maxWorkers)
        {
            currentResourceProductions = CreateResourceProductions(workers, maxWorkers);
            return currentResourceProductions;
        }

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
                if (!outputs[i].HasUsableItemId)
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
            var productionChanges = CreateResourceProductions(workers, maxWorkers);
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

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled || !TryResolveWorkforce(building, out var workers, out var maxWorkers))
            {
                return;
            }

            var changes = PreviewResourceProductions(workers, maxWorkers);
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

        private IReadOnlyList<BuildingResourceChange> CreateResourceProductions(int workers, int maxWorkers)
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
            if (building is IBuildingWorkforceFundingSource workforce)
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
    public sealed class BM_库存格容量 : BuildingModuleBase
    {
        [SerializeField, LabelText("提供库存格数"), Min(0)]
        [PropertyTooltip("单位：格。该建筑存在时提供的额外库存格子数量。")]
        private int providedSlotCount = 5;

        public override string ModuleDescription => "让建筑存在时提供额外库存格子，GameSystem 会汇总所有启用的库存容量模块。";
        public int ProvidedSlotCount => Mathf.Max(0, providedSlotCount);

        public void SetProvidedSlotCount(int slotCount)
        {
            providedSlotCount = Mathf.Max(0, slotCount);
        }

        public override void Normalize()
        {
            providedSlotCount = Mathf.Max(0, providedSlotCount);
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
    }

    [Serializable]
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

    [Serializable]
    public sealed class BM_施工材料消耗 : BuildingModuleBase, IBuildingModuleStateSerializer
    {
        [Serializable]
        private struct ConstructionTurnCost
        {
            [SerializeField, LabelText("本回合消耗")]
            private BuildingCost[] costs;

            public IReadOnlyList<BuildingCost> Costs => costs ?? Array.Empty<BuildingCost>();

            public ConstructionTurnCost Normalize()
            {
                costs ??= Array.Empty<BuildingCost>();
                for (var i = 0; i < costs.Length; i++)
                {
                    costs[i] = costs[i].Normalized();
                }

                return this;
            }
        }

        [Serializable]
        private sealed class ConstructionConsumptionState
        {
            public int LastCompletedTurnIndex = -1;
        }

        [SerializeField, LabelText("逐回合施工消耗")]
        private ConstructionTurnCost[] turnCosts = Array.Empty<ConstructionTurnCost>();

        private int lastCompletedTurnIndex = -1;

        public override string ModuleDescription => "按施工进度扣除当前回合配置的材料；扣除成功后由在建脚本推进等级升级经验。";
        public int ConfiguredTurnCount => turnCosts == null ? 0 : turnCosts.Length;
        public IReadOnlyList<BuildingResourceChange> LastResourceConsumptions =>
            CreateResourceChanges(lastCompletedTurnIndex);

        public override void Normalize()
        {
            turnCosts ??= Array.Empty<ConstructionTurnCost>();
            for (var i = 0; i < turnCosts.Length; i++)
            {
                turnCosts[i] = turnCosts[i].Normalize();
            }

            if (lastCompletedTurnIndex < -1 || lastCompletedTurnIndex >= turnCosts.Length)
            {
                lastCompletedTurnIndex = -1;
            }
        }

        public IReadOnlyList<BuildingResourceChange> GetResourceConsumptionsForTurn(int turnIndex)
        {
            return CreateResourceChanges(turnIndex);
        }

        public IReadOnlyList<BuildingCost> GetTotalConstructionCosts()
        {
            Normalize();
            List<BuildingCost> totals = null;
            for (var turnIndex = 0; turnIndex < turnCosts.Length; turnIndex++)
            {
                var costs = turnCosts[turnIndex].Costs;
                for (var costIndex = 0; costIndex < costs.Count; costIndex++)
                {
                    var cost = costs[costIndex];
                    if (!cost.IsValid)
                    {
                        continue;
                    }

                    totals ??= new List<BuildingCost>();
                    var existingIndex = FindCostIndex(totals, cost.ItemId);
                    if (existingIndex < 0)
                    {
                        totals.Add(cost);
                        continue;
                    }

                    var existing = totals[existingIndex];
                    totals[existingIndex] = new BuildingCost(
                        existing.ItemDefinition == null ? cost.ItemDefinition : existing.ItemDefinition,
                        existing.Amount + cost.Amount);
                }
            }

            return totals == null ? Array.Empty<BuildingCost>() : totals;
        }

        public bool TryConsumeForTurn(
            BuildingBase building,
            int turnIndex,
            out string failureStatusId,
            out string failureStatusText)
        {
            Normalize();
            lastCompletedTurnIndex = -1;
            failureStatusId = string.Empty;
            failureStatusText = string.Empty;

            if (!TryGetValidCosts(turnIndex, out var costs))
            {
                failureStatusId = BuildingRuntimeStatusCatalog.BS_消耗失败;
                failureStatusText = "施工材料配置异常";
                return false;
            }

            var inventory = building == null || building.GameSystem == null
                ? null
                : building.GameSystem.Inventory;
            if (inventory == null)
            {
                failureStatusId = BuildingRuntimeStatusCatalog.BS_库存缺失;
                failureStatusText = "库存服务缺失";
                return false;
            }

            if (!inventory.CanAffordBuildingCosts(costs)
                || !inventory.TrySpendBuildingCosts(costs))
            {
                failureStatusId = BuildingRuntimeStatusCatalog.BS_消耗失败;
                failureStatusText = "施工材料不足";
                return false;
            }

            lastCompletedTurnIndex = turnIndex;
            return true;
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new ConstructionConsumptionState
            {
                LastCompletedTurnIndex = lastCompletedTurnIndex
            });
            return !string.IsNullOrWhiteSpace(json);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                lastCompletedTurnIndex = -1;
                return;
            }

            var state = JsonUtility.FromJson<ConstructionConsumptionState>(json);
            lastCompletedTurnIndex = state == null ? -1 : state.LastCompletedTurnIndex;
            Normalize();
        }

        private bool TryGetValidCosts(int turnIndex, out IReadOnlyList<BuildingCost> costs)
        {
            costs = Array.Empty<BuildingCost>();
            if (turnIndex < 0 || turnCosts == null || turnIndex >= turnCosts.Length)
            {
                return false;
            }

            var configuredCosts = turnCosts[turnIndex].Costs;
            for (var i = 0; i < configuredCosts.Count; i++)
            {
                if (configuredCosts[i].IsValid)
                {
                    costs = configuredCosts;
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<BuildingResourceChange> CreateResourceChanges(int turnIndex)
        {
            if (!TryGetValidCosts(turnIndex, out var costs))
            {
                return Array.Empty<BuildingResourceChange>();
            }

            List<BuildingResourceChange> changes = null;
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                var change = new BuildingResourceChange(cost.ItemId, cost.Amount);
                if (!change.IsValid)
                {
                    continue;
                }

                changes ??= new List<BuildingResourceChange>();
                changes.Add(change);
            }

            return changes == null ? Array.Empty<BuildingResourceChange>() : changes;
        }

        private static int FindCostIndex(IReadOnlyList<BuildingCost> costs, string itemId)
        {
            for (var i = 0; i < costs.Count; i++)
            {
                if (string.Equals(costs[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [Serializable]
    public sealed class BM_等级升级 : BuildingModuleBase
    {
        [SerializeField, LabelText("自动升级")]
        private bool autoUpgradeEnabled = true;

        [SerializeField, LabelText("当前经验"), Min(0)]
        private int currentExperience;

        [SerializeField, LabelText("升级所需经验"), Min(1)]
        private int requiredExperience = 10;

        [SerializeField, LabelText("升级目标预制体")]
        private BuildingBase upgradeTargetPrefab;

        [SerializeReference, LabelText("升级条件")]
        private GameCondition upgradeCondition;

        [SerializeField, LabelText("升级消耗")]
        private BuildingCost[] upgradeCosts = Array.Empty<BuildingCost>();

        public override string ModuleDescription => "保存升级经验，检查升级条件和消耗，满足后把当前建筑替换为目标 prefab。";
        public bool AutoUpgradeEnabled => autoUpgradeEnabled;
        public int CurrentExperience => Mathf.Max(0, currentExperience);
        public int RequiredExperience => Mathf.Max(1, requiredExperience);
        public BuildingBase UpgradeTargetPrefab => upgradeTargetPrefab;
        public GameCondition UpgradeCondition => upgradeCondition;
        public IReadOnlyList<BuildingCost> UpgradeCosts => upgradeCosts ?? Array.Empty<BuildingCost>();
        public float ExperienceProgress => Mathf.Clamp01(CurrentExperience / (float)RequiredExperience);
        public bool IsReadyToUpgrade => CurrentExperience >= RequiredExperience;
        public bool HasUpgradeCosts => HasAnyValidCost(UpgradeCosts);

        public override void Normalize()
        {
            currentExperience = Mathf.Max(0, currentExperience);
            requiredExperience = Mathf.Max(1, requiredExperience);
            NormalizeCosts();
        }

        public void SetAutoUpgradeEnabled(bool enabled)
        {
            autoUpgradeEnabled = enabled;
        }

        public void SetExperience(int experience)
        {
            currentExperience = Mathf.Clamp(experience, 0, RequiredExperience);
        }

        public void AddExperience(int experience)
        {
            if (experience <= 0)
            {
                return;
            }

            SetExperience(CurrentExperience + experience);
        }

        public bool CanUpgrade(BuildingBase building)
        {
            return building != null
                   && IsReadyToUpgrade
                   && upgradeTargetPrefab != null
                   && upgradeTargetPrefab.HasDefinition
                   && building.GameSystem != null
                   && building.GameSystem.Buildings != null
                   && IsUpgradeConditionMet(building)
                   && building.GameSystem.Buildings.CanReplace(building, upgradeTargetPrefab, false)
                   && CanAffordUpgradeCosts(building);
        }

        public bool TryUpgrade(BuildingBase building)
        {
            if (!CanUpgrade(building))
            {
                return false;
            }

            if (!TrySpendUpgradeCosts(building))
            {
                return false;
            }

            return building.GameSystem.Buildings.TryReplace(building, upgradeTargetPrefab, out _);
        }

        public bool TryAutoUpgrade(BuildingBase building)
        {
            return autoUpgradeEnabled && TryUpgrade(building);
        }

        public bool CanAffordUpgradeCosts(BuildingBase building)
        {
            if (!HasUpgradeCosts)
            {
                return true;
            }

            var inventory = building == null || building.GameSystem == null ? null : building.GameSystem.Inventory;
            return inventory != null && inventory.CanAffordBuildingCosts(UpgradeCosts);
        }

        private bool TrySpendUpgradeCosts(BuildingBase building)
        {
            if (!HasUpgradeCosts)
            {
                return true;
            }

            var inventory = building == null || building.GameSystem == null ? null : building.GameSystem.Inventory;
            return inventory != null && inventory.TrySpendBuildingCosts(UpgradeCosts);
        }

        private bool IsUpgradeConditionMet(BuildingBase building)
        {
            return upgradeCondition == null || upgradeCondition.IsMet(building.GameSystem);
        }

        private void NormalizeCosts()
        {
            if (upgradeCosts == null)
            {
                upgradeCosts = Array.Empty<BuildingCost>();
                return;
            }

            for (var i = 0; i < upgradeCosts.Length; i++)
            {
                upgradeCosts[i] = upgradeCosts[i].Normalized();
            }
        }

        private static bool HasAnyValidCost(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null)
            {
                return false;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
