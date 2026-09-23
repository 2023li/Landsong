using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class QuestCatalogBaking
    {
        public static BlobAssetReference<QuestCatalogBlob> Compile(GameContentSetAsset content) => QuestCatalogCompiler.Build(content.Get<QuestCatalogAsset>(), new BuffCatalogIndex(content.Get<BuffCatalogAsset>()), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new ExpeditionCatalogIndex(content.Get<ExpeditionCatalogAsset>()), new FeatureCatalogIndex(content.Get<FeatureCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()), new TechnologyCatalogIndex(content.Get<TechnologyCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<QuestCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 任务领域目录。");
                DependsOn(authoring.Content.Get<QuestCatalogAsset>());
                foreach (var asset in authoring.Content.Get<QuestCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<BuffCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuffCatalogAsset>().Definitions)
                    DependsOn(asset);
                var buffIndex = new BuffCatalogIndex(authoring.Content.Get<BuffCatalogAsset>());
                DependsOn(authoring.Content.Get<BuildingCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuildingCatalogAsset>().Definitions)
                    DependsOn(asset);
                var buildingIndex = new BuildingCatalogIndex(authoring.Content.Get<BuildingCatalogAsset>());
                DependsOn(authoring.Content.Get<ExpeditionCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ExpeditionCatalogAsset>().Definitions)
                    DependsOn(asset);
                var expeditionIndex = new ExpeditionCatalogIndex(authoring.Content.Get<ExpeditionCatalogAsset>());
                DependsOn(authoring.Content.Get<FeatureCatalogAsset>());
                foreach (var asset in authoring.Content.Get<FeatureCatalogAsset>().Definitions)
                    DependsOn(asset);
                var featureIndex = new FeatureCatalogIndex(authoring.Content.Get<FeatureCatalogAsset>());
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                DependsOn(authoring.Content.Get<TechnologyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TechnologyCatalogAsset>().Definitions)
                    DependsOn(asset);
                var technologyIndex = new TechnologyCatalogIndex(authoring.Content.Get<TechnologyCatalogAsset>());
                var blob = QuestCatalogCompiler.Build(authoring.Content.Get<QuestCatalogAsset>(), buffIndex, buildingIndex, expeditionIndex, featureIndex, itemIndex, technologyIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new QuestCatalog { Value = blob });
                AddBuffer<ClaimedQuest>(entity);
                AddBuffer<TrackedQuest>(entity);
            }
        }
    }
}
