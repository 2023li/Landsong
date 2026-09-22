using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingResidentFoodCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingResidentFoodSource source, ref global::Landsong.ECS.Definitions.BuildingResidentFood target, ItemGroupCatalogIndex itemGroupIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingResidentFood 配置。");
            if (source.Level < 0)
                throw new InvalidOperationException("适用等级不能为负。");
            target.Level = source.Level;
            target.FoodGroup = itemGroupIndex.Resolve(source.FoodGroup, true);
            target.Varieties = source.Varieties;
            target.AmountPerResident = source.AmountPerResident;
        }
    }
}
