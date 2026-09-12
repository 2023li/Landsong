using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WritePeople(PeopleContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result,ContentSource source)
        {
            if(module==null)throw new InvalidOperationException("缺少人物与任职模块");
            if(!module.Enabled)return;
            if(module.Wages==null)throw new InvalidOperationException("人才工资列表为空引用");
            foreach(var entry in module.Wages)
            {
                if(entry==null)throw new InvalidOperationException("人才工资存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Talent))throw new InvalidOperationException(owner+"：不支持人才工资模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.Wage,Target=-1,Secondary=-1,Amount=entry.BaseAmount,B=entry.PerLevel});
            }
            if(module.Traits==null)throw new InvalidOperationException("初始特性列表为空引用");
            foreach(var entry in module.Traits)
            {
                if(entry==null)throw new InvalidOperationException("初始特性存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Talent&&owner.Value!=ContentKind.RoyalTrait))throw new InvalidOperationException(owner+"：不支持初始特性模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.Trait,Target=references.Resolve(entry.Trait,false,"初始特性 / 王室特性",ContentKind.RoyalTrait),Secondary=-1});
            }
            if(module.Conflicts==null)throw new InvalidOperationException("冲突特性列表为空引用");
            foreach(var entry in module.Conflicts)
            {
                if(entry==null)throw new InvalidOperationException("冲突特性存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Talent&&owner.Value!=ContentKind.RoyalTrait))throw new InvalidOperationException(owner+"：不支持冲突特性模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.GeneConflict,Target=references.Resolve(entry.Trait,false,"冲突特性 / 王室特性",ContentKind.RoyalTrait),Secondary=-1});
            }
            if(module.Dependencies==null)throw new InvalidOperationException("依赖特性列表为空引用");
            foreach(var entry in module.Dependencies)
            {
                if(entry==null)throw new InvalidOperationException("依赖特性存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Talent&&owner.Value!=ContentKind.RoyalTrait))throw new InvalidOperationException(owner+"：不支持依赖特性模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.GeneRequired,Target=references.Resolve(entry.Trait,false,"依赖特性 / 王室特性",ContentKind.RoyalTrait),Secondary=-1});
            }
            if(module.SocialTasks==null)throw new InvalidOperationException("人物委托列表为空引用");
            foreach(var entry in module.SocialTasks)
            {
                if(entry==null)throw new InvalidOperationException("人物委托存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Talent))throw new InvalidOperationException(owner+"：不支持人物委托模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.SocialTask,Target=references.Resolve(entry.Item,false,"人物委托 / 提交物品",ContentKind.Item),Secondary=-1,Amount=entry.Quantity,B=entry.AffectionReward});
            }
            if(module.PeriodicItems==null)throw new InvalidOperationException("人才每回合产物列表为空引用");
            foreach(var entry in module.PeriodicItems)
            {
                if(entry==null)throw new InvalidOperationException("人才每回合产物存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Talent))throw new InvalidOperationException(owner+"：不支持人才每回合产物模块条目");
                Finite(entry.PerLevel,source.Id+" / 人才每回合产物 / 执行顺序 "+entry.Order+" / 每级增加数量");
                TalentGrantAuthoringValidation.ValidateIncome(source,entry);
                Add(result,entry,new Rule{Kind=RuleKind.RewardItem,Target=references.Resolve(entry.Item,false,"人才每回合产物 / 物品",ContentKind.Item),Secondary=-1,Amount=entry.BaseQuantity,Extra=entry.PerLevel});
            }
            if(module.Effects==null)throw new InvalidOperationException("人才任职效果列表为空引用");
            for(int index=0;index<module.Effects.Length;index++)
            {
                var entry=module.Effects[index];
                var context=source.Id+" / 人才任职效果["+index+"]";
                if(entry==null)throw new InvalidOperationException(context+"：条目为空引用");
                if(owner.HasValue&&(owner.Value!=ContentKind.Talent&&owner.Value!=ContentKind.TalentSlot))throw new InvalidOperationException(context+"："+owner+"不支持人才任职效果模块条目");
                if(!Enum.IsDefined(typeof(TalentEffectType),entry.Effect))throw new InvalidOperationException(context+"：效果枚举值无效");
                if(!Enum.IsDefined(typeof(TalentEffectTiming),entry.Timing))throw new InvalidOperationException(context+"：生效时机枚举值无效");
                if(!Enum.IsDefined(typeof(TalentEffectScaling),entry.Scaling))throw new InvalidOperationException(context+"：数值缩放依据枚举值无效");
                Finite(entry.BaseValue,context+" / 基础效果");
                Finite(entry.PerLevel,context+" / 每级增长");
                var target=references.Resolve(entry.Subject,entry.Effect!=TalentEffectType.内容许可,context+" / 指定作用对象");
                ValidateTalentEffectScope(source,entry,target<0?null:references.Definitions[target],context);
                TalentGrantAuthoringValidation.ValidateIncome(source,entry);
                if(entry.Effect==TalentEffectType.内容许可)
                    TalentGrantAuthoringValidation.Validate(source,entry,references.Definitions[target]);
                Add(result,entry,new Rule{Kind=RuleKind.TalentEffect,Target=target,Secondary=-1,Amount=(int)entry.Effect,B=(int)entry.Timing,C=(int)entry.Scaling,Value=entry.BaseValue,Extra=entry.PerLevel});
            }

        }

        static void ValidateTalentEffectScope(ContentSource source, TalentJobEffect entry, ContentSource target, string context)
        {
            if (!EffectRules.SupportsTalentEffect(source.Kind, (int)entry.Timing, (int)entry.Effect))
                throw new InvalidOperationException(context + "：该来源、生效时机与效果组合没有结算入口；岗位仅支持被动生产、攻击或民意，人才每回合仅支持物品、科研点或内容许可");
            // License targets and numeric scaling have the stricter shared reward validation below.
            if (entry.Effect == TalentEffectType.内容许可) return;

            ContentKind? kind = target == null ? null : target.Kind;
            if (entry.Scaling == TalentEffectScaling.每百份物品 && kind != ContentKind.Item)
                throw new InvalidOperationException(context + "：每百份物品缩放必须指定物品");
            if (entry.Scaling == TalentEffectScaling.运营建筑数 && target != null && kind != ContentKind.Building)
                throw new InvalidOperationException(context + "：运营建筑数缩放仅可指定建筑或留空统计全部建筑");

            if (entry.Timing == TalentEffectTiming.被动)
            {
                RuleKind effect = entry.Effect == TalentEffectType.生产百分比 ? RuleKind.ProductionBonus
                    : entry.Effect == TalentEffectType.攻击百分比 ? RuleKind.AttackBonus : RuleKind.PublicOpinion;
                // The current serialized Subject is both the recipient filter and the scaling source.
                // Reject conflicting uses instead of silently reinterpreting an item's ID as a unit ID.
                if (!EffectRules.SupportsTarget(effect, kind))
                    throw new InvalidOperationException(context + "：作用对象与被动效果或缩放来源不兼容");
            }
            else if (entry.Effect == TalentEffectType.物品)
            {
                if (kind != ContentKind.Item) throw new InvalidOperationException(context + "：每回合物品效果必须指定物品");
            }
            else if (target != null && entry.Scaling != TalentEffectScaling.每百份物品
                && entry.Scaling != TalentEffectScaling.运营建筑数)
                throw new InvalidOperationException(context + "：科研点是全局值，只有物品或建筑数量缩放可以指定来源对象");
        }
    }
}
