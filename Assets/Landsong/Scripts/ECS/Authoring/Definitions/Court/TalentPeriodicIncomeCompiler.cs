using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentPeriodicIncomeCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentPeriodicIncomeSource source, ref global::Landsong.ECS.Definitions.TalentPeriodicIncome target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentPeriodicIncome 配置。");
            if (source.Items == null)
                throw new InvalidOperationException("物品列表不能为空引用。");
            var orderedItems = source.Items.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Items = builder.Allocate(ref target.Items, orderedItems.Length);
            for (int i = 0; i < orderedItems.Length; i++)
            {
                TalentItemIncomeCompiler.Compile(ref builder, orderedItems[i], ref Items[i], itemIndex);
            }

            if (source.ScaledItems == null)
                throw new InvalidOperationException("按条件缩放的物品收益列表不能为空引用。");
            var orderedScaledItems = source.ScaledItems.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var ScaledItems = builder.Allocate(ref target.ScaledItems, orderedScaledItems.Length);
            for (int i = 0; i < orderedScaledItems.Length; i++)
            {
                TalentScaledItemIncomeCompiler.Compile(ref builder, orderedScaledItems[i], ref ScaledItems[i], buildingIndex, itemIndex);
            }

            if (source.ResearchPoints == null)
                throw new InvalidOperationException("科研点收益列表不能为空引用。");
            var orderedResearchPoints = source.ResearchPoints.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var ResearchPoints = builder.Allocate(ref target.ResearchPoints, orderedResearchPoints.Length);
            for (int i = 0; i < orderedResearchPoints.Length; i++)
            {
                TalentResearchIncomeCompiler.Compile(ref builder, orderedResearchPoints[i], ref ResearchPoints[i], buildingIndex, itemIndex);
            }

            if (source.Blueprints == null)
                throw new InvalidOperationException("建筑蓝图列表不能为空引用。");
            var orderedBlueprints = source.Blueprints.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Blueprints = builder.Allocate(ref target.Blueprints, orderedBlueprints.Length);
            for (int i = 0; i < orderedBlueprints.Length; i++)
            {
                TalentBlueprintIncomeCompiler.Compile(ref builder, orderedBlueprints[i], ref Blueprints[i], buildingIndex, itemIndex);
            }

            if (source.Buffs == null)
                throw new InvalidOperationException("增益列表不能为空引用。");
            var orderedBuffs = source.Buffs.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Buffs = builder.Allocate(ref target.Buffs, orderedBuffs.Length);
            for (int i = 0; i < orderedBuffs.Length; i++)
            {
                TalentBuffIncomeCompiler.Compile(ref builder, orderedBuffs[i], ref Buffs[i], buffIndex, buildingIndex, itemIndex);
            }

            if (source.Features == null)
                throw new InvalidOperationException("功能许可列表不能为空引用。");
            var orderedFeatures = source.Features.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Features = builder.Allocate(ref target.Features, orderedFeatures.Length);
            for (int i = 0; i < orderedFeatures.Length; i++)
            {
                TalentFeatureIncomeCompiler.Compile(ref builder, orderedFeatures[i], ref Features[i], buildingIndex, featureIndex, itemIndex);
            }
        }
    }
}
