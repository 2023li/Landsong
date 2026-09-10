using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct ResourceProviderQuote { public Entity Entity; public ulong Id; public int Priority; public float Cost; public string Reason; }
    public sealed class ResourceNetworkQuote { public Entity Selected; public readonly System.Collections.Generic.List<ResourceProviderQuote> Candidates = new System.Collections.Generic.List<ResourceProviderQuote>(); }
    public static class ResourceNetworkOps
    {
        // A day-only weighted flood over baked cells. No colliders or scene-object lookups.
        public static Entity Provider(EntityManager em, Entity root, Entity consumer) => Quote(em, root, consumer).Selected;
        public static ResourceNetworkQuote Quote(EntityManager em, Entity root, Entity consumer)
        {
            var quote = new ResourceNetworkQuote();
            var grid = em.GetComponentData<GridData>(root);
            using var distance = BuildingRangeOps.Reach(em, root, consumer, Allocator.Temp);
            using var buildings = Sim.Entities<Building>(em);
            var best = Entity.Null; var bestCost = float.PositiveInfinity; var bestPriority = int.MinValue; ulong bestId = ulong.MaxValue;
            foreach (var e in buildings)
            {
                if (e == consumer || em.GetComponentData<BuildingStats>(e).IsProvider == 0) continue;
                var b = em.GetComponentData<Building>(e); var cost = float.PositiveInfinity; var id = em.GetComponentData<Identity>(e);
                for (var y = 0; y < b.Size.y; y++) for (var x = 0; x < b.Size.x; x++)
                { var at = GridOps.Index(grid, b.Cell + new int2(x, y)); if (at >= 0) cost = math.min(cost, distance[at]); }
                var priority = Sim.Definition(em, root, id.Definition).BuildingPolicy.ProviderPriority;
                var reason = !Sim.Operational(em, e) ? "未运营" : b.Maintained == 0 ? "维护未满足" : !BuildingRangeOps.IsProvider(em, e) ? "缺少工人" : !math.isfinite(cost) ? "断连或超过行动力" : "可连接";
                quote.Candidates.Add(new ResourceProviderQuote { Entity = e, Id = id.Id, Priority = priority, Cost = cost, Reason = reason });
                if (reason != "可连接") continue;
                if (priority > bestPriority || priority == bestPriority && (cost < bestCost || cost == bestCost && id.Id < bestId))
                { best = e; bestCost = cost; bestId = id.Id; bestPriority = priority; }
            }
            quote.Selected = best;
            quote.Candidates.Sort((a, b) => { var c = b.Priority.CompareTo(a.Priority); if (c == 0) c = a.Cost.CompareTo(b.Cost); return c != 0 ? c : a.Id.CompareTo(b.Id); });
            return quote;
        }
        public static void Record(EntityManager em, Entity root, Entity provider, int item, int amount)
        {
            if (provider == Entity.Null || amount <= 0) return;
            var b = em.GetComponentData<Building>(provider); var value = (long)Sim.Definition(em, root, item).Value * amount;
            b.MarketValue += value;
            var d = em.GetComponentData<Identity>(provider).Definition;
            if (Sim.Operational(em, provider) && b.Maintained != 0 && Sim.Rule(em, root, d, RuleKind.Market, b.Level).Level >= 0)
                b.MarketLifetimeValue = b.MarketLifetimeValue > long.MaxValue - value ? long.MaxValue : b.MarketLifetimeValue + value;
            em.SetComponentData(provider, b);
        }
        public static void PayRecord(EntityManager em, Entity root, Entity provider, int definition, RuleKind kind, int level)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (EconomyOps.Matches(r, kind, level)) Record(em, root, provider, r.Target, r.Amount); }
        }
        public static void SettleMarkets(EntityManager em, Entity root)
        {
            using var buildings = Sim.OrderedEntities<Building>(em);
            foreach (var e in buildings)
            {
                if (!Sim.Operational(em, e)) continue;
                using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Market);
                var b = em.GetComponentData<Building>(e); var market = Sim.Rule(em, root, em.GetComponentData<Identity>(e).Definition, RuleKind.Market, b.Level);
                if (market.Level >= 0 && b.MarketValue > 0)
                {
                    var amount = (int)math.floor(b.MarketValue * market.Value);
                    if (InventoryOps.Add(em, root, market.Target, amount) < amount) EconomyJournalOps.Note(em, root, "市场收益仅记录实际入库，溢出未获得");
                }
                b.MarketValue = 0; em.SetComponentData(e, b);
            }
        }
    }
}
