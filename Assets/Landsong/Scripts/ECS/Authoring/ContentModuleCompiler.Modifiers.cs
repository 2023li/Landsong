using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteModifiers(ModifiersContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result)
        {
            if(module==null)throw new InvalidOperationException("缺少属性与经济效果模块");
            if(!module.Enabled)return;
            int firstRule = result.Count;
            if(module.LossModifier==null)throw new InvalidOperationException("损耗倍率列表为空引用");
            foreach(var entry in module.LossModifier)
            {
                if(entry==null)throw new InvalidOperationException("损耗倍率存在空条目");
                Finite(entry.Magnitude,"损耗倍率 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.LossModifier,Target=references.Resolve(entry.Subject,true,"损耗倍率 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.ProductionBonus==null)throw new InvalidOperationException("生产倍率加成列表为空引用");
            foreach(var entry in module.ProductionBonus)
            {
                if(entry==null)throw new InvalidOperationException("生产倍率加成存在空条目");
                Finite(entry.Magnitude,"生产倍率加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.ProductionBonus,Target=references.Resolve(entry.Subject,true,"生产倍率加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.AttackBonus==null)throw new InvalidOperationException("攻击加成列表为空引用");
            foreach(var entry in module.AttackBonus)
            {
                if(entry==null)throw new InvalidOperationException("攻击加成存在空条目");
                Finite(entry.Magnitude,"攻击加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.AttackBonus,Target=references.Resolve(entry.Subject,true,"攻击加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.HealthBonus==null)throw new InvalidOperationException("生命加成列表为空引用");
            foreach(var entry in module.HealthBonus)
            {
                if(entry==null)throw new InvalidOperationException("生命加成存在空条目");
                Finite(entry.Magnitude,"生命加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.HealthBonus,Target=references.Resolve(entry.Subject,true,"生命加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.PublicOpinion==null)throw new InvalidOperationException("每回合民意列表为空引用");
            foreach(var entry in module.PublicOpinion)
            {
                if(entry==null)throw new InvalidOperationException("每回合民意存在空条目");
                Finite(entry.Magnitude,"每回合民意 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.PublicOpinion,Target=references.Resolve(entry.Subject,true,"每回合民意 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.SoldierAttackBonus==null)throw new InvalidOperationException("士兵力量加成列表为空引用");
            foreach(var entry in module.SoldierAttackBonus)
            {
                if(entry==null)throw new InvalidOperationException("士兵力量加成存在空条目");
                Finite(entry.Magnitude,"士兵力量加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.SoldierAttackBonus,Target=references.Resolve(entry.Subject,true,"士兵力量加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.SoldierSpeedBonus==null)throw new InvalidOperationException("士兵敏捷加成列表为空引用");
            foreach(var entry in module.SoldierSpeedBonus)
            {
                if(entry==null)throw new InvalidOperationException("士兵敏捷加成存在空条目");
                Finite(entry.Magnitude,"士兵敏捷加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.SoldierSpeedBonus,Target=references.Resolve(entry.Subject,true,"士兵敏捷加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.ActionPowerBonus==null)throw new InvalidOperationException("行动力加成列表为空引用");
            foreach(var entry in module.ActionPowerBonus)
            {
                if(entry==null)throw new InvalidOperationException("行动力加成存在空条目");
                Finite(entry.Magnitude,"行动力加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.ActionPowerBonus,Target=references.Resolve(entry.Subject,true,"行动力加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.CropHarvestBonus==null)throw new InvalidOperationException("作物收获加成列表为空引用");
            foreach(var entry in module.CropHarvestBonus)
            {
                if(entry==null)throw new InvalidOperationException("作物收获加成存在空条目");
                Finite(entry.Magnitude,"作物收获加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.CropHarvestBonus,Target=references.Resolve(entry.Subject,true,"作物收获加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.PlotRisk==null)throw new InvalidOperationException("阴谋风险加成列表为空引用");
            foreach(var entry in module.PlotRisk)
            {
                if(entry==null)throw new InvalidOperationException("阴谋风险加成存在空条目");
                Finite(entry.Magnitude,"阴谋风险加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.PlotRisk,Target=references.Resolve(entry.Subject,true,"阴谋风险加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.NaturalDeathRisk==null)throw new InvalidOperationException("自然死亡风险加成列表为空引用");
            foreach(var entry in module.NaturalDeathRisk)
            {
                if(entry==null)throw new InvalidOperationException("自然死亡风险加成存在空条目");
                Finite(entry.Magnitude,"自然死亡风险加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.NaturalDeathRisk,Target=references.Resolve(entry.Subject,true,"自然死亡风险加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.ResearchOutput==null)throw new InvalidOperationException("科研固定加成列表为空引用");
            foreach(var entry in module.ResearchOutput)
            {
                if(entry==null)throw new InvalidOperationException("科研固定加成存在空条目");
                Finite(entry.Magnitude,"科研固定加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.ResearchOutput,Target=references.Resolve(entry.Subject,true,"科研固定加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.RangeBonus==null)throw new InvalidOperationException("射程加成列表为空引用");
            foreach(var entry in module.RangeBonus)
            {
                if(entry==null)throw new InvalidOperationException("射程加成存在空条目");
                Finite(entry.Magnitude,"射程加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.RangeBonus,Target=references.Resolve(entry.Subject,true,"射程加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.AttackSpeedBonus==null)throw new InvalidOperationException("攻速加成列表为空引用");
            foreach(var entry in module.AttackSpeedBonus)
            {
                if(entry==null)throw new InvalidOperationException("攻速加成存在空条目");
                Finite(entry.Magnitude,"攻速加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.AttackSpeedBonus,Target=references.Resolve(entry.Subject,true,"攻速加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.SpeedBonus==null)throw new InvalidOperationException("移动速度加成列表为空引用");
            foreach(var entry in module.SpeedBonus)
            {
                if(entry==null)throw new InvalidOperationException("移动速度加成存在空条目");
                Finite(entry.Magnitude,"移动速度加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.SpeedBonus,Target=references.Resolve(entry.Subject,true,"移动速度加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.ArmorBonus==null)throw new InvalidOperationException("护甲加成列表为空引用");
            foreach(var entry in module.ArmorBonus)
            {
                if(entry==null)throw new InvalidOperationException("护甲加成存在空条目");
                Finite(entry.Magnitude,"护甲加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.ArmorBonus,Target=references.Resolve(entry.Subject,true,"护甲加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.DamageReductionBonus==null)throw new InvalidOperationException("减伤加成列表为空引用");
            foreach(var entry in module.DamageReductionBonus)
            {
                if(entry==null)throw new InvalidOperationException("减伤加成存在空条目");
                Finite(entry.Magnitude,"减伤加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.DamageReductionBonus,Target=references.Resolve(entry.Subject,true,"减伤加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.PenetrationBonus==null)throw new InvalidOperationException("穿甲加成列表为空引用");
            foreach(var entry in module.PenetrationBonus)
            {
                if(entry==null)throw new InvalidOperationException("穿甲加成存在空条目");
                Finite(entry.Magnitude,"穿甲加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.PenetrationBonus,Target=references.Resolve(entry.Subject,true,"穿甲加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.ProjectileSpeedBonus==null)throw new InvalidOperationException("弹速加成列表为空引用");
            foreach(var entry in module.ProjectileSpeedBonus)
            {
                if(entry==null)throw new InvalidOperationException("弹速加成存在空条目");
                Finite(entry.Magnitude,"弹速加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.ProjectileSpeedBonus,Target=references.Resolve(entry.Subject,true,"弹速加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.BlastRadiusBonus==null)throw new InvalidOperationException("爆炸半径加成列表为空引用");
            foreach(var entry in module.BlastRadiusBonus)
            {
                if(entry==null)throw new InvalidOperationException("爆炸半径加成存在空条目");
                Finite(entry.Magnitude,"爆炸半径加成 / 效果数值");
                Add(result,entry,new Rule{Kind=RuleKind.BlastRadiusBonus,Target=references.Resolve(entry.Subject,true,"爆炸半径加成 / 指定作用对象（空 = 全局）"),Secondary=-1,Value=entry.Magnitude});
            }
            if(module.FlatProduction==null)throw new InvalidOperationException("固定物品产出加成列表为空引用");
            foreach(var entry in module.FlatProduction)
            {
                if(entry==null)throw new InvalidOperationException("固定物品产出加成存在空条目");
                Add(result,entry,new Rule{Kind=RuleKind.FlatProductionBonus,Target=references.Resolve(entry.Item,false,"固定物品产出加成 / 物品",ContentKind.Item),Secondary=references.Resolve(entry.Building,true,"固定物品产出加成 / 指定建筑（空 = 全部）",ContentKind.Building),Amount=entry.Quantity});
            }
            if(module.Intelligence==null)throw new InvalidOperationException("情报加成列表为空引用");
            foreach(var entry in module.Intelligence)
            {
                if(entry==null)throw new InvalidOperationException("情报加成存在空条目");
                Add(result,entry,new Rule{Kind=RuleKind.Intelligence,Target=references.Resolve(entry.Technology,true,"情报加成 / 所需科技（可选）",ContentKind.Technology),Secondary=-1,Level=entry.Level,Amount=entry.Points});
            }

            for (int i = firstRule; i < result.Count; i++)
            {
                var rule = result[i].rule;
                if (owner.HasValue && !EffectRules.SupportsOwner(owner.Value, rule.Kind))
                    throw new InvalidOperationException(owner + "：不支持效果 " + rule.Kind);
                ContentKind? target = rule.Target < 0 ? null : references.Definitions[rule.Target].Kind;
                if (!EffectRules.SupportsTarget(rule.Kind, target))
                    throw new InvalidOperationException(rule.Kind + "：作用对象类型没有对应结算；全局属性须留空");
                if (rule.Kind == RuleKind.Intelligence && (rule.Level < 0 || owner == ContentKind.Policy && rule.Level > 1))
                    throw new InvalidOperationException("情报等级须为非负值；政策只有通用或一级效果，政策层级不是效果等级");
            }

        }
    }
}
