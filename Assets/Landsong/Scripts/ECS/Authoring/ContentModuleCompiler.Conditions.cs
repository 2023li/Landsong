using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteConditions(ConditionsContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result)
        {
            if(module==null)throw new InvalidOperationException("缺少解锁与显示条件模块");
            if(!module.Enabled)return;
            if(module.Completions==null)throw new InvalidOperationException("完成前置列表为空引用");
            foreach(var entry in module.Completions)
            {
                if(entry==null)throw new InvalidOperationException("完成前置存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Technology&&owner.Value!=ContentKind.Quest&&owner.Value!=ContentKind.Expedition&&owner.Value!=ContentKind.RoyalTrait&&owner.Value!=ContentKind.Talent))throw new InvalidOperationException(owner+"：不支持完成前置模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.Prerequisite,Target=references.Resolve(entry.Content,false,"完成前置 / 已获得或已完成内容"),Secondary=-1,Amount=entry.Count});
            }
            if(module.Visibility==null)throw new InvalidOperationException("显示前置列表为空引用");
            foreach(var entry in module.Visibility)
            {
                if(entry==null)throw new InvalidOperationException("显示前置存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Expedition))throw new InvalidOperationException(owner+"：不支持显示前置模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.VisiblePrerequisite,Target=references.Resolve(entry.Content,false,"显示前置 / 已拥有或已完成内容"),Secondary=-1,Amount=entry.Count});
            }

        }
    }
}
