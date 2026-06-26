using UnityEngine;

namespace Landsong.GridSystem
{
    public sealed class GridDebugRenderer : MonoBehaviour
    {
        [SerializeField] private GridMapBehaviour gridMap;
        [SerializeField] private GridPointerProbe pointerProbe;
        [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color blockedCellColor = new Color(0.8f, 0.1f, 0.1f, 0.25f);
        [SerializeField] private Color occupiedCellColor = new Color(1f, 0.55f, 0.05f, 0.35f);
        [SerializeField] private Color currentCellColor = new Color(0.15f, 0.75f, 1f, 0.45f);
        [SerializeField, Min(0f)] private float overlayThickness = 0.02f;
        [SerializeField] private bool drawRuntimeCellStates = true;
        [SerializeField] private bool drawPointerCell = true;

        private void Reset()
        {
            gridMap = GetComponent<GridMapBehaviour>();
            pointerProbe = GetComponent<GridPointerProbe>();
        }

        private void OnDrawGizmos()
        {
            if (gridMap == null)
            {
                gridMap = GetComponent<GridMapBehaviour>();
            }

            if (gridMap == null)
            {
                return;
            }

            var layout = Application.isPlaying && gridMap.Layout != null
                ? gridMap.Layout
                : gridMap.CreateLayoutSnapshot();

            DrawLines(layout, gridMap.Width, gridMap.Height);
            DrawRuntimeCellStates(layout);
            DrawPointerCell(layout);
        }

        private void DrawLines(GridLayoutService layout, int width, int height)
        {
            Gizmos.color = lineColor;

            for (var x = 0; x <= width; x++)
            {
                Gizmos.DrawLine(layout.GridToWorldPoint(x, 0), layout.GridToWorldPoint(x, height));
            }

            for (var y = 0; y <= height; y++)
            {
                Gizmos.DrawLine(layout.GridToWorldPoint(0, y), layout.GridToWorldPoint(width, y));
            }
        }

        private void DrawRuntimeCellStates(GridLayoutService layout)
        {
            if (!drawRuntimeCellStates || !Application.isPlaying || gridMap.Map == null)
            {
                return;
            }

            var cellSize = layout.GetCellWorldSize(overlayThickness);
            foreach (var cell in gridMap.Map.Cells)
            {
                if (!cell.IsBuildable)
                {
                    DrawCellMarker(layout, cell.Position, blockedCellColor, cellSize);
                    continue;
                }

                if (cell.IsOccupied)
                {
                    DrawCellMarker(layout, cell.Position, occupiedCellColor, cellSize);
                }
            }
        }

        private void DrawPointerCell(GridLayoutService layout)
        {
            if (!drawPointerCell || pointerProbe == null || !pointerProbe.HasCurrentCell)
            {
                return;
            }

            Gizmos.color = currentCellColor;
            DrawCellMarker(layout, pointerProbe.CurrentCell, currentCellColor, layout.GetCellWorldSize(overlayThickness * 2f));
        }

        private static void DrawCellMarker(GridLayoutService layout, GridPosition position, Color color, Vector3 cellSize)
        {
            Gizmos.color = color;

            if (!layout.IsIsometricDiamond)
            {
                Gizmos.DrawCube(layout.GridToWorldCenter(position), cellSize);
                return;
            }

            var corners = layout.GetCellCorners(position);
            for (var i = 0; i < corners.Length; i++)
            {
                Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
            }

            Gizmos.DrawSphere(layout.GridToWorldCenter(position), Mathf.Max(0.02f, layout.CellSize * 0.04f));
        }
    }
}
