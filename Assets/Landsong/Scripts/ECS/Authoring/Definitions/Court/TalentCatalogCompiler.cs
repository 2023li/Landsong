using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Talent catalog. It cannot resolve another domain.</summary>
    public sealed class TalentCatalogIndex
    {
        readonly Dictionary<TalentDefinitionAsset, TalentId> assets = new Dictionary<TalentDefinitionAsset, TalentId>();
        public TalentCatalogIndex(TalentCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Talent 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Talent 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, TalentId.FromIndex(i));
            }
        }

        public TalentId Resolve(TalentDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Talent 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Talent 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class TalentCatalogCompiler
    {
        public static BlobAssetReference<TalentCatalogBlob> Build(TalentCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, RoyalTraitCatalogIndex royalTraitIndex, SoldierCatalogIndex soldierIndex, TechnologyCatalogIndex technologyIndex)
        {
            var talentIndex = new TalentCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<TalentCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, expeditionIndex, featureIndex, heroIndex, itemIndex, questIndex, royalTraitIndex, soldierIndex, talentIndex, technologyIndex);
                return builder.CreateBlobAssetReference<TalentCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, TalentDefinitionAsset source, ref TalentDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, RoyalTraitCatalogIndex royalTraitIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Talent 定义配置。");
            if (source.InitialLevel < 1 || source.MaximumLevel < source.InitialLevel || source.BaseLevelExperience < 1 || source.LevelExperienceIncrement < 0)
                throw new InvalidOperationException("人才成长配置无效。");
            EffectAuthoringBoundary.NoIntelligence(source.Effects);
            TalentIncomeValidation.Validate(source);
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.InitialLevel = source.InitialLevel;
            target.MaximumLevel = source.MaximumLevel;
            target.BaseLevelExperience = source.BaseLevelExperience;
            target.LevelExperienceIncrement = source.LevelExperienceIncrement;
            target.Specialty = source.Specialty;
            TalentWageCompiler.Compile(ref builder, source.Wage, ref target.Wage);
            if (source.InitialTraits == null)
                throw new InvalidOperationException("初始特性列表不能为空引用。");
            var InitialTraits = builder.Allocate(ref target.InitialTraits, source.InitialTraits.Length);
            for (int i = 0; i < source.InitialTraits.Length; i++)
            {
                InitialTraits[i] = royalTraitIndex.Resolve(source.InitialTraits[i], false);
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
            if (source.SocialTasks == null)
                throw new InvalidOperationException("人物委托列表不能为空引用。");
            var orderedSocialTasks = source.SocialTasks.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var SocialTasks = builder.Allocate(ref target.SocialTasks, orderedSocialTasks.Length);
            for (int i = 0; i < orderedSocialTasks.Length; i++)
            {
                TalentSocialTaskCompiler.Compile(ref builder, orderedSocialTasks[i], ref SocialTasks[i], itemIndex);
            }

            TalentPeriodicIncomeCompiler.Compile(ref builder, source.PeriodicIncome, ref target.PeriodicIncome, buffIndex, buildingIndex, featureIndex, itemIndex);
            TalentJobEffectsCompiler.Compile(ref builder, source.JobEffects, ref target.JobEffects, buildingIndex, heroIndex, itemIndex, soldierIndex);
            DefinitionEffectsCompiler.Compile(ref builder, source.Effects, ref target.Effects, buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
        }
    }
}
