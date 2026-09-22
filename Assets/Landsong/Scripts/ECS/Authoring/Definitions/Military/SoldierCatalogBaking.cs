using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class SoldierCatalogBaking
    {
        public static BlobAssetReference<SoldierCatalogBlob> Compile(GameContentSetAsset content) => SoldierCatalogCompiler.Build(content.Get<SoldierCatalogAsset>(), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<SoldierCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 士兵领域目录。");
                DependsOn(authoring.Content.Get<SoldierCatalogAsset>());
                foreach (var asset in authoring.Content.Get<SoldierCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                var blob = SoldierCatalogCompiler.Build(authoring.Content.Get<SoldierCatalogAsset>(), itemIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new SoldierCatalog { Value = blob });
                var prefabs = AddBuffer<SoldierPrefab>(entity);
                for (int i = 0; i < authoring.Content.Get<SoldierCatalogAsset>().Definitions.Length; i++)
                {
                    var prefab = authoring.Content.Get<SoldierCatalogAsset>().Definitions[i].Prefab;
                    if (prefab == null)
                        throw new InvalidOperationException("Soldier 定义缺少实体预制体。");
                    DependsOn(prefab);
                    prefabs.Add(new SoldierPrefab { Definition = SoldierId.FromIndex(i), Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }
        }
    }
}
