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
        public int Stacking;
    }
    public sealed class SpatialQuote { public float Value; public readonly List<SpatialContribution> Sources = new List<SpatialContribution>(); }
    public static class SpatialOps
    {
        // Same Manhattan footprint calculation and tie-break order for actual values and explanatory rows.
        public static SpatialQuote Quote(EntityManager em, Entity root, Entity target, int kind)
        {
            var quote = new SpatialQuote(); var targetBuilding = em.GetComponentData<Building>(target); var targetDefinition = em.GetComponentData<Identity>(target).Definition;
            var groups = new Dictionary<string, SpatialContribution>(); SpatialContribution highest = null;
            using var all = Sim.OrderedEntities<Building>(em);
            foreach (var e in all)
            {
                var id = em.GetComponentData<Identity>(e); var b = em.GetComponentData<Building>(e); var d = Sim.Definition(em, root, id.Definition);
                var gap = math.max(0, math.max(b.Cell - (targetBuilding.Cell + targetBuilding.Size - 1), targetBuilding.Cell - (b.Cell + b.Size - 1)));
                for (var i = 0; i < d.RuleCount; i++)
                {
                    var r = Sim.GetRule(em, root, d.RuleStart + i);
                    if (!EconomyOps.Matches(r, RuleKind.SpatialEffect, b.Level) || r.B != kind || gap.x + gap.y > r.Value) continue;
                    var line = new SpatialContribution { Source = id.Id, Amount = r.Amount, Group = r.Key.ToString(), Stacking = (int)r.Extra };
                    quote.Sources.Add(line);
                    if (!Sim.Operational(em, e)) { line.Reason = "未运营"; continue; }
                    if (b.Workers < r.C) { line.Reason = "工人未达到 " + r.C; continue; }
                    if (r.Target >= 0 && r.Target != targetDefinition) { line.Reason = "目标建筑不匹配"; continue; }
                    line.Reason = "已计入";
                    if (r.Extra == 10) line.Applied = r.Amount;
                    else if (r.Extra == 20)
                    {
                        if (r.Amount > 0 && (highest == null || r.Amount > highest.Amount)) { if (highest != null) { highest.Applied = 0; highest.Reason = "同类取最高，已被覆盖"; } highest = line; line.Applied = r.Amount; }
                        else line.Reason = "同类取最高，未叠加";
                    }
                    else
                    {
                        groups.TryGetValue(line.Group, out var prior);
                        if (r.Amount > 0 && (prior == null || r.Amount > prior.Amount)) { if (prior != null) { prior.Applied = 0; prior.Reason = "同效果组取最高，已被覆盖"; } groups[line.Group] = line; line.Applied = r.Amount; }
                        else line.Reason = "同效果组未叠加";
                    }
                }
            }
            foreach (var line in quote.Sources) quote.Value += line.Applied;
            return quote;
        }
    }
}
