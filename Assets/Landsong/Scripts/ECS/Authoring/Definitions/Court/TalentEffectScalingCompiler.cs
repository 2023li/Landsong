using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentEffectScalingCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentEffectScalingSource source, ref global::Landsong.ECS.Definitions.TalentEffectScaling target, BuildingCatalogIndex buildingIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentEffectScaling 配置。");
            if (!Enum.IsDefined(typeof(TalentScalingKind), source.Kind))
                throw new InvalidOperationException("人才缩放方式无效。");
            if (source.Kind == TalentScalingKind.PerHundredItems && source.SourceItem == null)
                throw new InvalidOperationException("每百份物品缩放必须指定来源物品。");
            if (source.Kind != TalentScalingKind.PerHundredItems && source.SourceItem != null || source.Kind != TalentScalingKind.OperatingBuildings && source.SourceBuilding != null)
                throw new InvalidOperationException("缩放来源与缩放方式不匹配。");
            target.Kind = source.Kind;
            target.SourceItem = itemIndex.Resolve(source.SourceItem, true);
            target.SourceBuilding = buildingIndex.Resolve(source.SourceBuilding, true);
        }
    }
}
