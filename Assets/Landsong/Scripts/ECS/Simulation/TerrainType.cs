using System;
using Sirenix.OdinInspector;

namespace Landsong.ECS
{
    // Stable explicit values: never renumber. Maps select one value; building filters select a set.
    [Flags]
    public enum TerrainType
    {
        [LabelText("无")] None=0,
        [LabelText("陆地")] 陆地=1,
        [LabelText("水域")] 水域=2,
        [LabelText("石地")] 石地=4,
        [LabelText("泥地")] 泥地=8,
        [LabelText("沼泽")] 沼泽=16,
        [LabelText("障碍")] 障碍=32,
        [LabelText("全部地形")] All=陆地|水域|石地|泥地|沼泽|障碍
    }
    public static class TerrainTypes
    {
        public static bool Valid(TerrainType value)=>(value & ~TerrainType.All)==0;
        public static bool Single(TerrainType value)=>Valid(value) && value!=0 && (((int)value & ((int)value-1))==0);
        public static bool Allows(TerrainType ground,TerrainType allowed,TerrainType excluded)
            =>Single(ground) && Valid(allowed) && Valid(excluded) && (ground & excluded)==0 && (ground & allowed)!=0;
        // Only legacy authoring/import adapters use names; runtime placement uses the enum mask.
        public static TerrainType ParseLegacy(string name)
        {
            switch((name??"").Trim().ToLowerInvariant())
            {
                case "陆地":case "草地":case "land":case "road":case "道路":case "高级道路":return TerrainType.陆地;
                case "水域":case "水":case "water":return TerrainType.水域;
                case "石地":case "石头地":case "石矿":case "stone":case "stone_deposit":return TerrainType.石地;
                case "泥地":case "土地":case "mud":case "dirt":return TerrainType.泥地;
                case "沼泽":case "swamp":return TerrainType.沼泽;
                case "障碍":case "obstacle":return TerrainType.障碍;
                default:throw new InvalidOperationException("未知逻辑地形，请显式映射枚举："+name);
            }
        }
        public static TerrainType ResolveLegacy(string primary,System.Collections.Generic.IEnumerable<string> overlays)
        {
            var result=ParseLegacy(primary);
            // The old stone-deposit tag described a land+ore surface; it becomes a single stone terrain.
            foreach(var name in overlays??Array.Empty<string>())
            {var type=ParseLegacy(name);if(type==TerrainType.石地 || type==TerrainType.障碍)result=type;}
            return result;
        }
    }
}
