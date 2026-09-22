using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Buff catalog. It cannot resolve another domain.</summary>
    public sealed class BuffCatalogIndex
    {
        readonly Dictionary<BuffDefinitionAsset, BuffId> assets = new Dictionary<BuffDefinitionAsset, BuffId>();
        public BuffCatalogIndex(BuffCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Buff 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Buff 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, BuffId.FromIndex(i));
            }
        }

        public BuffId Resolve(BuffDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Buff 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Buff 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class BuffCatalogCompiler
    {
        public static BlobAssetReference<BuffCatalogBlob> Build(BuffCatalogAsset catalog, BuildingCatalogIndex buildingIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            var buffIndex = new BuffCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<BuffCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
                return builder.CreateBlobAssetReference<BuffCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, BuffDefinitionAsset source, ref BuffDefinition target, BuildingCatalogIndex buildingIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Buff 定义配置。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            DefinitionEffectsCompiler.Compile(ref builder, source.Effects, ref target.Effects, buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
        }
    }
}
