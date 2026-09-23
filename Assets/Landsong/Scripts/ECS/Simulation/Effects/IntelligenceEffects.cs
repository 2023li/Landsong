using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class IntelligenceEffects
    {
        public static EffectQuote Query(EntityManager em, Entity root)
        {
            var quote = new EffectQuote
            {
                Unit = EffectValueUnit.Flat
            };
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var entity in buildings)
            {
                var building = em.GetComponentData<Building>(entity);
                BuildingWorkforceState buildingWorkforce = em.GetComponentData<BuildingWorkforceState>(entity);
                BuildingMaintenanceState buildingMaintenance = em.GetComponentData<BuildingMaintenanceState>(entity);
                var identity = em.GetComponentData<Identity>(entity);
                ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(entity).Definition);
                if (!definition.Capabilities.Defence.Enabled)
                    continue;
                for (int i = 0; i < definition.Capabilities.Defence.Intelligence.Length; i++)
                {
                    var effect = definition.Capabilities.Defence.Intelligence[i];
                    string reason = !BuildingStatus.Operational(em, entity) ? "建筑未运营或起火" : buildingMaintenance.Maintained == 0 ? "维护未满足" : buildingWorkforce.Workers < effect.RequiredWorkers ? "工人不足（需要 " + effect.RequiredWorkers + "）" : RequiredTechnology(em, root, effect.Technology);
                    quote.Value += new EffectOrigin(EffectSourceKind.Building, building.Level, identity.Name + " / " + definition.Metadata.Name, reason, identity.Id).Apply(effect.Points, EffectContributions.LevelMatches(effect.Level, building.Level, EffectDomain.Intelligence), true, quote.Sources);
                }
            }

            EffectContributions.Visit(em, root, EffectDomain.Intelligence, (ref DefinitionEffects effects, EffectOrigin source) =>
            {
                for (int i = 0; i < effects.Intelligence.Length; i++)
                {
                    var effect = effects.Intelligence[i];
                    quote.Value += source.Apply(effect.Points, EffectContributions.LevelMatches(effect.Level, source.Level, EffectDomain.Intelligence), true, quote.Sources, RequiredTechnology(em, root, effect.RequiredTechnology));
                }
            });
            return quote;
        }

        static string RequiredTechnology(EntityManager em, Entity root, TechnologyId technology) => technology.IsValid && ResearchOps.Completed(em, root, technology) == 0 ? "需要科技：" + TechnologyDefinitions.Get(em, root, technology).Metadata.Name : "";
    }
}
