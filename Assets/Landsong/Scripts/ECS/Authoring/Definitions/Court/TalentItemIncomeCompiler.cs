using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentItemIncomeCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentItemIncomeSource source, ref global::Landsong.ECS.Definitions.TalentItemIncome target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentItemIncome 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.BaseQuantity = source.BaseQuantity;
            if (!math.isfinite(source.PerLevel))
                throw new InvalidOperationException("每级增加量必须是有限数值。");
            target.PerLevel = source.PerLevel;
        }
    }
}
