using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class ItemQuantityRangeCompiler
    {
        public static void Compile(ref BlobBuilder builder, ItemQuantityRangeSource source, ref global::Landsong.ECS.Definitions.ItemQuantityRange target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 ItemQuantityRange 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.MinimumQuantity = source.MinimumQuantity;
            target.MaximumQuantity = source.MaximumQuantity;
        }
    }
}
