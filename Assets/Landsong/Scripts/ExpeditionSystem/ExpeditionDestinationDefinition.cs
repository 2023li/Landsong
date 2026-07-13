using System;
using System.Collections.Generic;
using System.Text;
using Landsong.BuildingSystem;
using Landsong.ConditionSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ExpeditionSystem
{
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

                var buildingId = building.FamilyId;
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
}
