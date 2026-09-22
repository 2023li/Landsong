using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingProductionCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingProductionSource source, ref global::Landsong.ECS.Definitions.BuildingProduction target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingProduction 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Inputs, 0);
                builder.Allocate(ref target.Cycles, 0);
                builder.Allocate(ref target.ProcessingTiers, 0);
                builder.Allocate(ref target.Outputs, 0);
                builder.Allocate(ref target.RareOutputs, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Inputs == null)
                throw new InvalidOperationException("配置项（Inputs）列表不能为空引用。");
            var Inputs = builder.Allocate(ref target.Inputs, source.Inputs.Length);
            for (int i = 0; i < source.Inputs.Length; i++)
            {
                BuildingInputCompiler.Compile(ref builder, source.Inputs[i], ref Inputs[i], itemIndex);
            }

            if (source.Cycles == null)
                throw new InvalidOperationException("配置项（Cycles）列表不能为空引用。");
            var Cycles = builder.Allocate(ref target.Cycles, source.Cycles.Length);
            for (int i = 0; i < source.Cycles.Length; i++)
            {
                BuildingProductionCycleCompiler.Compile(ref builder, source.Cycles[i], ref Cycles[i]);
            }

            if (source.ProcessingTiers == null)
                throw new InvalidOperationException("配置项（ProcessingTiers）列表不能为空引用。");
            var ProcessingTiers = builder.Allocate(ref target.ProcessingTiers, source.ProcessingTiers.Length);
            for (int i = 0; i < source.ProcessingTiers.Length; i++)
            {
                BuildingProcessingTierCompiler.Compile(ref builder, source.ProcessingTiers[i], ref ProcessingTiers[i]);
            }

            if (source.Outputs == null)
                throw new InvalidOperationException("配置项（Outputs）列表不能为空引用。");
            var Outputs = builder.Allocate(ref target.Outputs, source.Outputs.Length);
            for (int i = 0; i < source.Outputs.Length; i++)
            {
                BuildingProductionOutputCompiler.Compile(ref builder, source.Outputs[i], ref Outputs[i], itemIndex);
            }

            if (source.RareOutputs == null)
                throw new InvalidOperationException("配置项（RareOutputs）列表不能为空引用。");
            var RareOutputs = builder.Allocate(ref target.RareOutputs, source.RareOutputs.Length);
            for (int i = 0; i < source.RareOutputs.Length; i++)
            {
                BuildingRareProductionCompiler.Compile(ref builder, source.RareOutputs[i], ref RareOutputs[i], itemIndex);
            }
        }
    }
}
