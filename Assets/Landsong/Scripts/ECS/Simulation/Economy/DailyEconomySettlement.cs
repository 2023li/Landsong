using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class DailyEconomySettlement
    {
        public static void Settle(EntityManager em, Entity root, bool forecast = false)
        {
            GameClock sessionClock = em.GetComponentData<GameClock>(root);
            DaySettlementState sessionSettlement = em.GetComponentData<DaySettlementState>(root);
            if (sessionSettlement.LastSettledTurn == sessionClock.Turn)
                return;
            sessionSettlement.LastSettledTurn = sessionClock.Turn;
            {
                em.SetComponentData(root, sessionClock);
                em.SetComponentData(root, sessionSettlement);
            }

            EconomyJournalOps.Begin(em, root, forecast);
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            // Stable, vertical building order; new construction cannot produce in this settlement.
            WorkforceSettlement.ReconcilePopulation(em, root);
            try
            {
                SocialOps.PayWages(em, root);
                foreach (var e in buildings)
                {
                    var b = em.GetComponentData<Building>(e);
                    if (b.Stage == LifeStage.Construction || b.Stage == LifeStage.Repairing)
                    {
                        ConstructionSettlement.Settle(em, root, e);
                        continue;
                    }

                    if (!BuildingStatus.Operational(em, e))
                        continue;
                    BuildingMaintenanceSettlement.Settle(em, root, e);
                    WorkforceSettlement.Settle(em, root, e);
                    BuildingProductionSettlement.Settle(em, root, e);
                    ResidentialSettlement.Settle(em, root, e);
                    BuildingExperienceSettlement.Settle(em, root, e);
                    SanctumOffering.Settle(em, root, e);
                }

                ResourceNetworkOps.SettleMarkets(em, root);
                InventoryDecay.Settle(em, root);
                TurnSettlement.Settle(em, root);
                WorkforceSettlement.ReconcilePopulation(em, root);
                GarrisonOps.ReconcileGarrisons(em, root);
                BuildingChangeNotifications.Publish(em, root);
            }
            finally
            {
                EconomyJournalOps.End(em, root);
            }

            if (!forecast)
                EconomyBillOps.CaptureSettlement(em, root);
        }
    }
}
