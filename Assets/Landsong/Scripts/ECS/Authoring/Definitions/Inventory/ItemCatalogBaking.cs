using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class ItemCatalogBaking
    {
        public static BlobAssetReference<ItemCatalogBlob> Compile(GameContentSetAsset content) => ItemCatalogCompiler.Build(content.Get<ItemCatalogAsset>(), new ItemGroupCatalogIndex(content.Get<ItemGroupCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<ItemCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 物品领域目录。");
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<ItemGroupCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemGroupCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemGroupIndex = new ItemGroupCatalogIndex(authoring.Content.Get<ItemGroupCatalogAsset>());
                var blob = ItemCatalogCompiler.Build(authoring.Content.Get<ItemCatalogAsset>(), itemGroupIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ItemCatalog { Value = blob });
            }
        }
    }
}
