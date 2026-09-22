using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingWorkforceCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingWorkforceSource source, ref global::Landsong.ECS.Definitions.BuildingWorkforce target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingWorkforce 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Levels, 0);
                builder.Allocate(ref target.EfficiencyTiers, 0);
                builder.Allocate(ref target.Attraction, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Levels == null)
                throw new InvalidOperationException("配置项（Levels）列表不能为空引用。");
            var Levels = builder.Allocate(ref target.Levels, source.Levels.Length);
            for (int i = 0; i < source.Levels.Length; i++)
            {
                BuildingWorkforceLevelCompiler.Compile(ref builder, source.Levels[i], ref Levels[i], itemIndex);
            }

            if (source.EfficiencyTiers == null)
                throw new InvalidOperationException("配置项（EfficiencyTiers）列表不能为空引用。");
            var EfficiencyTiers = builder.Allocate(ref target.EfficiencyTiers, source.EfficiencyTiers.Length);
            for (int i = 0; i < source.EfficiencyTiers.Length; i++)
            {
                BuildingWorkerEfficiencyTierCompiler.Compile(ref builder, source.EfficiencyTiers[i], ref EfficiencyTiers[i]);
            }

            if (source.Attraction == null)
                throw new InvalidOperationException("配置项（Attraction）列表不能为空引用。");
            var Attraction = builder.Allocate(ref target.Attraction, source.Attraction.Length);
            for (int i = 0; i < source.Attraction.Length; i++)
            {
                BuildingAttractionCompiler.Compile(ref builder, source.Attraction[i], ref Attraction[i]);
            }
        }
    }
}
