using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class ExpeditionSupplyCompiler
    {
        public static void Compile(ref BlobBuilder builder, ExpeditionSupplySource source, ref global::Landsong.ECS.Definitions.ExpeditionSupply target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 ExpeditionSupply 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.MinimumQuantity = source.MinimumQuantity;
            target.ExtraLimit = source.ExtraLimit;
            if (!math.isfinite(source.SuccessPerExtra))
                throw new InvalidOperationException("每份补给成功率加成必须是有限数值。");
            target.SuccessPerExtra = source.SuccessPerExtra;
            if (!math.isfinite(source.RewardPerExtra))
                throw new InvalidOperationException("每份补给奖励加成必须是有限数值。");
            target.RewardPerExtra = source.RewardPerExtra;
        }
    }
}
