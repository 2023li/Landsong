using System;
using System.Collections.Generic;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        public static Rule[] CompileNight(NightConditionsContentModule module,ContentReferenceResolver references)
        {
            if(module==null)throw new InvalidOperationException("缺少夜间条件配置");
            var result=new List<(int order,Rule rule)>();
            if(module.Enabled)
            {
                if(module.Turns==null)throw new InvalidOperationException("回合条件列表为空引用");
                foreach(var entry in module.Turns)
                {
                    if(entry==null)throw new InvalidOperationException("回合条件存在空条目");
                    Add(result,entry,new Rule{Kind=RuleKind.RequireTurn,Target=-1,Secondary=-1,Amount=entry.MinimumTurn});
                }
                if(module.Buildings==null)throw new InvalidOperationException("建筑条件列表为空引用");
                foreach(var entry in module.Buildings)
                {
                    if(entry==null)throw new InvalidOperationException("建筑条件存在空条目");
                    Add(result,entry,new Rule{Kind=RuleKind.RequireBuilding,Target=references.Resolve(entry.Building,false,"建筑条件 / 建筑",ContentKind.Building),Secondary=-1,Amount=entry.Count,Level=entry.MinimumLevel});
                }
                if(module.Items==null)throw new InvalidOperationException("物品条件列表为空引用");
                foreach(var entry in module.Items)
                {
                    if(entry==null)throw new InvalidOperationException("物品条件存在空条目");
                    Add(result,entry,new Rule{Kind=RuleKind.RequireItem,Target=references.Resolve(entry.Item,false,"物品条件 / 物品",ContentKind.Item),Secondary=-1,Amount=entry.Quantity});
                }
                if(module.Technologies==null)throw new InvalidOperationException("科技条件列表为空引用");
                foreach(var entry in module.Technologies)
                {
                    if(entry==null)throw new InvalidOperationException("科技条件存在空条目");
                    Add(result,entry,new Rule{Kind=RuleKind.RequireTechnology,Target=references.Resolve(entry.Technology,false,"科技条件 / 科技",ContentKind.Technology),Secondary=-1,Amount=entry.Count});
                }
                if(module.Completions==null)throw new InvalidOperationException("完成前置列表为空引用");
                foreach(var entry in module.Completions)
                {
                    if(entry==null)throw new InvalidOperationException("完成前置存在空条目");
                    Add(result,entry,new Rule{Kind=RuleKind.Prerequisite,Target=references.Resolve(entry.Content,false,"完成前置 / 已获许可内容"),Secondary=-1,Amount=entry.Count});
                }
            
            }
            return Finish(result);
        }
    }
}
