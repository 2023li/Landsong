using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingUpgradeMaintenanceCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingUpgradeMaintenanceSource source, ref global::Landsong.ECS.Definitions.BuildingUpgradeMaintenance target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingUpgradeMaintenance 配置。");
            target.TargetLevel = source.TargetLevel;
        }
    }
}
