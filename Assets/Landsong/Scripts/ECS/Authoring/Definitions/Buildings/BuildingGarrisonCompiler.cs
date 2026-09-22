using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingGarrisonCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingGarrisonSource source, ref global::Landsong.ECS.Definitions.BuildingGarrison target, SoldierCatalogIndex soldierIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingGarrison 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Levels, 0);
                builder.Allocate(ref target.InitialUnits, 0);
                return;
            }

            target.Enabled = source.Enabled;
            target.RecruitmentLimitPerTurn = source.RecruitmentLimitPerTurn;
            if (source.Levels == null)
                throw new InvalidOperationException("配置项（Levels）列表不能为空引用。");
            var Levels = builder.Allocate(ref target.Levels, source.Levels.Length);
            for (int i = 0; i < source.Levels.Length; i++)
            {
                BuildingGarrisonLevelCompiler.Compile(ref builder, source.Levels[i], ref Levels[i]);
            }

            if (source.InitialUnits == null)
                throw new InvalidOperationException("配置项（InitialUnits）列表不能为空引用。");
            var InitialUnits = builder.Allocate(ref target.InitialUnits, source.InitialUnits.Length);
            for (int i = 0; i < source.InitialUnits.Length; i++)
            {
                BuildingInitialGarrisonCompiler.Compile(ref builder, source.InitialUnits[i], ref InitialUnits[i], soldierIndex);
            }
        }
    }
}
