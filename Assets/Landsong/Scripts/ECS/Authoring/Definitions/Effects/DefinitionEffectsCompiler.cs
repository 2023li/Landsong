using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class DefinitionEffectsCompiler
    {
        public static void Compile(ref BlobBuilder builder, DefinitionEffectsSource source, ref global::Landsong.ECS.Definitions.DefinitionEffects target, BuildingCatalogIndex buildingIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, SoldierCatalogIndex soldierIndex, TalentCatalogIndex talentIndex, TechnologyCatalogIndex technologyIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 DefinitionEffects 配置。");
            if (source.Items == null)
                throw new InvalidOperationException("物品列表不能为空引用。");
            var orderedItems = source.Items.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Items = builder.Allocate(ref target.Items, orderedItems.Length);
            for (int i = 0; i < orderedItems.Length; i++)
            {
                ItemNumericEffectCompiler.Compile(ref builder, orderedItems[i], ref Items[i], itemIndex);
            }

            if (source.Buildings == null)
                throw new InvalidOperationException("建筑列表不能为空引用。");
            var orderedBuildings = source.Buildings.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Buildings = builder.Allocate(ref target.Buildings, orderedBuildings.Length);
            for (int i = 0; i < orderedBuildings.Length; i++)
            {
                BuildingNumericEffectCompiler.Compile(ref builder, orderedBuildings[i], ref Buildings[i], buildingIndex);
            }

            if (source.Soldiers == null)
                throw new InvalidOperationException("士兵列表不能为空引用。");
            var orderedSoldiers = source.Soldiers.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Soldiers = builder.Allocate(ref target.Soldiers, orderedSoldiers.Length);
            for (int i = 0; i < orderedSoldiers.Length; i++)
            {
                SoldierNumericEffectCompiler.Compile(ref builder, orderedSoldiers[i], ref Soldiers[i], soldierIndex);
            }

            if (source.Heroes == null)
                throw new InvalidOperationException("英雄列表不能为空引用。");
            var orderedHeroes = source.Heroes.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Heroes = builder.Allocate(ref target.Heroes, orderedHeroes.Length);
            for (int i = 0; i < orderedHeroes.Length; i++)
            {
                HeroNumericEffectCompiler.Compile(ref builder, orderedHeroes[i], ref Heroes[i], heroIndex);
            }

            if (source.Talents == null)
                throw new InvalidOperationException("人才列表不能为空引用。");
            var orderedTalents = source.Talents.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Talents = builder.Allocate(ref target.Talents, orderedTalents.Length);
            for (int i = 0; i < orderedTalents.Length; i++)
            {
                TalentNumericEffectCompiler.Compile(ref builder, orderedTalents[i], ref Talents[i], talentIndex);
            }

            if (source.Kingdom == null)
                throw new InvalidOperationException("王国全局列表不能为空引用。");
            var orderedKingdom = source.Kingdom.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Kingdom = builder.Allocate(ref target.Kingdom, orderedKingdom.Length);
            for (int i = 0; i < orderedKingdom.Length; i++)
            {
                KingdomNumericEffectCompiler.Compile(ref builder, orderedKingdom[i], ref Kingdom[i]);
            }

            if (source.Intelligence == null)
                throw new InvalidOperationException("情报列表不能为空引用。");
            var orderedIntelligence = source.Intelligence.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Intelligence = builder.Allocate(ref target.Intelligence, orderedIntelligence.Length);
            for (int i = 0; i < orderedIntelligence.Length; i++)
            {
                IntelligenceEffectCompiler.Compile(ref builder, orderedIntelligence[i], ref Intelligence[i], technologyIndex);
            }

            if (source.FlatProduction == null)
                throw new InvalidOperationException("固定产出列表不能为空引用。");
            var orderedFlatProduction = source.FlatProduction.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var FlatProduction = builder.Allocate(ref target.FlatProduction, orderedFlatProduction.Length);
            for (int i = 0; i < orderedFlatProduction.Length; i++)
            {
                FlatProductionEffectCompiler.Compile(ref builder, orderedFlatProduction[i], ref FlatProduction[i], buildingIndex, itemIndex);
            }
        }
    }
}
