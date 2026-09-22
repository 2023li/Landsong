using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingAllowedCropCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingAllowedCropSource source, ref global::Landsong.ECS.Definitions.BuildingAllowedCrop target, CropCatalogIndex cropIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingAllowedCrop 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.Crop = cropIndex.Resolve(source.Crop, true);
        }
    }
}
