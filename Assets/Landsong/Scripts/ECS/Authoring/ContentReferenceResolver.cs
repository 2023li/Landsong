using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
namespace Landsong.ECS.Authoring
{
    // Resolves authoring references once. No string-based intermediate rule objects.
    public sealed class ContentReferenceResolver
    {
        readonly Dictionary<GameDefinitionAsset,int> assets = new Dictionary<GameDefinitionAsset,int>();
        public readonly ContentSource[] Definitions;
        public ContentReferenceResolver(GameCatalogAsset catalog)
        {
            if(catalog==null||catalog.Definitions==null) throw new InvalidOperationException("缺少内容目录");
            Definitions=catalog.Content;var ids=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<Definitions.Length;i++)
            {
                if(catalog.Definitions[i]==null||Definitions[i]==null||string.IsNullOrWhiteSpace(Definitions[i].Id)||!ids.Add(Definitions[i].Id))throw new InvalidOperationException("内容目录存在空引用或重复 ID");
                assets.Add(catalog.Definitions[i],i);
            }
        }
        public int Resolve(GameDefinitionAsset asset,bool optional,string field,params ContentKind[] kinds)
        {
            if(asset==null){if(optional)return -1;throw new InvalidOperationException(field+"：请选择内容资产");}
            if(!assets.TryGetValue(asset,out var at))throw new InvalidOperationException(field+"：引用资产未注册（相同 ID 的其它资产不能替代）");
            if(kinds.Length>0&&!kinds.Contains(Definitions[at].Kind))throw new InvalidOperationException(field+"：内容类型不匹配");
            return at;
        }
    }
}
