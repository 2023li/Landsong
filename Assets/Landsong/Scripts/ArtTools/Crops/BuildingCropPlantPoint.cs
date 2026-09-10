using UnityEngine;

namespace Landsong.VisualSystem
{
    /// <summary>
    /// 农田固定种植点的空节点标记。实际点位数据完全来自 Transform。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Landsong/Building/Crop Plant Point")]
    public sealed class BuildingCropPlantPoint : MonoBehaviour
    {
        public Vector3 LocalPosition => transform.localPosition;
        public Quaternion LocalRotation => transform.localRotation;
        public float UniformScale => transform.localScale.x;

        public bool TryValidateConfiguration(
            BuildingCropVisualController expectedController,
            out string error)
        {
            if (expectedController == null || transform.parent != expectedController.transform)
            {
                error = $"固定种植点 {name} 必须是 BuildingCropVisualController 的直接子对象。";
                return false;
            }

            if (!gameObject.activeSelf || !enabled)
            {
                error = $"固定种植点 {name} 及其标记组件必须默认启用。";
                return false;
            }

            if (transform.childCount != 0)
            {
                error = $"固定种植点 {name} 必须是没有子对象的空节点。";
                return false;
            }

            var components = GetComponents<Component>();
            if (components.Length != 2)
            {
                error = $"固定种植点 {name} 只能包含 Transform 和 BuildingCropPlantPoint。";
                return false;
            }

            var position = transform.localPosition;
            var rotation = transform.localRotation;
            var scale = transform.localScale;
            if (!IsFinite(position.x)
                || !IsFinite(position.y)
                || !IsFinite(position.z)
                || !IsFinite(rotation.x)
                || !IsFinite(rotation.y)
                || !IsFinite(rotation.z)
                || !IsFinite(rotation.w)
                || !IsFinite(scale.x)
                || !IsFinite(scale.y)
                || !IsFinite(scale.z))
            {
                error = $"固定种植点 {name} 的 Transform 包含无效数值。";
                return false;
            }

            if (scale.x <= 0f
                || !Mathf.Approximately(scale.x, scale.y)
                || !Mathf.Approximately(scale.x, scale.z))
            {
                error = $"固定种植点 {name} 必须使用大于 0 的统一缩放。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void OnDrawGizmos()
        {
            DrawPointGizmo(new Color(0.3f, 1f, 0.45f, 0.85f));
        }

        private void OnDrawGizmosSelected()
        {
            DrawPointGizmo(new Color(1f, 0.68f, 0.2f, 1f));
        }

        private void DrawPointGizmo(Color pointColor)
        {
            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = pointColor;
            Gizmos.DrawWireSphere(Vector3.zero, 0.06f);
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.18f);
            Gizmos.matrix = previousMatrix;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
