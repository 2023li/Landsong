using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [CreateAssetMenu(
        menuName = "Landsong/Inventory/Slot Type Catalog",
        fileName = "InventorySlotTypeCatalog")]
    public sealed class InventorySlotTypeCatalog : ScriptableObject
    {
        [SerializeField, AssetsOnly, LabelText("槽位类型定义")]
        private InventorySlotTypeDefinition[] definitions =
            Array.Empty<InventorySlotTypeDefinition>();

        private Dictionary<InventorySlotType, InventorySlotTypeDefinition> definitionsByType;

        public IReadOnlyList<InventorySlotTypeDefinition> Definitions =>
            definitions ?? Array.Empty<InventorySlotTypeDefinition>();

        public bool TryGetDefinition(
            InventorySlotType slotType,
            out InventorySlotTypeDefinition definition)
        {
            EnsureIndex();
            return definitionsByType.TryGetValue(slotType, out definition);
        }

        public float CalculateLossRate(
            InventorySlotType slotType,
            ItemDefinition item,
            float runtimeLossRateMultiplier = 1f)
        {
            return TryGetDefinition(slotType, out var definition)
                ? definition.CalculateLossRate(item, runtimeLossRateMultiplier)
                : item == null
                    ? 0f
                    : Mathf.Clamp01(
                        item.LossRatePerTurn * Mathf.Max(0f, runtimeLossRateMultiplier));
        }

        public int GetAutoStorePriority(InventorySlotType slotType)
        {
            return TryGetDefinition(slotType, out var definition)
                ? definition.AutoStorePriority
                : 0;
        }

        public void ConfigureDefinitions(IEnumerable<InventorySlotTypeDefinition> source)
        {
            definitions = source == null
                ? Array.Empty<InventorySlotTypeDefinition>()
                : new List<InventorySlotTypeDefinition>(source).ToArray();
            RebuildIndex();
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

        private void OnValidate()
        {
            definitions ??= Array.Empty<InventorySlotTypeDefinition>();
            RebuildIndex();
        }

        private void EnsureIndex()
        {
            if (definitionsByType == null)
            {
                RebuildIndex();
            }
        }

        private void RebuildIndex()
        {
            definitionsByType =
                new Dictionary<InventorySlotType, InventorySlotTypeDefinition>();
            var values = Definitions;
            for (var i = 0; i < values.Count; i++)
            {
                var definition = values[i];
                if (definition == null || definitionsByType.ContainsKey(definition.SlotType))
                {
                    continue;
                }

                definitionsByType.Add(definition.SlotType, definition);
            }
        }
    }
}
