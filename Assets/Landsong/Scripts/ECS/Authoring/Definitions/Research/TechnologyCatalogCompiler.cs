using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Technology catalog. It cannot resolve another domain.</summary>
    public sealed class TechnologyCatalogIndex
    {
        readonly Dictionary<TechnologyDefinitionAsset, TechnologyId> assets = new Dictionary<TechnologyDefinitionAsset, TechnologyId>();
        public TechnologyCatalogIndex(TechnologyCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Technology 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Technology 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, TechnologyId.FromIndex(i));
            }
        }

        public TechnologyId Resolve(TechnologyDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Technology 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Technology 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class TechnologyCatalogCompiler
    {
        public static BlobAssetReference<TechnologyCatalogBlob> Build(TechnologyCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex)
        {
            var technologyIndex = new TechnologyCatalogIndex(catalog);
            TechnologyCatalogValidation.Validate(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<TechnologyCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, expeditionIndex, featureIndex, heroIndex, itemIndex, questIndex, soldierIndex, talentIndex, technologyIndex);
                return builder.CreateBlobAssetReference<TechnologyCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, TechnologyDefinitionAsset source, ref TechnologyDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Technology 定义配置。");
            if (source.ResearchPointCost < 0)
                throw new InvalidOperationException("研究点费用不能为负。");
            EffectAuthoringBoundary.Technology(source.Effects);
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.ResearchPointCost = source.ResearchPointCost;
            target.Repeatable = source.Repeatable;
            DefinitionPrerequisitesCompiler.Compile(ref builder, source.Prerequisites, ref target.Prerequisites, buffIndex, buildingIndex, expeditionIndex, featureIndex, questIndex, technologyIndex);
            DefinitionRewardsCompiler.Compile(ref builder, source.Rewards, ref target.Rewards, buffIndex, buildingIndex, featureIndex, itemIndex);
            DefinitionEffectsCompiler.Compile(ref builder, source.Effects, ref target.Effects, buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
        }
    }
}
