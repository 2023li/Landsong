using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class HeroCatalogBaking
    {
        public static BlobAssetReference<HeroCatalogBlob> Compile(GameContentSetAsset content) => HeroCatalogCompiler.Build(content.Get<HeroCatalogAsset>(), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<HeroCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 英雄领域目录。");
                DependsOn(authoring.Content.Get<HeroCatalogAsset>());
                foreach (var asset in authoring.Content.Get<HeroCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                var blob = HeroCatalogCompiler.Build(authoring.Content.Get<HeroCatalogAsset>(), itemIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new HeroCatalog { Value = blob });
                var prefabs = AddBuffer<HeroPrefab>(entity);
                for (int i = 0; i < authoring.Content.Get<HeroCatalogAsset>().Definitions.Length; i++)
                {
                    var prefab = authoring.Content.Get<HeroCatalogAsset>().Definitions[i].Prefab;
                    if (prefab == null)
                        throw new InvalidOperationException("Hero 定义缺少实体预制体。");
                    DependsOn(prefab);
                    prefabs.Add(new HeroPrefab { Definition = HeroId.FromIndex(i), Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }
        }
    }
}
