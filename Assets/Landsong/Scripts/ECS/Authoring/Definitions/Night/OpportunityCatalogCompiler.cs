using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Opportunity catalog. It cannot resolve another domain.</summary>
    public sealed class OpportunityCatalogIndex
    {
        readonly Dictionary<OpportunityDefinitionAsset, OpportunityId> assets = new Dictionary<OpportunityDefinitionAsset, OpportunityId>();
        public OpportunityCatalogIndex(OpportunityCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Opportunity 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Opportunity 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, OpportunityId.FromIndex(i));
            }
        }

        public OpportunityId Resolve(OpportunityDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Opportunity 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Opportunity 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class OpportunityCatalogCompiler
    {
        public static BlobAssetReference<OpportunityCatalogBlob> Build(OpportunityCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            var opportunityIndex = new OpportunityCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<OpportunityCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, featureIndex, itemIndex);
                return builder.CreateBlobAssetReference<OpportunityCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, OpportunityDefinitionAsset source, ref OpportunityDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Opportunity 定义配置。");
            OpportunityProfileValidation.Validate(source.VisitorProfile);
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.VisitorProfile = source.VisitorProfile;
            DefinitionRewardsCompiler.Compile(ref builder, source.Rewards, ref target.Rewards, buffIndex, buildingIndex, featureIndex, itemIndex);
        }
    }
}
