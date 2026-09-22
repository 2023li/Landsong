using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingConstructionCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingConstructionSource source, ref global::Landsong.ECS.Definitions.BuildingConstruction target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingConstruction 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.PlacementCosts, 0);
                builder.Allocate(ref target.StageCosts, 0);
                builder.Allocate(ref target.StageOutputs, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.PlacementCosts == null)
                throw new InvalidOperationException("配置项（PlacementCosts）列表不能为空引用。");
            var PlacementCosts = builder.Allocate(ref target.PlacementCosts, source.PlacementCosts.Length);
            for (int i = 0; i < source.PlacementCosts.Length; i++)
            {
                BuildingPlacementCostCompiler.Compile(ref builder, source.PlacementCosts[i], ref PlacementCosts[i], itemIndex);
            }

            if (source.StageCosts == null)
                throw new InvalidOperationException("配置项（StageCosts）列表不能为空引用。");
            var StageCosts = builder.Allocate(ref target.StageCosts, source.StageCosts.Length);
            for (int i = 0; i < source.StageCosts.Length; i++)
            {
                BuildingConstructionCostCompiler.Compile(ref builder, source.StageCosts[i], ref StageCosts[i], itemIndex);
            }

            if (source.StageOutputs == null)
                throw new InvalidOperationException("配置项（StageOutputs）列表不能为空引用。");
            var StageOutputs = builder.Allocate(ref target.StageOutputs, source.StageOutputs.Length);
            for (int i = 0; i < source.StageOutputs.Length; i++)
            {
                BuildingConstructionOutputCompiler.Compile(ref builder, source.StageOutputs[i], ref StageOutputs[i], itemIndex);
            }
        }
    }
}
