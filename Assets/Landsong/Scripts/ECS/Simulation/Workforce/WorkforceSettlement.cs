using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class WorkforceSettlement
    {
        public static bool Locked(EntityManager em, ulong site)
        {
            using var expeditions = WorldQueries.OrderedEntities<Expedition>(em);
            foreach (var e in expeditions)
                if (em.GetComponentData<Expedition>(e).Site == site && em.GetComponentData<Expedition>(e).Status == ExpeditionStatus.Travelling)
                    return true;
            return false;
        }

        public static float Attraction(EntityManager em, Entity root, Entity e)
        {
            return math.clamp(WorkforceOps.NaturalAttraction(em, root, e), 0, 100);
        }

        internal static void Settle(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Workforce);
            BuildingWorkforceStats statsWorkforce = em.GetComponentData<BuildingWorkforceStats>(e);
            if (statsWorkforce.Capacity <= 0)
                return;
            if (WorkforceSettlement.Locked(em, em.GetComponentData<Identity>(e).Id))
            {
                EconomyJournalOps.Note(em, root, "远征在途，岗位锁定");
                return;
            }

            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            var quote = WorkforceOps.Quote(em, root, e);
            var attraction = quote.Natural;
            bWorkforce.PaidSubsidy = 0;
            bWorkforce.PaidSubsidyTurn = em.GetComponentData<GameClock>(root).Turn;
            if (quote.SubsidyCost > 0)
            {
                if (InventoryOps.Remove(em, root, quote.Gold, quote.SubsidyCost))
                {
                    bWorkforce.PaidSubsidy = quote.SubsidyCost;
                    attraction = quote.Planned;
                }
                else
                {
                    SimulationEvents.Emit(em, root, EventKind.Message, "岗位补贴资金不足", em.GetComponentData<Identity>(e).Id, category: HistoryCategory.Economy);
                    EconomyJournalOps.Note(em, root, "补贴资金不足，未支付");
                }
            }

            bWorkforce.StableWorkers = WorkforceOps.Stable(statsWorkforce.Capacity, attraction);
            if (EconomyJournalOps.Forecast(em, root))
            {
                {
                    em.SetComponentData(e, bWorkforce);
                }

                EconomyJournalOps.Note(em, root, "预测沿用当前工人，实际可能招入或流失");
                InventoryProviders.Provision(em, root, e);
                return;
            }

            var roll = SimulationRandom.NextRandom(em, root) % 100;
            if (bWorkforce.Workers < bWorkforce.StableWorkers && PopulationOps.Population(em, root) > PopulationOps.Employed(em))
            {
                var chance = math.clamp(40 + attraction * .4f + (bWorkforce.StableWorkers - bWorkforce.Workers) * 30f / statsWorkforce.Capacity, 20, 95);
                if (roll < chance)
                    bWorkforce.Workers++;
            }
            else if (bWorkforce.Workers > bWorkforce.StableWorkers && em.GetComponentData<GameClock>(root).Turn > bWorkforce.ProtectionUntil)
            {
                var chance = math.clamp((bWorkforce.Workers - bWorkforce.StableWorkers) * 60f / statsWorkforce.Capacity + (100 - attraction) * .2f, 5, 70);
                if (roll < chance)
                    bWorkforce.Workers--;
            }

            {
                em.SetComponentData(e, bWorkforce);
            }

            InventoryProviders.Provision(em, root, e);
        }

        public static ResultCode ChangeWorkers(EntityManager em, Entity root, Entity e, int amount)
        {
            if (!BuildingStatus.Operational(em, e))
                return ResultCode.InvalidTarget;
            var q = WorkforceOps.Quote(em, root, e);
            var result = WorkforceOps.CanChange(q, amount);
            if (result != ResultCode.Success)
                return result;
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            if (amount > 0 && !InventoryOps.Remove(em, root, q.Gold, checked(q.RecruitCost * amount)))
                return ResultCode.InsufficientResources;
            bWorkforce.Workers += amount;
            {
                em.SetComponentData(e, bWorkforce);
            }

            InventoryProviders.Provision(em, root, e);
            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }

        public static void ReconcilePopulation(EntityManager em, Entity root)
        {
            var excess = PopulationOps.Employed(em) - PopulationOps.Population(em, root);
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            var ordered = new System.Collections.Generic.List<Entity>();
            foreach (var e in buildings)
                if (BuildingStatus.Operational(em, e))
                    ordered.Add(e);
            ordered.Sort((a, b) =>
            {
                var comparison = WorkforceSettlement.Attraction(em, root, a).CompareTo(WorkforceSettlement.Attraction(em, root, b));
                return comparison != 0 ? comparison : em.GetComponentData<Identity>(a).Id.CompareTo(em.GetComponentData<Identity>(b).Id);
            });
            // Housing losses release ordinary jobs, never named soldiers/heroes.
            foreach (var e in ordered)
            {
                if (excess <= 0)
                    break;
                BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                var remove = math.min(excess, bWorkforce.Workers);
                bWorkforce.Workers -= remove;
                excess -= remove;
                {
                    em.SetComponentData(e, bWorkforce);
                }

                InventoryProviders.Provision(em, root, e);
            }
        }
    }
}
