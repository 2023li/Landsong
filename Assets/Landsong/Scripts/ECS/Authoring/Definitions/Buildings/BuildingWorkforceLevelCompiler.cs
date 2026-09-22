using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingWorkforceLevelCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingWorkforceLevelSource source, ref global::Landsong.ECS.Definitions.BuildingWorkforceLevel target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingWorkforceLevel 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Currency = itemIndex.Resolve(source.Currency, true);
            target.Capacity = source.Capacity;
            target.InitialWorkers = source.InitialWorkers;
            target.InitialSubsidy = source.InitialSubsidy;
            if (!math.isfinite(source.BaseAttraction))
                throw new InvalidOperationException("配置项（BaseAttraction）必须是有限数值。");
            target.BaseAttraction = source.BaseAttraction;
            if (!math.isfinite(source.RecruitmentCost))
                throw new InvalidOperationException("配置项（RecruitmentCost）必须是有限数值。");
            target.RecruitmentCost = source.RecruitmentCost;
        }
    }
}
