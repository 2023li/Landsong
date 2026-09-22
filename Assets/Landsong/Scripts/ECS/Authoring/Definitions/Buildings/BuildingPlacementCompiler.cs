using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingPlacementCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingPlacementSource source, ref global::Landsong.ECS.Definitions.BuildingPlacement target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingPlacement 配置。");
            target.AllowedTerrains = source.AllowedTerrains;
            target.ExcludedTerrains = source.ExcludedTerrains;
        }
    }
}
