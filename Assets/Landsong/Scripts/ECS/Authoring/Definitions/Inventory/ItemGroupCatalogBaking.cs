using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class ItemGroupCatalogBaking
    {
        public static BlobAssetReference<ItemGroupCatalogBlob> Compile(GameContentSetAsset content) => ItemGroupCatalogCompiler.Build(content.Get<ItemGroupCatalogAsset>());
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<ItemGroupCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 物品组领域目录。");
                DependsOn(authoring.Content.Get<ItemGroupCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemGroupCatalogAsset>().Definitions)
                    DependsOn(asset);
                var blob = ItemGroupCatalogCompiler.Build(authoring.Content.Get<ItemGroupCatalogAsset>());
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ItemGroupCatalog { Value = blob });
            }
        }
    }
}
