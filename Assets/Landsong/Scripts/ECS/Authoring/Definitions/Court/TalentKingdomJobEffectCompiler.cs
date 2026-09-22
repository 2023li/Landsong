using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentKingdomJobEffectCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentKingdomJobEffectSource source, ref global::Landsong.ECS.Definitions.TalentKingdomJobEffect target, BuildingCatalogIndex buildingIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentKingdomJobEffect 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Effect != KingdomEffectKind.PublicOpinion)
                throw new InvalidOperationException("人才王国任职效果只支持民意。");
            target.Order = source.Order;
            target.Effect = source.Effect;
            if (!math.isfinite(source.BaseMagnitude))
                throw new InvalidOperationException("基础效果必须是有限数值。");
            target.BaseMagnitude = source.BaseMagnitude;
            if (!math.isfinite(source.PerLevel))
                throw new InvalidOperationException("每级增加量必须是有限数值。");
            target.PerLevel = source.PerLevel;
            TalentEffectScalingCompiler.Compile(ref builder, source.Scaling, ref target.Scaling, buildingIndex, itemIndex);
        }
    }
}
