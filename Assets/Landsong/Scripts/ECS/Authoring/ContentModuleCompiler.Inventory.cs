using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteInventory(InventoryContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result)
        {
            if(module==null)throw new InvalidOperationException("缺少物品分组与库存规则模块");
            if(!module.Enabled)return;
            if(module.Groups==null)throw new InvalidOperationException("额外所属物品组列表为空引用");
            foreach(var entry in module.Groups)
            {
                if(entry==null)throw new InvalidOperationException("额外所属物品组存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Item))throw new InvalidOperationException(owner+"：不支持额外所属物品组模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.ItemGroup,Target=references.Resolve(entry.Group,false,"额外所属物品组 / 物品组",ContentKind.ItemGroup),Secondary=-1});
            }
            if(module.Accepted==null)throw new InvalidOperationException("允许收纳列表为空引用");
            foreach(var entry in module.Accepted)
            {
                if(entry==null)throw new InvalidOperationException("允许收纳存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.SlotType))throw new InvalidOperationException(owner+"：不支持允许收纳模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.SlotAccept,Target=references.Resolve(entry.Content,false,"允许收纳 / 物品或物品组",ContentKind.Item,ContentKind.ItemGroup),Secondary=-1});
            }
            if(module.Losses==null)throw new InvalidOperationException("指定物品损耗倍率列表为空引用");
            foreach(var entry in module.Losses)
            {
                if(entry==null)throw new InvalidOperationException("指定物品损耗倍率存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.SlotType))throw new InvalidOperationException(owner+"：不支持指定物品损耗倍率模块条目");
                Finite(entry.Multiplier,"指定物品损耗倍率 / 损耗倍率");
                Add(result,entry,new Rule{Kind=RuleKind.SlotLoss,Target=references.Resolve(entry.Content,false,"指定物品损耗倍率 / 物品或物品组",ContentKind.Item,ContentKind.ItemGroup),Secondary=-1,Value=entry.Multiplier});
            }

        }
    }
}
