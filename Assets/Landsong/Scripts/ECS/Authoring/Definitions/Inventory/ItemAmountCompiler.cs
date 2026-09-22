using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class ItemAmountCompiler
    {
        public static void Compile(ref BlobBuilder builder, ItemAmountSource source, ref global::Landsong.ECS.Definitions.ItemAmount target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 ItemAmount 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.Quantity = source.Quantity;
        }
    }
}
