using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public sealed class InventorySaveData
    {
        [SerializeField] private InventorySlotData[] slots = Array.Empty<InventorySlotData>();

        public InventorySaveData()
        {
            slots = Array.Empty<InventorySlotData>();
        }

        public InventorySaveData(IEnumerable<InventorySlotData> slots)
        {
            var validSlots = new List<InventorySlotData>();
            if (slots != null)
            {
                foreach (var slot in slots)
                {
                    var normalized = slot.Normalized();
                    if (normalized.IsValid)
                    {
                        validSlots.Add(normalized);
                    }
                }
            }

            this.slots = validSlots.ToArray();
        }

        public IReadOnlyList<InventorySlotData> Slots => slots ?? Array.Empty<InventorySlotData>();
    }
}
