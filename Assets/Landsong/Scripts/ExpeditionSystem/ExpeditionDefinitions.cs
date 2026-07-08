using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.ConditionSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ExpeditionSystem
{
    public enum ExpeditionStatus
    {
        Active = 0,
        Succeeded = 1,
        Failed = 2
    }

    public enum ExpeditionDestinationUnavailableReason
    {
        None = 0,
        Hidden = 10,
        ConditionLocked = 30,
        AlreadyCompleted = 40
    }

    public enum ExpeditionStartFailureReason
    {
        None = 0,
        InvalidDestination = 10,
        DestinationUnavailable = 20,
        PopulationTooLow = 30,
        PopulationTooHigh = 40,
        PopulationUnavailable = 50,
        InvalidSupply = 60,
        RequiredSupplyMissing = 70,
        SupplyLimitExceeded = 80,
        InventoryMissing = 90,
        InventoryRemoveFailed = 100,
        ActiveExpeditionLimitReached = 110
    }

    public enum ExpeditionClaimFailureReason
    {
        None = 0,
        InvalidExpedition = 10,
        NotSucceeded = 20,
        AlreadyClaimed = 30,
        InventoryMissing = 40,
        InventoryFull = 50
    }

    [Serializable]
    public sealed class ExpeditionSupplyOption
    {
        [SerializeField, LabelText("物品")]
        private ItemDefinition itemDefinition;

        [SerializeField, LabelText("最低携带数量"), Min(0)]
        private int requiredAmount;

        [SerializeField, LabelText("额外物资上限"), Min(0)]
        [PropertyTooltip("0 表示使用最低携带数量的 50%，非 0 时也不会超过最低携带数量的 50%。")]
        private int maxAmount;

        [SerializeField, LabelText("每个额外物品成功率加成"), Range(0f, 1f)]
        private float successChancePerItem;

        [SerializeField, LabelText("每个额外物品收益率加成"), Range(0f, 1f)]
        private float rewardYieldBonusPerItem;

        public ItemDefinition ItemDefinition => itemDefinition;
        public string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;
        public string DisplayName => itemDefinition == null ? ItemId : itemDefinition.DisplayName;
        public Sprite Icon => itemDefinition == null ? null : itemDefinition.Icon;
        public int RequiredAmount => Mathf.Max(0, requiredAmount);
        public int MaxAmount => Mathf.Max(0, maxAmount);
        public int DefaultExtraAmountLimit => Mathf.FloorToInt(RequiredAmount * 0.5f);
        public int ExtraAmountLimit => CalculateExtraAmountLimit();
        public int MaxAssignedAmount => RequiredAmount + ExtraAmountLimit;
        public bool HasExtraAmountLimit => ExtraAmountLimit > 0;
        public float SuccessChancePerItem => Mathf.Clamp01(successChancePerItem);
        public float RewardYieldBonusPerItem => Mathf.Clamp01(rewardYieldBonusPerItem);
        public bool IsValid => itemDefinition != null && !string.IsNullOrWhiteSpace(ItemId);

        public void Normalize()
        {
            requiredAmount = Mathf.Max(0, requiredAmount);
            maxAmount = Mathf.Max(0, maxAmount);
            var defaultExtraAmountLimit = Mathf.FloorToInt(requiredAmount * 0.5f);
            if (maxAmount > defaultExtraAmountLimit)
            {
                maxAmount = defaultExtraAmountLimit;
            }

            successChancePerItem = Mathf.Clamp01(successChancePerItem);
            rewardYieldBonusPerItem = Mathf.Clamp01(rewardYieldBonusPerItem);
        }

        public int ClampAssignedAmount(int amount)
        {
            amount = Mathf.Max(0, amount);
            return Mathf.Min(amount, MaxAssignedAmount);
        }

        public int GetExtraAssignedAmount(int amount)
        {
            return Mathf.Clamp(Mathf.Max(0, amount) - RequiredAmount, 0, ExtraAmountLimit);
        }

        private int CalculateExtraAmountLimit()
        {
            var defaultLimit = DefaultExtraAmountLimit;
            if (MaxAmount <= 0)
            {
                return defaultLimit;
            }

            return Mathf.Min(MaxAmount, defaultLimit);
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Expedition/Destination", fileName = "ExpeditionDestination")]
    public sealed class ExpeditionDestinationDefinition : ScriptableObject
    {
        [TitleGroup("基础信息")]
        [SerializeField, LabelText("目的地ID")]
        private string destinationId;

        [SerializeField, LabelText("显示名称")]
        private string displayName;

        [SerializeField, TextArea, LabelText("描述")]
        private string description;

        [SerializeField, PreviewField(72), LabelText("图标")]
        private Sprite icon;

        [TitleGroup("条件")]
        [SerializeField, LabelText("完成后仍可重复出发")]
        private bool repeatable = true;

        [SerializeReference, LabelText("显示条件")]
        private GameCondition visibleCondition;

        [SerializeReference, LabelText("可用条件")]
        private GameCondition availableCondition;

        [TitleGroup("远征规则")]
        [SerializeField, LabelText("持续回合"), Min(1)]
        private int durationTurns = 3;

        [SerializeField, LabelText("最低人口"), Min(1)]
        private int minPopulation = 1;

        [SerializeField, LabelText("最高人口"), Min(0)]
        [PropertyTooltip("0 表示不限制最高人口。")]
        private int maxPopulation;

        [SerializeField, LabelText("基础成功率"), Range(0f, 1f)]
        private float baseSuccessChance = 0.5f;

        [SerializeField, LabelText("每人口成功率加成"), Range(0f, 1f)]
        private float successChancePerPopulation = 0.02f;

        [SerializeField, LabelText("最高成功率"), Range(0f, 1f)]
        private float maxSuccessChance = 0.95f;

        [SerializeField, LabelText("可携带物品")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        private ExpeditionSupplyOption[] supplyOptions = Array.Empty<ExpeditionSupplyOption>();

        [TitleGroup("失败补贴")]
        [SerializeField, LabelText("基础补贴金币"), Min(0)]
        private int baseFailureSubsidyGold;

        [SerializeField, LabelText("每人口补贴金币"), Min(0)]
        private int failureSubsidyGoldPerPopulation = 1;

        [TitleGroup("成功奖励")]
        [SerializeField, LabelText("物品奖励")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        private ItemAmount[] itemRewards = Array.Empty<ItemAmount>();

        [SerializeField, LabelText("奇迹蓝图奖励")]
        [ListDrawerSettings(DefaultExpandedState = true)]
        private BuildingBase[] blueprintRewards = Array.Empty<BuildingBase>();

        public string DestinationId => NormalizeId(destinationId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(DestinationId) ? "未命名目的地" : DestinationId)
            : displayName.Trim();
        public string Description => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        public Sprite Icon => icon;
        public bool Repeatable => repeatable;
        public GameCondition VisibleCondition => visibleCondition;
        public GameCondition AvailableCondition => availableCondition;
        public int DurationTurns => Mathf.Max(1, durationTurns);
        public int MinPopulation => Mathf.Max(1, minPopulation);
        public int MaxPopulation => Mathf.Max(0, maxPopulation);
        public bool HasMaxPopulation => MaxPopulation > 0;
        public float BaseSuccessChance => Mathf.Clamp01(baseSuccessChance);
        public float SuccessChancePerPopulation => Mathf.Clamp01(successChancePerPopulation);
        public float MaxSuccessChance => Mathf.Clamp01(maxSuccessChance);
        public IReadOnlyList<ExpeditionSupplyOption> SupplyOptions => supplyOptions ?? Array.Empty<ExpeditionSupplyOption>();
        public int BaseFailureSubsidyGold => Mathf.Max(0, baseFailureSubsidyGold);
        public int FailureSubsidyGoldPerPopulation => Mathf.Max(0, failureSubsidyGoldPerPopulation);
        public IReadOnlyList<ItemAmount> ItemRewards => itemRewards ?? Array.Empty<ItemAmount>();
        public IReadOnlyList<BuildingBase> BlueprintRewards => blueprintRewards ?? Array.Empty<BuildingBase>();
        public bool IsValid => !string.IsNullOrWhiteSpace(DestinationId);
        public bool HasRewards => HasItemRewards() || HasBlueprintRewards();

        private void OnValidate()
        {
            Normalize();
        }

        public void Normalize()
        {
            destinationId = NormalizeId(destinationId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            durationTurns = Mathf.Max(1, durationTurns);
            minPopulation = Mathf.Max(1, minPopulation);
            maxPopulation = Mathf.Max(0, maxPopulation);
            if (maxPopulation > 0 && maxPopulation < minPopulation)
            {
                maxPopulation = minPopulation;
            }

            baseSuccessChance = Mathf.Clamp01(baseSuccessChance);
            successChancePerPopulation = Mathf.Clamp01(successChancePerPopulation);
            maxSuccessChance = Mathf.Clamp01(maxSuccessChance);
            baseFailureSubsidyGold = Mathf.Max(0, baseFailureSubsidyGold);
            failureSubsidyGoldPerPopulation = Mathf.Max(0, failureSubsidyGoldPerPopulation);

            supplyOptions ??= Array.Empty<ExpeditionSupplyOption>();
            for (var i = 0; i < supplyOptions.Length; i++)
            {
                supplyOptions[i]?.Normalize();
            }

            itemRewards ??= Array.Empty<ItemAmount>();
            for (var i = 0; i < itemRewards.Length; i++)
            {
                itemRewards[i] = itemRewards[i].Normalized();
            }

            blueprintRewards ??= Array.Empty<BuildingBase>();
        }

        public bool TryGetSupplyOption(string itemId, out ExpeditionSupplyOption option)
        {
            itemId = NormalizeId(itemId);
            var options = SupplyOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var candidate = options[i];
                if (candidate == null || !candidate.IsValid)
                {
                    continue;
                }

                if (string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
                {
                    option = candidate;
                    return true;
                }
            }

            option = null;
            return false;
        }

        public float CalculateSuccessChance(int population, IReadOnlyDictionary<string, int> assignedSupplyAmounts)
        {
            var chance = BaseSuccessChance + Mathf.Max(0, population) * SuccessChancePerPopulation;
            var options = SupplyOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null || !option.IsValid || assignedSupplyAmounts == null)
                {
                    continue;
                }

                assignedSupplyAmounts.TryGetValue(option.ItemId, out var amount);
                chance += option.GetExtraAssignedAmount(amount) * option.SuccessChancePerItem;
            }

            return Mathf.Clamp(chance, 0f, MaxSuccessChance);
        }

        public float CalculateSupplyRewardYieldBonus(IReadOnlyDictionary<string, int> assignedSupplyAmounts)
        {
            if (assignedSupplyAmounts == null)
            {
                return 0f;
            }

            var bonus = 0f;
            var options = SupplyOptions;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                if (option == null || !option.IsValid)
                {
                    continue;
                }

                assignedSupplyAmounts.TryGetValue(option.ItemId, out var amount);
                bonus += option.GetExtraAssignedAmount(amount) * option.RewardYieldBonusPerItem;
            }

            return Mathf.Max(0f, bonus);
        }

        public int CalculateFailureSubsidyGold(int population)
        {
            return BaseFailureSubsidyGold + Mathf.Max(0, population) * FailureSubsidyGoldPerPopulation;
        }

        public IEnumerable<string> GetBlueprintRewardBuildingIds()
        {
            var rewards = BlueprintRewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                var building = rewards[i];
                if (building == null || !building.HasDefinition)
                {
                    continue;
                }

                var buildingId = building.Definition.BuildingId;
                if (!string.IsNullOrWhiteSpace(buildingId))
                {
                    yield return buildingId.Trim();
                }
            }
        }

        public static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private bool HasItemRewards()
        {
            var rewards = ItemRewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].Normalized().IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasBlueprintRewards()
        {
            var rewards = BlueprintRewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i] != null && rewards[i].HasDefinition)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [CreateAssetMenu(menuName = "Landsong/Expedition/Destination Catalog", fileName = "ExpeditionDestinationCatalog")]
    public sealed class ExpeditionDestinationCatalog : ScriptableObject
    {
        [SerializeField, LabelText("远征目的地")]
        private ExpeditionDestinationDefinition[] destinations = Array.Empty<ExpeditionDestinationDefinition>();

        private Dictionary<string, ExpeditionDestinationDefinition> destinationsById;

        public IReadOnlyList<ExpeditionDestinationDefinition> Destinations => destinations ?? Array.Empty<ExpeditionDestinationDefinition>();

        private void OnEnable()
        {
            Normalize();
            RebuildIndex();
        }

        private void OnValidate()
        {
            Normalize();
            RebuildIndex();
        }

        public bool TryGetDestination(string destinationId, out ExpeditionDestinationDefinition destination)
        {
            destinationId = ExpeditionDestinationDefinition.NormalizeId(destinationId);
            if (string.IsNullOrWhiteSpace(destinationId))
            {
                destination = null;
                return false;
            }

            EnsureIndex();
            return destinationsById.TryGetValue(destinationId, out destination);
        }

        public void RebuildIndex()
        {
            destinationsById = new Dictionary<string, ExpeditionDestinationDefinition>(StringComparer.Ordinal);
            var source = Destinations;
            for (var i = 0; i < source.Count; i++)
            {
                var destination = source[i];
                if (destination == null || !destination.IsValid)
                {
                    continue;
                }

                if (destinationsById.ContainsKey(destination.DestinationId))
                {
                    Debug.LogWarning($"远征目的地 ID 重复，已忽略后续配置：{destination.DestinationId}", this);
                    continue;
                }

                destinationsById.Add(destination.DestinationId, destination);
            }
        }

        private void EnsureIndex()
        {
            if (destinationsById == null)
            {
                RebuildIndex();
            }
        }

        private void Normalize()
        {
            destinations ??= Array.Empty<ExpeditionDestinationDefinition>();
            for (var i = 0; i < destinations.Length; i++)
            {
                destinations[i]?.Normalize();
            }
        }
    }
}
