using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

#endif
namespace Landsong.GridSystem
{
    /// <summary>TWC editing adapter, never a gameplay host. MapAsset is the runtime baking source.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Landsong/Map/Map Content Authoring")]
    public sealed class MapContentAuthoring : MonoBehaviour
    {
        [LabelText("地图标识"), ReadOnly]
        public string MapId;
        [LabelText("显示名称")]
        public string DisplayName;
        [LabelText("地图说明"), TextArea]
        public string Description;
        [LabelText("缩略图")]
        public Sprite Thumbnail;
        [HideInInspector, UnityEngine.Serialization.FormerlySerializedAs("BaseLayerName")]
        public string LegacyBaseLayerName = "Base";
        [LabelText("边缘（格）"), MinValue(0), Tooltip("从 TWC Settings 宽高构成的矩形边界向内取指定格数。边缘不可建造；只有存在且可通行的地表参与刷怪入场。0 表示不预留边缘。")]
        public int EdgeWidth = 5;
        [HideIf(nameof(UsesLegacyTerrainInput)), LabelText("地形映射规则"), Required, InlineEditor, Tooltip("跨 Layer 共用的地形枚举与权限规则；地图范围和边缘宽度在本组件配置。")]
        public MapTerrainRules TerrainRules;
        [HideInInspector]
        public bool UsesLegacyTerrainInput;
#if UNITY_EDITOR
        [HideInInspector]
        public LegacyMapTerrainInput LegacyTerrainInput = new LegacyMapTerrainInput();
#endif
        [LabelText("建筑目录"), Required]
        public BuildingCatalogAsset Buildings;
        [LabelText("初始王朝名称")]
        public string DynastyName = "新王朝";
        [LabelText("初始基础人口"), MinValue(0)]
        public int BasePopulation;
        [LabelText("初始随机种子")]
        public uint Seed = 13579;
        [LabelText("目标运行地图"), ReadOnly]
        public MapAsset TargetMap;
        [FoldoutGroup("地图美化"), HideReferenceObjectPicker, LabelText("生成配置")]
        public MapPopulationSettings PopulationTools = new MapPopulationSettings();
#if UNITY_EDITOR
        public static Func<MapContentAuthoring, string, string> PopulationAction;
        [FoldoutGroup("地图美化"), Button("填充默认树木与资源堆列表"), DisableInPlayMode]
        void PopulateDefaults() => RunPopulationAction("Defaults");
        [FoldoutGroup("地图美化"), Button("预览生成数量（不修改地图）"), DisableInPlayMode]
        void PreviewPopulation() => RunPopulationAction("Preview");
        [FoldoutGroup("地图美化"), Button("生成树林"), DisableInPlayMode]
        void GenerateForest() => RunPopulationAction("Forest");
        [FoldoutGroup("地图美化"), Button("随机放置建筑"), DisableInPlayMode]
        void GenerateBuildings() => RunPopulationAction("Buildings");
        [FoldoutGroup("地图美化"), Button("清除全部"), DisableInPlayMode, Tooltip("移除所有初始建筑，包括手工放置的王宫。支持撤销，不删除 TWC 地形。")]
        void ClearAllBuildings() => RunPopulationAction("ClearAll");
        [FoldoutGroup("地图美化"), Button("清除自动生成"), DisableInPlayMode, Tooltip("只移除工具生成的树林和建筑，保留手工建筑。支持撤销。")]
        void ClearGeneratedBuildings() => RunPopulationAction("ClearGenerated");
        void RunPopulationAction(string action)
        {
            try
            {
                if (PopulationAction == null)
                    throw new InvalidOperationException("地图美化工具尚未加载，请等待编辑器编译完成。");
                Debug.Log(PopulationAction(this, action), this);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                EditorUtility.DisplayDialog("地图美化未完成", error.Message, "确定");
            }
        }

        [HideInInspector]
        public string OwnerSceneGuid;
        [HideInInspector]
        public UnityEngine.Object EntityScene;
        [HideInInspector]
        public UnityEngine.Object TwcConfiguration;
        [HideInInspector]
        public TileWorldCreatorMapBakeProfile BakeProfile;
#endif
        [SerializeField, Sirenix.OdinInspector.LabelText("世界网格")]
        UnityEngine.Grid unityGrid;
        [SerializeField, Sirenix.OdinInspector.LabelText("逻辑地形定义")]
        GridMapDefinition mapDefinition;
        [SerializeField, Sirenix.OdinInspector.LabelText("地图表现根对象")]
        List<GameObject> mapVisualRoots = new List<GameObject>();
#if UNITY_EDITOR
        [SerializeField, Sirenix.OdinInspector.LabelText("显示初始建筑占地")]
        bool showInitialBuildingFootprints = true;
        [SerializeField, FoldoutGroup("地图美化"), LabelText("显示建筑名称"), OnValueChanged(nameof(RepaintBuildingLabels)), Tooltip("控制场景视图中所有初始建筑的名称及占地提示文字，包括树木、资源堆和手工建筑；不影响地图校验。")]
        bool showInitialBuildingLabels = true;
        [SerializeField, Sirenix.OdinInspector.LabelText("显示已烘焙地块")]
        bool showBakedGridCells;
        [SerializeField, Sirenix.OdinInspector.LabelText("显示未烘焙地块")]
        bool showUnbakedGridCells = true;
        [SerializeField, Sirenix.OdinInspector.LabelText("显示已烘焙格坐标")]
        bool showBakedGridCoordinates;
        [SerializeField, Sirenix.OdinInspector.LabelText("已烘焙地块颜色")]
        Color bakedGridCellColor = new Color(0, .85f, .35f, .16f);
        [SerializeField, Sirenix.OdinInspector.LabelText("未烘焙地块颜色")]
        Color unbakedGridCellColor = new Color(1, .15f, .05f, .1f);
#endif
        public UnityEngine.Grid UnityGrid => unityGrid;
        public GridMapDefinition MapDefinition => mapDefinition;
        public IReadOnlyList<GameObject> MapVisualRoots => mapVisualRoots;

        public bool TryValidateConfiguration(out string error) => TryValidateConfiguration(mapDefinition, out error);
        public bool TryValidateConfiguration(GridMapDefinition sourceMap, out string error)
        {
            if (unityGrid == null || sourceMap == null)
            {
                error = "请先通过 TWC 烘焙绑定网格和逻辑地形。";
                return false;
            }

            if (!sourceMap.TryValidate(out error))
                return false;
            var layout = new GridLayoutService(unityGrid);
            var origin = layout.GridToWorldPoint(0, 0);
            if (layout.PlaneMode != GridPlaneMode.XZ || !Mathf.Approximately((layout.GridToWorldPoint(1, 0) - origin).magnitude, sourceMap.CellSize) || !Mathf.Approximately((layout.GridToWorldPoint(0, 1) - origin).magnitude, sourceMap.CellSize))
            {
                error = "TWC 网格必须为等尺寸 XZ 平面，且格子尺寸与烘焙数据一致。";
                return false;
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        [ShowInInspector, ReadOnly, LabelText("地图范围（TWC 宽 × 高）"), Tooltip("直接读取 TWC Settings，不依赖任何 Blueprint 的瓦片范围。")]
        Vector2Int MapCanvasSize
        {
            get
            {
                var config = TwcConfiguration as GiantGrey.TileWorldCreator.Configuration;
                if (config == null)
                    config = GetComponent<GiantGrey.TileWorldCreator.TileWorldCreatorManager>()?.configuration;
                return config == null ? Vector2Int.zero : new Vector2Int(config.width, config.height);
            }
        }

        public void ConfigureFromTileWorldCreator(UnityEngine.Grid grid, GridMapDefinition bakedMap, IEnumerable<GameObject> visualRoots)
        {
            unityGrid = grid;
            mapDefinition = bakedMap;
            mapVisualRoots = (visualRoots ?? Array.Empty<GameObject>()).Where(r => r != null).Distinct().ToList();
            EditorUtility.SetDirty(this);
        }

        public InitialBuildingPreview[] Previews => GetComponentsInChildren<InitialBuildingPreview>(true);

        public bool TryCollectInitialBuildings(out InitialSource[] buildings, out string error) => TryCollectInitialBuildings(mapDefinition, out buildings, out error);
        public bool TryCollectInitialBuildings(GridMapDefinition sourceMap, out InitialSource[] buildings, out string error)
        {
            buildings = Array.Empty<InitialSource>();
            if (!TryValidateConfiguration(sourceMap, out error))
                return false;
            var layout = new GridLayoutService(unityGrid);
            var result = new List<InitialSource>();
            var occupied = new HashSet<GridPosition>();
            foreach (var preview in Previews)
            {
                var data = preview.Definition == null ? null : preview.Definition;
                if (data == null || preview.Level < 1 || preview.Level > data.MaximumLevel)
                {
                    error = "初始建筑需要有效的 ECS 建筑定义和等级：" + preview.name;
                    return false;
                }

                var orientation = BuildingOrientationUtility.FromWorldRotation(preview.transform.rotation, layout.PlaneMode);
                var size = orientation.GetEffectiveSize(data.Footprint);
                var point = layout.WorldToGridPoint(preview.transform.position);
                var origin = new GridPosition(Mathf.RoundToInt(point.x - size.x * .5f), Mathf.RoundToInt(point.y - size.y * .5f));
                if (data.Capabilities?.Connection?.Enabled == true)
                {
                    // The candidate map is validated with the same ECS connection rules as runtime placement.
                    for (int z = 0; z < size.y; z++)
                        for (int x = 0; x < size.x; x++)
                            if (!occupied.Add(new GridPosition(origin.X + x, origin.Z + z)))
                            {
                                error = "初始建筑占地重叠：" + preview.name;
                                return false;
                            }

                    result.Add(new InitialSource { Definition = preview.Definition, Name = preview.name, Cell = new Vector2Int(origin.X, origin.Z), Rotation = (int)orientation, Level = preview.Level });
                    continue;
                }

                if (!GridPlacementRuleEvaluator.TryResolveFlatFootprint(sourceMap, origin, data.Footprint, orientation, out var footprint, out var failure, out var failedCell) || !GridPlacementRuleEvaluator.TryValidateStaticPlacement(sourceMap, footprint, data.Capabilities.Placement.AllowedTerrains, data.Capabilities.Placement.ExcludedTerrains, out failure, out failedCell))
                {
                    error = preview.name + ": " + failure + " @ " + failedCell;
                    return false;
                }

                foreach (var cell in footprint.Positions())
                    if (!occupied.Add(cell))
                    {
                        error = "初始建筑占地重叠：" + preview.name;
                        return false;
                    }

                result.Add(new InitialSource { Definition = preview.Definition, Name = preview.name, Cell = new Vector2Int(origin.X, origin.Z), Rotation = (int)orientation, Level = preview.Level });
            }

            buildings = result.ToArray();
            error = string.Empty;
            return true;
        }

        public bool TrySnapInitialBuildingsToGrid(out string error)
        {
            if (Application.isPlaying)
            {
                error = "请先退出 Play Mode。";
                return false;
            }

            if (!TryCollectInitialBuildings(out var buildings, out error))
                return false;
            var layout = new GridLayoutService(unityGrid);
            var previews = Previews;
            Undo.RecordObjects(previews.Select(p => p.transform).ToArray(), "吸附 ECS 初始建筑预览");
            for (var i = 0; i < previews.Length; i++)
            {
                var item = buildings[i];
                var orientation = (BuildingOrientation)item.Rotation;
                var size = orientation.GetEffectiveSize(previews[i].Definition.Footprint);
                var anchor = new Unity.Mathematics.int2(item.Cell.x, item.Cell.y);
                var definition = previews[i].Definition;
                if (definition.Capabilities?.Connection?.Enabled == true)
                    anchor = TerrainConnectionOps.Port(anchor, new Unity.Mathematics.int2(definition.Footprint.x, definition.Footprint.y), item.Rotation, 0, 0);
                if (!mapDefinition.TryGetCell(new GridPosition(anchor.x, anchor.y), out var cell))
                {
                    error = "初始建筑连接入口没有地表：" + previews[i].name;
                    return false;
                }

                previews[i].transform.SetPositionAndRotation(layout.GridToWorldPoint(item.Cell.x + size.x * .5f, item.Cell.y + size.y * .5f) + layout.PlaneNormal * cell.ElevationLevel * mapDefinition.ElevationWorldStep, orientation.ToWorldRotation(layout.PlaneMode));
                PrefabUtility.RecordPrefabInstancePropertyModifications(previews[i].transform);
            }

            return true;
        }

        void RepaintBuildingLabels() => SceneView.RepaintAll();
        void OnDrawGizmos()
        {
            if (Application.isPlaying || unityGrid == null || mapDefinition == null)
                return;
            var layout = new GridLayoutService(unityGrid);
            void Draw(GridPosition position, Color color)
            {
                var corners = layout.GetCellCorners(position);
                var height = mapDefinition.TryGetCell(position, out var cell) ? cell.ElevationLevel * mapDefinition.ElevationWorldStep : 0;
                for (var i = 0; i < corners.Length; i++)
                    corners[i] += layout.PlaneNormal * (height + .025f);
                Handles.color = color;
                Handles.DrawAAConvexPolygon(corners);
                Handles.color = new Color(color.r, color.g, color.b, .8f);
                Handles.DrawAAPolyLine(corners.Concat(new[] { corners[0] }).ToArray());
                if (showBakedGridCoordinates)
                    Handles.Label((corners[0] + corners[2]) * .5f, position.ToString());
            }

            if (showBakedGridCells)
            {
                var bounds = mapDefinition.DeclaredCellBounds;
                for (var z = bounds.zMin; z < bounds.zMax; z++)
                    for (var x = bounds.xMin; x < bounds.xMax; x++)
                    {
                        var cell = new GridPosition(x, z);
                        var exists = mapDefinition.HasCell(cell);
                        if (exists || showUnbakedGridCells)
                            Draw(cell, exists ? bakedGridCellColor : unbakedGridCellColor);
                    }
            }

            var valid = TryCollectInitialBuildings(out var items, out _);
            var previews = Previews;
            for (var i = 0; i < previews.Length; i++)
            {
                if (showInitialBuildingLabels)
                    Handles.Label(previews[i].transform.position, previews[i].name + (valid ? "" : " [请检查占地/定义]"));
                if (!showInitialBuildingFootprints || !valid)
                    continue;
                var item = items[i];
                var size = ((BuildingOrientation)item.Rotation).GetEffectiveSize(previews[i].Definition.Footprint);
                for (var x = 0; x < size.x; x++)
                    for (var z = 0; z < size.y; z++)
                        Draw(new GridPosition(item.Cell.x + x, item.Cell.y + z), new Color(0, 1, 1, .2f));
            }
        }
#endif
    }
}
