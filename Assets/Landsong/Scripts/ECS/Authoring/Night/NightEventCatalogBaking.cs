using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;

namespace Landsong.ECS.Authoring
{
    public static class NightEventCatalogBaking
    {
        public static BlobAssetReference<NightEventCatalogBlob> Compile(GameContentSetAsset content) => NightEventCatalogCompiler.Build(content.NightEvents, new EnemyCatalogIndex(content.Enemies), new BuildingCatalogIndex(content.Buildings), new ItemCatalogIndex(content.Items), new TechnologyCatalogIndex(content.Technologies), new BuffCatalogIndex(content.Buffs), new FeatureCatalogIndex(content.Features), new QuestCatalogIndex(content.Quests), new ExpeditionCatalogIndex(content.Expeditions));

        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                DependsOn(authoring.Content.Get<NightEventCatalogAsset>());
                DependsOn(authoring.Content.Get<EnemyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<EnemyCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<BuildingCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuildingCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<TechnologyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TechnologyCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<BuffCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuffCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<FeatureCatalogAsset>());
                foreach (var asset in authoring.Content.Get<FeatureCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<QuestCatalogAsset>());
                foreach (var asset in authoring.Content.Get<QuestCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<ExpeditionCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ExpeditionCatalogAsset>().Definitions)
                    DependsOn(asset);
                var blob = NightEventCatalogCompiler.Build(authoring.Content.Get<NightEventCatalogAsset>(), new EnemyCatalogIndex(authoring.Content.Get<EnemyCatalogAsset>()), new BuildingCatalogIndex(authoring.Content.Get<BuildingCatalogAsset>()), new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>()), new TechnologyCatalogIndex(authoring.Content.Get<TechnologyCatalogAsset>()), new BuffCatalogIndex(authoring.Content.Get<BuffCatalogAsset>()), new FeatureCatalogIndex(authoring.Content.Get<FeatureCatalogAsset>()), new QuestCatalogIndex(authoring.Content.Get<QuestCatalogAsset>()), new ExpeditionCatalogIndex(authoring.Content.Get<ExpeditionCatalogAsset>()));
                AddBlobAsset(ref blob, out _);
                AddComponent(GetEntity(TransformUsageFlags.None), new NightEventCatalog { Value = blob });
                AddBuffer<PreparedSoldier>(GetEntity(TransformUsageFlags.None));
                AddBuffer<PreparedHero>(GetEntity(TransformUsageFlags.None));
                AddBuffer<PreparedBuildingDefense>(GetEntity(TransformUsageFlags.None));
                AddBuffer<NightItemReward>(GetEntity(TransformUsageFlags.None));
                AddBuffer<NightBlueprintReward>(GetEntity(TransformUsageFlags.None));
                AddBuffer<NightBuffReward>(GetEntity(TransformUsageFlags.None));
                AddBuffer<NightFeatureReward>(GetEntity(TransformUsageFlags.None));
            }
        }
    }
}
