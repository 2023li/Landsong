using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public static partial class ContentModuleCompiler
    {
        static void Finite(float value,string field){if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException(field+"：必须是有限数值");}
        static void Add(List<(int order,Rule rule)> result,OrderedContentEntry entry,Rule rule)
        {if(entry.Order<0)throw new InvalidOperationException("执行顺序不能小于 0");result.Add((entry.Order,rule));}
        static Rule[] Finish(List<(int order,Rule rule)> result)=>result.OrderBy(x=>x.order).Select(x=>x.rule).ToArray();
        public static Rule[] Compile(ContentSource source,ContentReferenceResolver references)
        {
            if(source.Configuration==null)throw new InvalidOperationException(source.Id+"：缺少功能配置");
            var result=new List<(int order,Rule rule)>();
            WriteConditions(source.Configuration.Conditions,references,source.Kind,result);
            WriteObjectives(source.Configuration.Objectives,references,source.Kind,result);
            WriteRewards(source.Configuration.Rewards,references,source.Kind,result,source.Id);
            WriteUnitCosts(source.Configuration.UnitCosts,references,source.Kind,result);
            WriteCrops(source.Configuration.Crops,references,source.Kind,result);
            WriteInventory(source.Configuration.Inventory,references,source.Kind,result);
            WriteExpeditions(source.Configuration.Expeditions,references,source.Kind,result);
            WriteModifiers(source.Configuration.Modifiers,references,source.Kind,result);
            WritePeople(source.Configuration.People,references,source.Kind,result,source);
            return Finish(result);
        }
        public static Rule[] CompileRewards(RewardsContentModule module,ContentReferenceResolver references)
        {
            var result=new List<(int order,Rule rule)>();WriteRewards(module,references,null,result,"开局发放");
            var rules=Finish(result);
            foreach(var rule in rules)
            {
                if(rule.Kind<RuleKind.RewardItem||rule.Kind>RuleKind.RewardFeature)throw new InvalidOperationException("开局发放只接受物品、蓝图、增益或功能许可");
            }
            return rules;
        }
        internal static bool AnyEnabled(ContentModules modules)
        {
            if(modules==null)throw new InvalidOperationException("缺少内容功能配置");
            return modules.Conditions?.Enabled==true||modules.Objectives?.Enabled==true||modules.Rewards?.Enabled==true||modules.UnitCosts?.Enabled==true||modules.Crops?.Enabled==true||modules.Inventory?.Enabled==true||modules.Expeditions?.Enabled==true||modules.Modifiers?.Enabled==true||modules.People?.Enabled==true;
        }
        internal static bool AnyEnabled(BuildingModules modules)
        {
            if(modules==null)throw new InvalidOperationException("缺少建筑功能配置");
            return modules.Construction?.Enabled==true||modules.Upgrade?.Enabled==true||modules.Maintenance?.Enabled==true||modules.Production?.Enabled==true||modules.Quests?.Enabled==true||modules.Placement?.Enabled==true||modules.Storage?.Enabled==true||modules.Workforce?.Enabled==true||modules.Housing?.Enabled==true||modules.Research?.Enabled==true||modules.Farming?.Enabled==true||modules.Gathering?.Enabled==true||modules.Garrison?.Enabled==true||modules.Sanctum?.Enabled==true||modules.Market?.Enabled==true||modules.Effects?.Enabled==true||modules.Defence?.Enabled==true||modules.Expeditions?.Enabled==true;
        }
    }
}
