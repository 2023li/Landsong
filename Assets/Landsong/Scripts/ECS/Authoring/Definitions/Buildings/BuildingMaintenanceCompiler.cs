using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingMaintenanceCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingMaintenanceSource source, ref global::Landsong.ECS.Definitions.BuildingMaintenance target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingMaintenance 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Costs, 0);
                builder.Allocate(ref target.Repairs, 0);
                return;
            }

            target.Enabled = source.Enabled;
            target.RepairTurns = source.RepairTurns;
            if (source.Costs == null)
                throw new InvalidOperationException("配置项（Costs）列表不能为空引用。");
            var Costs = builder.Allocate(ref target.Costs, source.Costs.Length);
            for (int i = 0; i < source.Costs.Length; i++)
            {
                BuildingMaintenanceCostCompiler.Compile(ref builder, source.Costs[i], ref Costs[i], itemIndex);
            }

            if (source.Repairs == null)
                throw new InvalidOperationException("配置项（Repairs）列表不能为空引用。");
            var Repairs = builder.Allocate(ref target.Repairs, source.Repairs.Length);
            for (int i = 0; i < source.Repairs.Length; i++)
            {
                BuildingRepairMaterialCompiler.Compile(ref builder, source.Repairs[i], ref Repairs[i], itemIndex);
            }
        }
    }
}
