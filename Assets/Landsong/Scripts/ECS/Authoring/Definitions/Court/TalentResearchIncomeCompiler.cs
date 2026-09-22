using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentResearchIncomeCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentResearchIncomeSource source, ref global::Landsong.ECS.Definitions.TalentResearchIncome target, BuildingCatalogIndex buildingIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentResearchIncome 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            if (!math.isfinite(source.BasePoints))
                throw new InvalidOperationException("基础科研点必须是有限数值。");
            target.BasePoints = source.BasePoints;
            if (!math.isfinite(source.PerLevel))
                throw new InvalidOperationException("每级增加量必须是有限数值。");
            target.PerLevel = source.PerLevel;
            TalentEffectScalingCompiler.Compile(ref builder, source.Scaling, ref target.Scaling, buildingIndex, itemIndex);
        }
    }
}
