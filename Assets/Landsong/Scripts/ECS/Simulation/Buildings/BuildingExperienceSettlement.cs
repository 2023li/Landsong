using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class BuildingExperienceSettlement
    {
        internal static void Settle(EntityManager em, Entity root, Entity e)
        {
            var state = em.GetComponentData<Building>(e);
            BuildingWorkforceState stateWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingHousingState stateHousing = em.GetComponentData<BuildingHousingState>(e);
            BuildingExperienceState stateExperience = em.GetComponentData<BuildingExperienceState>(e);
            BuildingMaintenanceState stateMaintenance = em.GetComponentData<BuildingMaintenanceState>(e);
            int population = em.GetComponentData<BuildingHousingStats>(e).MaxPopulation;
            if (stateMaintenance.Maintained == 0 || population > 0 && stateHousing.Population < population)
                return;
            ref var experience = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition).Capabilities.Upgrade.Experience;
            for (int i = 0; i < experience.Length; i++)
            {
                var row = experience[i];
                if ((row.Level == 0 || row.Level == state.Level) && stateWorkforce.Workers >= row.RequiredWorkers)
                    stateExperience.Experience += row.ExperiencePerTurn;
            }

            {
                em.SetComponentData(e, state);
                em.SetComponentData(e, stateWorkforce);
                em.SetComponentData(e, stateHousing);
                em.SetComponentData(e, stateExperience);
                em.SetComponentData(e, stateMaintenance);
            }
        }
    }
}
