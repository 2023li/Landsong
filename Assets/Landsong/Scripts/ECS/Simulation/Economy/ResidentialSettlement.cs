using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class ResidentialSettlement
    {
        internal static void Settle(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Food);
            var b = em.GetComponentData<Building>(e);
            BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
            var stats = em.GetComponentData<BuildingHousingStats>(e);
            if (stats.MaxPopulation <= 0)
                return;
            var definition = em.GetComponentData<BuildingDefinitionRef>(e).Definition;
            ref var d = ref BuildingDefinitions.Get(em, root, definition);
            ref var housing = ref d.Capabilities.Housing;
            BuildingResidenceLevel residence = default;
            for (int i = 0; i < housing.Residences.Length; i++)
                if (housing.Residences[i].Level == 0 || housing.Residences[i].Level == b.Level)
                {
                    residence = housing.Residences[i];
                    break;
                }

            var fed = ResidentialFoodOps.Pay(em, root, e);
            var environment = true;
            for (int i = 0; i < housing.Environment.Length; i++)
            {
                var requirement = housing.Environment[i];
                if ((requirement.Level == 0 || requirement.Level == b.Level) && BuildingEnvironment.Value(em, root, e, requirement.Type) < requirement.RequiredValue)
                    environment = false;
            }

            if (!fed)
            {
                EconomyJournalOps.Note(em, root, "完整食谱不足，整栋不扣食物");
                bHousing.Growth = 0;
                bHousing.TaxProgress = 0;
                bHousing.FoodFailures++;
                if (bHousing.FoodFailures >= math.max(1, residence.StarvationThreshold))
                {
                    bHousing.Population = math.max(0, bHousing.Population - 1);
                    bHousing.FoodFailures = 0;
                }
            }
            else
            {
                bHousing.FoodFailures = 0;
                if (bHousing.Population < stats.MaxPopulation)
                {
                    if (!environment)
                        EconomyJournalOps.Note(em, root, "食物已满足，但环境不足，人口不增长");
                    bHousing.TaxProgress = 0;
                    if (environment && ++bHousing.Growth >= math.max(1, residence.GrowthInterval))
                    {
                        bHousing.Population++;
                        bHousing.Growth = 0;
                    }
                }
                else
                {
                    bHousing.Growth = 0;
                    int taxIndex = -1;
                    for (int i = 0; i < housing.Taxes.Length; i++)
                        if (housing.Taxes[i].Level == 0 || housing.Taxes[i].Level == b.Level)
                        {
                            taxIndex = i;
                            break;
                        }

                    var tax = taxIndex < 0 ? default : housing.Taxes[taxIndex];
                    if (taxIndex >= 0 && ++bHousing.TaxProgress >= math.max(1, tax.Interval))
                    {
                        using var taxScope = EconomyJournalOps.For(em, root, e, EconomyReason.Tax);
                        using var storage = new InventoryTransaction(em, root);
                        if (InventoryOps.Add(em, root, tax.Item, bHousing.Population * math.max(1, tax.PerResident)) == bHousing.Population * math.max(1, tax.PerResident))
                        {
                            bHousing.TaxProgress = 0;
                            storage.Commit();
                        }
                        else
                            storage.Reject("税收放不下，本期税收未入库");
                    }
                }
            }

            {
                em.SetComponentData(e, b);
                em.SetComponentData(e, bHousing);
            }
        }
    }
}
