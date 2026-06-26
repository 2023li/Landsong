using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public abstract class BuildingBehaviour : MonoBehaviour
    {
        [SerializeField] private BuildingDefinition definition;
        [SerializeField] private InventoryBehaviour inventory;
        [SerializeField, Min(0)] private int completedConstructionTurns;

        public BuildingDefinition Definition => definition;
        public InventoryBehaviour Inventory => inventory;
        public int CompletedConstructionTurns => completedConstructionTurns;
        public int ConstructionTurns => definition == null ? 0 : definition.ConstructionTurns;
        public bool HasDefinition => definition != null;
        public bool IsConstructionComplete => definition != null && completedConstructionTurns >= definition.ConstructionTurns;

        public event Action<BuildingBehaviour> StateChanged;
        public event Action<BuildingBehaviour, BuildingDefinition, BuildingDefinition> DefinitionChanged;

        protected virtual void OnValidate()
        {
            ClampConstructionProgress();
            NotifyStateChanged();
        }

        public void Initialize(BuildingDefinition newDefinition, InventoryBehaviour newInventory)
        {
            var previousDefinition = definition;
            definition = newDefinition;
            inventory = newInventory;
            ClampConstructionProgress();

            if (previousDefinition != definition)
            {
                DefinitionChanged?.Invoke(this, previousDefinition, definition);
            }

            NotifyStateChanged();
        }

        public void SetInventory(InventoryBehaviour newInventory)
        {
            inventory = newInventory;
            NotifyStateChanged();
        }

        public void SetConstructionProgress(int completedTurns)
        {
            completedConstructionTurns = Mathf.Max(0, completedTurns);
            ClampConstructionProgress();
            NotifyStateChanged();
        }

        public bool TryAdvanceConstructionTurn()
        {
            EnsureDefinition();

            if (IsConstructionComplete)
            {
                return true;
            }

            var turnIndex = completedConstructionTurns;
            var costs = definition.GetConstructionCostsForTurnIndex(turnIndex);
            if (!TrySpendCosts(costs))
            {
                OnConstructionTurnCostsFailed(turnIndex, costs);
                return false;
            }

            completedConstructionTurns++;
            OnConstructionTurnCostsConsumed(turnIndex, costs);

            if (IsConstructionComplete)
            {
                OnConstructionCompleted();
            }

            NotifyStateChanged();
            return true;
        }

        public bool TryConsumeOperatingTurnCosts()
        {
            EnsureDefinition();

            if (!IsConstructionComplete)
            {
                OnOperatingCostsConsumeFailed(definition.OperatingCostsPerTurn);
                return false;
            }

            var costs = definition.OperatingCostsPerTurn;
            if (!TrySpendCosts(costs))
            {
                OnOperatingCostsConsumeFailed(costs);
                return false;
            }

            OnOperatingCostsConsumed(costs);
            NotifyStateChanged();
            return true;
        }

        protected virtual void OnConstructionTurnCostsConsumed(int turnIndex, IReadOnlyList<BuildingCost> costs)
        {
        }

        protected virtual void OnConstructionTurnCostsFailed(int turnIndex, IReadOnlyList<BuildingCost> costs)
        {
        }

        protected virtual void OnConstructionCompleted()
        {
        }

        protected virtual void OnOperatingCostsConsumed(IReadOnlyList<BuildingCost> costs)
        {
        }

        protected virtual void OnOperatingCostsConsumeFailed(IReadOnlyList<BuildingCost> costs)
        {
        }

        protected void NotifyStateChanged()
        {
            StateChanged?.Invoke(this);
        }

        private bool TrySpendCosts(IReadOnlyList<BuildingCost> costs)
        {
            if (!HasAnyValidCost(costs))
            {
                return true;
            }

            return inventory != null && inventory.TrySpendBuildingCosts(costs);
        }

        private void EnsureDefinition()
        {
            if (definition == null)
            {
                throw new InvalidOperationException($"Building '{name}' has no BuildingDefinition assigned.");
            }
        }

        private void ClampConstructionProgress()
        {
            if (definition == null)
            {
                completedConstructionTurns = 0;
                return;
            }

            completedConstructionTurns = Mathf.Clamp(completedConstructionTurns, 0, definition.ConstructionTurns);
        }

        private static bool HasAnyValidCost(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null)
            {
                return false;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
