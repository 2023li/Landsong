using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Expedition catalog. It cannot resolve another domain.</summary>
    public sealed class ExpeditionCatalogIndex
    {
        readonly Dictionary<ExpeditionDefinitionAsset, ExpeditionId> assets = new Dictionary<ExpeditionDefinitionAsset, ExpeditionId>();
        public ExpeditionCatalogIndex(ExpeditionCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Expedition 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Expedition 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, ExpeditionId.FromIndex(i));
            }
        }

        public ExpeditionId Resolve(ExpeditionDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Expedition 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Expedition 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class ExpeditionCatalogCompiler
    {
        public static BlobAssetReference<ExpeditionCatalogBlob> Build(ExpeditionCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, TechnologyCatalogIndex technologyIndex)
        {
            var expeditionIndex = new ExpeditionCatalogIndex(catalog);
            ExpeditionCatalogValidation.Validate(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<ExpeditionCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, expeditionIndex, featureIndex, itemIndex, questIndex, technologyIndex);
                return builder.CreateBlobAssetReference<ExpeditionCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, ExpeditionDefinitionAsset source, ref ExpeditionDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Expedition 定义配置。");
            if (source.MinimumCrew < 1 || source.MaximumCrew > 0 && source.MaximumCrew < source.MinimumCrew || source.TravelTurns < 1)
                throw new InvalidOperationException("远征人数或行程回合无效。");
            foreach (var penalty in source.FailurePenalties)
                if (penalty == null || penalty.Quantity <= 0)
                    throw new InvalidOperationException("失败惩罚数量必须为正。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.MinimumSiteLevel = source.MinimumSiteLevel;
            target.MinimumCrew = source.MinimumCrew;
            target.MaximumCrew = source.MaximumCrew;
            target.TravelTurns = source.TravelTurns;
            if (!math.isfinite(source.BaseSuccessChance))
                throw new InvalidOperationException("基础成功率必须是有限数值。");
            target.BaseSuccessChance = source.BaseSuccessChance;
            if (!math.isfinite(source.SuccessChancePerCrew))
                throw new InvalidOperationException("每人成功率加成必须是有限数值。");
            target.SuccessChancePerCrew = source.SuccessChancePerCrew;
            if (!math.isfinite(source.MaximumSuccessChance))
                throw new InvalidOperationException("成功率上限必须是有限数值。");
            target.MaximumSuccessChance = source.MaximumSuccessChance;
            if (!math.isfinite(source.FailureCasualtyRatio))
                throw new InvalidOperationException("失败伤亡比例必须是有限数值。");
            target.FailureCasualtyRatio = source.FailureCasualtyRatio;
            target.BaseCompensation = source.BaseCompensation;
            target.CompensationPerCrew = source.CompensationPerCrew;
            target.Repeatable = source.Repeatable;
            DefinitionPrerequisitesCompiler.Compile(ref builder, source.Prerequisites, ref target.Prerequisites, buffIndex, buildingIndex, expeditionIndex, featureIndex, questIndex, technologyIndex);
            DefinitionPrerequisitesCompiler.Compile(ref builder, source.Visibility, ref target.Visibility, buffIndex, buildingIndex, expeditionIndex, featureIndex, questIndex, technologyIndex);
            if (source.Supplies == null)
                throw new InvalidOperationException("远征补给列表不能为空引用。");
            var orderedSupplies = source.Supplies.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Supplies = builder.Allocate(ref target.Supplies, orderedSupplies.Length);
            for (int i = 0; i < orderedSupplies.Length; i++)
            {
                ExpeditionSupplyCompiler.Compile(ref builder, orderedSupplies[i], ref Supplies[i], itemIndex);
            }

            DefinitionRewardsCompiler.Compile(ref builder, source.Rewards, ref target.Rewards, buffIndex, buildingIndex, featureIndex, itemIndex);
            if (source.FailurePenalties == null)
                throw new InvalidOperationException("失败惩罚列表不能为空引用。");
            var orderedFailurePenalties = source.FailurePenalties.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var FailurePenalties = builder.Allocate(ref target.FailurePenalties, orderedFailurePenalties.Length);
            for (int i = 0; i < orderedFailurePenalties.Length; i++)
            {
                ItemAmountCompiler.Compile(ref builder, orderedFailurePenalties[i], ref FailurePenalties[i], itemIndex);
            }
        }
    }
}
