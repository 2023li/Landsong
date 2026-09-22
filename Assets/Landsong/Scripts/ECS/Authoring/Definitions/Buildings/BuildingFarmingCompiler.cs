using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingFarmingCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingFarmingSource source, ref global::Landsong.ECS.Definitions.BuildingFarming target, CropCatalogIndex cropIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingFarming 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Crops, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Crops == null)
                throw new InvalidOperationException("配置项（Crops）列表不能为空引用。");
            var Crops = builder.Allocate(ref target.Crops, source.Crops.Length);
            for (int i = 0; i < source.Crops.Length; i++)
            {
                BuildingAllowedCropCompiler.Compile(ref builder, source.Crops[i], ref Crops[i], cropIndex);
            }
        }
    }
}
