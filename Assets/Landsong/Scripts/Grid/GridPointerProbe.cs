using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong.GridSystem
{
    public sealed class GridPointerProbe : MonoBehaviour
    {
        [SerializeField] private GridMapBehaviour gridMap;
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private bool logCellChanges = false;
        [SerializeField] private bool hasCurrentCell;
        [SerializeField] private GridPosition currentCell;

        public event Action<GridPosition> CurrentCellChanged;

        public bool HasCurrentCell => hasCurrentCell;
        public GridPosition CurrentCell => currentCell;

        private void Reset()
        {
            gridMap = GetComponent<GridMapBehaviour>();
            sourceCamera = Camera.main;
        }

        private void Awake()
        {
            if (sourceCamera == null)
            {
                sourceCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (gridMap == null || sourceCamera == null)
            {
                return;
            }

            if (!TryReadPointerScreenPosition(out var screenPosition))
            {
                SetNoCell();
                return;
            }

            if (!gridMap.TryGetGridPositionFromScreenPosition(sourceCamera, screenPosition, out var nextCell))
            {
                SetNoCell();
                return;
            }

            if (hasCurrentCell && currentCell == nextCell)
            {
                return;
            }

            hasCurrentCell = true;
            currentCell = nextCell;
            CurrentCellChanged?.Invoke(currentCell);

            if (logCellChanges)
            {
                Debug.Log($"Grid pointer cell: {currentCell}", this);
            }
        }

        private static bool TryReadPointerScreenPosition(out Vector2 screenPosition)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            {
                screenPosition = touchscreen.primaryTouch.position.ReadValue();
                return true;
            }

            var pointer = Pointer.current;
            if (pointer != null)
            {
                screenPosition = pointer.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        private void SetNoCell()
        {
            hasCurrentCell = false;
        }
    }
}
