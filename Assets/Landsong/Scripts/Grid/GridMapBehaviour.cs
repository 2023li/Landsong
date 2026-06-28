using UnityEngine;

namespace Landsong.GridSystem
{
    public sealed class GridMapBehaviour : MonoBehaviour
    {
        [SerializeField, Min(1)] private int width = 32;
        [SerializeField, Min(1)] private int height = 32;
        [SerializeField, Min(0.01f)] private float cellSize = 1f;
        [SerializeField] private Vector3 originOffset = Vector3.zero;
        [SerializeField] private GridPlaneMode planeMode = GridPlaneMode.IsometricDiamondXY;
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private bool defaultBuildable = true;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector3 WorldOrigin => transform.position + originOffset;
        public GridPlaneMode PlaneMode => planeMode;
        public GridMap Map { get; private set; }
        public GridLayoutService Layout { get; private set; }
        public bool IsInitialized => Map != null && Layout != null;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cellSize = Mathf.Max(0.01f, cellSize);
        }

        public void Initialize()
        {
            Map = new GridMap(width, height, defaultBuildable);
            RefreshLayout();
        }

        public void RefreshLayout()
        {
            Layout = CreateLayoutSnapshot();
        }

        public GridLayoutService CreateLayoutSnapshot()
        {
            return new GridLayoutService(cellSize, WorldOrigin, planeMode);
        }

        public bool TryGetGridPositionFromRay(Ray ray, out GridPosition position)
        {
            EnsureInitialized();

            if (!Layout.TryGetGridPosition(ray, out position))
            {
                return false;
            }

            return Map.Contains(position);
        }

        public bool TryGetGridPointFromRay(Ray ray, out Vector2 gridPoint)
        {
            EnsureInitialized();

            if (!Layout.TryRaycastToGridPlane(ray, out var worldPosition))
            {
                gridPoint = default;
                return false;
            }

            gridPoint = Layout.WorldToGridPoint(worldPosition);
            return true;
        }

        public bool TryGetGridPositionFromScreenPosition(Camera sourceCamera, Vector2 screenPosition, out GridPosition position)
        {
            if (sourceCamera == null)
            {
                position = default;
                return false;
            }

            var ray = sourceCamera.ScreenPointToRay(screenPosition);
            return TryGetGridPositionFromRay(ray, out position);
        }

        public bool TryGetGridPointFromScreenPosition(Camera sourceCamera, Vector2 screenPosition, out Vector2 gridPoint)
        {
            if (sourceCamera == null)
            {
                gridPoint = default;
                return false;
            }

            var ray = sourceCamera.ScreenPointToRay(screenPosition);
            return TryGetGridPointFromRay(ray, out gridPoint);
        }

        public Vector3 GetCellCenter(GridPosition position)
        {
            EnsureInitialized();
            return Layout.GridToWorldCenter(position);
        }

        public bool TryOccupy(GridPosition origin, Vector2Int size, string occupantId, out GridPlacementFailureReason failureReason)
        {
            EnsureInitialized();
            return Map.TryOccupy(origin, size, occupantId, out failureReason);
        }

        public bool CanOccupy(GridPosition origin, Vector2Int size, out GridPlacementFailureReason failureReason, string ignoredOccupantId = null)
        {
            EnsureInitialized();
            return Map.CanOccupy(origin, size, out failureReason, ignoredOccupantId);
        }

        public Vector3 GetFootprintCenter(GridPosition origin, Vector2Int size)
        {
            EnsureInitialized();
            return Layout.GridToWorldPoint(origin.X + size.x * 0.5f, origin.Y + size.y * 0.5f);
        }

        public int ClearOccupant(string occupantId)
        {
            EnsureInitialized();
            return Map.ClearOccupant(occupantId);
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                Initialize();
            }
        }
    }
}
