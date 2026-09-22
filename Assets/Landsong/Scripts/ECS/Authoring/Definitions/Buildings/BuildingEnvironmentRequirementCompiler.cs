using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingEnvironmentRequirementCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingEnvironmentRequirementSource source, ref global::Landsong.ECS.Definitions.BuildingEnvironmentRequirement target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingEnvironmentRequirement 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.RequiredValue = source.RequiredValue;
            target.Type = source.Type;
        }
    }
}
