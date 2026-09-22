using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingRareProductionCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingRareProductionSource source, ref global::Landsong.ECS.Definitions.BuildingRareProduction target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingRareProduction 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Item = itemIndex.Resolve(source.Item, true);
            target.Quantity = source.Quantity;
            target.RequiredWorkers = source.RequiredWorkers;
            if (!math.isfinite(source.Probability))
                throw new InvalidOperationException("配置项（Probability）必须是有限数值。");
            target.Probability = source.Probability;
        }
    }
}
