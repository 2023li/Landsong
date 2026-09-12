using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteUnitCosts(UnitCostsContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result)
        {
            if(module==null)throw new InvalidOperationException("缺少招募、唤醒与供奉模块");
            if(!module.Enabled)return;
            if(module.Recruitment==null)throw new InvalidOperationException("募兵费用列表为空引用");
            foreach(var entry in module.Recruitment)
            {
                if(entry==null)throw new InvalidOperationException("募兵费用存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Soldier))throw new InvalidOperationException(owner+"：不支持募兵费用模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RecruitCost,Target=references.Resolve(entry.Item,false,"募兵费用 / 物品",ContentKind.Item),Secondary=-1,Level=entry.Level,Amount=entry.Quantity});
            }
            if(module.Awakening==null)throw new InvalidOperationException("唤醒费用列表为空引用");
            foreach(var entry in module.Awakening)
            {
                if(entry==null)throw new InvalidOperationException("唤醒费用存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Hero))throw new InvalidOperationException(owner+"：不支持唤醒费用模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.WakeCost,Target=references.Resolve(entry.Item,false,"唤醒费用 / 物品",ContentKind.Item),Secondary=-1,Level=entry.Level,Amount=entry.Quantity});
            }
            if(module.Offerings==null)throw new InvalidOperationException("供奉费用列表为空引用");
            foreach(var entry in module.Offerings)
            {
                if(entry==null)throw new InvalidOperationException("供奉费用存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Hero))throw new InvalidOperationException(owner+"：不支持供奉费用模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.Supply,Target=references.Resolve(entry.Item,false,"供奉费用 / 物品",ContentKind.Item),Secondary=-1,Level=entry.Level,Amount=entry.Quantity});
            }

        }
    }
}
