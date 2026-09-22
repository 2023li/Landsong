using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class ItemLossOverrideCompiler
    {
        public static void Compile(ref BlobBuilder builder, ItemLossOverrideSource source, ref global::Landsong.ECS.Definitions.ItemLossOverride target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 ItemLossOverride 配置。");
            target.Item = itemIndex.Resolve(source.Item, false);
            if (!math.isfinite(source.Multiplier))
                throw new InvalidOperationException("倍率必须是有限数值。");
            target.Multiplier = source.Multiplier;
        }
    }
}
