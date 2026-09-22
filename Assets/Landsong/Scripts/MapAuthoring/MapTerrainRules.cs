using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Landsong.GridSystem
{
    // Shared rules contain no scene, configuration, output or resolved layer GUID references.
    [CreateAssetMenu(menuName = "Landsong/地图/分层地形映射规则", fileName = "分层地形规则")]
    public sealed class MapTerrainRules : ScriptableObject
    {
        [FormerlySerializedAs("useLayerPresetRules"), LabelText("使用 Layer / Blueprint 逻辑")] public bool useLayerBlueprintRules=true;
        [ShowIf(nameof(useLayerBlueprintRules)), LabelText("跨 Layer 共用地形规则")]
        public List<BlueprintTerrainRule> blueprintRules = new List<BlueprintTerrainRule>();
        [ShowIf(nameof(useLayerBlueprintRules)),LabelText("同层覆盖排序"),Required,InlineEditor] public TerrainOverlapRules overlapRules;
        [HideInInspector] public string edgeLayerName = "边缘区";
        [HideInInspector, LabelText("基础层名称")] public string baseLayerName = "Base";
        [HideInInspector, LabelText("默认地形标识")] public string baseTerrainKey = "水域";
        [HideInInspector, LabelText("默认允许建造")] public bool baseBuildable;
        [HideInInspector, LabelText("默认允许通行")] public bool baseTraversable = true;
        [HideInInspector, LabelText("高度最小单位"), MinValue(.001f)] public float elevationWorldStep = .1f;
        [HideInInspector, LabelText("图层映射（按顺序应用）")] public List<TileWorldCreatorLayerBinding> layerBindings = new List<TileWorldCreatorLayerBinding>();
    }
}
