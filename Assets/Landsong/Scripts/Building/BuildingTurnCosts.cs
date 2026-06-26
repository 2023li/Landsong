using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    public struct BuildingTurnCosts
    {
        public static readonly BuildingTurnCosts Empty = new BuildingTurnCosts(Array.Empty<BuildingCost>());

        [SerializeField] private BuildingCost[] costs;

        public BuildingTurnCosts(BuildingCost[] costs)
        {
            this.costs = costs ?? Array.Empty<BuildingCost>();
        }

        public IReadOnlyList<BuildingCost> Costs => costs ?? Array.Empty<BuildingCost>();

        public BuildingTurnCosts Normalized()
        {
            if (costs == null)
            {
                return Empty;
            }

            for (var i = 0; i < costs.Length; i++)
            {
                costs[i] = costs[i].Normalized();
            }

            return this;
        }
    }
}
