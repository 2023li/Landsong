using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentSocialTaskCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentSocialTaskSource source, ref global::Landsong.ECS.Definitions.TalentSocialTask target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentSocialTask 配置。");
            if (source.Order < 0)
                throw new InvalidOperationException("执行顺序不能为负。");
            target.Order = source.Order;
            target.Item = itemIndex.Resolve(source.Item, false);
            target.Quantity = source.Quantity;
            target.AffectionReward = source.AffectionReward;
        }
    }
}
