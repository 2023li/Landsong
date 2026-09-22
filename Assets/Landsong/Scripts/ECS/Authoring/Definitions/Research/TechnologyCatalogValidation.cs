using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TechnologyCatalogValidation
    {
        public static void Validate(TechnologyCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少科技目录。");
            var members = new HashSet<TechnologyDefinitionAsset>(catalog.Definitions);
            var marks = new Dictionary<TechnologyDefinitionAsset, byte>();
            foreach (var asset in catalog.Definitions)
            {
                if (asset == null || asset.Prerequisites == null)
                    throw new InvalidOperationException("科技定义或前置配置为空。");
                var source = asset;
                var requirements = source.Prerequisites;
                if (requirements.BuildingRequirements.Length != 0 || requirements.BuffRequirements.Length != 0 || requirements.FeatureRequirements.Length != 0 || requirements.QuestRequirements.Length != 0 || requirements.ExpeditionRequirements.Length != 0)
                    throw new InvalidOperationException(source.Metadata.Id + "：科技前置只能引用科技完成记录。");
                if (!math.isfinite(source.TreePosition.x) || !math.isfinite(source.TreePosition.y) || source.TreePosition.x < 0 || source.TreePosition.y < 0)
                    throw new InvalidOperationException(source.Metadata.Id + "：科技树位置必须为非负有限坐标。");
                var parents = new HashSet<TechnologyDefinitionAsset>();
                foreach (var requirement in requirements.TechnologyRequirements)
                    if (requirement == null || requirement.Technology == null || requirement.Technology == asset || requirement.Required != 1 || !members.Contains(requirement.Technology) || !parents.Add(requirement.Technology))
                        throw new InvalidOperationException(source.Metadata.Id + "：科技前置无效、重复、引用自身或完成次数不是一。");
                foreach (var intelligence in source.Effects.Intelligence)
                    if (!source.Repeatable && intelligence.Level > 1)
                        throw new InvalidOperationException(source.Metadata.Id + "：不可重复科技的情报等级只能为零或一。");
            }

            void Visit(TechnologyDefinitionAsset asset)
            {
                if (marks.TryGetValue(asset, out var mark))
                {
                    if (mark == 1)
                        throw new InvalidOperationException(asset.Metadata.Id + "：科技前置存在循环。");
                    return;
                }

                marks[asset] = 1;
                foreach (var requirement in asset.Prerequisites.TechnologyRequirements)
                    Visit(requirement.Technology);
                marks[asset] = 2;
            }

            foreach (var asset in catalog.Definitions)
                Visit(asset);
        }
    }
}
