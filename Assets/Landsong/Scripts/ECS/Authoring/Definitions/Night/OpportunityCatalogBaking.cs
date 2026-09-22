using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class OpportunityCatalogBaking
    {
        public static BlobAssetReference<OpportunityCatalogBlob> Compile(GameContentSetAsset content) => OpportunityCatalogCompiler.Build(content.Get<OpportunityCatalogAsset>(), new BuffCatalogIndex(content.Get<BuffCatalogAsset>()), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new FeatureCatalogIndex(content.Get<FeatureCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<OpportunityCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 夜晚访客领域目录。");
                DependsOn(authoring.Content.Get<OpportunityCatalogAsset>());
                foreach (var asset in authoring.Content.Get<OpportunityCatalogAsset>().Definitions)
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
                var blob = OpportunityCatalogCompiler.Build(authoring.Content.Get<OpportunityCatalogAsset>(), buffIndex, buildingIndex, featureIndex, itemIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new OpportunityCatalog { Value = blob });
                var prefabs = AddBuffer<OpportunityPrefab>(entity);
                for (int i = 0; i < authoring.Content.Get<OpportunityCatalogAsset>().Definitions.Length; i++)
                {
                    var prefab = authoring.Content.Get<OpportunityCatalogAsset>().Definitions[i].Prefab;
                    if (prefab == null)
                        throw new InvalidOperationException("Opportunity 定义缺少实体预制体。");
                    DependsOn(prefab);
                    prefabs.Add(new OpportunityPrefab { Definition = OpportunityId.FromIndex(i), Prefab = GetEntity(prefab, TransformUsageFlags.Dynamic) });
                }
            }
        }
    }
}
