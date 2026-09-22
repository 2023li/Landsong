using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class EnemyCatalogBaking
    {
        public static BlobAssetReference<EnemyCatalogBlob> Compile(GameContentSetAsset content) => EnemyCatalogCompiler.Build(content.Get<EnemyCatalogAsset>(), new BuffCatalogIndex(content.Get<BuffCatalogAsset>()), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new FeatureCatalogIndex(content.Get<FeatureCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<EnemyCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 敌军领域目录。");
                DependsOn(authoring.Content.Get<EnemyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<EnemyCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<BuffCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuffCatalogAsset>().Definitions)
                    DependsOn(asset);
                var buffIndex = new BuffCatalogIndex(authoring.Content.Get<BuffCatalogAsset>());
                DependsOn(authoring.Content.Get<BuildingCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuildingCatalogAsset>().Definitions)
                    DependsOn(asset);
                var buildingIndex = new BuildingCatalogIndex(authoring.Content.Get<BuildingCatalogAsset>());
                DependsOn(authoring.Content.Get<FeatureCatalogAsset>());
                foreach (var asset in authoring.Content.Get<FeatureCatalogAsset>().Definitions)
                    DependsOn(asset);
                var featureIndex = new FeatureCatalogIndex(authoring.Content.Get<FeatureCatalogAsset>());
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                var blob = EnemyCatalogCompiler.Build(authoring.Content.Get<EnemyCatalogAsset>(), buffIndex, buildingIndex, featureIndex, itemIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EnemyCatalog { Value = blob });
                var prefabs = AddBuffer<EnemyPrefab>(entity);
                for (int i = 0; i < authoring.Content.Get<EnemyCatalogAsset>().Definitions.Length; i++)
                {
                    var prefab = authoring.Content.Get<EnemyCatalogAsset>().Definitions[i].Prefab;
                    if (prefab == null)
                        throw new InvalidOperationException("Enemy 定义缺少实体预制体。");
                    DependsOn(prefab);
                    prefabs.Add(new EnemyPrefab { Definition = EnemyId.FromIndex(i), Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }
        }
    }
}
