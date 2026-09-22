using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class TechnologyCatalogBaking
    {
        public static BlobAssetReference<TechnologyCatalogBlob> Compile(GameContentSetAsset content) => TechnologyCatalogCompiler.Build(content.Get<TechnologyCatalogAsset>(), new BuffCatalogIndex(content.Get<BuffCatalogAsset>()), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new ExpeditionCatalogIndex(content.Get<ExpeditionCatalogAsset>()), new FeatureCatalogIndex(content.Get<FeatureCatalogAsset>()), new HeroCatalogIndex(content.Get<HeroCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()), new QuestCatalogIndex(content.Get<QuestCatalogAsset>()), new SoldierCatalogIndex(content.Get<SoldierCatalogAsset>()), new TalentCatalogIndex(content.Get<TalentCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<TechnologyCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 科技领域目录。");
                DependsOn(authoring.Content.Get<TechnologyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TechnologyCatalogAsset>().Definitions)
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
                DependsOn(authoring.Content.Get<HeroCatalogAsset>());
                foreach (var asset in authoring.Content.Get<HeroCatalogAsset>().Definitions)
                    DependsOn(asset);
                var heroIndex = new HeroCatalogIndex(authoring.Content.Get<HeroCatalogAsset>());
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                DependsOn(authoring.Content.Get<QuestCatalogAsset>());
                foreach (var asset in authoring.Content.Get<QuestCatalogAsset>().Definitions)
                    DependsOn(asset);
                var questIndex = new QuestCatalogIndex(authoring.Content.Get<QuestCatalogAsset>());
                DependsOn(authoring.Content.Get<SoldierCatalogAsset>());
                foreach (var asset in authoring.Content.Get<SoldierCatalogAsset>().Definitions)
                    DependsOn(asset);
                var soldierIndex = new SoldierCatalogIndex(authoring.Content.Get<SoldierCatalogAsset>());
                DependsOn(authoring.Content.Get<TalentCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TalentCatalogAsset>().Definitions)
                    DependsOn(asset);
                var talentIndex = new TalentCatalogIndex(authoring.Content.Get<TalentCatalogAsset>());
                var blob = TechnologyCatalogCompiler.Build(authoring.Content.Get<TechnologyCatalogAsset>(), buffIndex, buildingIndex, expeditionIndex, featureIndex, heroIndex, itemIndex, questIndex, soldierIndex, talentIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new TechnologyCatalog { Value = blob });
                AddBuffer<TechnologyProgress>(entity);
            }
        }
    }
}
