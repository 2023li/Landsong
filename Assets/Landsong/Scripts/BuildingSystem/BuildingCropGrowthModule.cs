using System;
using System.Collections.Generic;
using System.Text;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public sealed class BuildingCropGrowthModule : BuildingModuleBase, IBuildingCropFieldSource, IBuildingModuleStateSerializer
    {
        private const string StatusMissingInventory = BuildingRuntimeStatusCatalog.BS_库存缺失;
        private const string StatusInsufficientWorkers = BuildingRuntimeStatusCatalog.BS_工人不足;
        private const string StatusInvalidCrop = "invalid_crop";
        private const string StatusPlantCostMissing = "crop_plant_cost_missing";
        private const string StatusAutoHarvestCostMissing = "crop_auto_harvest_cost_missing";
        private const string StatusHarvestStorageFailed = "crop_harvest_storage_failed";
        private const string StatusCropNotMature = "crop_not_mature";

        private static readonly IReadOnlyList<BuildingCropOption> EmptyCropOptions =
            Array.Empty<BuildingCropOption>();

        private static readonly IReadOnlyList<BuildingResourceChange> EmptyHarvestRewards =
            Array.Empty<BuildingResourceChange>();

        [Serializable]
        private sealed class CropDefinition
        {
            [SerializeField, LabelText("作物ID")]
            private string cropId;

            [SerializeField, LabelText("显示名")]
            private string displayName;

            [SerializeField, LabelText("成熟回合"), Min(1)]
            private int growTurns = 3;

            [SerializeField, LabelText("种植消耗")]
            private BuildingCost[] plantCosts = Array.Empty<BuildingCost>();

            [SerializeField, LabelText("收获产出")]
            private CropHarvestReward[] harvestRewards = Array.Empty<CropHarvestReward>();

            public string CropId => string.IsNullOrWhiteSpace(cropId) ? string.Empty : cropId.Trim();
            public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? CropId : displayName.Trim();
            public int GrowTurns => Mathf.Max(1, growTurns);
            public IReadOnlyList<BuildingCost> PlantCosts => plantCosts ?? Array.Empty<BuildingCost>();
            public IReadOnlyList<CropHarvestReward> HarvestRewards => harvestRewards ?? Array.Empty<CropHarvestReward>();
            public bool IsValid => !string.IsNullOrWhiteSpace(CropId);

            public void Normalize()
            {
                cropId = CropId;
                displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
                growTurns = Mathf.Max(1, growTurns);
                NormalizeCosts(ref plantCosts);

                if (harvestRewards == null)
                {
                    harvestRewards = Array.Empty<CropHarvestReward>();
                    return;
                }

                for (var i = 0; i < harvestRewards.Length; i++)
                {
                    harvestRewards[i]?.Normalize();
                }
            }

            public BuildingCropOption ToOption()
            {
                return new BuildingCropOption(CropId, DisplayName, GrowTurns);
            }
        }

        [Serializable]
        private sealed class CropHarvestReward
        {
            [SerializeField, LabelText("物品")]
            private ItemDefinition itemDefinition;

            [SerializeField, LabelText("最小数量"), Min(0)]
            private int minAmount = 1;

            [SerializeField, LabelText("最大数量"), Min(0)]
            private int maxAmount = 1;

            public string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;
            public string DisplayName => itemDefinition == null ? ItemId : itemDefinition.DisplayName;
            public int MinAmount => Mathf.Max(0, minAmount);
            public int MaxAmount => Mathf.Max(MinAmount, maxAmount);
            public bool IsValid => itemDefinition != null && !string.IsNullOrWhiteSpace(ItemId) && MaxAmount > 0;

            public void Normalize()
            {
                minAmount = Mathf.Max(0, minAmount);
                maxAmount = Mathf.Max(minAmount, maxAmount);
            }

            public ItemAmount Roll()
            {
                if (!IsValid)
                {
                    return default;
                }

                var amount = UnityEngine.Random.Range(MinAmount, MaxAmount + 1);
                return new ItemAmount(itemDefinition, amount);
            }

            public string FormatRange()
            {
                if (!IsValid)
                {
                    return "无";
                }

                return MinAmount == MaxAmount
                    ? $"{DisplayName} x{MinAmount}"
                    : $"{DisplayName} x{MinAmount}-{MaxAmount}";
            }
        }

        [Serializable]
        private sealed class CropGrowthState
        {
            public string PlantedCropId;
            public int GrowthProgressTurns;
            public bool AutoHarvestEnabled;
        }

        [SerializeField, LabelText("作物配置")]
        private CropDefinition[] crops = Array.Empty<CropDefinition>();

        [SerializeField, LabelText("自动收获消耗")]
        private BuildingCost[] autoHarvestCosts = Array.Empty<BuildingCost>();

        [SerializeField, ReadOnly, LabelText("已种植作物")]
        private string plantedCropId;

        [SerializeField, ReadOnly, LabelText("生长进度")]
        private int growthProgressTurns;

        [SerializeField, ReadOnly, LabelText("自动收获")]
        private bool autoHarvestEnabled;

        private IReadOnlyList<BuildingCropOption> cropOptions = EmptyCropOptions;
        private IReadOnlyList<BuildingResourceChange> lastHarvestRewards = EmptyHarvestRewards;
        private string lastAbnormalStatusId = string.Empty;
        private string lastAbnormalStatusText = string.Empty;

        public override string ModuleDescription => "保存作物配置与种植状态，处理播种、成熟、收获、铲除和自动收获。";
        public string PlantedCropId => string.IsNullOrWhiteSpace(plantedCropId) ? string.Empty : plantedCropId.Trim();
        public string PlantedCropDisplayName => TryGetCrop(PlantedCropId, out var crop) ? crop.DisplayName : PlantedCropId;
        public int GrowthProgressTurns => Mathf.Clamp(growthProgressTurns, 0, RequiredGrowTurns);
        public int RequiredGrowTurns => TryGetCrop(PlantedCropId, out var crop) ? crop.GrowTurns : 0;
        public int RemainingGrowTurns => HasCrop ? Mathf.Max(0, RequiredGrowTurns - GrowthProgressTurns) : 0;
        public bool HasCrop => !string.IsNullOrWhiteSpace(PlantedCropId);
        public bool IsMature => HasCrop && RemainingGrowTurns <= 0;
        public bool AutoHarvestEnabled => autoHarvestEnabled;
        public IReadOnlyList<BuildingCropOption> CropOptions => cropOptions ?? EmptyCropOptions;
        public IReadOnlyList<BuildingResourceChange> LastHarvestRewards => lastHarvestRewards ?? EmptyHarvestRewards;

        public override string ToString()
        {
            return "BM_作物种植";
        }

        public override void Normalize()
        {
            if (crops == null)
            {
                crops = Array.Empty<CropDefinition>();
            }

            for (var i = 0; i < crops.Length; i++)
            {
                crops[i]?.Normalize();
            }

            NormalizeCosts(ref autoHarvestCosts);
            plantedCropId = PlantedCropId;
            growthProgressTurns = Mathf.Max(0, growthProgressTurns);
            RebuildCropOptions();

            if (HasCrop && TryGetCrop(PlantedCropId, out var crop))
            {
                growthProgressTurns = Mathf.Clamp(growthProgressTurns, 0, crop.GrowTurns);
            }
        }

        public bool CanPlant(BuildingBase owner, string cropId)
        {
            Normalize();
            return !HasCrop
                   && TryGetCrop(cropId, out var crop)
                   && CanSpendCosts(owner, crop.PlantCosts);
        }

        public bool TryPlant(BuildingBase owner, string cropId, out bool stateChanged)
        {
            stateChanged = false;
            Normalize();

            if (HasCrop)
            {
                SetLastAbnormalStatus(StatusInvalidCrop, "已有作物");
                return false;
            }

            if (!TryGetCrop(cropId, out var crop))
            {
                SetLastAbnormalStatus(StatusInvalidCrop, "作物配置异常");
                return false;
            }

            if (!TrySpendCosts(owner, crop.PlantCosts, StatusPlantCostMissing, "种植材料不足"))
            {
                return false;
            }

            plantedCropId = crop.CropId;
            growthProgressTurns = 0;
            lastHarvestRewards = EmptyHarvestRewards;
            ClearLastAbnormalStatus();
            stateChanged = true;
            return true;
        }

        public bool CanHarvest()
        {
            Normalize();
            return IsMature && TryGetCrop(PlantedCropId, out var crop) && HasAnyValidHarvestReward(crop);
        }

        public bool TryHarvest(BuildingBase owner, out bool stateChanged)
        {
            return TryHarvest(owner, false, out stateChanged);
        }

        public bool CanClearCrop()
        {
            return HasCrop;
        }

        public bool TryClearCrop(BuildingBase owner, out bool stateChanged)
        {
            stateChanged = false;
            if (!HasCrop)
            {
                return false;
            }

            plantedCropId = string.Empty;
            growthProgressTurns = 0;
            lastHarvestRewards = EmptyHarvestRewards;
            ClearLastAbnormalStatus();
            stateChanged = true;
            return true;
        }

        public bool TrySetAutoHarvestEnabled(bool enabled, out bool stateChanged)
        {
            stateChanged = autoHarvestEnabled != enabled;
            autoHarvestEnabled = enabled;
            if (stateChanged)
            {
                ClearLastAbnormalStatus();
            }

            return true;
        }

        public bool ProcessTurn(BuildingBase owner, bool hasEnoughWorkers, out bool stateChanged)
        {
            stateChanged = false;
            ClearLastAbnormalStatus();

            if (!HasCrop)
            {
                return true;
            }

            if (!hasEnoughWorkers)
            {
                SetLastAbnormalStatus(StatusInsufficientWorkers, "工人不足");
                return false;
            }

            if (!IsMature)
            {
                growthProgressTurns++;
                Normalize();
                stateChanged = true;
            }

            if (IsMature && autoHarvestEnabled)
            {
                var harvested = TryHarvest(owner, true, out var harvestChanged);
                stateChanged |= harvestChanged;
                return harvested;
            }

            return true;
        }

        public bool TryGetLastRuntimeStatus(out BuildingRuntimeStatus status)
        {
            status = string.IsNullOrWhiteSpace(lastAbnormalStatusId)
                ? default
                : new BuildingRuntimeStatus(lastAbnormalStatusId, lastAbnormalStatusText);
            return status.IsValid;
        }

        public void ClearLastTurnState()
        {
            lastHarvestRewards = EmptyHarvestRewards;
            ClearLastAbnormalStatus();
        }

        public bool TryCaptureState(out string json)
        {
            json = JsonUtility.ToJson(new CropGrowthState
            {
                PlantedCropId = PlantedCropId,
                GrowthProgressTurns = GrowthProgressTurns,
                AutoHarvestEnabled = autoHarvestEnabled
            });

            return !string.IsNullOrWhiteSpace(json);
        }

        public void RestoreState(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                plantedCropId = string.Empty;
                growthProgressTurns = 0;
                autoHarvestEnabled = false;
                return;
            }

            var state = JsonUtility.FromJson<CropGrowthState>(json);
            plantedCropId = state == null ? string.Empty : state.PlantedCropId;
            growthProgressTurns = state == null ? 0 : state.GrowthProgressTurns;
            autoHarvestEnabled = state != null && state.AutoHarvestEnabled;
            Normalize();
        }

        public override void AppendFunctionBlockEntries(
            BuildingBase building,
            ref List<BuildingFunctionBlockEntry> entries)
        {
            if (!IsEnabled)
            {
                return;
            }

            Normalize();
            if (!HasCrop)
            {
                AddFunctionBlockEntry(
                    ref entries,
                    new BuildingFunctionBlockEntry(
                        BuildingFunctionBlockGroup.功能性,
                        "未种植",
                        1,
                        new BuildingFunctionBlockSidebarRow("可选作物", FormatCropOptions())));
                return;
            }

            var rows = BuildCropSidebarRows();
            if (IsMature)
            {
                AddFunctionBlockEntry(
                    ref entries,
                    new BuildingFunctionBlockEntry(
                        BuildingFunctionBlockGroup.功能性,
                        "可收获",
                        1,
                        rows));
                return;
            }

            AddFunctionBlockEntry(
                ref entries,
                new BuildingFunctionBlockEntry(
                    BuildingFunctionBlockGroup.功能性,
                    "成熟剩余回合",
                    Mathf.Max(1, RemainingGrowTurns),
                    rows));
        }

        private bool TryHarvest(BuildingBase owner, bool chargeAutoHarvestCost, out bool stateChanged)
        {
            stateChanged = false;
            Normalize();

            if (!HasCrop || !TryGetCrop(PlantedCropId, out var crop))
            {
                SetLastAbnormalStatus(StatusInvalidCrop, "作物配置异常");
                return false;
            }

            if (!IsMature)
            {
                SetLastAbnormalStatus(StatusCropNotMature, "作物尚未成熟");
                return false;
            }

            if (!TryBuildHarvestRewards(crop, out var rewards, out var rewardChanges))
            {
                SetLastAbnormalStatus(StatusInvalidCrop, "收获产出配置异常");
                return false;
            }

            var inventory = owner == null || owner.GameSystem == null ? null : owner.GameSystem.Inventory;
            if (inventory == null)
            {
                SetLastAbnormalStatus(StatusMissingInventory, "库存服务缺失");
                return false;
            }

            if (!inventory.CanAddItems(rewards))
            {
                SetLastAbnormalStatus(StatusHarvestStorageFailed, "收获存入失败");
                return false;
            }

            if (chargeAutoHarvestCost
                && !TrySpendCosts(owner, autoHarvestCosts, StatusAutoHarvestCostMissing, "自动收获金币不足"))
            {
                return false;
            }

            if (!inventory.TryAddItems(rewards))
            {
                SetLastAbnormalStatus(StatusHarvestStorageFailed, "收获存入失败");
                return false;
            }

            plantedCropId = string.Empty;
            growthProgressTurns = 0;
            lastHarvestRewards = rewardChanges;
            ClearLastAbnormalStatus();
            stateChanged = true;
            return true;
        }

        private bool TryBuildHarvestRewards(
            CropDefinition crop,
            out List<ItemAmount> rewards,
            out IReadOnlyList<BuildingResourceChange> rewardChanges)
        {
            rewards = null;
            rewardChanges = EmptyHarvestRewards;
            if (crop == null || crop.HarvestRewards == null)
            {
                return false;
            }

            var changes = new List<BuildingResourceChange>();
            for (var i = 0; i < crop.HarvestRewards.Count; i++)
            {
                var reward = crop.HarvestRewards[i];
                if (reward == null || !reward.IsValid)
                {
                    continue;
                }

                var item = reward.Roll();
                if (!item.IsValid)
                {
                    continue;
                }

                rewards ??= new List<ItemAmount>();
                rewards.Add(item);
                changes.Add(new BuildingResourceChange(item.ItemId, item.Amount));
            }

            if (rewards == null || rewards.Count == 0)
            {
                return false;
            }

            rewardChanges = changes;
            return true;
        }

        private bool TryGetCrop(string cropId, out CropDefinition crop)
        {
            crop = null;
            if (string.IsNullOrWhiteSpace(cropId) || crops == null)
            {
                return false;
            }

            var normalizedCropId = cropId.Trim();
            for (var i = 0; i < crops.Length; i++)
            {
                var candidate = crops[i];
                if (candidate != null
                    && candidate.IsValid
                    && string.Equals(candidate.CropId, normalizedCropId, StringComparison.Ordinal))
                {
                    crop = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool HasAnyValidHarvestReward(CropDefinition crop)
        {
            if (crop == null || crop.HarvestRewards == null)
            {
                return false;
            }

            for (var i = 0; i < crop.HarvestRewards.Count; i++)
            {
                if (crop.HarvestRewards[i] != null && crop.HarvestRewards[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static void NormalizeCosts(ref BuildingCost[] costs)
        {
            if (costs == null)
            {
                costs = Array.Empty<BuildingCost>();
                return;
            }

            for (var i = 0; i < costs.Length; i++)
            {
                costs[i] = costs[i].Normalized();
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

        private static bool CanSpendCosts(BuildingBase owner, IReadOnlyList<BuildingCost> costs)
        {
            if (!HasAnyValidCost(costs))
            {
                return true;
            }

            var inventory = owner == null || owner.GameSystem == null ? null : owner.GameSystem.Inventory;
            return inventory != null && inventory.CanAffordBuildingCosts(costs);
        }

        private bool TrySpendCosts(
            BuildingBase owner,
            IReadOnlyList<BuildingCost> costs,
            string missingStatusId,
            string missingStatusText)
        {
            if (!HasAnyValidCost(costs))
            {
                return true;
            }

            var inventory = owner == null || owner.GameSystem == null ? null : owner.GameSystem.Inventory;
            if (inventory == null)
            {
                SetLastAbnormalStatus(StatusMissingInventory, "库存服务缺失");
                return false;
            }

            if (!inventory.CanAffordBuildingCosts(costs) || !inventory.TrySpendBuildingCosts(costs))
            {
                SetLastAbnormalStatus(missingStatusId, missingStatusText);
                return false;
            }

            return true;
        }

        private void RebuildCropOptions()
        {
            if (crops == null || crops.Length == 0)
            {
                cropOptions = EmptyCropOptions;
                return;
            }

            List<BuildingCropOption> options = null;
            for (var i = 0; i < crops.Length; i++)
            {
                if (crops[i] == null || !crops[i].IsValid)
                {
                    continue;
                }

                options ??= new List<BuildingCropOption>();
                options.Add(crops[i].ToOption());
            }

            cropOptions = options == null ? EmptyCropOptions : options;
        }

        private IReadOnlyList<BuildingFunctionBlockSidebarRow> BuildCropSidebarRows()
        {
            var rows = new List<BuildingFunctionBlockSidebarRow>
            {
                new BuildingFunctionBlockSidebarRow("当前作物", PlantedCropDisplayName),
                new BuildingFunctionBlockSidebarRow("生长进度", $"{GrowthProgressTurns}/{RequiredGrowTurns}"),
                new BuildingFunctionBlockSidebarRow("自动收获", autoHarvestEnabled ? "开启" : "关闭"),
                new BuildingFunctionBlockSidebarRow("自动收获消耗", FormatCosts(autoHarvestCosts))
            };

            if (TryGetCrop(PlantedCropId, out var crop))
            {
                rows.Add(new BuildingFunctionBlockSidebarRow("收获产出", FormatHarvestRewards(crop.HarvestRewards)));
            }

            return rows;
        }

        private string FormatCropOptions()
        {
            if (CropOptions.Count == 0)
            {
                return "未配置";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < CropOptions.Count; i++)
            {
                if (builder.Length > 0)
                {
                    builder.Append("，");
                }

                builder.Append(CropOptions[i].DisplayName);
            }

            return builder.ToString();
        }

        private static string FormatCosts(IReadOnlyList<BuildingCost> costs)
        {
            if (!HasAnyValidCost(costs))
            {
                return "无";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < costs.Count; i++)
            {
                if (!costs[i].IsValid)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("，");
                }

                builder.Append(costs[i]);
            }

            return builder.Length == 0 ? "无" : builder.ToString();
        }

        private static string FormatHarvestRewards(IReadOnlyList<CropHarvestReward> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return "无";
            }

            var builder = new StringBuilder();
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null || !reward.IsValid)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("，");
                }

                builder.Append(reward.FormatRange());
            }

            return builder.Length == 0 ? "无" : builder.ToString();
        }

        private void SetLastAbnormalStatus(string statusId, string statusText)
        {
            lastAbnormalStatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
            lastAbnormalStatusText = string.IsNullOrWhiteSpace(statusText) ? lastAbnormalStatusId : statusText.Trim();
        }

        private void ClearLastAbnormalStatus()
        {
            lastAbnormalStatusId = string.Empty;
            lastAbnormalStatusText = string.Empty;
        }
    }
}
