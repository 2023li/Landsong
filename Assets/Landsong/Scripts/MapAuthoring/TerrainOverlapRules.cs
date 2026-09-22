using System;
using System.Collections.Generic;
using Landsong.ECS;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    [CreateAssetMenu(menuName="Landsong/地图/地形覆盖排序",fileName="地形覆盖排序")]
    public sealed class TerrainOverlapRules:ScriptableObject
    {
        [LabelText("覆盖顺序（上方优先）"),ListDrawerSettings(DraggableItems=true),ValueDropdown("@BlueprintTerrainRule.TerrainChoices"),Tooltip("仅比较同一个 Layer 的同一逻辑格。获胜地形完整替换被覆盖地形及其权限。")]
        public List<TerrainType> HighToLow=new List<TerrainType>{TerrainType.障碍,TerrainType.石地,TerrainType.泥地,TerrainType.沼泽,TerrainType.陆地,TerrainType.水域};
        public Dictionary<TerrainType,int> Compile()
        {
            var result=new Dictionary<TerrainType,int>();
            foreach(var terrain in HighToLow??new List<TerrainType>())
                if(!TerrainTypes.Single(terrain) || !result.TryAdd(terrain,result.Count))throw new InvalidOperationException("覆盖排序包含重复、空或组合地形。");
            if(result.Count==0)throw new InvalidOperationException("覆盖排序不能为空。");return result;
        }
    }
}
