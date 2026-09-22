using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit StorageSlot catalog. It cannot resolve another domain.</summary>
    public sealed class StorageSlotCatalogIndex
    {
        readonly Dictionary<StorageSlotDefinitionAsset, StorageSlotId> assets = new Dictionary<StorageSlotDefinitionAsset, StorageSlotId>();
        public StorageSlotCatalogIndex(StorageSlotCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 StorageSlot 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("StorageSlot 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, StorageSlotId.FromIndex(i));
            }
        }

        public StorageSlotId Resolve(StorageSlotDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 StorageSlot 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("StorageSlot 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class StorageSlotCatalogCompiler
    {
        public static BlobAssetReference<StorageSlotCatalogBlob> Build(StorageSlotCatalogAsset catalog, ItemCatalogIndex itemIndex, ItemGroupCatalogIndex itemGroupIndex)
        {
            var storageSlotIndex = new StorageSlotCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<StorageSlotCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], itemIndex, itemGroupIndex);
                return builder.CreateBlobAssetReference<StorageSlotCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, StorageSlotDefinitionAsset source, ref StorageSlotDefinition target, ItemCatalogIndex itemIndex, ItemGroupCatalogIndex itemGroupIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 StorageSlot 定义配置。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            if (!math.isfinite(source.DefaultLossMultiplier))
                throw new InvalidOperationException("默认损耗倍率必须是有限数值。");
            target.DefaultLossMultiplier = source.DefaultLossMultiplier;
            if (source.AcceptedItems == null)
                throw new InvalidOperationException("允许收纳物品列表不能为空引用。");
            var AcceptedItems = builder.Allocate(ref target.AcceptedItems, source.AcceptedItems.Length);
            for (int i = 0; i < source.AcceptedItems.Length; i++)
            {
                AcceptedItems[i] = itemIndex.Resolve(source.AcceptedItems[i], false);
            }

            if (source.AcceptedGroups == null)
                throw new InvalidOperationException("允许收纳物品组列表不能为空引用。");
            var AcceptedGroups = builder.Allocate(ref target.AcceptedGroups, source.AcceptedGroups.Length);
            for (int i = 0; i < source.AcceptedGroups.Length; i++)
            {
                AcceptedGroups[i] = itemGroupIndex.Resolve(source.AcceptedGroups[i], false);
            }

            if (source.ItemLosses == null)
                throw new InvalidOperationException("物品损耗倍率列表不能为空引用。");
            var ItemLosses = builder.Allocate(ref target.ItemLosses, source.ItemLosses.Length);
            for (int i = 0; i < source.ItemLosses.Length; i++)
            {
                ItemLossOverrideCompiler.Compile(ref builder, source.ItemLosses[i], ref ItemLosses[i], itemIndex);
            }

            if (source.GroupLosses == null)
                throw new InvalidOperationException("物品组损耗倍率列表不能为空引用。");
            var GroupLosses = builder.Allocate(ref target.GroupLosses, source.GroupLosses.Length);
            for (int i = 0; i < source.GroupLosses.Length; i++)
            {
                ItemGroupLossOverrideCompiler.Compile(ref builder, source.GroupLosses[i], ref GroupLosses[i], itemGroupIndex);
            }
        }
    }
}
