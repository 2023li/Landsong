using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit ItemGroup catalog. It cannot resolve another domain.</summary>
    public sealed class ItemGroupCatalogIndex
    {
        readonly Dictionary<ItemGroupDefinitionAsset, ItemGroupId> assets = new Dictionary<ItemGroupDefinitionAsset, ItemGroupId>();
        public ItemGroupCatalogIndex(ItemGroupCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 ItemGroup 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("ItemGroup 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, ItemGroupId.FromIndex(i));
            }
        }

        public ItemGroupId Resolve(ItemGroupDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 ItemGroup 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("ItemGroup 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class ItemGroupCatalogCompiler
    {
        public static BlobAssetReference<ItemGroupCatalogBlob> Build(ItemGroupCatalogAsset catalog)
        {
            var itemGroupIndex = new ItemGroupCatalogIndex(catalog);
            ItemGroupCatalogValidation.Validate(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<ItemGroupCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], itemGroupIndex);
                return builder.CreateBlobAssetReference<ItemGroupCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, ItemGroupDefinitionAsset source, ref ItemGroupDefinition target, ItemGroupCatalogIndex itemGroupIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 ItemGroup 定义配置。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.ParentGroup = itemGroupIndex.Resolve(source.ParentGroup, true);
        }
    }
}
