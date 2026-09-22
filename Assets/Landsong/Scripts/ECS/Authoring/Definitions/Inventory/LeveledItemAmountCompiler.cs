using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class LeveledItemAmountCompiler
    {
        public static void Compile(ref BlobBuilder builder, LeveledItemAmountSource source, ref global::Landsong.ECS.Definitions.LeveledItemAmount target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 LeveledItemAmount 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            if (source.Quantity < 0)
                throw new InvalidOperationException("物品费用不能为负。");
            target.Order = source.Order;
            target.Level = source.Level;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.Quantity = source.Quantity;
        }
    }
}
