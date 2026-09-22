using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingExpeditionSiteLevelCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingExpeditionSiteLevelSource source, ref global::Landsong.ECS.Definitions.BuildingExpeditionSiteLevel target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingExpeditionSiteLevel 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.MinimumCrew = source.MinimumCrew;
            target.MaximumCrew = source.MaximumCrew;
            if (!math.isfinite(source.FullCrewRewardBonus))
                throw new InvalidOperationException("配置项（FullCrewRewardBonus）必须是有限数值。");
            target.FullCrewRewardBonus = source.FullCrewRewardBonus;
        }
    }
}
