using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct ResourceProviderQuote
    {
        public Entity Entity;
        public ulong Id;
        public int Priority;
        public float Cost;
        public string Reason;
    }

    public sealed class ResourceNetworkQuote
    {
        public Entity Selected;
        public readonly System.Collections.Generic.List<ResourceProviderQuote> Candidates = new System.Collections.Generic.List<ResourceProviderQuote>();
    }

    public static class ResourceNetworkOps
    {
        // A day-only weighted flood over baked cells. No colliders or scene-object lookups.
        public static Entity Provider(EntityManager em, Entity root, Entity consumer) => Quote(em, root, consumer).Selected;
        public static ResourceNetworkQuote Quote(EntityManager em, Entity root, Entity consumer)
        {
            var quote = new ResourceNetworkQuote();
            var grid = em.GetComponentData<GridData>(root);
            using var distance = BuildingRangeOps.Reach(em, root, consumer, Allocator.Temp);
            using var buildings = WorldQueries.Entities<Building>(em);
            var best = Entity.Null;
            var bestCost = float.PositiveInfinity;
            var bestPriority = int.MinValue;
            ulong bestId = ulong.MaxValue;
            foreach (var e in buildings)
            {
                if (e == consumer || em.GetComponentData<BuildingStorageStats>(e).IsProvider == 0)
                    continue;
                BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(e);
                BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(e);
                var cost = float.PositiveInfinity;
                var id = em.GetComponentData<Identity>(e);
                for (var y = 0; y < bPlacement.Size.y; y++)
                    for (var x = 0; x < bPlacement.Size.x; x++)
                    {
                        var at = GridOps.Index(grid, bPlacement.Cell + new int2(x, y));
                        if (at >= 0)
                            cost = math.min(cost, distance[at]);
                    }

                var priority = BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition).PlacementAndVisuals.ProviderPriority;
                var reason = !BuildingStatus.Operational(em, e) ? "未运营" : bMaintenance.Maintained == 0 ? "维护未满足" : !BuildingRangeOps.IsProvider(em, e) ? "缺少工人" : !math.isfinite(cost) ? "断连或超过行动力" : "可连接";
                quote.Candidates.Add(new ResourceProviderQuote { Entity = e, Id = id.Id, Priority = priority, Cost = cost, Reason = reason });
                if (reason != "可连接")
                    continue;
                if (priority > bestPriority || priority == bestPriority && (cost < bestCost || cost == bestCost && id.Id < bestId))
                {
                    best = e;
                    bestCost = cost;
                    bestId = id.Id;
                    bestPriority = priority;
                }
            }

            quote.Selected = best;
            quote.Candidates.Sort((a, b) =>
            {
                var c = b.Priority.CompareTo(a.Priority);
                if (c == 0)
                    c = a.Cost.CompareTo(b.Cost);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            return quote;
        }

        public static void Record(EntityManager em, Entity root, Entity provider, ItemId item, int amount)
        {
            if (provider == Entity.Null || amount <= 0)
                return;
            var b = em.GetComponentData<Building>(provider);
            BuildingMarketState bMarket = em.GetComponentData<BuildingMarketState>(provider);
            BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(provider);
            var value = (long)ItemDefinitions.Get(em, root, item).TradeValue * amount;
            bMarket.TurnValue += value;
            var d = em.GetComponentData<BuildingDefinitionRef>(provider).Definition;
            if (BuildingStatus.Operational(em, provider) && bMaintenance.Maintained != 0 && TryMarket(em, root, d, b.Level, out _))
                bMarket.LifetimeValue = bMarket.LifetimeValue > long.MaxValue - value ? long.MaxValue : bMarket.LifetimeValue + value;
            {
                em.SetComponentData(provider, b);
                em.SetComponentData(provider, bMarket);
                em.SetComponentData(provider, bMaintenance);
            }
        }

        public static void PayRecord(EntityManager em, Entity root, Entity provider, List<BuildingCost> costs)
        {
            foreach (var cost in costs)
                Record(em, root, provider, cost.Item, cost.Amount);
        }

        static bool TryMarket(EntityManager em, Entity root, BuildingId definition, int level, out BuildingMarketLevel market)
        {
            ref var levels = ref BuildingDefinitions.Get(em, root, definition).Capabilities.Market.Levels;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].Level == 0 || levels[i].Level == level)
                {
                    market = levels[i];
                    return true;
                }

            market = default;
            return false;
        }

        public static void SettleMarkets(EntityManager em, Entity root)
        {
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var e in buildings)
            {
                if (!BuildingStatus.Operational(em, e))
                    continue;
                using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Market);
                var b = em.GetComponentData<Building>(e);
                BuildingMarketState bMarket = em.GetComponentData<BuildingMarketState>(e);
                bool hasMarket = TryMarket(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition, b.Level, out var market);
                if (hasMarket && bMarket.TurnValue > 0)
                {
                    var amount = (int)math.floor(bMarket.TurnValue * market.IncomeRatio);
                    if (InventoryOps.Add(em, root, market.Currency, amount) < amount)
                        EconomyJournalOps.Note(em, root, "市场收益仅记录实际入库，溢出未获得");
                }

                bMarket.TurnValue = 0;
                {
                    em.SetComponentData(e, b);
                    em.SetComponentData(e, bMarket);
                }
            }
        }
    }
}
