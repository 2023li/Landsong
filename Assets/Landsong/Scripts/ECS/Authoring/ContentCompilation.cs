using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    public sealed class ContentCompilation
    {
        public readonly ContentReferenceResolver References;
        public readonly Rule[] StartingRewards;
        public readonly Rule[][] NightConditions;
        readonly Dictionary<ContentSource,Rule[]> rules=new Dictionary<ContentSource,Rule[]>();
        public ContentCompilation(GameCatalogAsset catalog)
        {
            References=new ContentReferenceResolver(catalog);
            foreach(var source in References.Definitions)
            {
                if(source.Kind==ContentKind.Building&&ContentModuleCompiler.AnyEnabled(source.Configuration)||source.Kind!=ContentKind.Building&&ContentModuleCompiler.AnyEnabled(source.Modules))throw new InvalidOperationException(source.Id+"：建筑与非建筑功能模块不能混用");
                if(!Enum.IsDefined(typeof(ContentKind),source.Kind))throw new InvalidOperationException(source.Id+"：内容类型无效");
                rules.Add(source,source.Kind==ContentKind.Building?BuildingModuleCompiler.Compile(source.Modules,References):ContentModuleCompiler.Compile(source,References));
            }
            StartingRewards=ContentModuleCompiler.CompileRewards(catalog.StartingRewards,References);
            if(catalog.NightEvents==null)throw new InvalidOperationException("缺少夜间事件目录");
            NightConditions=catalog.NightEvents.Select(e=>e==null?throw new InvalidOperationException("夜间事件为空"):ContentModuleCompiler.CompileNight(e.Conditions,References)).ToArray();
        }
        public Rule[] For(ContentSource source)=>rules[source];
        public string Id(int index)=>index<0?"":References.Definitions[index].Id;
        public ContentSource Definition(int index)=>index<0?null:References.Definitions[index];
    }
}
