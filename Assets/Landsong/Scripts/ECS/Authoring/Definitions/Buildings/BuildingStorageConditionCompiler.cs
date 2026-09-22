using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingStorageConditionCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingStorageConditionSource source, ref global::Landsong.ECS.Definitions.BuildingStorageCondition target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingStorageCondition 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.RequiredWorkers = source.RequiredWorkers;
            target.MaintenanceLossPercent = source.MaintenanceLossPercent;
            if (!math.isfinite(source.AttractionPenalty))
                throw new InvalidOperationException("配置项（AttractionPenalty）必须是有限数值。");
            target.AttractionPenalty = source.AttractionPenalty;
            if (!math.isfinite(source.UnderstaffedLossMultiplier))
                throw new InvalidOperationException("配置项（UnderstaffedLossMultiplier）必须是有限数值。");
            target.UnderstaffedLossMultiplier = source.UnderstaffedLossMultiplier;
        }
    }
}
