using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class BuildingCatalogBaking
    {
        public static BlobAssetReference<BuildingCatalogBlob> Compile(GameContentSetAsset content) => BuildingCatalogCompiler.Build(content.Get<BuildingCatalogAsset>(), new BuildingLimitGroupCatalogIndex(content.Get<BuildingLimitGroupCatalogAsset>()), new CropCatalogIndex(content.Get<CropCatalogAsset>()), new HeroCatalogIndex(content.Get<HeroCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()), new ItemGroupCatalogIndex(content.Get<ItemGroupCatalogAsset>()), new SoldierCatalogIndex(content.Get<SoldierCatalogAsset>()), new StorageSlotCatalogIndex(content.Get<StorageSlotCatalogAsset>()), new TechnologyCatalogIndex(content.Get<TechnologyCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<BuildingCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 建筑领域目录。");
                DependsOn(authoring.Content.Get<BuildingCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuildingCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<BuildingLimitGroupCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuildingLimitGroupCatalogAsset>().Definitions)
                    DependsOn(asset);
                var buildingLimitGroupIndex = new BuildingLimitGroupCatalogIndex(authoring.Content.Get<BuildingLimitGroupCatalogAsset>());
                DependsOn(authoring.Content.Get<CropCatalogAsset>());
                foreach (var asset in authoring.Content.Get<CropCatalogAsset>().Definitions)
                    DependsOn(asset);
                var cropIndex = new CropCatalogIndex(authoring.Content.Get<CropCatalogAsset>());
                DependsOn(authoring.Content.Get<HeroCatalogAsset>());
                foreach (var asset in authoring.Content.Get<HeroCatalogAsset>().Definitions)
                    DependsOn(asset);
                var heroIndex = new HeroCatalogIndex(authoring.Content.Get<HeroCatalogAsset>());
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                DependsOn(authoring.Content.Get<ItemGroupCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemGroupCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemGroupIndex = new ItemGroupCatalogIndex(authoring.Content.Get<ItemGroupCatalogAsset>());
                DependsOn(authoring.Content.Get<SoldierCatalogAsset>());
                foreach (var asset in authoring.Content.Get<SoldierCatalogAsset>().Definitions)
                    DependsOn(asset);
                var soldierIndex = new SoldierCatalogIndex(authoring.Content.Get<SoldierCatalogAsset>());
                DependsOn(authoring.Content.Get<StorageSlotCatalogAsset>());
                foreach (var asset in authoring.Content.Get<StorageSlotCatalogAsset>().Definitions)
                    DependsOn(asset);
                var storageSlotIndex = new StorageSlotCatalogIndex(authoring.Content.Get<StorageSlotCatalogAsset>());
                DependsOn(authoring.Content.Get<TechnologyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TechnologyCatalogAsset>().Definitions)
                    DependsOn(asset);
                var technologyIndex = new TechnologyCatalogIndex(authoring.Content.Get<TechnologyCatalogAsset>());
                var blob = BuildingCatalogCompiler.Build(authoring.Content.Get<BuildingCatalogAsset>(), buildingLimitGroupIndex, cropIndex, heroIndex, itemIndex, itemGroupIndex, soldierIndex, storageSlotIndex, technologyIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BuildingCatalog { Value = blob });
                AddBuffer<BlueprintUnlock>(entity);
                var prefabs = AddBuffer<BuildingPrefab>(entity);
                for (int i = 0; i < authoring.Content.Get<BuildingCatalogAsset>().Definitions.Length; i++)
                {
                    var prefab = authoring.Content.Get<BuildingCatalogAsset>().Definitions[i].Prefab;
                    if (prefab == null)
                        throw new InvalidOperationException("Building 定义缺少实体预制体。");
                    DependsOn(prefab);
                    prefabs.Add(new BuildingPrefab { Definition = BuildingId.FromIndex(i), Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }
        }
    }
}
