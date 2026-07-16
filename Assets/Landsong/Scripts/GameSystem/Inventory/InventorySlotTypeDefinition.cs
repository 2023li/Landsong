using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [CreateAssetMenu(
        menuName = "Landsong/Inventory/Slot Type Definition",
        fileName = "InventorySlotTypeDefinition")]
    public sealed class InventorySlotTypeDefinition : ScriptableObject
    {
        [SerializeField, LabelText("槽位类型")]
        private InventorySlotType slotType;

        [SerializeField, LabelText("名称")]
        private string displayName;

        [SerializeField, LabelText("基础损耗倍率"), Min(0f)]
        private float baseLossRateMultiplier = 1f;

        [SerializeField, LabelText("分类损耗修正")]
        private InventorySlotLossModifier[] lossModifiers =
            Array.Empty<InventorySlotLossModifier>();

        [SerializeField, LabelText("自动存放优先级")]
        private int autoStorePriority;

        public InventorySlotType SlotType => slotType;
        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? slotType.ToString() : displayName.Trim();
        public float BaseLossRateMultiplier => Mathf.Max(0f, baseLossRateMultiplier);
        public IReadOnlyList<InventorySlotLossModifier> LossModifiers =>
            lossModifiers ?? Array.Empty<InventorySlotLossModifier>();
        public int AutoStorePriority => autoStorePriority;

        public float CalculateLossRate(ItemDefinition item, float runtimeLossRateMultiplier = 1f)
        {
            if (item == null || item.LossRatePerTurn <= 0f)
            {
                return 0f;
            }

            var groupMultiplier = 1f;
            var modifiers = LossModifiers;
            for (var i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].Matches(item))
                {
                    groupMultiplier *= modifiers[i].LossRateMultiplier;
                }
            }

            return Mathf.Clamp01(
                item.LossRatePerTurn
                * BaseLossRateMultiplier
                * Mathf.Max(0f, runtimeLossRateMultiplier)
                * groupMultiplier);
        }

        public void Configure(
            InventorySlotType type,
            string typeDisplayName,
            float lossRateMultiplier,
            IEnumerable<InventorySlotLossModifier> typeLossModifiers,
            int storagePriority)
        {
            slotType = type;
            displayName = string.IsNullOrWhiteSpace(typeDisplayName)
                ? string.Empty
                : typeDisplayName.Trim();
            baseLossRateMultiplier = Mathf.Max(0f, lossRateMultiplier);
            lossModifiers = typeLossModifiers == null
                ? Array.Empty<InventorySlotLossModifier>()
                : new List<InventorySlotLossModifier>(typeLossModifiers).ToArray();
            autoStorePriority = storagePriority;
            Normalize();
        }

        private void OnValidate()
        {
            Normalize();
        }

        private void Normalize()
        {
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? string.Empty
                : displayName.Trim();
            baseLossRateMultiplier = Mathf.Max(0f, baseLossRateMultiplier);
            lossModifiers ??= Array.Empty<InventorySlotLossModifier>();
        }
    }
}
