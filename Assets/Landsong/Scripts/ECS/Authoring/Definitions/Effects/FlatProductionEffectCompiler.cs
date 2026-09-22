using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class FlatProductionEffectCompiler
    {
        public static void Compile(ref BlobBuilder builder, FlatProductionEffectSource source, ref global::Landsong.ECS.Definitions.FlatProductionEffect target, BuildingCatalogIndex buildingIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 FlatProductionEffect 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Order = source.Order;
            target.Level = source.Level;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.Building = buildingIndex.Resolve(source.Building, true);
            target.Quantity = source.Quantity;
        }
    }
}
