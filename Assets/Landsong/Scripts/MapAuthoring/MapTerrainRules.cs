using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    // Shared rules contain no scene, configuration, output or resolved layer GUID references.
    [CreateAssetMenu(menuName = "Landsong/地图/公共地形规则", fileName = "公共地形规则")]
    public sealed class MapTerrainRules : ScriptableObject
    {
        [LabelText("基础层名称")] public string baseLayerName = "Base";
        [LabelText("默认地形标识")] public string baseTerrainKey = "水域";
        [LabelText("默认允许建造")] public bool baseBuildable;
        [LabelText("默认允许通行")] public bool baseTraversable = true;
        [LabelText("高度最小单位"), MinValue(.001f)] public float elevationWorldStep = .1f;
        [LabelText("图层映射（按顺序应用）")] public List<TileWorldCreatorLayerBinding> layerBindings = new List<TileWorldCreatorLayerBinding>();
    }
}
