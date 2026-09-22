using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class DefinitionPrerequisitesCompiler
    {
        public static void Compile(ref BlobBuilder builder, DefinitionPrerequisitesSource source, ref global::Landsong.ECS.Definitions.DefinitionPrerequisites target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, QuestCatalogIndex questIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 DefinitionPrerequisites 配置。");
            if (source.BuildingRequirements == null)
                throw new InvalidOperationException("建筑条件列表不能为空引用。");
            var orderedBuildingRequirements = source.BuildingRequirements.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var BuildingRequirements = builder.Allocate(ref target.BuildingRequirements, orderedBuildingRequirements.Length);
            for (int i = 0; i < orderedBuildingRequirements.Length; i++)
            {
                BuildingRequirementCompiler.Compile(ref builder, orderedBuildingRequirements[i], ref BuildingRequirements[i], buildingIndex);
            }

            if (source.TechnologyRequirements == null)
                throw new InvalidOperationException("科技条件列表不能为空引用。");
            var orderedTechnologyRequirements = source.TechnologyRequirements.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var TechnologyRequirements = builder.Allocate(ref target.TechnologyRequirements, orderedTechnologyRequirements.Length);
            for (int i = 0; i < orderedTechnologyRequirements.Length; i++)
            {
                TechnologyRequirementCompiler.Compile(ref builder, orderedTechnologyRequirements[i], ref TechnologyRequirements[i], technologyIndex);
            }

            if (source.BuffRequirements == null)
                throw new InvalidOperationException("增益条件列表不能为空引用。");
            var orderedBuffRequirements = source.BuffRequirements.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var BuffRequirements = builder.Allocate(ref target.BuffRequirements, orderedBuffRequirements.Length);
            for (int i = 0; i < orderedBuffRequirements.Length; i++)
            {
                BuffRequirementCompiler.Compile(ref builder, orderedBuffRequirements[i], ref BuffRequirements[i], buffIndex);
            }

            if (source.FeatureRequirements == null)
                throw new InvalidOperationException("功能许可条件列表不能为空引用。");
            var orderedFeatureRequirements = source.FeatureRequirements.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var FeatureRequirements = builder.Allocate(ref target.FeatureRequirements, orderedFeatureRequirements.Length);
            for (int i = 0; i < orderedFeatureRequirements.Length; i++)
            {
                FeatureRequirementCompiler.Compile(ref builder, orderedFeatureRequirements[i], ref FeatureRequirements[i], featureIndex);
            }

            if (source.QuestRequirements == null)
                throw new InvalidOperationException("任务条件列表不能为空引用。");
            var orderedQuestRequirements = source.QuestRequirements.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var QuestRequirements = builder.Allocate(ref target.QuestRequirements, orderedQuestRequirements.Length);
            for (int i = 0; i < orderedQuestRequirements.Length; i++)
            {
                QuestRequirementCompiler.Compile(ref builder, orderedQuestRequirements[i], ref QuestRequirements[i], questIndex);
            }

            if (source.ExpeditionRequirements == null)
                throw new InvalidOperationException("远征条件列表不能为空引用。");
            var orderedExpeditionRequirements = source.ExpeditionRequirements.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var ExpeditionRequirements = builder.Allocate(ref target.ExpeditionRequirements, orderedExpeditionRequirements.Length);
            for (int i = 0; i < orderedExpeditionRequirements.Length; i++)
            {
                ExpeditionRequirementCompiler.Compile(ref builder, orderedExpeditionRequirements[i], ref ExpeditionRequirements[i], expeditionIndex);
            }
        }
    }
}
