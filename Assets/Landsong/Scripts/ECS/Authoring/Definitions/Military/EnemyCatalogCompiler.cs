using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Enemy catalog. It cannot resolve another domain.</summary>
    public sealed class EnemyCatalogIndex
    {
        readonly Dictionary<EnemyDefinitionAsset, EnemyId> assets = new Dictionary<EnemyDefinitionAsset, EnemyId>();
        public EnemyCatalogIndex(EnemyCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Enemy 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Enemy 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, EnemyId.FromIndex(i));
            }
        }

        public EnemyId Resolve(EnemyDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Enemy 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Enemy 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class EnemyCatalogCompiler
    {
        public static BlobAssetReference<EnemyCatalogBlob> Build(EnemyCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            var enemyIndex = new EnemyCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<EnemyCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, featureIndex, itemIndex);
                return builder.CreateBlobAssetReference<EnemyCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, EnemyDefinitionAsset source, ref EnemyDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Enemy 定义配置。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            UnitCombatStatsCompiler.Compile(ref builder, source.CombatStats, ref target.CombatStats);
            target.ThreatValue = source.ThreatValue;
            if (source.NightPower < 0) throw new InvalidOperationException("敌人夜晚战力不能为负数。");
            target.NightPower = source.NightPower;
            target.Behavior = source.Behavior;
            target.PreferredTargetCategory = source.PreferredTargetCategory;
            DefinitionRewardsCompiler.Compile(ref builder, source.KillRewards, ref target.KillRewards, buffIndex, buildingIndex, featureIndex, itemIndex);
            if (source.SpecialDrops == null)
                throw new InvalidOperationException("特殊掉落列表不能为空引用。");
            var orderedSpecialDrops = source.SpecialDrops.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var SpecialDrops = builder.Allocate(ref target.SpecialDrops, orderedSpecialDrops.Length);
            for (int i = 0; i < orderedSpecialDrops.Length; i++)
            {
                SpecialItemDropCompiler.Compile(ref builder, orderedSpecialDrops[i], ref SpecialDrops[i], itemIndex);
            }
        }
    }
}
