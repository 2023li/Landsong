using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS;
using Landsong.ECS.Authoring;
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
        [LabelText("目标运行地图")] public MapAsset TargetMap;
        [SerializeField, Sirenix.OdinInspector.LabelText("世界网格")] UnityEngine.Grid unityGrid;
        [SerializeField, Sirenix.OdinInspector.LabelText("逻辑地形定义")] GridMapDefinition mapDefinition;
        [SerializeField, Sirenix.OdinInspector.LabelText("地图表现根对象")] List<GameObject> mapVisualRoots = new List<GameObject>();
#if UNITY_EDITOR
        [SerializeField, Sirenix.OdinInspector.LabelText("显示初始建筑占地")] bool showInitialBuildingFootprints = true;
        [SerializeField, Sirenix.OdinInspector.LabelText("显示初始建筑标签")] bool showInitialBuildingLabels = true;
        [SerializeField, Sirenix.OdinInspector.LabelText("显示已烘焙地块")] bool showBakedGridCells;
        [SerializeField, Sirenix.OdinInspector.LabelText("显示未烘焙地块")] bool showUnbakedGridCells = true;
        [SerializeField, Sirenix.OdinInspector.LabelText("显示已烘焙格坐标")] bool showBakedGridCoordinates;
        [SerializeField, Sirenix.OdinInspector.LabelText("已烘焙地块颜色")] Color bakedGridCellColor = new Color(0, .85f, .35f, .16f);
        [SerializeField, Sirenix.OdinInspector.LabelText("未烘焙地块颜色")] Color unbakedGridCellColor = new Color(1, .15f, .05f, .1f);
#endif

        public UnityEngine.Grid UnityGrid => unityGrid;
        public GridMapDefinition MapDefinition => mapDefinition;
        public IReadOnlyList<GameObject> MapVisualRoots => mapVisualRoots;
        public bool TryValidateConfiguration(out string error)
        {
            if (unityGrid == null || mapDefinition == null) { error = "请先通过 TWC 烘焙绑定网格和逻辑地形。"; return false; }
            if (!mapDefinition.TryValidate(out error)) return false;
            var layout = new GridLayoutService(unityGrid);
            var origin = layout.GridToWorldPoint(0, 0);
            if (layout.PlaneMode != GridPlaneMode.XZ ||
                !Mathf.Approximately((layout.GridToWorldPoint(1, 0) - origin).magnitude, mapDefinition.CellSize) ||
                !Mathf.Approximately((layout.GridToWorldPoint(0, 1) - origin).magnitude, mapDefinition.CellSize))
            { error = "TWC 网格必须为等尺寸 XZ 平面，且格子尺寸与烘焙数据一致。"; return false; }
            error = string.Empty; return true;
        }

#if UNITY_EDITOR
        public void ConfigureFromTileWorldCreator(UnityEngine.Grid grid, GridMapDefinition bakedMap, IEnumerable<GameObject> visualRoots)
        {
            unityGrid = grid; mapDefinition = bakedMap;
            mapVisualRoots = (visualRoots ?? Array.Empty<GameObject>()).Where(r => r != null).Distinct().ToList();
            EditorUtility.SetDirty(this);
        }
        public InitialBuildingPreview[] Previews => GetComponentsInChildren<InitialBuildingPreview>(true);

        public bool TryCollectInitialBuildings(out InitialSource[] buildings, out string error)
        {
            buildings = Array.Empty<InitialSource>();
            if (!TryValidateConfiguration(out error)) return false;
            var layout = new GridLayoutService(unityGrid); var result = new List<InitialSource>(); var occupied = new HashSet<GridPosition>();
            foreach (var preview in Previews)
            {
                var data = preview.Definition == null ? null : preview.Definition.Data;
                if (data == null || data.Kind != ContentKind.Building || preview.Level < 1 || preview.Level > data.Level)
                { error = "初始建筑需要有效的 ECS 建筑定义和等级：" + preview.name; return false; }
                var orientation = BuildingOrientationUtility.FromWorldRotation(preview.transform.rotation, layout.PlaneMode);
                var size = orientation.GetEffectiveSize(data.Size); var point = layout.WorldToGridPoint(preview.transform.position);
                var origin = new GridPosition(Mathf.RoundToInt(point.x - size.x * .5f), Mathf.RoundToInt(point.y - size.y * .5f));
                if (!GridPlacementRuleEvaluator.TryResolveFlatFootprint(mapDefinition, origin, data.Size, orientation, out var footprint, out var failure, out var failedCell) ||
                    !GridPlacementRuleEvaluator.TryValidateStaticPlacement(mapDefinition, footprint,
                        data.Modules.Placement.Enabled ? data.Modules.Placement.RequiredTerrains.Select(r => r.Terrain).ToArray() : Array.Empty<string>(),
                        data.Modules.Placement.Enabled ? data.Modules.Placement.AlternativeTerrains.Select(r => r.Terrain).ToArray() : Array.Empty<string>(), out failure, out failedCell))
                { error = preview.name + ": " + failure + " @ " + failedCell; return false; }
                foreach (var cell in footprint.Positions()) if (!occupied.Add(cell)) { error = "初始建筑占地重叠：" + preview.name; return false; }
                result.Add(new InitialSource { Definition = data.Id, Name = preview.name, Cell = new Vector2Int(origin.X, origin.Z), Rotation = (int)orientation, Level = preview.Level });
            }
            buildings = result.ToArray(); error = string.Empty; return true;
        }

        public bool TrySnapInitialBuildingsToGrid(out string error)
        {
            if (Application.isPlaying) { error = "请先退出 Play Mode。"; return false; }
            if (!TryCollectInitialBuildings(out var buildings, out error)) return false;
            var layout = new GridLayoutService(unityGrid); var previews = Previews;
            Undo.RecordObjects(previews.Select(p => p.transform).ToArray(), "吸附 ECS 初始建筑预览");
            for (var i = 0; i < previews.Length; i++)
            {
                var item = buildings[i]; var orientation = (BuildingOrientation)item.Rotation;
                var size = orientation.GetEffectiveSize(previews[i].Definition.Data.Size);
                mapDefinition.TryGetCell(new GridPosition(item.Cell.x, item.Cell.y), out var cell);
                previews[i].transform.SetPositionAndRotation(layout.GridToWorldPoint(item.Cell.x + size.x * .5f, item.Cell.y + size.y * .5f) +
                    layout.PlaneNormal * cell.ElevationLevel * mapDefinition.ElevationWorldStep, orientation.ToWorldRotation(layout.PlaneMode));
                PrefabUtility.RecordPrefabInstancePropertyModifications(previews[i].transform);
            }
            return true;
        }

        void OnDrawGizmos()
        {
            if (Application.isPlaying || unityGrid == null || mapDefinition == null) return;
            var layout = new GridLayoutService(unityGrid);
            void Draw(GridPosition position, Color color)
            {
                var corners = layout.GetCellCorners(position);
                var height = mapDefinition.TryGetCell(position, out var cell) ? cell.ElevationLevel * mapDefinition.ElevationWorldStep : 0;
                for (var i = 0; i < corners.Length; i++) corners[i] += layout.PlaneNormal * (height + .025f);
                Handles.color = color; Handles.DrawAAConvexPolygon(corners);
                Handles.color = new Color(color.r, color.g, color.b, .8f); Handles.DrawAAPolyLine(corners.Concat(new[] { corners[0] }).ToArray());
                if (showBakedGridCoordinates) Handles.Label((corners[0] + corners[2]) * .5f, position.ToString());
            }
            if (showBakedGridCells)
            {
                var bounds = mapDefinition.DeclaredCellBounds;
                for (var z = bounds.zMin; z < bounds.zMax; z++) for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var cell = new GridPosition(x, z); var exists = mapDefinition.HasCell(cell);
                    if (exists || showUnbakedGridCells) Draw(cell, exists ? bakedGridCellColor : unbakedGridCellColor);
                }
            }
            var valid = TryCollectInitialBuildings(out var items, out _); var previews = Previews;
            for (var i = 0; i < previews.Length; i++)
            {
                if (showInitialBuildingLabels) Handles.Label(previews[i].transform.position, previews[i].name + (valid ? "" : " [请检查占地/定义]"));
                if (!showInitialBuildingFootprints || !valid) continue;
                var item = items[i]; var size = ((BuildingOrientation)item.Rotation).GetEffectiveSize(previews[i].Definition.Data.Size);
                for (var x = 0; x < size.x; x++) for (var z = 0; z < size.y; z++) Draw(new GridPosition(item.Cell.x + x, item.Cell.y + z), new Color(0, 1, 1, .2f));
            }
        }
#endif
    }
}
