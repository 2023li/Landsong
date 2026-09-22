using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class BuildingLimitGroupCatalogBaking
    {
        public static BlobAssetReference<BuildingLimitGroupCatalogBlob> Compile(GameContentSetAsset content) => BuildingLimitGroupCatalogCompiler.Build(content.Get<BuildingLimitGroupCatalogAsset>());
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<BuildingLimitGroupCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 建筑限制分组领域目录。");
                DependsOn(authoring.Content.Get<BuildingLimitGroupCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuildingLimitGroupCatalogAsset>().Definitions)
                    DependsOn(asset);
                var blob = BuildingLimitGroupCatalogCompiler.Build(authoring.Content.Get<BuildingLimitGroupCatalogAsset>());
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BuildingLimitGroupCatalog { Value = blob });
            }
        }
    }
}
