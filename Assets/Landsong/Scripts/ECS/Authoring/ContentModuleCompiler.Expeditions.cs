using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteExpeditions(ExpeditionsContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result)
        {
            if(module==null)throw new InvalidOperationException("缺少远征补给模块");
            if(!module.Enabled)return;
            if(module.Supplies==null)throw new InvalidOperationException("远征补给列表为空引用");
            foreach(var entry in module.Supplies)
            {
                if(entry==null)throw new InvalidOperationException("远征补给存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Expedition))throw new InvalidOperationException(owner+"：不支持远征补给模块条目");
                Finite(entry.SuccessPerExtra,"远征补给 / 每份额外补给的成功率加成");
                Finite(entry.RewardPerExtra,"远征补给 / 每份额外补给的奖励加成");
                Add(result,entry,new Rule{Kind=RuleKind.Supply,Target=references.Resolve(entry.Item,false,"远征补给 / 物品",ContentKind.Item),Secondary=-1,Amount=entry.MinimumQuantity,B=entry.ExtraLimit,Value=entry.SuccessPerExtra,Extra=entry.RewardPerExtra});
            }

        }
    }
}
