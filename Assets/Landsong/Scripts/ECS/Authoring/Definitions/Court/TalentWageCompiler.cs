using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentWageCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentWageSource source, ref global::Landsong.ECS.Definitions.TalentWage target)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentWage 配置。");
            if (source.BaseAmount < 0 || source.PerLevel < 0)
                throw new InvalidOperationException("工资不能为负。");
            target.BaseAmount = source.BaseAmount;
            target.PerLevel = source.PerLevel;
        }
    }
}
