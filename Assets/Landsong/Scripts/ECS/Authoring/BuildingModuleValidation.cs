using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Authoring
{
    public static class BuildingModuleValidation
    {
        public static void Validate(GameCatalogAsset catalog,ContentCompilation compiled=null)
        {
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var asset in catalog.Definitions)
            {
                if(asset==null)throw new InvalidOperationException("目录中存在未指定的内容资产");
                if(asset.Data==null||string.IsNullOrWhiteSpace(asset.Data.Id)||!ids.Add(asset.Data.Id))throw new InvalidOperationException("目录中存在空或重复的内容 ID");
                if(asset.Data.Kind==ContentKind.Building)Validate(asset.Data,catalog,compiled?.For(asset.Data));
            }
        }
        public static void Validate(ContentSource source,GameCatalogAsset catalog,Rule[] compiledRules=null)
        {
            void Fail(string message)=>throw new InvalidOperationException(source.Id+"："+message);
            if(source.Level<1||source.Duration<1)Fail("建筑最高等级和施工回合数必须大于零");
            if(source.Modules==null)Fail("缺少建筑模块配置");
            void Visit(object value,string path)
            {
                if(value==null)Fail(path+"存在空配置");
                var type=value.GetType();var enabled=type.GetField("Enabled");if(enabled!=null&&!(bool)enabled.GetValue(value))return;
                foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    var item=field.GetValue(value);var label=field.GetCustomAttribute<LabelTextAttribute>()?.Text??field.Name;string at=path+" / "+label;
                    if(field.GetCustomAttribute<ContentReferenceAttribute>() is ContentReferenceAttribute reference)
                    {
                        var asset=item as GameDefinitionAsset;if(asset==null){if(!reference.Optional)Fail(at+"未选择内容");continue;}
                        if(asset.Data.Kind!=reference.Kind)Fail(at+"必须选择 "+reference.Kind+" 类型");
                        if(catalog==null||catalog.Find(asset.Data.Id)<0||catalog.Content[catalog.Find(asset.Data.Id)].Kind!=reference.Kind)Fail(at+"引用的内容未注册到当前目录或类型不匹配");
                        if(string.IsNullOrWhiteSpace(asset.Data.Id)||catalog.Definitions.Count(a=>a!=null&&a.Data.Id==asset.Data.Id)!=1)Fail(at+"引用内容的 ID 为空或重复");
                    }
                    else if(field.FieldType.IsEnum){if(!Enum.IsDefined(field.FieldType,item))Fail(at+"枚举值无效");}
                    else if(item is int integer)
                    {
                        if(integer<0||integer>1000000)Fail(at+"必须在 0～1000000 之间");
                        var min=field.GetCustomAttribute<MinValueAttribute>();if(min!=null&&integer<min.MinValue)Fail(at+"低于允许下限");
                        if((field.Name=="Level"||field.Name=="TargetLevel")&&integer>source.Level)Fail(at+"超过建筑最高等级");
                        if(field.Name=="Stage"&&integer>source.Duration)Fail(at+"超过建筑施工回合数");
                    }
                    else if(item is float number)
                    {
                        if(float.IsNaN(number)||float.IsInfinity(number)||Math.Abs(number)>1000000)Fail(at+"必须是有限数值");
                        var min=field.GetCustomAttribute<MinValueAttribute>();if(min!=null&&number<min.MinValue)Fail(at+"低于允许下限");
                        var range=field.GetCustomAttribute<RangeAttribute>();if(range!=null&&(number<range.min||number>range.max))Fail(at+"超出允许范围");
                    }
                    else if(field.FieldType==typeof(string))
                    {
                        if(System.Text.Encoding.UTF8.GetByteCount((string)item??"")>60)Fail(at+"过长");
                        if(field.Name=="Terrain"&&string.IsNullOrWhiteSpace((string)item))Fail(at+"不能为空");
                    }
                    else if(item is Array array){for(int i=0;i<array.Length;i++)Visit(array.GetValue(i),at+"["+(i+1)+"]");}
                    else if(field.FieldType.IsClass)Visit(item,at);
                }
            }
            Visit(source.Modules,"模块");
            var rules=compiledRules??BuildingModuleCompiler.Compile(source.Modules,new ContentReferenceResolver(catalog));
            for(int level=1;level<=source.Level;level++)
            {
                var active=rules.Where(r=>r.Level==0||r.Level==level).ToArray();
                foreach(var kind in new[]{RuleKind.Workforce,RuleKind.Residence,RuleKind.Garrison,RuleKind.Experience,RuleKind.Production,RuleKind.Market,RuleKind.Sanctum,RuleKind.ExpeditionSite,RuleKind.StorageCondition})
                    if(active.Count(r=>r.Kind==kind)>1)Fail("等级 "+level+" 的 "+kind+"配置重复；通用项和指定等级项不能重叠");
                var workforce=active.FirstOrDefault(r=>r.Kind==RuleKind.Workforce);int workers=workforce.Amount;
                if(active.Any(r=>r.Kind==RuleKind.Workforce)&&workforce.B>workers)Fail("初始工人数超过岗位容量");
                if(active.Any(r=>r.Kind==RuleKind.Workforce))
                {
                    var tiers=source.Modules.Workforce.EfficiencyTiers.Where(t=>t.Level==0||t.Level==level).OrderBy(t=>t.MinimumWorkers).ToArray();
                    int nextWorker=0;
                    foreach(var tier in tiers)
                    {
                        if(tier.MinimumWorkers!=nextWorker||tier.MaximumWorkers<tier.MinimumWorkers||tier.MaximumWorkers>workers)
                            Fail("等级 "+level+" 工作效率档位必须连续、不重叠且不超过岗位容量");
                        nextWorker=tier.MaximumWorkers+1;
                    }
                    if(tiers.Length==0||nextWorker!=workers+1)Fail("等级 "+level+" 工作效率档位必须显式覆盖 0～"+workers+" 人，不会自动生成");
                    // A configured range must have one meaning. Editing effect thresholds also requires editing the tier boundaries.
                    void Boundary(int threshold)
                    {
                        if(threshold>0&&threshold<=workers&&tiers.Any(t=>t.MinimumWorkers<threshold&&t.MaximumWorkers>=threshold))
                            Fail("等级 "+level+" 在 "+threshold+" 人时效果发生变化，工作效率档位必须在此处分档");
                    }
                    foreach(var row in active)
                    {
                        if(row.Kind==RuleKind.Production||row.Kind==RuleKind.ProcessingTier||row.Kind==RuleKind.ProductionTier||row.Kind==RuleKind.Warehouse||row.Kind==RuleKind.RareOutput)Boundary(row.B);
                        if(row.Kind==RuleKind.ProductionTier&&row.C>0)Boundary(row.C+1);
                        if(row.Kind==RuleKind.SpatialEffect||row.Kind==RuleKind.Experience)Boundary(row.C);
                        if(row.Kind==RuleKind.Sanctum||row.Kind==RuleKind.StorageCondition)Boundary(row.Amount);
                        if(row.Kind==RuleKind.Provider)Boundary(1);
                        if(row.Kind==RuleKind.QuestCapacity||row.Kind==RuleKind.Market)Boundary(workers);
                        if(row.Kind==RuleKind.ExpeditionSite)for(int count=1;count<=Math.Min(workers,row.B);count++)Boundary(count);
                        if(row.Kind==RuleKind.Crop&&catalog!=null)
                        {
                            int cropIndex=row.Target;
                            if(cropIndex>=0){var crop=catalog.Content[cropIndex];Boundary(crop.Population);Boundary(crop.Capacity);}
                        }
                    }
                }
                var residence=active.FirstOrDefault(r=>r.Kind==RuleKind.Residence);if(active.Any(r=>r.Kind==RuleKind.Residence)&&residence.B>residence.Amount)Fail("完工居民数超过住宅容量");
                var garrison=active.FirstOrDefault(r=>r.Kind==RuleKind.Garrison);long initial=active.Where(r=>r.Kind==RuleKind.InitialGarrison).Sum(r=>(long)r.Amount);
                if(initial>garrison.Amount)Fail("等级 "+level+" 的初始驻军总人数超过驻军容量");
                if(active.Any(r=>r.Kind==RuleKind.Garrison)&&(garrison.B<1||garrison.Amount>1000))Fail("驻军每批出勤至少 1 人，容量不得超过 1000");
                foreach(var row in active.Where(r=>r.Kind==RuleKind.ProductionTier))if(row.C>0&&row.C<row.B)Fail("生产档位最多工人不能小于最少工人");
                foreach(var row in active.Where(r=>r.Kind==RuleKind.Production||r.Kind==RuleKind.ProcessingTier))if(row.Amount<1)Fail("生产间隔必须至少为 1 回合");
                foreach(var row in active.Where(r=>r.Kind==RuleKind.QuestSource))if(row.C<1||row.Value<row.C)Fail("邀约刷新回合必须满足 1 ≤ 最短 ≤ 最长");
                foreach(var row in active.Where(r=>r.Kind==RuleKind.ExpeditionSite))if(row.Amount<1||row.B<row.Amount)Fail("远征人数范围无效");
            }
        }
    }
}
