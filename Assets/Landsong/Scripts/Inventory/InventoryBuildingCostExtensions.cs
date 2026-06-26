using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;

namespace Landsong.InventorySystem
{
    public static class InventoryBuildingCostExtensions
    {
        public static bool CanAffordConstructionTurnCosts(this Inventory inventory, BuildingDefinition definition)
        {
            return inventory.CanAffordConstructionTurnCosts(definition, 0);
        }

        public static bool CanAffordConstructionTurnCosts(this Inventory inventory, BuildingDefinition definition, int turnIndex)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return inventory.CanAffordBuildingCosts(definition.GetConstructionCostsForTurnIndex(turnIndex));
        }

        public static bool CanAffordOperatingTurnCosts(this Inventory inventory, BuildingDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return inventory.CanAffordBuildingCosts(definition.OperatingCostsPerTurn);
        }

        public static bool CanAffordBuildingCosts(this Inventory inventory, IEnumerable<BuildingCost> costs)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return inventory.HasItems(ToItemAmounts(costs));
        }

        public static bool TrySpendConstructionTurnCosts(this Inventory inventory, BuildingDefinition definition)
        {
            return inventory.TrySpendConstructionTurnCosts(definition, 0);
        }

        public static bool TrySpendConstructionTurnCosts(this Inventory inventory, BuildingDefinition definition, int turnIndex)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return inventory.TrySpendBuildingCosts(definition.GetConstructionCostsForTurnIndex(turnIndex));
        }

        public static bool TrySpendOperatingTurnCosts(this Inventory inventory, BuildingDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            return inventory.TrySpendBuildingCosts(definition.OperatingCostsPerTurn);
        }

        public static bool TrySpendBuildingCosts(this Inventory inventory, IEnumerable<BuildingCost> costs)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            return inventory.TryRemoveItems(ToItemAmounts(costs));
        }

        public static bool CanAffordConstructionTurnCosts(this InventoryBehaviour inventoryBehaviour, BuildingDefinition definition)
        {
            return inventoryBehaviour.CanAffordConstructionTurnCosts(definition, 0);
        }

        public static bool CanAffordConstructionTurnCosts(this InventoryBehaviour inventoryBehaviour, BuildingDefinition definition, int turnIndex)
        {
            if (inventoryBehaviour == null)
            {
                throw new ArgumentNullException(nameof(inventoryBehaviour));
            }

            return inventoryBehaviour.Inventory.CanAffordConstructionTurnCosts(definition, turnIndex);
        }

        public static bool CanAffordOperatingTurnCosts(this InventoryBehaviour inventoryBehaviour, BuildingDefinition definition)
        {
            if (inventoryBehaviour == null)
            {
                throw new ArgumentNullException(nameof(inventoryBehaviour));
            }

            return inventoryBehaviour.Inventory.CanAffordOperatingTurnCosts(definition);
        }

        public static bool CanAffordBuildingCosts(this InventoryBehaviour inventoryBehaviour, IEnumerable<BuildingCost> costs)
        {
            if (inventoryBehaviour == null)
            {
                throw new ArgumentNullException(nameof(inventoryBehaviour));
            }

            return inventoryBehaviour.Inventory.CanAffordBuildingCosts(costs);
        }

        public static bool TrySpendConstructionTurnCosts(this InventoryBehaviour inventoryBehaviour, BuildingDefinition definition)
        {
            return inventoryBehaviour.TrySpendConstructionTurnCosts(definition, 0);
        }

        public static bool TrySpendConstructionTurnCosts(this InventoryBehaviour inventoryBehaviour, BuildingDefinition definition, int turnIndex)
        {
            if (inventoryBehaviour == null)
            {
                throw new ArgumentNullException(nameof(inventoryBehaviour));
            }

            return inventoryBehaviour.Inventory.TrySpendConstructionTurnCosts(definition, turnIndex);
        }

        public static bool TrySpendOperatingTurnCosts(this InventoryBehaviour inventoryBehaviour, BuildingDefinition definition)
        {
            if (inventoryBehaviour == null)
            {
                throw new ArgumentNullException(nameof(inventoryBehaviour));
            }

            return inventoryBehaviour.Inventory.TrySpendOperatingTurnCosts(definition);
        }

        public static bool TrySpendBuildingCosts(this InventoryBehaviour inventoryBehaviour, IEnumerable<BuildingCost> costs)
        {
            if (inventoryBehaviour == null)
            {
                throw new ArgumentNullException(nameof(inventoryBehaviour));
            }

            return inventoryBehaviour.Inventory.TrySpendBuildingCosts(costs);
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
