using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class TalentEffectScalingOps
    {
        public static float Value(EntityManager em, Entity root, TalentEffectScaling scaling, float baseMagnitude, float perLevel, int level)
        {
            float value = baseMagnitude + perLevel * (level - 1);
            switch (scaling.Kind)
            {
                case TalentScalingKind.PerHundredItems:
                    if (scaling.SourceItem.IsValid)
                        value *= InventoryOps.Count(em, root, scaling.SourceItem) / 100f;
                    break;
                case TalentScalingKind.TalentLevel:
                    value *= level;
                    break;
                case TalentScalingKind.KingdomPopulation:
                    value *= PopulationOps.Population(em, root);
                    break;
                case TalentScalingKind.OperatingBuildings:
                    int count = 0;
                    using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                        foreach (var building in buildings)
                            if (BuildingStatus.Operational(em, building) && (!scaling.SourceBuilding.IsValid || em.GetComponentData<BuildingDefinitionRef>(building).Definition == scaling.SourceBuilding))
                                count++;
                    value *= count;
                    break;
            }

            return value;
        }
    }
}
