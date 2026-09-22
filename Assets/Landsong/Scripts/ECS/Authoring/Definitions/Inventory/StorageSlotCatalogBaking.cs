using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class StorageSlotCatalogBaking
    {
        public static BlobAssetReference<StorageSlotCatalogBlob> Compile(GameContentSetAsset content) => StorageSlotCatalogCompiler.Build(content.Get<StorageSlotCatalogAsset>(), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()), new ItemGroupCatalogIndex(content.Get<ItemGroupCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<StorageSlotCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 库存槽类型领域目录。");
                DependsOn(authoring.Content.Get<StorageSlotCatalogAsset>());
                foreach (var asset in authoring.Content.Get<StorageSlotCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                DependsOn(authoring.Content.Get<ItemGroupCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemGroupCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemGroupIndex = new ItemGroupCatalogIndex(authoring.Content.Get<ItemGroupCatalogAsset>());
                var blob = StorageSlotCatalogCompiler.Build(authoring.Content.Get<StorageSlotCatalogAsset>(), itemIndex, itemGroupIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new StorageSlotCatalog { Value = blob });
            }
        }
    }
}
