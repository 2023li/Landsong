using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Crop catalog. It cannot resolve another domain.</summary>
    public sealed class CropCatalogIndex
    {
        readonly Dictionary<CropDefinitionAsset, CropId> assets = new Dictionary<CropDefinitionAsset, CropId>();
        public CropCatalogIndex(CropCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Crop 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Crop 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, CropId.FromIndex(i));
            }
        }

        public CropId Resolve(CropDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Crop 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Crop 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class CropCatalogCompiler
    {
        public static BlobAssetReference<CropCatalogBlob> Build(CropCatalogAsset catalog, ItemCatalogIndex itemIndex)
        {
            var cropIndex = new CropCatalogIndex(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<CropCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], itemIndex);
                return builder.CreateBlobAssetReference<CropCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, CropDefinitionAsset source, ref CropDefinition target, ItemCatalogIndex itemIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Crop 定义配置。");
            if (source.GrowthTurns < 1 || source.RequiredWorkers < 0 || source.FullStaffBonusWorkers < source.RequiredWorkers)
                throw new InvalidOperationException("作物成长或工人要求无效。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.GrowthTurns = source.GrowthTurns;
            target.RequiredWorkers = source.RequiredWorkers;
            target.FullStaffBonusWorkers = source.FullStaffBonusWorkers;
            target.FullStaffYieldBonus = source.FullStaffYieldBonus;
            if (source.PlantingCosts == null)
                throw new InvalidOperationException("种植费用列表不能为空引用。");
            var orderedPlantingCosts = source.PlantingCosts.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var PlantingCosts = builder.Allocate(ref target.PlantingCosts, orderedPlantingCosts.Length);
            for (int i = 0; i < orderedPlantingCosts.Length; i++)
            {
                ItemAmountCompiler.Compile(ref builder, orderedPlantingCosts[i], ref PlantingCosts[i], itemIndex);
            }

            if (source.AutomaticHarvestCosts == null)
                throw new InvalidOperationException("自动收获费用列表不能为空引用。");
            var orderedAutomaticHarvestCosts = source.AutomaticHarvestCosts.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var AutomaticHarvestCosts = builder.Allocate(ref target.AutomaticHarvestCosts, orderedAutomaticHarvestCosts.Length);
            for (int i = 0; i < orderedAutomaticHarvestCosts.Length; i++)
            {
                ItemAmountCompiler.Compile(ref builder, orderedAutomaticHarvestCosts[i], ref AutomaticHarvestCosts[i], itemIndex);
            }

            if (source.HarvestOutputs == null)
                throw new InvalidOperationException("收获产物列表不能为空引用。");
            var orderedHarvestOutputs = source.HarvestOutputs.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var HarvestOutputs = builder.Allocate(ref target.HarvestOutputs, orderedHarvestOutputs.Length);
            for (int i = 0; i < orderedHarvestOutputs.Length; i++)
            {
                ItemQuantityRangeCompiler.Compile(ref builder, orderedHarvestOutputs[i], ref HarvestOutputs[i], itemIndex);
            }
        }
    }
}
