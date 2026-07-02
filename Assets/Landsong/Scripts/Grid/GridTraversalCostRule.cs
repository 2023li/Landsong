using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    [Serializable]
    public sealed class GridTraversalCostRule
    {
        [SerializeField, LabelText("地形 Key")] private string terrainKey;
        [SerializeField, LabelText("行动力消耗"), Min(1)] private int actionCost = 10;

        public GridTraversalCostRule()
        {
        }

        public GridTraversalCostRule(string terrainKey, int actionCost)
        {
            this.terrainKey = terrainKey;
            this.actionCost = Mathf.Max(1, actionCost);
        }

        public string TerrainKey => GridTerrainKeys.Normalize(terrainKey);
        public int ActionCost => Mathf.Max(1, actionCost);
        public bool IsValid => !string.IsNullOrWhiteSpace(TerrainKey);

        public void Normalize(int fallbackActionCost)
        {
            terrainKey = GridTerrainKeys.Normalize(terrainKey);
            actionCost = Mathf.Max(1, actionCost > 0 ? actionCost : fallbackActionCost);
        }
    }
}
