using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingUpgradeCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingUpgradeSource source, ref global::Landsong.ECS.Definitions.BuildingUpgrade target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingUpgrade 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Costs, 0);
                builder.Allocate(ref target.Workers, 0);
                builder.Allocate(ref target.Residents, 0);
                builder.Allocate(ref target.MaintenanceRequirements, 0);
                builder.Allocate(ref target.Experience, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Costs == null)
                throw new InvalidOperationException("配置项（Costs）列表不能为空引用。");
            var Costs = builder.Allocate(ref target.Costs, source.Costs.Length);
            for (int i = 0; i < source.Costs.Length; i++)
            {
                BuildingUpgradeCostCompiler.Compile(ref builder, source.Costs[i], ref Costs[i], itemIndex);
            }

            if (source.Workers == null)
                throw new InvalidOperationException("配置项（Workers）列表不能为空引用。");
            var Workers = builder.Allocate(ref target.Workers, source.Workers.Length);
            for (int i = 0; i < source.Workers.Length; i++)
            {
                BuildingUpgradeWorkersCompiler.Compile(ref builder, source.Workers[i], ref Workers[i]);
            }

            if (source.Residents == null)
                throw new InvalidOperationException("配置项（Residents）列表不能为空引用。");
            var Residents = builder.Allocate(ref target.Residents, source.Residents.Length);
            for (int i = 0; i < source.Residents.Length; i++)
            {
                BuildingUpgradePopulationCompiler.Compile(ref builder, source.Residents[i], ref Residents[i]);
            }

            if (source.MaintenanceRequirements == null)
                throw new InvalidOperationException("专项条件列表不能为空引用。");
            var MaintenanceRequirements = builder.Allocate(ref target.MaintenanceRequirements, source.MaintenanceRequirements.Length);
            for (int i = 0; i < source.MaintenanceRequirements.Length; i++)
            {
                BuildingUpgradeMaintenanceCompiler.Compile(ref builder, source.MaintenanceRequirements[i], ref MaintenanceRequirements[i]);
            }

            if (source.Experience == null)
                throw new InvalidOperationException("配置项（Experience）列表不能为空引用。");
            var Experience = builder.Allocate(ref target.Experience, source.Experience.Length);
            for (int i = 0; i < source.Experience.Length; i++)
            {
                BuildingExperienceCompiler.Compile(ref builder, source.Experience[i], ref Experience[i]);
            }
        }
    }
}
