using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    /// <summary>Index of one explicit Item catalog. It cannot resolve another domain.</summary>
    public sealed class ItemCatalogIndex
    {
        readonly Dictionary<ItemDefinitionAsset, ItemId> assets = new Dictionary<ItemDefinitionAsset, ItemId>();
        public ItemCatalogIndex(ItemCatalogAsset catalog)
        {
            if (catalog == null || catalog.Definitions == null)
                throw new InvalidOperationException("缺少 Item 目录。");
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < catalog.Definitions.Length; i++)
            {
                var asset = catalog.Definitions[i];
                if (asset == null || asset.Metadata == null || string.IsNullOrWhiteSpace(asset.Metadata.Id) || !stableIds.Add(asset.Metadata.Id) || assets.ContainsKey(asset))
                    throw new InvalidOperationException("Item 目录包含空定义、重复资源或重复稳定标识。");
                assets.Add(asset, ItemId.FromIndex(i));
            }
        }

        public ItemId Resolve(ItemDefinitionAsset asset, bool optional = false)
        {
            if (asset == null)
            {
                if (optional)
                    return default;
                throw new InvalidOperationException("缺少 Item 定义引用。");
            }

            if (!assets.TryGetValue(asset, out var id))
                throw new InvalidOperationException("Item 定义未注册到指定领域目录：" + asset.name);
            return id;
        }
    }

    public static class ItemCatalogCompiler
    {
        public static BlobAssetReference<ItemCatalogBlob> Build(ItemCatalogAsset catalog, ItemGroupCatalogIndex itemGroupIndex)
        {
            var itemIndex = new ItemCatalogIndex(catalog);
            var equipmentKinds = new HashSet<SoldierWeaponKind>();
            foreach (var item in catalog.Definitions)
                if (item is EquipmentDefinitionAsset equipment && equipment.Equipment.Weapon != SoldierWeaponKind.None && !equipmentKinds.Add(equipment.Equipment.Weapon))
                    throw new InvalidOperationException("同一种装备类型不能对应多个物品：" + item.name);
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<ItemCatalogBlob>();
                var definitions = builder.Allocate(ref root.Definitions, catalog.Definitions.Length);
                for (int i = 0; i < catalog.Definitions.Length; i++)
                    Compile(ref builder, catalog.Definitions[i], ref definitions[i], itemGroupIndex);
                return builder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        public static void Compile(ref BlobBuilder builder, ItemDefinitionAsset source, ref ItemDefinition target, ItemGroupCatalogIndex itemGroupIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 Item 定义配置。");
            if (source.MaximumStack <= 0)
                throw new InvalidOperationException("物品堆叠上限必须大于零。");
            if (source.NaturalLossRate < 0)
                throw new InvalidOperationException("物品自然损耗率不能为负。");
            var equipment = source is EquipmentDefinitionAsset equipmentSource ? equipmentSource.Equipment : default;
            if (source is EquipmentDefinitionAsset &&
                ((byte)equipment.Weapon == 0 || (byte)equipment.Weapon > (byte)SoldierWeaponKind.Club ||
                 equipment.AttackMode < WeaponAttackMode.Melee || equipment.AttackMode > WeaponAttackMode.Ranged ||
                 !math.isfinite(equipment.StrengthMultiplier) || equipment.StrengthMultiplier <= 0 ||
                 !math.isfinite(equipment.BreakChance) || equipment.BreakChance < 0 || equipment.BreakChance > 1))
                throw new InvalidOperationException("装备类型、攻击方式、力量系数或损坏概率无效：" + source.name);
            if ((byte)source.Theft.Protection > 7 || source.Theft.Weight < 0 || source.Theft.Weight > 10000 || source.Theft.Maximum < 0 || source.Theft.Maximum > 10000 || source.Theft.UnitValue < 1 || source.Theft.UnitValue > 100000)
                throw new InvalidOperationException("物品被盗价值、权重或数量上限无效。");
            if (source.Metadata == null || string.IsNullOrWhiteSpace(source.Metadata.Id))
                throw new InvalidOperationException("缺少稳定定义标识。");
            target.Metadata.Id = new FixedString128Bytes(source.Metadata.Id);
            target.Metadata.Name = new FixedString128Bytes(source.Metadata.Name ?? "");
            target.PrimaryGroup = itemGroupIndex.Resolve(source.PrimaryGroup, true);
            if (source.AdditionalGroups == null)
                throw new InvalidOperationException("额外物品组列表不能为空引用。");
            var AdditionalGroups = builder.Allocate(ref target.AdditionalGroups, source.AdditionalGroups.Length);
            for (int i = 0; i < source.AdditionalGroups.Length; i++)
            {
                AdditionalGroups[i] = itemGroupIndex.Resolve(source.AdditionalGroups[i], false);
            }

            target.MaximumStack = source.MaximumStack;
            target.TradeValue = source.TradeValue;
            if (!math.isfinite(source.NaturalLossRate))
                throw new InvalidOperationException("自然损耗率必须是有限数值。");
            target.NaturalLossRate = source.NaturalLossRate;
            target.Equipment = equipment;
            target.Theft = source.Theft;
        }
    }
}
