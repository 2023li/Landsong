using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Soldier catalog. It cannot resolve another domain.</summary>
    public sealed class SoldierCatalogIndex
    {
        readonly Dictionary<SoldierDefinitionAsset, SoldierId> assets = new Dictionary<SoldierDefinitionAsset, SoldierId>();
        public SoldierCatalogIndex(SoldierCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Soldier 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Soldier 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, SoldierId.FromIndex(i));
            }
        }

        public SoldierId Resolve(SoldierDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Soldier 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Soldier 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class SoldierCatalogCompiler
    {
        public static BlobAssetReference<SoldierCatalogBlob> Build(SoldierCatalogAsset catalog, ItemCatalogIndex itemIndex)
        {
            var soldierIndex = new SoldierCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<SoldierCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], itemIndex);
                return builder.CreateBlobAssetReference<SoldierCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, SoldierDefinitionAsset source, ref SoldierDefinition target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Soldier 定义配置。");
            UnitGrowthValidation.Soldier(source.Growth, source.PopulationCost, source.FallbackRecruitGold);
            foreach (var cost in source.RecruitmentCosts)
                if (cost == null || cost.Level < 0 || cost.Level > 1)
                    throw new InvalidOperationException("募兵费用等级只允许0或1。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            UnitCombatStatsCompiler.Compile(ref builder, source.CombatStats, ref target.CombatStats);
            target.ThreatValue = source.ThreatValue;
            target.TargetMode = source.TargetMode;
            target.PopulationCost = source.PopulationCost;
            target.FallbackRecruitGold = source.FallbackRecruitGold;
            target.Growth = source.Growth;
            if (source.RecruitmentCosts == null)
                throw new InvalidOperationException("募兵费用列表不能为空引用。");
            var orderedRecruitmentCosts = source.RecruitmentCosts.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var RecruitmentCosts = builder.Allocate(ref target.RecruitmentCosts, orderedRecruitmentCosts.Length);
            for (int i = 0; i < orderedRecruitmentCosts.Length; i++)
            {
                LeveledItemAmountCompiler.Compile(ref builder, orderedRecruitmentCosts[i], ref RecruitmentCosts[i], itemIndex);
            }
        }
    }
}
