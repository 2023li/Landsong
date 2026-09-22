using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentItemJobEffectCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentItemJobEffectSource source, ref global::Landsong.ECS.Definitions.TalentItemJobEffect target, BuildingCatalogIndex buildingIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentItemJobEffect 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Effect != NumericEffectKind.ProductionMultiplier)
                throw new InvalidOperationException("人才物品任职效果只支持生产倍率。");
            target.Order = source.Order;
            target.Recipient = itemIndex.Resolve(source.Recipient, true);
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
