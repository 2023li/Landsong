using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    public enum GridCellPermissionOverride
    {
        [LabelText("保持原值")]
        Keep = 0,
        [LabelText("允许")]
        Allow = 1,
        [LabelText("禁止")]
        Deny = 2
    }

    [Serializable]
    public sealed class TileWorldCreatorLayerBinding
    {
        [SerializeField, LabelText("TWC 图层 GUID"), ReadOnly]
        private string layerGuid;

        [SerializeField, LabelText("TWC 图层名称")]
        [PropertyTooltip("要映射为玩法数据的 TWC Blueprint Layer 名称。同步后会自动解析 GUID。")]
        private string layerName;

        [SerializeField, LabelText("地形标识")]
        [PropertyTooltip("写入逻辑格的地形 Key，例如：水域、道路、高地。")]
        private string terrainKey;

        [SerializeField, LabelText("替换主地形"), ToggleLeft]
        [PropertyTooltip("开启后将该图层的地形标识设为主地形；关闭时作为叠加地形写入。")]
        private bool replacePrimaryTerrain;

        [SerializeField, LabelText("建造权限")]
        private GridCellPermissionOverride buildable = GridCellPermissionOverride.Keep;

        [SerializeField, LabelText("通行权限")]
        private GridCellPermissionOverride traversable = GridCellPermissionOverride.Keep;

        [SerializeField, LabelText("使用 TWC 图层高度"), ToggleLeft]
        [PropertyTooltip("开启后读取该 Blueprint Layer 的 Default Layer Height，并按地图的高度最小单位换算为逻辑层级。")]
        private bool useLayerHeight = true;

        [SerializeField, LabelText("覆盖表面层"), ToggleLeft]
        private bool overrideSurfaceLayer;

        [SerializeField, LabelText("表面层级"), Min(0), ShowIf(nameof(overrideSurfaceLayer))]
        private int surfaceLayer;

        public string LayerGuid => layerGuid ?? string.Empty;
        public string LayerName => layerName ?? string.Empty;
        public string TerrainKey => GridTerrainKeys.Normalize(terrainKey);
        public bool ReplacePrimaryTerrain => replacePrimaryTerrain;
        public GridCellPermissionOverride Buildable => buildable;
        public GridCellPermissionOverride Traversable => traversable;
        public bool UseLayerHeight => useLayerHeight;
        public bool OverrideSurfaceLayer => overrideSurfaceLayer;
        public int SurfaceLayer => Mathf.Max(0, surfaceLayer);

#if UNITY_EDITOR
        public void SetResolvedLayer(string guid, string displayName)
        {
            layerGuid = guid ?? string.Empty;
            layerName = displayName ?? string.Empty;
        }
#endif
    }

    /// <summary>
    /// Editor-only authoring references are stored as UnityEngine.Object so runtime
    /// grid code never acquires a compile-time dependency on TileWorldCreator.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Landsong/地图/TWC 烘焙配置",
        fileName = "地图_烘焙配置")]
    public sealed class TileWorldCreatorMapBakeProfile : ScriptableObject
    {
        [SerializeField, TitleGroup("来源与输出"), LabelText("TWC 源配置"), Required]
        [PropertyTooltip("TileWorldCreator 的地图编辑配置，是本次烘焙的数据来源。")]
        private UnityEngine.Object sourceConfiguration;

        [SerializeField, TitleGroup("来源与输出"), LabelText("所属场景 GUID"), ReadOnly]
        private string sourceSceneGuid;

        [SerializeField, TitleGroup("来源与输出"), LabelText("所属场景路径"), ReadOnly]
        [PropertyTooltip("烘焙配置只属于这一张地图场景，不会按 TWC Configuration 跨场景复用。")]
        private string sourceScenePath;

        [SerializeField, TitleGroup("来源与输出"), LabelText("逻辑网格输出"), Required]
        [PropertyTooltip("烘焙产生的 GridMapDefinition。运行时建筑放置和地形判断读取该资产。")]
        private GridMapDefinition outputMap;

        [SerializeField, TitleGroup("基础逻辑层"), LabelText("基础层 GUID"), ReadOnly]
        private string baseLayerGuid;

        [SerializeField, TitleGroup("基础逻辑层"), LabelText("基础层名称")]
        [PropertyTooltip("定义地图有效格子范围的 TWC Blueprint Layer。")]
        private string baseLayerName = "base";

        [SerializeField, TitleGroup("基础逻辑层"), LabelText("默认地形标识")]
        private string baseTerrainKey = GridTerrainKeys.Land;

        [SerializeField, TitleGroup("基础逻辑层"), LabelText("默认允许建造"), ToggleLeft]
        private bool baseBuildable = true;

        [SerializeField, TitleGroup("基础逻辑层"), LabelText("默认允许通行"), ToggleLeft]
        private bool baseTraversable = true;

        [SerializeField, TitleGroup("世界映射"), LabelText("高度最小单位"), Min(0.001f)]
        [PropertyTooltip("一个逻辑高度层级对应多少世界单位。例如 0.1 可准确表示 0.5、0.6 等高度；不是建筑或地块的固定高度。")]
        private float elevationWorldStep = 1f;

        [SerializeField, TitleGroup("世界映射"), LabelText("烘焙前重新生成 TWC"), ToggleLeft]
        private bool regenerateTileWorldBeforeBake;

        [SerializeField, TitleGroup("世界映射"), LabelText("要求确定性随机种子"), ToggleLeft]
        private bool requireDeterministicGlobalSeed = true;

        [SerializeField, TitleGroup("附加玩法层"), LabelText("图层映射（从上到下应用）")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = "layerName")]
        private List<TileWorldCreatorLayerBinding> layerBindings =
            new List<TileWorldCreatorLayerBinding>();

        public UnityEngine.Object SourceConfiguration => sourceConfiguration;
        public string SourceSceneGuid => sourceSceneGuid ?? string.Empty;
        public string SourceScenePath => sourceScenePath ?? string.Empty;
        public GridMapDefinition OutputMap => outputMap;
        public string BaseLayerGuid => baseLayerGuid ?? string.Empty;
        public string BaseLayerName => baseLayerName ?? string.Empty;
        public string BaseTerrainKey
        {
            get
            {
                var normalized = GridTerrainKeys.Normalize(baseTerrainKey);
                return string.IsNullOrEmpty(normalized) ? GridTerrainKeys.Land : normalized;
            }
        }
        public bool BaseBuildable => baseBuildable;
        public bool BaseTraversable => baseTraversable;
        public float ElevationWorldStep => Mathf.Max(0.001f, elevationWorldStep);
        public bool RegenerateTileWorldBeforeBake => regenerateTileWorldBeforeBake;
        public bool RequireDeterministicGlobalSeed => requireDeterministicGlobalSeed;
        public IReadOnlyList<TileWorldCreatorLayerBinding> LayerBindings =>
            layerBindings != null ? layerBindings : EmptyBindings;

        private static readonly IReadOnlyList<TileWorldCreatorLayerBinding> EmptyBindings =
            Array.Empty<TileWorldCreatorLayerBinding>();

        private void OnValidate()
        {
            elevationWorldStep = Mathf.Max(0.001f, elevationWorldStep);
            baseTerrainKey = BaseTerrainKey;
            layerBindings ??= new List<TileWorldCreatorLayerBinding>();
        }

#if UNITY_EDITOR
        public void AssignSourceConfiguration(UnityEngine.Object configuration)
        {
            sourceConfiguration = configuration;
        }

        public void AssignSourceScene(string sceneGuid, string scenePath)
        {
            sourceSceneGuid = sceneGuid ?? string.Empty;
            sourceScenePath = scenePath ?? string.Empty;
        }

        public void AssignOutputMap(GridMapDefinition mapDefinition)
        {
            outputMap = mapDefinition;
        }

        public void SetResolvedBaseLayer(string guid, string displayName)
        {
            baseLayerGuid = guid ?? string.Empty;
            baseLayerName = displayName ?? string.Empty;
        }
#endif
    }
}
