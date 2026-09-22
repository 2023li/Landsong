using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    internal static class DefinitionPrerequisiteText
    {
        internal static List<string> Lines(EntityManager em, Entity root, ref DefinitionPrerequisites requirements)
        {
            var result = new List<(int Order, string Text)>();
            for (int i = 0; i < requirements.BuildingRequirements.Length; i++)
            {
                var entry = requirements.BuildingRequirements[i];
                result.Add((entry.Order, "前置：" + BuildingDefinitions.Get(em, root, entry.Building).Metadata.Name + (BuildingBlueprints.Has(em, root, entry.Building, entry.Required) ? "（已满足）" : "（未满足）")));
            }

            for (int i = 0; i < requirements.TechnologyRequirements.Length; i++)
            {
                var entry = requirements.TechnologyRequirements[i];
                result.Add((entry.Order, "前置：" + TechnologyDefinitions.Get(em, root, entry.Technology).Metadata.Name + (ResearchOps.Completed(em, root, entry.Technology) >= entry.Required ? "（已满足）" : "（未满足）")));
            }

            for (int i = 0; i < requirements.BuffRequirements.Length; i++)
            {
                var entry = requirements.BuffRequirements[i];
                result.Add((entry.Order, "前置：" + BuffDefinitions.Get(em, root, entry.Buff).Metadata.Name + (PermanentBuffs.Level(em, root, entry.Buff) >= entry.Required ? "（已满足）" : "（未满足）")));
            }

            for (int i = 0; i < requirements.FeatureRequirements.Length; i++)
            {
                var entry = requirements.FeatureRequirements[i];
                result.Add((entry.Order, "前置：" + FeatureDefinitions.Get(em, root, entry.Feature).Metadata.Name + (FeatureUnlocks.Has(em, root, entry.Feature) ? "（已满足）" : "（未满足）")));
            }

            for (int i = 0; i < requirements.QuestRequirements.Length; i++)
            {
                var entry = requirements.QuestRequirements[i];
                result.Add((entry.Order, "前置：" + QuestDefinitions.Get(em, root, entry.Quest).Metadata.Name + (QuestCompletions.Has(em, root, entry.Quest) ? "（已满足）" : "（未满足）")));
            }

            for (int i = 0; i < requirements.ExpeditionRequirements.Length; i++)
            {
                var entry = requirements.ExpeditionRequirements[i];
                result.Add((entry.Order, "前置：" + ExpeditionDefinitions.Get(em, root, entry.Expedition).Metadata.Name + (ExpeditionCompletions.Has(em, root, entry.Expedition) ? "（已满足）" : "（未满足）")));
            }

            return result.OrderBy(entry => entry.Order).Select(entry => entry.Text).ToList();
        }
    }
}
