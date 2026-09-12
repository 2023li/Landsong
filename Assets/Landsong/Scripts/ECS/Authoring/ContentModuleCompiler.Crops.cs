using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteCrops(CropsContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result)
        {
            if(module==null)throw new InvalidOperationException("缺少作物种植与收获模块");
            if(!module.Enabled)return;
            if(module.Seeds==null)throw new InvalidOperationException("种植费用列表为空引用");
            foreach(var entry in module.Seeds)
            {
                if(entry==null)throw new InvalidOperationException("种植费用存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Crop))throw new InvalidOperationException(owner+"：不支持种植费用模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.PlacementCost,Target=references.Resolve(entry.Item,false,"种植费用 / 物品",ContentKind.Item),Secondary=-1,Level=1,Amount=entry.Quantity});
            }
            if(module.AutomaticHarvest==null)throw new InvalidOperationException("自动收获费用列表为空引用");
            foreach(var entry in module.AutomaticHarvest)
            {
                if(entry==null)throw new InvalidOperationException("自动收获费用存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Crop))throw new InvalidOperationException(owner+"：不支持自动收获费用模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.Maintenance,Target=references.Resolve(entry.Item,false,"自动收获费用 / 物品",ContentKind.Item),Secondary=-1,Level=1,Amount=entry.Quantity});
            }
            if(module.Yields==null)throw new InvalidOperationException("收获产物列表为空引用");
            foreach(var entry in module.Yields)
            {
                if(entry==null)throw new InvalidOperationException("收获产物存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Crop))throw new InvalidOperationException(owner+"：不支持收获产物模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RewardItem,Target=references.Resolve(entry.Item,false,"收获产物 / 物品",ContentKind.Item),Secondary=-1,Amount=entry.MinimumQuantity,B=entry.MaximumQuantity});
            }

        }
    }
}
