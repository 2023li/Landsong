using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    [AddComponentMenu("Landsong/Map/弹体阻挡区域")]
    public sealed class MapProjectileRegionAuthoring : MonoBehaviour
    {
        [LabelText("区域尺寸")] public Vector2 Size = Vector2.one;
        [LabelText("阻挡弹体")] public bool BlocksProjectile = true;
        public bool Contains(Vector3 point)
        {
            var center = transform.position;
            return Mathf.Abs(point.x - center.x) < Size.x * transform.lossyScale.x * .5f && Mathf.Abs(point.z - center.z) < Size.y * transform.lossyScale.z * .5f;
        }
#if UNITY_EDITOR
        void OnDrawGizmos() { Gizmos.color = Color.red; Gizmos.DrawWireCube(transform.position, new Vector3(Size.x * transform.lossyScale.x, .2f, Size.y * transform.lossyScale.z)); }
#endif
    }
}
