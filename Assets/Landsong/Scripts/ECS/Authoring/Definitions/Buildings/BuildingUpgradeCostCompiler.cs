using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingUpgradeCostCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingUpgradeCostSource source, ref global::Landsong.ECS.Definitions.BuildingUpgradeCost target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingUpgradeCost 配置。");
            target.TargetLevel = source.TargetLevel;
            target.Item = itemIndex.Resolve(source.Item, true);
            target.Quantity = source.Quantity;
        }
    }
}
