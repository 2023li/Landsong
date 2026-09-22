using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingConstructionOutputCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingConstructionOutputSource source, ref global::Landsong.ECS.Definitions.BuildingConstructionOutput target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingConstructionOutput 配置。");
            target.Stage = source.Stage;
            target.Item = itemIndex.Resolve(source.Item, true);
            target.Quantity = source.Quantity;
        }
    }
}
