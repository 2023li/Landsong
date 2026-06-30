using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;

namespace Landsong.InventorySystem
{
    public static class InventoryBuildingCostExtensions
    {
        public static bool CanAffordBuildingCosts(this Inventory inventory, IEnumerable<BuildingCost> costs)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return inventory.HasItems(ToItemAmounts(costs));
        }

        public static bool TrySpendBuildingCosts(this Inventory inventory, IEnumerable<BuildingCost> costs)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return inventory.TryRemoveItems(ToItemAmounts(costs));
        }

        public static bool CanAffordBuildingCosts(this InventoryService inventoryService, IEnumerable<BuildingCost> costs)
        {
            if (inventoryService == null)
            {
                throw new ArgumentNullException(nameof(inventoryService));
            }

            return inventoryService.Inventory.CanAffordBuildingCosts(costs);
        }

        public static bool TrySpendBuildingCosts(this InventoryService inventoryService, IEnumerable<BuildingCost> costs)
        {
            if (inventoryService == null)
            {
                throw new ArgumentNullException(nameof(inventoryService));
            }

            return inventoryService.Inventory.TrySpendBuildingCosts(costs);
        }

        private static IEnumerable<ItemAmount> ToItemAmounts(IEnumerable<BuildingCost> costs)
        {
            if (costs == null)
            {
                yield break;
            }

            foreach (var cost in costs)
            {
                if (cost.IsValid)
                {
                    yield return new ItemAmount(cost.ItemDefinition, cost.Amount);
                }
            }
        }
    }
}
