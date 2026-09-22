using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingUpgradeWorkersCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingUpgradeWorkersSource source, ref global::Landsong.ECS.Definitions.BuildingUpgradeWorkers target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingUpgradeWorkers 配置。");
            target.TargetLevel = source.TargetLevel;
            target.Required = source.Required;
        }
    }
}
