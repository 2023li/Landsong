using UnityEngine;

namespace Landsong.GridSystem
{
    public sealed class GridDebugOccupier : MonoBehaviour
    {
        [SerializeField] private GridMapBehaviour gridMap;
        [SerializeField] private string occupantId = "DebugOccupant";
        [SerializeField] private GridPosition origin = GridPosition.Zero;
        [SerializeField] private Vector2Int size = Vector2Int.one;
        [SerializeField] private bool occupyOnStart = true;
        [SerializeField] private bool releaseOnDestroy = true;
        [SerializeField] private bool logResult = true;

        private void Reset()
        {
            gridMap = FindFirstObjectByType<GridMapBehaviour>();
        }

        private void OnValidate()
        {
            size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        private void Start()
        {
            if (occupyOnStart)
            {
                Occupy();
            }
        }

        private void OnDestroy()
        {
            if (releaseOnDestroy && gridMap != null)
            {
                gridMap.ClearOccupant(occupantId);
            }
        }

        public bool Occupy()
        {
            if (gridMap == null)
            {
                if (logResult)
                {
                    Debug.LogWarning("GridDebugOccupier has no GridMapBehaviour assigned.", this);
                }

                return false;
            }

            var occupied = gridMap.TryOccupy(origin, size, occupantId, out var failureReason);
            if (logResult)
            {
                Debug.Log(occupied
                    ? $"Occupied grid cells at {origin} with size {size} for '{occupantId}'."
                    : $"Failed to occupy grid cells at {origin} with size {size}: {failureReason}.", this);
            }

            return occupied;
        }
    }
}
