using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingWorkerEfficiencyTierCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingWorkerEfficiencyTierSource source, ref global::Landsong.ECS.Definitions.BuildingWorkerEfficiencyTier target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingWorkerEfficiencyTier 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.MinimumWorkers = source.MinimumWorkers;
            target.MaximumWorkers = source.MaximumWorkers;
        }
    }
}
