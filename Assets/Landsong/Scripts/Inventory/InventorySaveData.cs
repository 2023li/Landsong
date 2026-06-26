using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public sealed class InventorySaveData
    {
        [SerializeField, Min(0)] private int slotCount;
        [SerializeField] private InventorySlotData[] slots = Array.Empty<InventorySlotData>();

        public InventorySaveData()
        {
            slotCount = 0;
            slots = Array.Empty<InventorySlotData>();
        }

        public InventorySaveData(int slotCount, IEnumerable<InventorySlotData> slots)
        {
            this.slotCount = Mathf.Max(0, slotCount);

            var validSlots = new List<InventorySlotData>();
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    var normalized = slot.Normalized();
                    if (normalized.IsValid && normalized.SlotIndex < this.slotCount)
                    {
                        validSlots.Add(normalized);
                    }
                }
            }

            this.slots = validSlots.ToArray();
        }

        public int SlotCount => slotCount;
        public IReadOnlyList<InventorySlotData> Slots => slots ?? Array.Empty<InventorySlotData>();
    }
}
