using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit TalentSlot catalog. It cannot resolve another domain.</summary>
    public sealed class TalentSlotCatalogIndex
    {
        readonly Dictionary<TalentSlotDefinitionAsset, TalentSlotId> assets = new Dictionary<TalentSlotDefinitionAsset, TalentSlotId>();
        public TalentSlotCatalogIndex(TalentSlotCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 TalentSlot 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("TalentSlot 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, TalentSlotId.FromIndex(i));
            }
        }

        public TalentSlotId Resolve(TalentSlotDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 TalentSlot 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("TalentSlot 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class TalentSlotCatalogCompiler
    {
        public static BlobAssetReference<TalentSlotCatalogBlob> Build(TalentSlotCatalogAsset catalog, BuildingCatalogIndex buildingIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, RoyalTraitCatalogIndex royalTraitIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            var talentSlotIndex = new TalentSlotCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<TalentSlotCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buildingIndex, heroIndex, itemIndex, royalTraitIndex, soldierIndex, talentIndex, technologyIndex);
                return builder.CreateBlobAssetReference<TalentSlotCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, TalentSlotDefinitionAsset source, ref TalentSlotDefinition target, BuildingCatalogIndex buildingIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, RoyalTraitCatalogIndex royalTraitIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentSlot 定义配置。");
            EffectAuthoringBoundary.NoIntelligence(source.Effects);
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.AcceptedSpecialty = source.AcceptedSpecialty;
            if (source.RequiredTraits == null)
                throw new InvalidOperationException("所需特性列表不能为空引用。");
            var RequiredTraits = builder.Allocate(ref target.RequiredTraits, source.RequiredTraits.Length);
            for (int i = 0; i < source.RequiredTraits.Length; i++)
            {
                RequiredTraits[i] = royalTraitIndex.Resolve(source.RequiredTraits[i], false);
            }

            TalentJobEffectsCompiler.Compile(ref builder, source.JobEffects, ref target.JobEffects, buildingIndex, heroIndex, itemIndex, soldierIndex);
            DefinitionEffectsCompiler.Compile(ref builder, source.Effects, ref target.Effects, buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
        }
    }
}
