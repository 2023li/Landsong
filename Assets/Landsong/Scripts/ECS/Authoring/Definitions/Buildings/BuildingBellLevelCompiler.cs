using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingBellLevelCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingBellLevelSource source, ref global::Landsong.ECS.Definitions.BuildingBellLevel target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingBellLevel 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            if (!math.isfinite(source.Radius))
                throw new InvalidOperationException("配置项（Radius）必须是有限数值。");
            target.Radius = source.Radius;
        }
    }
}
