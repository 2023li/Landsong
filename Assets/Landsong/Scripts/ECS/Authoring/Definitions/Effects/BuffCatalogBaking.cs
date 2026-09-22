using System;
using UnityEngine;
using Unity.Entities;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Place beside the simulation root authoring so the domain catalog shares its entity.</summary>
    public static class BuffCatalogBaking
    {
        public static BlobAssetReference<BuffCatalogBlob> Compile(GameContentSetAsset content) => BuffCatalogCompiler.Build(content.Get<BuffCatalogAsset>(), new BuildingCatalogIndex(content.Get<BuildingCatalogAsset>()), new HeroCatalogIndex(content.Get<HeroCatalogAsset>()), new ItemCatalogIndex(content.Get<ItemCatalogAsset>()), new SoldierCatalogIndex(content.Get<SoldierCatalogAsset>()), new TalentCatalogIndex(content.Get<TalentCatalogAsset>()), new TechnologyCatalogIndex(content.Get<TechnologyCatalogAsset>()));
        public sealed class Baker : Baker<GameContentSetAuthoring>
        {
            public override void Bake(GameContentSetAuthoring authoring)
            {
                if (authoring.Content == null)
                    throw new InvalidOperationException("GameContentSetAuthoring 缺少游戏内容集。");
                DependsOn(authoring.Content);
                if (authoring.Content.Get<BuffCatalogAsset>() == null)
                    throw new InvalidOperationException("缺少 增益领域目录。");
                DependsOn(authoring.Content.Get<BuffCatalogAsset>());
                foreach (var asset in authoring.Content.Get<BuffCatalogAsset>().Definitions)
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
                var blob = BuffCatalogCompiler.Build(authoring.Content.Get<BuffCatalogAsset>(), buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
                AddBlobAsset(ref blob, out _);
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BuffCatalog { Value = blob });
                AddBuffer<OwnedBuff>(entity);
            }
        }
    }
}
