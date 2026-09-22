using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class BlueprintRuleNormalization
    {
        public static List<BlueprintTerrainRule> Collapse(IEnumerable<BlueprintTerrainRule> source)
        {
            var result=new List<BlueprintTerrainRule>();
            foreach(var rule in source)
            {
                if(rule==null)throw new InvalidOperationException("地形规则存在空条目。");
                string key=BlueprintRuleNames.Key(rule.BlueprintLayerName);
                var existing=result.SingleOrDefault(r=>r.BlueprintLayerName==key);
                if(existing!=null)
                {
                    if(existing.Kind!=rule.Kind || existing.Terrain!=rule.Terrain || existing.Buildable!=rule.Buildable || existing.Traversable!=rule.Traversable)
                        throw new InvalidOperationException("同名地形在不同 Layer 有冲突配置，无法合并："+key);
                    continue;
                }
                result.Add(new BlueprintTerrainRule{BlueprintLayerName=key,Kind=rule.Kind,Terrain=rule.Terrain,Buildable=rule.Buildable,Traversable=rule.Traversable});
            }
            return result;
        }
    }
}
