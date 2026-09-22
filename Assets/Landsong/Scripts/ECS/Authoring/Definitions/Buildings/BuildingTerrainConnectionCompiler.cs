using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingTerrainConnectionCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingTerrainConnectionSource source, ref global::Landsong.ECS.Definitions.BuildingTerrainConnection target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingTerrainConnection 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                return;
            }

            target.Enabled = source.Enabled;
            target.Rise = source.Rise;
            target.Bidirectional = source.Bidirectional;
            if (!math.isfinite(source.Clearance))
                throw new InvalidOperationException("配置项（Clearance）必须是有限数值。");
            target.Clearance = source.Clearance;
            if (!math.isfinite(source.DamagedCost))
                throw new InvalidOperationException("配置项（DamagedCost）必须是有限数值。");
            target.DamagedCost = source.DamagedCost;
        }
    }
}
