using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class BuildingHousingCompiler
    {
        public static void Compile(ref BlobBuilder builder, BuildingHousingSource source, ref global::Landsong.ECS.Definitions.BuildingHousing target, ItemCatalogIndex itemIndex, ItemGroupCatalogIndex itemGroupIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 BuildingHousing 配置。");
            if (!source.Enabled)
            {
                target.Enabled = false;
                builder.Allocate(ref target.Population, 0);
                builder.Allocate(ref target.Residences, 0);
                builder.Allocate(ref target.Food, 0);
                builder.Allocate(ref target.Taxes, 0);
                builder.Allocate(ref target.Environment, 0);
                return;
            }

            target.Enabled = source.Enabled;
            if (source.Population == null)
                throw new InvalidOperationException("配置项（Population）列表不能为空引用。");
            var Population = builder.Allocate(ref target.Population, source.Population.Length);
            for (int i = 0; i < source.Population.Length; i++)
            {
                BuildingBasePopulationCompiler.Compile(ref builder, source.Population[i], ref Population[i]);
            }

            if (source.Residences == null)
                throw new InvalidOperationException("配置项（Residences）列表不能为空引用。");
            var Residences = builder.Allocate(ref target.Residences, source.Residences.Length);
            for (int i = 0; i < source.Residences.Length; i++)
            {
                BuildingResidenceLevelCompiler.Compile(ref builder, source.Residences[i], ref Residences[i]);
            }

            if (source.Food == null)
                throw new InvalidOperationException("配置项（Food）列表不能为空引用。");
            var Food = builder.Allocate(ref target.Food, source.Food.Length);
            for (int i = 0; i < source.Food.Length; i++)
            {
                BuildingResidentFoodCompiler.Compile(ref builder, source.Food[i], ref Food[i], itemGroupIndex);
            }

            if (source.Taxes == null)
                throw new InvalidOperationException("配置项（Taxes）列表不能为空引用。");
            var Taxes = builder.Allocate(ref target.Taxes, source.Taxes.Length);
            for (int i = 0; i < source.Taxes.Length; i++)
            {
                BuildingResidenceTaxCompiler.Compile(ref builder, source.Taxes[i], ref Taxes[i], itemIndex);
            }

            if (source.Environment == null)
                throw new InvalidOperationException("配置项（Environment）列表不能为空引用。");
            var Environment = builder.Allocate(ref target.Environment, source.Environment.Length);
            for (int i = 0; i < source.Environment.Length; i++)
            {
                BuildingEnvironmentRequirementCompiler.Compile(ref builder, source.Environment[i], ref Environment[i]);
            }
        }
    }
}
