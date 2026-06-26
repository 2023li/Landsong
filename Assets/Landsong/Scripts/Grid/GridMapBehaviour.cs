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
