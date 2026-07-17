using System;
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
        FeatureLocked = 5,
        Hidden = 10,
        ConditionLocked = 30,
        AlreadyCompleted = 40
    }

    public enum ExpeditionStartFailureReason
    {
        None = 0,
        FeatureLocked = 5,
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
        FeatureLocked = 5,
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
}
