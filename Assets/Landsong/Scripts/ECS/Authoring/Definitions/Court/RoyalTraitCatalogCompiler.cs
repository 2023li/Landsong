using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit RoyalTrait catalog. It cannot resolve another domain.</summary>
    public sealed class RoyalTraitCatalogIndex
    {
        readonly Dictionary<RoyalTraitDefinitionAsset, RoyalTraitId> assets = new Dictionary<RoyalTraitDefinitionAsset, RoyalTraitId>();
        public RoyalTraitCatalogIndex(RoyalTraitCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 RoyalTrait 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("RoyalTrait 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, RoyalTraitId.FromIndex(i));
            }
        }

        public RoyalTraitId Resolve(RoyalTraitDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 RoyalTrait 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("RoyalTrait 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class RoyalTraitCatalogCompiler
    {
        public static BlobAssetReference<RoyalTraitCatalogBlob> Build(RoyalTraitCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            var royalTraitIndex = new RoyalTraitCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<RoyalTraitCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, expeditionIndex, featureIndex, heroIndex, itemIndex, questIndex, royalTraitIndex, soldierIndex, talentIndex, technologyIndex);
                return builder.CreateBlobAssetReference<RoyalTraitCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, RoyalTraitDefinitionAsset source, ref RoyalTraitDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, RoyalTraitCatalogIndex royalTraitIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 RoyalTrait 定义配置。");
            if (source.RevealAge < 0 || source.MinimumActivationAge < source.RevealAge || source.InheritanceChance < 0 || source.InheritanceChance > 1)
                throw new InvalidOperationException("特性揭示、激活年龄或遗传概率无效。");
            EffectAuthoringBoundary.NoIntelligence(source.Effects);
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.RevealAge = source.RevealAge;
            target.MinimumActivationAge = source.MinimumActivationAge;
            target.Heritable = source.Heritable;
            if (!math.isfinite(source.InheritanceChance))
                throw new InvalidOperationException("遗传概率必须是有限数值。");
            target.InheritanceChance = source.InheritanceChance;
            if (source.GrantedTraits == null)
                throw new InvalidOperationException("附带特性列表不能为空引用。");
            var GrantedTraits = builder.Allocate(ref target.GrantedTraits, source.GrantedTraits.Length);
            for (int i = 0; i < source.GrantedTraits.Length; i++)
            {
                GrantedTraits[i] = royalTraitIndex.Resolve(source.GrantedTraits[i], false);
            }

            if (source.ConflictingTraits == null)
                throw new InvalidOperationException("冲突特性列表不能为空引用。");
            var ConflictingTraits = builder.Allocate(ref target.ConflictingTraits, source.ConflictingTraits.Length);
            for (int i = 0; i < source.ConflictingTraits.Length; i++)
            {
                ConflictingTraits[i] = royalTraitIndex.Resolve(source.ConflictingTraits[i], false);
            }

            if (source.RequiredTraits == null)
                throw new InvalidOperationException("所需特性列表不能为空引用。");
            var RequiredTraits = builder.Allocate(ref target.RequiredTraits, source.RequiredTraits.Length);
            for (int i = 0; i < source.RequiredTraits.Length; i++)
            {
                RequiredTraits[i] = royalTraitIndex.Resolve(source.RequiredTraits[i], false);
            }

            DefinitionPrerequisitesCompiler.Compile(ref builder, source.Prerequisites, ref target.Prerequisites, buffIndex, buildingIndex, expeditionIndex, featureIndex, questIndex, technologyIndex);
            DefinitionEffectsCompiler.Compile(ref builder, source.Effects, ref target.Effects, buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
        }
    }
}
