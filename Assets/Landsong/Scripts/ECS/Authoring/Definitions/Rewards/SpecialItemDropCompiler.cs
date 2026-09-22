using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class SpecialItemDropCompiler
    {
        public static void Compile(ref BlobBuilder builder, SpecialItemDropSource source, ref global::Landsong.ECS.Definitions.SpecialItemDrop target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 SpecialItemDrop 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Quantity < 1 || source.Quantity > 100000 || (byte)source.Rarity < 1 || (byte)source.Rarity > 3)
                throw new InvalidOperationException("特殊掉落数量或稀有度无效。");
            target.Order = source.Order;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.Quantity = source.Quantity;
            target.Rarity = source.Rarity;
        }
    }
}
