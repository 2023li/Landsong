using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class TalentSlotCatalogBaking
    {
        public static BlobAssetReference<TalentSlotCatalogBlob> Compile(GameContentSetAsset content) => TalentSlotCatalogCompiler.Build(content.Get<TalentSlotCatalogAsset>(), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new HeroCatalogIndex(content.Get<HeroCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()), new RoyalTraitCatalogIndex(content.Get<RoyalTraitCatalogAsset>()), new SoldierCatalogIndex(content.Get<SoldierCatalogAsset>()), new TalentCatalogIndex(content.Get<TalentCatalogAsset>()), new TechnologyCatalogIndex(content.Get<TechnologyCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<TalentSlotCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 人才岗位领域目录。");
                DependsOn(authoring.Content.Get<TalentSlotCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TalentSlotCatalogAsset>().Definitions)
                    DependsOn(asset);
                DependsOn(authoring.Content.Get<BuildingCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuildingCatalogAsset>().Definitions)
                    DependsOn(asset);
                var buildingIndex = new BuildingCatalogIndex(authoring.Content.Get<BuildingCatalogAsset>());
                DependsOn(authoring.Content.Get<HeroCatalogAsset>());
                foreach (var asset in authoring.Content.Get<HeroCatalogAsset>().Definitions)
                    DependsOn(asset);
                var heroIndex = new HeroCatalogIndex(authoring.Content.Get<HeroCatalogAsset>());
                DependsOn(authoring.Content.Get<ItemCatalogAsset>());
                foreach (var asset in authoring.Content.Get<ItemCatalogAsset>().Definitions)
                    DependsOn(asset);
                var itemIndex = new ItemCatalogIndex(authoring.Content.Get<ItemCatalogAsset>());
                DependsOn(authoring.Content.Get<RoyalTraitCatalogAsset>());
                foreach (var asset in authoring.Content.Get<RoyalTraitCatalogAsset>().Definitions)
                    DependsOn(asset);
                var royalTraitIndex = new RoyalTraitCatalogIndex(authoring.Content.Get<RoyalTraitCatalogAsset>());
                DependsOn(authoring.Content.Get<SoldierCatalogAsset>());
                foreach (var asset in authoring.Content.Get<SoldierCatalogAsset>().Definitions)
                    DependsOn(asset);
                var soldierIndex = new SoldierCatalogIndex(authoring.Content.Get<SoldierCatalogAsset>());
                DependsOn(authoring.Content.Get<TalentCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TalentCatalogAsset>().Definitions)
                    DependsOn(asset);
                var talentIndex = new TalentCatalogIndex(authoring.Content.Get<TalentCatalogAsset>());
                DependsOn(authoring.Content.Get<TechnologyCatalogAsset>());
                foreach (var asset in authoring.Content.Get<TechnologyCatalogAsset>().Definitions)
                    DependsOn(asset);
                var technologyIndex = new TechnologyCatalogIndex(authoring.Content.Get<TechnologyCatalogAsset>());
                var blob = TalentSlotCatalogCompiler.Build(authoring.Content.Get<TalentSlotCatalogAsset>(), buildingIndex, heroIndex, itemIndex, royalTraitIndex, soldierIndex, talentIndex, technologyIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new TalentSlotCatalog { Value = blob });
            }
        }
    }
}
