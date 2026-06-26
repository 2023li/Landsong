using System;
using System.Collections.Generic;
using Landsong.GridSystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace Landsong.BuildingSystem
{
    [CreateAssetMenu(menuName = "Landsong/Building/Building Definition", fileName = "BuildingDefinition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        [SerializeField] private string buildingId;
        [SerializeField] private string displayName;
        [SerializeField] private Sprite icon;
        [SerializeField] private Vector2Int size = Vector2Int.one;
        [SerializeField] private string prefabAddressableKey;
        [SerializeField, Min(1)] private int constructionTurns = 1;
        [SerializeField, FormerlySerializedAs("constructionCostsPerTurn")] private BuildingTurnCosts[] constructionCostsByTurn = { BuildingTurnCosts.Empty };
        [SerializeField] private BuildingCost[] operatingCostsPerTurn = Array.Empty<BuildingCost>();
        [SerializeField] private BuildingCategory category = BuildingCategory.None;
        [SerializeField] private bool allowUpgrade;
        [SerializeField] private BuildingDefinition upgradeTargetDefinition;

        public string BuildingId => buildingId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public Sprite Icon => icon;
        public Vector2Int Size => size;
        public string PrefabAddressableKey => prefabAddressableKey;
        public int ConstructionTurns => constructionTurns;
        public IReadOnlyList<BuildingTurnCosts> ConstructionCostsByTurn => constructionCostsByTurn ?? Array.Empty<BuildingTurnCosts>();
        public IReadOnlyList<BuildingCost> OperatingCostsPerTurn => operatingCostsPerTurn ?? Array.Empty<BuildingCost>();
        public BuildingCategory Category => category;
        public bool AllowUpgrade => allowUpgrade;
        public BuildingDefinition UpgradeTargetDefinition => upgradeTargetDefinition;
        public bool HasIcon => icon != null;
        public bool HasPrefabAddress => !string.IsNullOrWhiteSpace(prefabAddressableKey);
        public bool HasUpgradeTarget => allowUpgrade && upgradeTargetDefinition != null;

        public GridFootprint CreateFootprint(GridPosition origin)
        {
            return new GridFootprint(origin, size);
        }

        public IReadOnlyList<BuildingCost> GetConstructionCostsForTurnIndex(int turnIndex)
        {
            if (turnIndex < 0 || turnIndex >= constructionTurns)
            {
                throw new ArgumentOutOfRangeException(nameof(turnIndex), $"Construction turn index {turnIndex} is outside 0..{constructionTurns - 1}.");
            }

            if (constructionCostsByTurn == null || turnIndex >= constructionCostsByTurn.Length)
            {
                return Array.Empty<BuildingCost>();
            }

            return constructionCostsByTurn[turnIndex].Costs;
        }

        private void OnValidate()
        {
            buildingId = string.IsNullOrWhiteSpace(buildingId) ? string.Empty : buildingId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            prefabAddressableKey = string.IsNullOrWhiteSpace(prefabAddressableKey) ? string.Empty : prefabAddressableKey.Trim();
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            constructionTurns = Mathf.Max(1, constructionTurns);
            constructionCostsByTurn = ResizeConstructionCostsByTurn(constructionCostsByTurn, constructionTurns);

            for (var i = 0; i < constructionCostsByTurn.Length; i++)
            {
                constructionCostsByTurn[i] = constructionCostsByTurn[i].Normalized();
            }

            if (operatingCostsPerTurn == null)
            {
                operatingCostsPerTurn = Array.Empty<BuildingCost>();
                return;
            }

            for (var i = 0; i < operatingCostsPerTurn.Length; i++)
            {
                operatingCostsPerTurn[i] = operatingCostsPerTurn[i].Normalized();
            }
        }

        private static BuildingTurnCosts[] ResizeConstructionCostsByTurn(BuildingTurnCosts[] current, int targetLength)
        {
            var resized = new BuildingTurnCosts[targetLength];
            current ??= Array.Empty<BuildingTurnCosts>();

            for (var i = 0; i < resized.Length; i++)
            {
                resized[i] = i < current.Length ? current[i] : BuildingTurnCosts.Empty;
            }

            return resized;
        }
    }
}
