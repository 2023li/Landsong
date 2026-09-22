using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class ProjectileCatalogBaking
    {
        public static BlobAssetReference<ProjectileCatalogBlob> Compile(GameContentSetAsset content) => ProjectileCatalogCompiler.Build(content.Get<ProjectileCatalogAsset>());
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<ProjectileCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 弹体领域目录。");
                DependsOn(authoring.Content.Get<ProjectileCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ProjectileCatalogAsset>().Definitions)
                    DependsOn(asset);
                var blob = ProjectileCatalogCompiler.Build(authoring.Content.Get<ProjectileCatalogAsset>());
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ProjectileCatalog { Value = blob });
                var prefabs = AddBuffer<ProjectilePrefab>(entity);
                for (int i = 0; i < authoring.Content.Get<ProjectileCatalogAsset>().Definitions.Length; i++)
                {
                    var prefab = authoring.Content.Get<ProjectileCatalogAsset>().Definitions[i].Prefab;
                    if (prefab == null)
                        throw new InvalidOperationException("Projectile 定义缺少实体预制体。");
                    DependsOn(prefab);
                    prefabs.Add(new ProjectilePrefab { Definition = ProjectileId.FromIndex(i), Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }
        }
    }
}
