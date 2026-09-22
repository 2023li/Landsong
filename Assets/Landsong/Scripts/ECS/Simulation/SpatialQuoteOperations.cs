using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class SpatialContribution
    {
        public ulong Source;
        public int Amount;
        public float Applied;
        public string Group, Reason;
        public BuildingEffectStacking Stacking;
    }

    public sealed class SpatialQuote
    {
        public float Value;
        public readonly List<SpatialContribution> Sources = new List<SpatialContribution>();
    }

    public static class SpatialOps
    {
        // Same Manhattan footprint calculation and tie-break order for actual values and explanatory rows.
        public static SpatialQuote Quote(EntityManager em, Entity root, Entity target, BuildingEnvironmentKind kind)
        {
            var quote = new SpatialQuote();
            BuildingPlacementState targetBuildingPlacement = em.GetComponentData<BuildingPlacementState>(target);
            var targetDefinition = em.GetComponentData<BuildingDefinitionRef>(target).Definition;
            var groups = new Dictionary<string, SpatialContribution>();
            SpatialContribution highest = null;
            using var all = WorldQueries.OrderedEntities<Building>(em);
            foreach (var e in all)
            {
                var id = em.GetComponentData<Identity>(e);
                var b = em.GetComponentData<Building>(e);
                BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                ref var effects = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition).Capabilities.Effects.Spatial;
                var gap = math.max(0, math.max(bPlacement.Cell - (targetBuildingPlacement.Cell + targetBuildingPlacement.Size - 1), targetBuildingPlacement.Cell - (bPlacement.Cell + bPlacement.Size - 1)));
                for (var i = 0; i < effects.Length; i++)
                {
                    var r = effects[i];
                    if (r.Level != 0 && r.Level != b.Level || r.Type != kind || gap.x + gap.y > r.Radius)
                        continue;
                    var line = new SpatialContribution
                    {
                        Source = id.Id,
                        Amount = r.Magnitude,
                        Group = r.Group.ToString(),
                        Stacking = r.Stacking
                    };
                    quote.Sources.Add(line);
                    if (!BuildingStatus.Operational(em, e))
                    {
                        line.Reason = "未运营";
                        continue;
                    }

                    if (bWorkforce.Workers < r.RequiredWorkers)
                    {
                        line.Reason = "工人未达到 " + r.RequiredWorkers;
                        continue;
                    }

                    if (r.Building.IsValid && r.Building != targetDefinition)
                    {
                        line.Reason = "目标建筑不匹配";
                        continue;
                    }

                    line.Reason = "已计入";
                    if (r.Stacking == BuildingEffectStacking.Additive)
                        line.Applied = r.Magnitude;
                    else if (r.Stacking == BuildingEffectStacking.HighestOfKind)
                    {
                        if (r.Magnitude > 0 && (highest == null || r.Magnitude > highest.Amount))
                        {
                            if (highest != null)
                            {
                                highest.Applied = 0;
                                highest.Reason = "同类取最高，已被覆盖";
                            }

                            highest = line;
                            line.Applied = r.Magnitude;
                        }
                        else
                            line.Reason = "同类取最高，未叠加";
                    }
                    else
                    {
                        groups.TryGetValue(line.Group, out var prior);
                        if (r.Magnitude > 0 && (prior == null || r.Magnitude > prior.Amount))
                        {
                            if (prior != null)
                            {
                                prior.Applied = 0;
                                prior.Reason = "同效果组取最高，已被覆盖";
                            }

                            groups[line.Group] = line;
                            line.Applied = r.Magnitude;
                        }
                        else
                            line.Reason = "同效果组未叠加";
                    }
                }
            }

            foreach (var line in quote.Sources)
                quote.Value += line.Applied;
            return quote;
        }
    }
}
