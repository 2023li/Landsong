using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingGarrisonLevelCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingGarrisonLevelSource source, ref global::Landsong.ECS.Definitions.BuildingGarrisonLevel target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingGarrisonLevel 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Capacity = source.Capacity;
            target.DeploymentBatchSize = source.DeploymentBatchSize;
        }
    }
}
