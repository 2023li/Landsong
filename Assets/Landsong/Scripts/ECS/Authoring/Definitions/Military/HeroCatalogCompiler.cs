using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Hero catalog. It cannot resolve another domain.</summary>
    public sealed class HeroCatalogIndex
    {
        readonly Dictionary<HeroDefinitionAsset, HeroId> assets = new Dictionary<HeroDefinitionAsset, HeroId>();
        public HeroCatalogIndex(HeroCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Hero 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Hero 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, HeroId.FromIndex(i));
            }
        }

        public HeroId Resolve(HeroDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Hero 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Hero 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class HeroCatalogCompiler
    {
        public static BlobAssetReference<HeroCatalogBlob> Build(HeroCatalogAsset catalog, ItemCatalogIndex itemIndex)
        {
            var heroIndex = new HeroCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<HeroCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], itemIndex);
                return builder.CreateBlobAssetReference<HeroCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, HeroDefinitionAsset source, ref HeroDefinition target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Hero 定义配置。");
            UnitGrowthValidation.Hero(source.Growth, source.PopulationCost, source.FallbackWakeGold);
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            UnitCombatStatsCompiler.Compile(ref builder, source.CombatStats, ref target.CombatStats);
            target.ThreatValue = source.ThreatValue;
            target.TargetMode = source.TargetMode;
            target.PopulationCost = source.PopulationCost;
            target.FallbackWakeGold = source.FallbackWakeGold;
            target.RevivalCooldownTurns = source.RevivalCooldownTurns;
            target.Growth = source.Growth;
            if (source.AwakeningCosts == null)
                throw new InvalidOperationException("唤醒费用列表不能为空引用。");
            var orderedAwakeningCosts = source.AwakeningCosts.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var AwakeningCosts = builder.Allocate(ref target.AwakeningCosts, orderedAwakeningCosts.Length);
            for (int i = 0; i < orderedAwakeningCosts.Length; i++)
            {
                LeveledItemAmountCompiler.Compile(ref builder, orderedAwakeningCosts[i], ref AwakeningCosts[i], itemIndex);
            }

            if (source.OfferingCosts == null)
                throw new InvalidOperationException("供奉费用列表不能为空引用。");
            var orderedOfferingCosts = source.OfferingCosts.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var OfferingCosts = builder.Allocate(ref target.OfferingCosts, orderedOfferingCosts.Length);
            for (int i = 0; i < orderedOfferingCosts.Length; i++)
            {
                LeveledItemAmountCompiler.Compile(ref builder, orderedOfferingCosts[i], ref OfferingCosts[i], itemIndex);
            }
        }
    }
}
