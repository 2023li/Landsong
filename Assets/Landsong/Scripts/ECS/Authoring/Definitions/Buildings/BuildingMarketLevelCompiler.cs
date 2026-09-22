using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingMarketLevelCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingMarketLevelSource source, ref global::Landsong.ECS.Definitions.BuildingMarketLevel target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingMarketLevel 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Currency = itemIndex.Resolve(source.Currency, true);
            target.ValuePerMarketPoint = source.ValuePerMarketPoint;
            if (!math.isfinite(source.IncomeRatio))
                throw new InvalidOperationException("配置项（IncomeRatio）必须是有限数值。");
            target.IncomeRatio = source.IncomeRatio;
        }
    }
}
