using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingProductionCycleCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingProductionCycleSource source, ref global::Landsong.ECS.Definitions.BuildingProductionCycle target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingProductionCycle 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Interval = source.Interval;
            target.RequiredWorkers = source.RequiredWorkers;
        }
    }
}
