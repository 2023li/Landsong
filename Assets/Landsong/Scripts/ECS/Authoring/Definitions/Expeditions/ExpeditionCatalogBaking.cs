using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class ExpeditionCatalogBaking
    {
        public static BlobAssetReference<ExpeditionCatalogBlob> Compile(GameContentSetAsset content) => ExpeditionCatalogCompiler.Build(content.Get<ExpeditionCatalogAsset>(), new BuffCatalogIndex(content.Get<BuffCatalogAsset>()), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new FeatureCatalogIndex(content.Get<FeatureCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()), new QuestCatalogIndex(content.Get<QuestCatalogAsset>()), new TechnologyCatalogIndex(content.Get<TechnologyCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<ExpeditionCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 远征领域目录。");
                DependsOn(authoring.Content.Get<ExpeditionCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ExpeditionCatalogAsset>().Definitions)
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
                DependsOn(authoring.Content.Get<QuestCatalogAsset>());
                foreach (var asset in authoring.Content.Get<QuestCatalogAsset>().Definitions)
                    DependsOn(asset);
                var questIndex = new QuestCatalogIndex(authoring.Content.Get<QuestCatalogAsset>());
                DependsOn(authoring.Content.Get<TechnologyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TechnologyCatalogAsset>().Definitions)
                    DependsOn(asset);
                var technologyIndex = new TechnologyCatalogIndex(authoring.Content.Get<TechnologyCatalogAsset>());
                var blob = ExpeditionCatalogCompiler.Build(authoring.Content.Get<ExpeditionCatalogAsset>(), buffIndex, buildingIndex, featureIndex, itemIndex, questIndex, technologyIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ExpeditionCatalog { Value = blob });
                AddBuffer<CompletedExpedition>(entity);
            }
        }
    }
}
