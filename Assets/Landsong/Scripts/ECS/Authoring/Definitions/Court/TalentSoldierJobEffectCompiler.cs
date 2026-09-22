using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentSoldierJobEffectCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentSoldierJobEffectSource source, ref global::Landsong.ECS.Definitions.TalentSoldierJobEffect target, BuildingCatalogIndex buildingIndex, ItemCatalogIndex itemIndex, SoldierCatalogIndex soldierIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentSoldierJobEffect 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Effect != NumericEffectKind.AttackMultiplier)
                throw new InvalidOperationException("人才军队任职效果只支持攻击倍率。");
            target.Order = source.Order;
            target.Recipient = soldierIndex.Resolve(source.Recipient, true);
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
