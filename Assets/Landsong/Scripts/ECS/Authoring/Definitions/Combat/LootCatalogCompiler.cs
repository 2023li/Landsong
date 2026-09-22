using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Loot catalog. It cannot resolve another domain.</summary>
    public sealed class LootCatalogIndex
    {
        readonly Dictionary<LootDefinitionAsset, LootId> assets = new Dictionary<LootDefinitionAsset, LootId>();
        public LootCatalogIndex(LootCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Loot 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Loot 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, LootId.FromIndex(i));
            }
        }

        public LootId Resolve(LootDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Loot 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Loot 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class LootCatalogCompiler
    {
        public static BlobAssetReference<LootCatalogBlob> Build(LootCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            var lootIndex = new LootCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<LootCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, featureIndex, itemIndex);
                return builder.CreateBlobAssetReference<LootCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, LootDefinitionAsset source, ref LootDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Loot 定义配置。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            DefinitionRewardsCompiler.Compile(ref builder, source.Rewards, ref target.Rewards, buffIndex, buildingIndex, featureIndex, itemIndex);
        }
    }
}
