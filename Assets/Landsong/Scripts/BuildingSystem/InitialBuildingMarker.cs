using Landsong.GridSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Landsong/Building/Initial Building Marker")]
    public sealed class InitialBuildingMarker : MonoBehaviour
    {
        [SerializeField, LabelText("建筑 Prefab")]
        private BuildingBase buildingPrefab;

        [SerializeField, LabelText("占地原点")]
        private GridPosition origin;

        public BuildingBase BuildingPrefab => buildingPrefab;
        public GridPosition Origin => origin;
        public bool IsValid => buildingPrefab != null && buildingPrefab.HasDefinition;

        private void OnValidate()
        {
            transform.localRotation = Quaternion.identity;
        }

        private void OnDrawGizmosSelected()
        {
            if (!IsValid)
            {
                return;
            }

            var content = GetComponentInParent<MapContentAuthoring>();
            if (content == null || content.UnityGrid == null)
            {
                return;
            }

            var layout = new GridLayoutService(content.UnityGrid);
            Gizmos.color = Color.cyan;
            foreach (var position in buildingPrefab.Definition.CreateFootprint(origin).Positions())
            {
                var corners = layout.GetCellCorners(position);
                for (var i = 0; i < corners.Length; i++)
                {
                    Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);
                }
            }
        }
    }
}
