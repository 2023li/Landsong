using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingSpatialEffectCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingSpatialEffectSource source, ref global::Landsong.ECS.Definitions.BuildingSpatialEffect target, BuildingCatalogIndex buildingIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingSpatialEffect 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Building = buildingIndex.Resolve(source.Building, true);
            target.Magnitude = source.Magnitude;
            target.Type = source.Type;
            target.RequiredWorkers = source.RequiredWorkers;
            if (!math.isfinite(source.Radius))
                throw new InvalidOperationException("配置项（Radius）必须是有限数值。");
            target.Radius = source.Radius;
            target.Stacking = source.Stacking;
            target.Group = new FixedString128Bytes(source.Group ?? "");
        }
    }
}
