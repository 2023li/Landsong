using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class DefinitionRewardsCompiler
    {
        public static void Compile(ref BlobBuilder builder, DefinitionRewardsSource source, ref global::Landsong.ECS.Definitions.DefinitionRewards target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex, float itemQuantityScale = 1)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 DefinitionRewards 配置。");
            foreach (var row in source.Items)
                if (row == null || row.Quantity <= 0)
                    throw new InvalidOperationException("奖励物品数量必须为正。");
            if (source.Items == null)
                throw new InvalidOperationException("物品列表不能为空引用。");
            var orderedItems = source.Items.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Items = builder.Allocate(ref target.Items, orderedItems.Length);
            for (int i = 0; i < orderedItems.Length; i++)
            {
                ItemAmountCompiler.Compile(ref builder, orderedItems[i], ref Items[i], itemIndex);
                Items[i].Quantity = ItemQuantityScaling.Apply(Items[i].Quantity, itemQuantityScale);
            }

            if (source.Blueprints == null)
                throw new InvalidOperationException("建筑蓝图列表不能为空引用。");
            var orderedBlueprints = source.Blueprints.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Blueprints = builder.Allocate(ref target.Blueprints, orderedBlueprints.Length);
            for (int i = 0; i < orderedBlueprints.Length; i++)
            {
                BlueprintRewardCompiler.Compile(ref builder, orderedBlueprints[i], ref Blueprints[i], buildingIndex);
            }

            if (source.Buffs == null)
                throw new InvalidOperationException("增益列表不能为空引用。");
            var orderedBuffs = source.Buffs.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Buffs = builder.Allocate(ref target.Buffs, orderedBuffs.Length);
            for (int i = 0; i < orderedBuffs.Length; i++)
            {
                BuffRewardCompiler.Compile(ref builder, orderedBuffs[i], ref Buffs[i], buffIndex);
            }

            if (source.Features == null)
                throw new InvalidOperationException("功能许可列表不能为空引用。");
            var orderedFeatures = source.Features.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Features = builder.Allocate(ref target.Features, orderedFeatures.Length);
            for (int i = 0; i < orderedFeatures.Length; i++)
            {
                FeatureRewardCompiler.Compile(ref builder, orderedFeatures[i], ref Features[i], featureIndex);
            }
        }
    }
}
