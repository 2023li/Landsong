using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.VisualSystem
{
    /// <summary>
    /// 供建筑 View 中风车扇叶、水车等独立动态部件使用的轻量旋转组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LS_LocalAxisRotator : MonoBehaviour
    {
        [SerializeField] [LabelText("局部旋转轴")] private Vector3 localAxis = Vector3.forward;
        [SerializeField] [LabelText("每秒旋转角度")] private float degreesPerSecond = 30f;
        [SerializeField] [LabelText("使用不缩放时间")] private bool useUnscaledTime;

        public Vector3 LocalAxis => localAxis;
        public float DegreesPerSecond => degreesPerSecond;
        public bool UseUnscaledTime => useUnscaledTime;

        private void OnValidate()
        {
            if (localAxis.sqrMagnitude <= 0.000001f)
            {
                localAxis = Vector3.forward;
            }
        }

        private void Update()
        {
            var axis = localAxis.sqrMagnitude <= 0.000001f
                ? Vector3.forward
                : localAxis.normalized;
            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            transform.Rotate(axis, degreesPerSecond * deltaTime, Space.Self);
        }

        public void Configure(Vector3 axis, float speed, bool unscaledTime)
        {
            localAxis = axis.sqrMagnitude <= 0.000001f ? Vector3.forward : axis;
            degreesPerSecond = speed;
            useUnscaledTime = unscaledTime;
        }
    }
}
