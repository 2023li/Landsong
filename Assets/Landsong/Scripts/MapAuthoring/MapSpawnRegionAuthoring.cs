using Landsong.ECS.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    [AddComponentMenu("Landsong/Map/出生区域")]
    public sealed class MapSpawnRegionAuthoring : MonoBehaviour
    {
        [LabelText("来袭方向")] public int Direction;
        [LabelText("区域尺寸")] public Vector3 Size = new Vector3(5, 1, 5);
        public RegionSource ToSource() => new RegionSource { Direction = Direction, Center = transform.position, Size = Vector3.Scale(Size, transform.lossyScale) };
#if UNITY_EDITOR
        void OnDrawGizmos() { Gizmos.color = Color.yellow; Gizmos.DrawWireCube(transform.position, ToSource().Size); }
#endif
    }
}
