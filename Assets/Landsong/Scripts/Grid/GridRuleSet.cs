using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    [CreateAssetMenu(menuName = "Landsong/Grid/Grid Rule Set", fileName = "GridRuleSet")]
    public sealed class GridRuleSet : ScriptableObject
    {
        [SerializeField, LabelText("默认地形 Key")]
        private string defaultTerrainKey = GridTerrainKeys.Land;

        [SerializeField, LabelText("默认可建造")]
        private bool defaultBuildable = true;

        [SerializeField, LabelText("普通格行动力消耗"), Min(1)]
        private int defaultTraversalActionCost = 10;

        [SerializeField, LabelText("地形行动力消耗")]
        private List<GridTraversalCostRule> traversalCostRules = new List<GridTraversalCostRule>
        {
            new GridTraversalCostRule(GridTerrainKeys.Road, 5),
            new GridTraversalCostRule(GridTerrainKeys.AdvancedRoad, 3)
        };

        public string DefaultTerrainKey
        {
            get
            {
                var normalized = GridTerrainKeys.Normalize(defaultTerrainKey);
                return string.IsNullOrEmpty(normalized) ? GridTerrainKeys.Land : normalized;
            }
        }

        public bool DefaultBuildable => defaultBuildable;
        public int DefaultTraversalActionCost => Mathf.Max(1, defaultTraversalActionCost);
        public IReadOnlyList<GridTraversalCostRule> TraversalCostRules =>
            traversalCostRules == null
                ? (IReadOnlyList<GridTraversalCostRule>)Array.Empty<GridTraversalCostRule>()
                : traversalCostRules;

        private void OnValidate()
        {
            defaultTerrainKey = DefaultTerrainKey;
            defaultTraversalActionCost = DefaultTraversalActionCost;
            traversalCostRules ??= new List<GridTraversalCostRule>();
            for (var i = 0; i < traversalCostRules.Count; i++)
            {
                traversalCostRules[i]?.Normalize(defaultTraversalActionCost);
            }
        }

        public int GetTraversalActionCost(string terrainKey)
        {
            terrainKey = GridTerrainKeys.Normalize(terrainKey);
            var best = DefaultTraversalActionCost;
            if (traversalCostRules == null)
            {
                return best;
            }

            for (var i = 0; i < traversalCostRules.Count; i++)
            {
                var rule = traversalCostRules[i];
                if (rule != null
                    && rule.IsValid
                    && string.Equals(rule.TerrainKey, terrainKey, StringComparison.Ordinal))
                {
                    best = Mathf.Min(best, rule.ActionCost);
                }
            }

            return Mathf.Max(1, best);
        }
    }
}
