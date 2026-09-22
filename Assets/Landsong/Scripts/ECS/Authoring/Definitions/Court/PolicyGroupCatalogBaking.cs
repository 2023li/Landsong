using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class PolicyGroupCatalogBaking
    {
        public static BlobAssetReference<PolicyGroupCatalogBlob> Compile(GameContentSetAsset content) => PolicyGroupCatalogCompiler.Build(content.Get<PolicyGroupCatalogAsset>());
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<PolicyGroupCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 政策组领域目录。");
                DependsOn(authoring.Content.Get<PolicyGroupCatalogAsset>());
                foreach (var asset in authoring.Content.Get<PolicyGroupCatalogAsset>().Definitions)
                    DependsOn(asset);
                var blob = PolicyGroupCatalogCompiler.Build(authoring.Content.Get<PolicyGroupCatalogAsset>());
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PolicyGroupCatalog { Value = blob });
            }
        }
    }
}
