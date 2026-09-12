using System;
using System.Collections.Generic;

namespace Landsong.ECS.Authoring
{
    public static class TalentGrantAuthoringValidation
    {
        public static void Validate(ContentSource source, TalentJobEffect effect, ContentSource target)
        {
            string context = source.Id + " / 人才任职效果 / 执行顺序 " + effect.Order + " / 目标 " + target.Id;
            void Fail(string reason) => throw new InvalidOperationException(context + "：" + reason);
            if (source.Kind != ContentKind.Talent || effect.Timing != TalentEffectTiming.每回合)
                Fail("内容许可仅支持人才自身每回合发放，不支持岗位或被动发放");
            if (target.Kind != ContentKind.Building && target.Kind != ContentKind.Buff && target.Kind != ContentKind.Feature)
                Fail("只能发放建筑蓝图、永久增益或功能许可；完成标记由所属领域记录");
            if (effect.Scaling != TalentEffectScaling.固定 && effect.Scaling != TalentEffectScaling.人才等级)
                Fail("许可等级仅支持固定或人才等级缩放，不能复用物品、人口或建筑数量作为授权等级");
            foreach (int level in EffectLevels(source, effect))
            {
                float value = effect.BaseValue + effect.PerLevel * (level - 1);
                if (effect.Scaling == TalentEffectScaling.人才等级) value *= level;
                double grant = Math.Floor(value);
                if (double.IsNaN(grant) || double.IsInfinity(grant) || grant < 1 || grant > int.MaxValue)
                    Fail("人才等级 " + level + " 计算出无效许可等级 " + value);
                var error = EntitlementRules.Error(target.Kind, target.Id, target.Level, target.Flags, (int)grant);
                if (error != null) Fail("人才等级 " + level + "，许可等级 " + grant + "：" + error);
            }
        }

        public static void ValidateIncome(ContentSource source, TalentItemIncome income)
        {
            GrowthRange(source);
            foreach (int level in new[] { source.Level, source.Capacity })
            {
                float increment = income.PerLevel * (level - 1);
                double amount = income.BaseQuantity + Math.Truncate((double)increment);
                if (float.IsNaN(increment) || float.IsInfinity(increment) || (double)increment < int.MinValue
                    || (double)increment > int.MaxValue || amount < 0 || amount > int.MaxValue)
                    throw new InvalidOperationException(source.Id + " / 人才每回合产物 / 执行顺序 " + income.Order
                        + "：人才等级 " + level + " 计算出无效物品数量 " + amount);
            }
        }

        public static void ValidateIncome(ContentSource source, TalentJobEffect effect)
        {
            // Passive negative modifiers are valid; grants have a separate strictly-positive contract.
            if (effect.Timing != TalentEffectTiming.每回合
                || effect.Effect != TalentEffectType.物品 && effect.Effect != TalentEffectType.科研点) return;
            string context = source.Id + " / 人才任职效果 / 执行顺序 " + effect.Order;
            bool counted = effect.Scaling == TalentEffectScaling.每百份物品
                || effect.Scaling == TalentEffectScaling.王国人口 || effect.Scaling == TalentEffectScaling.运营建筑数;
            foreach (int level in EffectLevels(source, effect))
            {
                float value = effect.BaseValue + effect.PerLevel * (level - 1);
                if (effect.Scaling == TalentEffectScaling.人才等级) value *= level;
                // Counted sources are only known at settlement. Validate their coefficient here;
                // the runtime checked conversion/addition protects the actual scaled quantity.
                double amount = counted ? value : Math.Floor(value);
                if (double.IsNaN(amount) || double.IsInfinity(amount) || amount < 0 || !counted && amount > int.MaxValue)
                    throw new InvalidOperationException(context + "：人才等级 " + level + " 计算出无效"
                        + (counted ? "收益系数 " : "周期收益 ") + value);
            }
        }

        static HashSet<int> EffectLevels(ContentSource source, TalentJobEffect effect)
        {
            GrowthRange(source);
            var levels = new HashSet<int> { source.Level, source.Capacity };
            if (effect.Scaling == TalentEffectScaling.人才等级 && effect.PerLevel != 0)
            {
                // Level scaling is quadratic. Check the integer neighbours of its turning point
                // as well as both endpoints, without iterating an unbounded authored capacity.
                double vertex = ((double)effect.PerLevel - effect.BaseValue) / (2d * effect.PerLevel);
                if (vertex > source.Level && vertex < source.Capacity)
                {
                    int at = (int)Math.Floor(vertex);
                    levels.Add(at); levels.Add(at + 1);
                }
            }
            return levels;
        }

        static void GrowthRange(ContentSource source)
        {
            if (source.Level < 1 || source.Capacity < source.Level)
                throw new InvalidOperationException(source.Id + "：人才初始等级与最高等级范围无效");
        }
    }
}
