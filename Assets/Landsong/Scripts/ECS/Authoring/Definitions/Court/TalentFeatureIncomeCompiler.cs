using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentFeatureIncomeCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentFeatureIncomeSource source, ref global::Landsong.ECS.Definitions.TalentFeatureIncome target, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentFeatureIncome 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Feature = featureIndex.Resolve(source.Feature, false);
            if (!math.isfinite(source.BaseLevel))
                throw new InvalidOperationException("基础等级必须是有限数值。");
            target.BaseLevel = source.BaseLevel;
            if (!math.isfinite(source.PerLevel))
                throw new InvalidOperationException("每级增加量必须是有限数值。");
            target.PerLevel = source.PerLevel;
            TalentEffectScalingCompiler.Compile(ref builder, source.Scaling, ref target.Scaling, buildingIndex, itemIndex);
        }
    }
}
