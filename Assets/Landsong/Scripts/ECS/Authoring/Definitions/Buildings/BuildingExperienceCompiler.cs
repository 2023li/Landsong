using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingExperienceCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingExperienceSource source, ref global::Landsong.ECS.Definitions.BuildingExperience target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingExperience 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.ExperiencePerTurn = source.ExperiencePerTurn;
            target.UpgradeExperience = source.UpgradeExperience;
            target.RequiredWorkers = source.RequiredWorkers;
        }
    }
}
