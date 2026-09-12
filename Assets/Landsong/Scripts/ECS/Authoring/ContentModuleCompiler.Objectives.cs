using System;
using System.Collections.Generic;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void WriteObjectives(ObjectivesContentModule module,ContentReferenceResolver references,ContentKind? owner,List<(int order,Rule rule)> result)
        {
            if(module==null)throw new InvalidOperationException("缺少任务目标模块");
            if(!module.Enabled)return;
            if(module.Buildings==null)throw new InvalidOperationException("建筑目标列表为空引用");
            foreach(var entry in module.Buildings)
            {
                if(entry==null)throw new InvalidOperationException("建筑目标存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持建筑目标模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RequireBuilding,Target=references.Resolve(entry.Building,false,"建筑目标 / 建筑",ContentKind.Building),Secondary=-1,Amount=entry.Count,B=entry.MinimumLevel,C=entry.CompletedOnly?1:0,Key=new FixedString64Bytes(entry.Key??"")});
            }
            if(module.PlantedBuildings==null)throw new InvalidOperationException("种植目标列表为空引用");
            foreach(var entry in module.PlantedBuildings)
            {
                if(entry==null)throw new InvalidOperationException("种植目标存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持种植目标模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RequireCrop,Target=references.Resolve(entry.Building,false,"种植目标 / 种植建筑",ContentKind.Building),Secondary=-1,Amount=entry.Count,Key=new FixedString64Bytes(entry.Key??"")});
            }
            if(module.OwnedItems==null)throw new InvalidOperationException("持有物品列表为空引用");
            foreach(var entry in module.OwnedItems)
            {
                if(entry==null)throw new InvalidOperationException("持有物品存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持持有物品模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RequireItem,Target=references.Resolve(entry.Item,false,"持有物品 / 物品",ContentKind.Item),Secondary=-1,Amount=entry.Quantity,Key=new FixedString64Bytes(entry.Key??"")});
            }
            if(module.SubmittedItems==null)throw new InvalidOperationException("提交物品列表为空引用");
            foreach(var entry in module.SubmittedItems)
            {
                if(entry==null)throw new InvalidOperationException("提交物品存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持提交物品模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.SubmitItem,Target=references.Resolve(entry.Item,false,"提交物品 / 物品",ContentKind.Item),Secondary=-1,Amount=entry.Quantity,Key=new FixedString64Bytes(entry.Key??"")});
            }
            if(module.CameraMoves==null)throw new InvalidOperationException("移动镜头列表为空引用");
            foreach(var entry in module.CameraMoves)
            {
                if(entry==null)throw new InvalidOperationException("移动镜头存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持移动镜头模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RequireCameraMove,Target=-1,Secondary=-1,Amount=entry.Count,Key=new FixedString64Bytes(entry.Key??"")});
            }
            if(module.CameraZooms==null)throw new InvalidOperationException("缩放镜头列表为空引用");
            foreach(var entry in module.CameraZooms)
            {
                if(entry==null)throw new InvalidOperationException("缩放镜头存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持缩放镜头模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RequireCameraZoom,Target=-1,Secondary=-1,Amount=entry.Count,Key=new FixedString64Bytes(entry.Key??"")});
            }
            if(module.Technologies==null)throw new InvalidOperationException("研究科技列表为空引用");
            foreach(var entry in module.Technologies)
            {
                if(entry==null)throw new InvalidOperationException("研究科技存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持研究科技模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RequireTechnology,Target=references.Resolve(entry.Technology,true,"研究科技 / 科技（空 = 任意）",ContentKind.Technology),Secondary=-1,Amount=entry.Count,Key=new FixedString64Bytes(entry.Key??"")});
            }
            if(module.Turns==null)throw new InvalidOperationException("回合目标列表为空引用");
            foreach(var entry in module.Turns)
            {
                if(entry==null)throw new InvalidOperationException("回合目标存在空条目");
                if(owner.HasValue&&(owner.Value!=ContentKind.Quest))throw new InvalidOperationException(owner+"：不支持回合目标模块条目");
                Add(result,entry,new Rule{Kind=RuleKind.RequireTurn,Target=-1,Secondary=-1,Amount=entry.Turns,B=entry.SinceAccepted?1:0,Key=new FixedString64Bytes(entry.Key??"")});
            }

        }
    }
}
