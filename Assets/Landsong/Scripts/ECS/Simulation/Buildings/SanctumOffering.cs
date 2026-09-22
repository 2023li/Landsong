using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class SanctumOffering
    {
        internal static void Settle(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Offering);
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingSanctumState bSanctum = em.GetComponentData<BuildingSanctumState>(e);
            if (bSanctum.Offering == 0)
                return;
            BuildingSanctumStats statsSanctum = em.GetComponentData<BuildingSanctumStats>(e);
            if (!statsSanctum.Hero.IsValid)
                return;
            if (bWorkforce.Workers < statsSanctum.RequiredWorkers)
            {
                EconomyJournalOps.Note(em, root, "工人未达到供奉要求，本次未支付");
                return;
            }

            using var heroes = WorldQueries.OrderedEntities<Hero>(em);
            var id = em.GetComponentData<Identity>(e).Id;
            foreach (var hero in heroes)
            {
                var h = em.GetComponentData<Hero>(hero);
                if (h.Sanctum != id || h.Recruited == 0)
                    continue;
                ref var heroDefinition = ref HeroDefinitions.Get(em, root, statsSanctum.Hero);
                var costs = new System.Collections.Generic.List<BuildingCost>();
                for (int i = 0; i < heroDefinition.OfferingCosts.Length; i++)
                {
                    var cost = heroDefinition.OfferingCosts[i];
                    if (cost.Level == 0 || cost.Level == 1)
                        BuildingCostOps.Add(costs, cost.Item, cost.Quantity);
                }

                if (!BuildingCostOps.Pay(em, root, costs))
                {
                    bSanctum.Offering = 0;
                    SimulationEvents.Emit(em, root, EventKind.Message, "金币不足，持续供奉已中断", id, category: HistoryCategory.Economy);
                    EconomyJournalOps.Note(em, root, "供奉资金不足，持续供奉中断");
                }
                else
                {
                    bSanctum.PaidOfferingTurn = em.GetComponentData<GameClock>(root).Turn;
                    var growth = heroDefinition.Growth;
                    int before = h.Experience;
                    h.Experience = HeroOps.AddHeroExperience(growth, h.Experience, growth.OfferingExperience);
                    em.SetComponentData(hero, h);
                    var heroId = em.GetComponentData<Identity>(hero);
                    em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.HeroOfferingExperience, Id = heroId.Id, Amount = h.Experience - before, SourceName = heroId.Name });
                    foreach (var cost in costs)
                        em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.HeroOfferingCost, Id = heroId.Id, Item = cost.Item, Amount = cost.Amount, SourceName = heroId.Name });
                }

                {
                    em.SetComponentData(e, bWorkforce);
                    em.SetComponentData(e, bSanctum);
                }

                break;
            }
        }
    }
}
