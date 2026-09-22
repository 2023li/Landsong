using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class FeatureCatalogBaking
    {
        public static BlobAssetReference<FeatureCatalogBlob> Compile(GameContentSetAsset content) => FeatureCatalogCompiler.Build(content.Get<FeatureCatalogAsset>());
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<FeatureCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 功能许可领域目录。");
                DependsOn(authoring.Content.Get<FeatureCatalogAsset>());
                foreach (var asset in authoring.Content.Get<FeatureCatalogAsset>().Definitions)
                    DependsOn(asset);
                var blob = FeatureCatalogCompiler.Build(authoring.Content.Get<FeatureCatalogAsset>());
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new FeatureCatalog { Value = blob });
                AddBuffer<UnlockedFeature>(entity);
            }
        }
    }
}
