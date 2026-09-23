using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;

namespace Landsong.ECS
{
    // One effective cost for both navigation and weighted building reach.
    public static class RoadWeatherCostOps
    {
        public const float SnowMultiplier = 1.3f;

        public static bool Snowing(EntityManager em, Entity root)
            => em.HasComponent<SeasonWeatherState>(root) && em.GetComponentData<SeasonWeatherState>(root).Weather == WeatherKind.Snow;

        public sealed class Context
        {
            readonly HashSet<ulong> roads = new HashSet<ulong>();
            public readonly bool Snowing;

            public Context(EntityManager em, Entity root)
            {
                Snowing = RoadWeatherCostOps.Snowing(em, root);
                if (!Snowing)
                    return;
                using var buildings = WorldQueries.Entities<Building>(em);
                foreach (var entity in buildings)
                {
                    var definition = em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
                    if ((BuildingDefinitions.Get(em, root, definition).PlacementAndVisuals.Category & BuildingCategory.Road) != 0)
                        roads.Add(em.GetComponentData<Identity>(entity).Id);
                }
            }

            public float Effective(Occupancy occupancy)
            {
                var baseCost = occupancy.MovementCost > 0 ? occupancy.MovementCost : 1;
                return Snowing && roads.Contains(occupancy.Owner) ? baseCost * SnowMultiplier : baseCost;
            }
        }
    }
}
