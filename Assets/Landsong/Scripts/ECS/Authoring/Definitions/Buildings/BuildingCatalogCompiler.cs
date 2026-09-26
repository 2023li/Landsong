using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Building catalog. It cannot resolve another domain.</summary>
    public sealed class BuildingCatalogIndex
    {
        readonly Dictionary<BuildingDefinitionAsset, BuildingId> assets = new Dictionary<BuildingDefinitionAsset, BuildingId>();
        public BuildingCatalogIndex(BuildingCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Building 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Building 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, BuildingId.FromIndex(i));
            }
        }

        public BuildingId Resolve(BuildingDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Building 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Building 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class BuildingCatalogCompiler
    {
        public static BlobAssetReference<BuildingCatalogBlob> Build(BuildingCatalogAsset catalog, BuildingLimitGroupCatalogIndex buildingLimitGroupIndex, CropCatalogIndex cropIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, ItemGroupCatalogIndex itemGroupIndex, SoldierCatalogIndex soldierIndex, StorageSlotCatalogIndex storageSlotIndex, TechnologyCatalogIndex technologyIndex)
        {
            var buildingIndex = new BuildingCatalogIndex(catalog);
            BuildingCatalogValidation.Validate(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<BuildingCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buildingIndex, buildingLimitGroupIndex, cropIndex, heroIndex, itemIndex, itemGroupIndex, soldierIndex, storageSlotIndex, technologyIndex);
                return builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, BuildingDefinitionAsset source, ref BuildingDefinition target, BuildingCatalogIndex buildingIndex, BuildingLimitGroupCatalogIndex buildingLimitGroupIndex, CropCatalogIndex cropIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, ItemGroupCatalogIndex itemGroupIndex, SoldierCatalogIndex soldierIndex, StorageSlotCatalogIndex storageSlotIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Building 定义配置。");
            if (source.MaximumLevel < 1 || source.Footprint.x < 1 || source.Footprint.y < 1 || source.MaximumDurability <= 0)
                throw new InvalidOperationException("建筑等级、占地与耐久必须大于零。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            if (!Enum.IsDefined(typeof(BuildingFaction), source.Faction))
                throw new InvalidOperationException("建筑阵营配置无效：" + source.name);
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.Faction = source.Faction;
            target.LimitGroup = buildingLimitGroupIndex.Resolve(source.LimitGroup, true);
            target.MaximumLevel = source.MaximumLevel;
            target.ConstructionTurns = source.ConstructionTurns;
            target.ResourceConnectionActionPower = source.ResourceConnectionActionPower;
            target.MaximumCount = source.MaximumCount;
            target.Footprint = new int2(source.Footprint.x, source.Footprint.y);
            if (!math.isfinite(source.MaximumDurability))
                throw new InvalidOperationException("耐久上限必须是有限数值。");
            target.MaximumDurability = source.MaximumDurability;
            if (source.NightPower < 0) throw new InvalidOperationException("建筑夜晚战力不能为负数。");
            target.NightPower = source.NightPower;
            if (!math.isfinite(source.MovementCost))
                throw new InvalidOperationException("通行消耗必须是有限数值。");
            target.MovementCost = source.MovementCost;
            UnitCombatStatsCompiler.Compile(ref builder, source.DefenseStats, ref target.DefenseStats);
            BuildingPlacementAndVisualsCompiler.Compile(ref builder, source.PlacementAndVisuals, ref target.PlacementAndVisuals);
            BuildingCapabilitiesCompiler.Compile(ref builder, source.Capabilities, ref target.Capabilities, buildingIndex, cropIndex, heroIndex, itemIndex, itemGroupIndex, soldierIndex, storageSlotIndex, technologyIndex);
        }
    }
}
