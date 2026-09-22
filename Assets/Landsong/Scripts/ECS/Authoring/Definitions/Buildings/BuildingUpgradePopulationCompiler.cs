using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingUpgradePopulationCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingUpgradePopulationSource source, ref global::Landsong.ECS.Definitions.BuildingUpgradePopulation target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingUpgradePopulation 配置。");
            target.TargetLevel = source.TargetLevel;
            target.Required = source.Required;
        }
    }
}
