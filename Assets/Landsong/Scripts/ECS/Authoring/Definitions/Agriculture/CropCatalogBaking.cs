using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class CropCatalogBaking
    {
        public static BlobAssetReference<CropCatalogBlob> Compile(GameContentSetAsset content) => CropCatalogCompiler.Build(content.Get<CropCatalogAsset>(), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<CropCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 作物领域目录。");
                DependsOn(authoring.Content.Get<CropCatalogAsset>());
                foreach (var asset in authoring.Content.Get<CropCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                var blob = CropCatalogCompiler.Build(authoring.Content.Get<CropCatalogAsset>(), itemIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new CropCatalog { Value = blob });
            }
        }
    }
}
