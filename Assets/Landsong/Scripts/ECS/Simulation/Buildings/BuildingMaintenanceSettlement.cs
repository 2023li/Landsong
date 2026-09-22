using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class BuildingMaintenanceSettlement
    {
        internal static void Settle(EntityManager em, Entity root, Entity e)
        {
            using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.Maintenance);
            var b = em.GetComponentData<Building>(e);
            BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(e);
            var definition = em.GetComponentData<BuildingDefinitionRef>(e).Definition;
            bMaintenance.Maintained = (byte)(BuildingCostOps.Pay(em, root, BuildingCostOps.Maintenance(em, root, definition, b.Level)) ? 1 : 0);
            if (bMaintenance.Maintained == 0)
                EconomyJournalOps.Note(em, root, "维护材料不足，按配置影响吸引力和损耗");
            {
                em.SetComponentData(e, b);
                em.SetComponentData(e, bMaintenance);
            }

            InventoryProviders.Provision(em, root, e);
        }
    }
}
