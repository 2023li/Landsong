using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class TalentJobEffectsCompiler
    {
        public static void Compile(ref BlobBuilder builder, TalentJobEffectsSource source, ref global::Landsong.ECS.Definitions.TalentJobEffects target, BuildingCatalogIndex buildingIndex, HeroCatalogIndex heroIndex, ItemCatalogIndex itemIndex, SoldierCatalogIndex soldierIndex)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 TalentJobEffects 配置。");
            if (source.Items == null)
                throw new InvalidOperationException("物品列表不能为空引用。");
            var orderedItems = source.Items.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Items = builder.Allocate(ref target.Items, orderedItems.Length);
            for (int i = 0; i < orderedItems.Length; i++)
            {
                TalentItemJobEffectCompiler.Compile(ref builder, orderedItems[i], ref Items[i], buildingIndex, itemIndex);
            }

            if (source.Soldiers == null)
                throw new InvalidOperationException("士兵列表不能为空引用。");
            var orderedSoldiers = source.Soldiers.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Soldiers = builder.Allocate(ref target.Soldiers, orderedSoldiers.Length);
            for (int i = 0; i < orderedSoldiers.Length; i++)
            {
                TalentSoldierJobEffectCompiler.Compile(ref builder, orderedSoldiers[i], ref Soldiers[i], buildingIndex, itemIndex, soldierIndex);
            }

            if (source.Heroes == null)
                throw new InvalidOperationException("英雄列表不能为空引用。");
            var orderedHeroes = source.Heroes.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Heroes = builder.Allocate(ref target.Heroes, orderedHeroes.Length);
            for (int i = 0; i < orderedHeroes.Length; i++)
            {
                TalentHeroJobEffectCompiler.Compile(ref builder, orderedHeroes[i], ref Heroes[i], buildingIndex, heroIndex, itemIndex);
            }

            if (source.Kingdom == null)
                throw new InvalidOperationException("王国全局列表不能为空引用。");
            var orderedKingdom = source.Kingdom.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var Kingdom = builder.Allocate(ref target.Kingdom, orderedKingdom.Length);
            for (int i = 0; i < orderedKingdom.Length; i++)
            {
                TalentKingdomJobEffectCompiler.Compile(ref builder, orderedKingdom[i], ref Kingdom[i], buildingIndex, itemIndex);
            }
        }
    }
}
