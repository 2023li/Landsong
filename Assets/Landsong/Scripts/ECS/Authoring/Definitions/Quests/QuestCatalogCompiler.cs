using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Quest catalog. It cannot resolve another domain.</summary>
    public sealed class QuestCatalogIndex
    {
        readonly Dictionary<QuestDefinitionAsset, QuestId> assets = new Dictionary<QuestDefinitionAsset, QuestId>();
        public QuestCatalogIndex(QuestCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Quest 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Quest 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, QuestId.FromIndex(i));
            }
        }

        public QuestId Resolve(QuestDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Quest 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Quest 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class QuestCatalogCompiler
    {
        public static BlobAssetReference<QuestCatalogBlob> Build(QuestCatalogAsset catalog, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex, TechnologyCatalogIndex technologyIndex)
        {
            var questIndex = new QuestCatalogIndex(catalog);
            QuestCatalogValidation.Validate(catalog);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<QuestCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], buffIndex, buildingIndex, expeditionIndex, featureIndex, itemIndex, questIndex, technologyIndex);
                return builder.CreateBlobAssetReference<QuestCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, QuestDefinitionAsset source, ref QuestDefinition target, BuffCatalogIndex buffIndex, BuildingCatalogIndex buildingIndex, ExpeditionCatalogIndex expeditionIndex, FeatureCatalogIndex featureIndex, ItemCatalogIndex itemIndex, QuestCatalogIndex questIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Quest 定义配置。");
            if (source.DeadlineTurns < 0 || source.Intensity < 0 || source.Intensity > 3 || source.ItemQuantityScale <= 0 || source.OfferWeight < 0 || source.MinimumRefreshTurns < 0 || source.MaximumRefreshTurns < source.MinimumRefreshTurns)
                throw new InvalidOperationException("任务时限、强度、权重或数量倍率无效。");
            foreach (var penalty in source.FailurePenalties)
                if (penalty == null || penalty.Quantity <= 0)
                    throw new InvalidOperationException("失败惩罚数量必须为正。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.DeadlineTurns = source.DeadlineTurns;
            target.Behavior = source.Behavior;
            target.OfferType = source.OfferType;
            target.Intensity = source.Intensity;
            if (!math.isfinite(source.OfferWeight))
                throw new InvalidOperationException("抽取权重必须是有限数值。");
            target.OfferWeight = source.OfferWeight;
            if (!math.isfinite(source.ItemQuantityScale))
                throw new InvalidOperationException("物品数量倍率必须是有限数值。");
            target.ItemQuantityScale = source.ItemQuantityScale;
            target.NextQuest = questIndex.Resolve(source.NextQuest, true);
            target.MinimumRefreshTurns = source.MinimumRefreshTurns;
            target.MaximumRefreshTurns = source.MaximumRefreshTurns;
            DefinitionPrerequisitesCompiler.Compile(ref builder, source.RefreshPrerequisites, ref target.RefreshPrerequisites, buffIndex, buildingIndex, expeditionIndex, featureIndex, questIndex, technologyIndex);
            DefinitionPrerequisitesCompiler.Compile(ref builder, source.Prerequisites, ref target.Prerequisites, buffIndex, buildingIndex, expeditionIndex, featureIndex, questIndex, technologyIndex);
            QuestObjectivesCompiler.Compile(ref builder, source.Objectives, ref target.Objectives, buildingIndex, itemIndex, technologyIndex, source.ItemQuantityScale);
            DefinitionRewardsCompiler.Compile(ref builder, source.Rewards, ref target.Rewards, buffIndex, buildingIndex, featureIndex, itemIndex, source.ItemQuantityScale);
            if (source.FailurePenalties == null)
                throw new InvalidOperationException("失败惩罚列表不能为空引用。");
            var orderedFailurePenalties = source.FailurePenalties.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var FailurePenalties = builder.Allocate(ref target.FailurePenalties, orderedFailurePenalties.Length);
            for (int i = 0; i < orderedFailurePenalties.Length; i++)
            {
                ItemAmountCompiler.Compile(ref builder, orderedFailurePenalties[i], ref FailurePenalties[i], itemIndex);
                FailurePenalties[i].Quantity = ItemQuantityScaling.Apply(FailurePenalties[i].Quantity, source.ItemQuantityScale);
            }
        }
    }
}
