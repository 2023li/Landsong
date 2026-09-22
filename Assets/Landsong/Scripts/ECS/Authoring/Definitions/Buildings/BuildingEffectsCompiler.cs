using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingEffectsCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingEffectsSource source, ref global::Landsong.ECS.Definitions.BuildingEffects target, BuildingCatalogIndex buildingIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingEffects 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Spatial, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Spatial == null)
                throw new InvalidOperationException("配置项（Spatial）列表不能为空引用。");
            var Spatial = builder.Allocate(ref target.Spatial, source.Spatial.Length);
            for (int i = 0; i < source.Spatial.Length; i++)
            {
                BuildingSpatialEffectCompiler.Compile(ref builder, source.Spatial[i], ref Spatial[i], buildingIndex);
            }
        }
    }
}
