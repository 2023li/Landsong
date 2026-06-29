using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [AddComponentMenu("Landsong/Building/Initial Building Placement")]
    public sealed class InitialBuildingPlacement : MonoBehaviour
    {
        [SerializeField, LabelText("Grid Map")] private GridMapBehaviour gridMap;
        [SerializeField, LabelText("Include Inactive Children")] private bool includeInactiveChildren;
        [SerializeField, LabelText("Snap Children On Awake")] private bool snapChildrenToGridOnAwake = true;
        [SerializeField, LabelText("Occupy On Awake")] private bool occupyGridOnAwake = true;

        private bool placementApplied;

        private void Reset()
        {
            ResolveGridMap();
        }

        private void Awake()
        {
            if (occupyGridOnAwake)
            {
                ApplyInitialPlacement();
            }
        }

        public void ApplyInitialPlacement()
        {
            if (placementApplied || !ResolveGridMap())
            {
                return;
            }

            var buildings = GetChildBuildings();
            var placedCount = 0;

            foreach (var childBuilding in buildings)
            {
                if (TryApplyInitialPlacement(childBuilding))
                {
                    placedCount++;
                }
            }

            if (placedCount == 0)
            {
                Debug.LogWarning($"InitialBuildingPlacement on '{name}' did not place any child buildings.", this);
            }

            placementApplied = true;
        }

        [Button("Snap Children To Grid")]
        [ContextMenu("Snap Children To Grid")]
        public void SnapChildrenToGrid()
        {
            if (!ResolveGridMap())
            {
                return;
            }

            var buildings = GetChildBuildings();
            foreach (var childBuilding in buildings)
            {
                if (childBuilding == null || !childBuilding.HasDefinition)
                {
                    continue;
                }

                SnapBuildingToGrid(childBuilding);
            }
        }

        private bool TryApplyInitialPlacement(BuildingBase childBuilding)
        {
            if (childBuilding == null)
            {
                return false;
            }

            if (!childBuilding.HasDefinition)
            {
                Debug.LogWarning($"Initial child building '{childBuilding.name}' has no BuildingDefinition.", childBuilding);
                return false;
            }

            var size = childBuilding.Definition.Size;
            var origin = GetNearestOrigin(gridMap, childBuilding.transform.position, size);
            var occupancyId = CreateOccupancyId(childBuilding, origin);
            if (!gridMap.TryOccupy(origin, size, occupancyId, out var failureReason))
            {
                Debug.LogWarning(
                    $"Cannot apply initial placement for child building '{childBuilding.name}' at {origin}: {failureReason}.",
                    childBuilding);
                return false;
            }

            if (snapChildrenToGridOnAwake)
            {
                SnapBuildingToGrid(childBuilding, origin, size);
            }

            childBuilding.SetPlacement(origin, occupancyId, gridMap);
            return true;
        }

        private void SnapBuildingToGrid(BuildingBase childBuilding)
        {
            var size = childBuilding.Definition.Size;
            var origin = GetNearestOrigin(gridMap, childBuilding.transform.position, size);
            SnapBuildingToGrid(childBuilding, origin, size);
        }

        private void SnapBuildingToGrid(BuildingBase childBuilding, GridPosition origin, Vector2Int size)
        {
#if UNITY_EDITOR
            RecordUndo(childBuilding.transform, "Snap Initial Buildings To Grid");
#endif
            childBuilding.transform.position = gridMap.GetFootprintCenter(origin, size);
        }

        private bool ResolveGridMap()
        {
            if (gridMap == null)
            {
                gridMap = FindFirstObjectByType<GridMapBehaviour>();
            }

            if (gridMap != null)
            {
                return true;
            }

            Debug.LogWarning($"InitialBuildingPlacement on '{name}' could not find a GridMapBehaviour.", this);
            return false;
        }

        private BuildingBase[] GetChildBuildings()
        {
            var buildings = GetComponentsInChildren<BuildingBase>(includeInactiveChildren);
            var writeIndex = 0;
            for (var i = 0; i < buildings.Length; i++)
            {
                var childBuilding = buildings[i];
                if (childBuilding == null || childBuilding.transform == transform)
                {
                    continue;
                }

                buildings[writeIndex] = childBuilding;
                writeIndex++;
            }

            if (writeIndex == buildings.Length)
            {
                return buildings;
            }

            var result = new BuildingBase[writeIndex];
            for (var i = 0; i < writeIndex; i++)
            {
                result[i] = buildings[i];
            }

            return result;
        }

        private static GridPosition GetNearestOrigin(GridMapBehaviour targetGridMap, Vector3 worldPosition, Vector2Int size)
        {
            var layout = targetGridMap.IsInitialized ? targetGridMap.Layout : targetGridMap.CreateLayoutSnapshot();
            var gridPoint = layout.WorldToGridPoint(worldPosition);
            return new GridPosition(
                Mathf.RoundToInt(gridPoint.x - size.x * 0.5f),
                Mathf.RoundToInt(gridPoint.y - size.y * 0.5f));
        }

        private static string CreateOccupancyId(BuildingBase childBuilding, GridPosition origin)
        {
            var definition = childBuilding.Definition;
            var buildingId = definition != null && !string.IsNullOrWhiteSpace(definition.BuildingId)
                ? definition.BuildingId
                : childBuilding.name;
            return $"initial_{buildingId}_{origin.X}_{origin.Y}_{childBuilding.GetInstanceID()}";
        }

        private void OnDrawGizmosSelected()
        {
            var targetGridMap = gridMap;
            if (targetGridMap == null)
            {
                targetGridMap = FindFirstObjectByType<GridMapBehaviour>();
            }

            if (targetGridMap == null)
            {
                return;
            }

            var layout = targetGridMap.IsInitialized ? targetGridMap.Layout : targetGridMap.CreateLayoutSnapshot();
            var buildings = GetChildBuildings();

            Gizmos.color = Color.cyan;
            foreach (var childBuilding in buildings)
            {
                if (childBuilding == null || !childBuilding.HasDefinition)
                {
                    continue;
                }

                var size = childBuilding.Definition.Size;
                var origin = GetNearestOrigin(targetGridMap, childBuilding.transform.position, size);
                var footprint = new GridFootprint(origin, size);
                foreach (var position in footprint.Positions())
                {
                    DrawCellGizmo(layout, position);
                }
            }
        }

        private static void DrawCellGizmo(GridLayoutService layout, GridPosition position)
        {
            var corners = layout.GetCellCorners(position);
            for (var i = 0; i < corners.Length; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
            }
        }

#if UNITY_EDITOR
        private static void RecordUndo(Object target, string label)
        {
            if (Application.isPlaying)
            {
                return;
            }

            Undo.RecordObject(target, label);
            EditorUtility.SetDirty(target);
        }
#endif
    }
}
