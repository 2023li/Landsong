using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingSanctumCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingSanctumSource source, ref global::Landsong.ECS.Definitions.BuildingSanctum target, HeroCatalogIndex heroIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingSanctum 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Levels, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Levels == null)
                throw new InvalidOperationException("配置项（Levels）列表不能为空引用。");
            var Levels = builder.Allocate(ref target.Levels, source.Levels.Length);
            for (int i = 0; i < source.Levels.Length; i++)
            {
                BuildingSanctumLevelCompiler.Compile(ref builder, source.Levels[i], ref Levels[i], heroIndex);
            }
        }
    }
}
