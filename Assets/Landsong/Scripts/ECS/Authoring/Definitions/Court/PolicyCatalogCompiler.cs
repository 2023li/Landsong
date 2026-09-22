using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Policy catalog. It cannot resolve another domain.</summary>
    public sealed class PolicyCatalogIndex
    {
        readonly Dictionary<PolicyDefinitionAsset, PolicyId> assets = new Dictionary<PolicyDefinitionAsset, PolicyId>();
        public PolicyCatalogIndex(PolicyCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Policy 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Policy 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, PolicyId.FromIndex(i));
            }
        }

        public PolicyId Resolve(PolicyDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Policy 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Policy 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class PolicyCatalogCompiler
    {
        public static BlobAssetReference<PolicyCatalogBlob> Build(PolicyCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, PolicyGroupCatalogIndex policyGroupIndex, QuestCatalogIndex questIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            var policyIndex = new PolicyCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<PolicyCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, expeditionIndex, featureIndex, heroIndex, itemIndex, policyGroupIndex, questIndex, soldierIndex, talentIndex, technologyIndex);
                return builder.CreateBlobAssetReference<PolicyCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, PolicyDefinitionAsset source, ref PolicyDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, PolicyGroupCatalogIndex policyGroupIndex, QuestCatalogIndex questIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Policy 定义配置。");
            EffectAuthoringBoundary.Policy(source.Effects);
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.PolicyGroup = policyGroupIndex.Resolve(source.PolicyGroup, true);
            target.PolicyTier = source.PolicyTier;
            target.RequiredPublicOpinion = source.RequiredPublicOpinion;
            DefinitionPrerequisitesCompiler.Compile(ref builder, source.Prerequisites, ref target.Prerequisites, buffIndex, buildingIndex, expeditionIndex, featureIndex, questIndex, technologyIndex);
            DefinitionEffectsCompiler.Compile(ref builder, source.Effects, ref target.Effects, buildingIndex, heroIndex, itemIndex, soldierIndex, talentIndex, technologyIndex);
        }
    }
}
