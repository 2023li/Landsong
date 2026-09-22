using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class ItemGroupLossOverrideCompiler
    {
        public static void Compile(ref BlobBuilder builder, ItemGroupLossOverrideSource source, ref global::Landsong.ECS.Definitions.ItemGroupLossOverride target, ItemGroupCatalogIndex itemGroupIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 ItemGroupLossOverride 配置。");
            target.Group = itemGroupIndex.Resolve(source.Group, false);
            if (!math.isfinite(source.Multiplier))
                throw new InvalidOperationException("倍率必须是有限数值。");
            target.Multiplier = source.Multiplier;
        }
    }
}
