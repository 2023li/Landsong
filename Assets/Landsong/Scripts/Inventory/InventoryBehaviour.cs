using System;
using System.Collections.Generic;

namespace Landsong.InventorySystem
{
    [Obsolete("Use InventoryService owned by GameSystem instead.")]
    public sealed class InventoryBehaviour : InventoryService
    {
        public InventoryBehaviour(ItemCatalog itemCatalog, int slotCount, IEnumerable<ItemAmount> startingItems = null)
            : base(itemCatalog, slotCount, startingItems)
        {
        }
    }
}
