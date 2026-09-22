using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    // Cross-domain facts are explicitly typed. Each owning domain remains responsible for its state.
    public static class PrerequisiteEvaluation
    {
        public static bool Satisfied(EntityManager em, Entity root, ref DefinitionPrerequisites requirements)
        {
            for (var i = 0; i < requirements.BuildingRequirements.Length; i++)
            {
                var entry = requirements.BuildingRequirements[i];
                if (!BuildingBlueprints.Has(em, root, entry.Building, entry.Required))
                    return false;
            }

            for (var i = 0; i < requirements.TechnologyRequirements.Length; i++)
            {
                var entry = requirements.TechnologyRequirements[i];
                if (entry.Required <= 0 || !TechnologyDefinitions.IsValid(em, root, entry.Technology) || TechnologyProgression.Read(em, root, entry.Technology).Completions < entry.Required)
                    return false;
            }

            for (var i = 0; i < requirements.BuffRequirements.Length; i++)
            {
                var entry = requirements.BuffRequirements[i];
                if (entry.Required <= 0 || PermanentBuffs.Level(em, root, entry.Buff) < entry.Required)
                    return false;
            }

            for (var i = 0; i < requirements.FeatureRequirements.Length; i++)
            {
                var entry = requirements.FeatureRequirements[i];
                if (entry.Required != 1 || !FeatureUnlocks.Has(em, root, entry.Feature))
                    return false;
            }

            for (var i = 0; i < requirements.QuestRequirements.Length; i++)
            {
                var entry = requirements.QuestRequirements[i];
                if (entry.Required != 1 || !QuestCompletions.Has(em, root, entry.Quest))
                    return false;
            }

            for (var i = 0; i < requirements.ExpeditionRequirements.Length; i++)
            {
                var entry = requirements.ExpeditionRequirements[i];
                if (entry.Required != 1 || !ExpeditionCompletions.Has(em, root, entry.Expedition))
                    return false;
            }

            return true;
        }
    }
}
