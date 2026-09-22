using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class LootCatalogBaking
    {
        public static BlobAssetReference<LootCatalogBlob> Compile(GameContentSetAsset content) => LootCatalogCompiler.Build(content.Get<LootCatalogAsset>(), new BuffCatalogIndex(content.Get<BuffCatalogAsset>()), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new FeatureCatalogIndex(content.Get<FeatureCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<LootCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 掉落实体领域目录。");
                DependsOn(authoring.Content.Get<LootCatalogAsset>());
                foreach (var asset in authoring.Content.Get<LootCatalogAsset>().Definitions)
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
                var blob = LootCatalogCompiler.Build(authoring.Content.Get<LootCatalogAsset>(), buffIndex, buildingIndex, featureIndex, itemIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new LootCatalog { Value = blob });
                var prefabs = AddBuffer<LootPrefab>(entity);
                for (int i = 0; i < authoring.Content.Get<LootCatalogAsset>().Definitions.Length; i++)
                {
                    var prefab = authoring.Content.Get<LootCatalogAsset>().Definitions[i].Prefab;
                    if (prefab == null)
                        throw new InvalidOperationException("Loot 定义缺少实体预制体。");
                    DependsOn(prefab);
                    prefabs.Add(new LootPrefab { Definition = LootId.FromIndex(i), Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }
        }
    }
}
