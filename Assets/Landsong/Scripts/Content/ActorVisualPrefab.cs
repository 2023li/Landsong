using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.Content
{
    public struct ActorVisualPrefab : IComponentData
    {
        public UnityObjectRef<MonoBehaviour> Prefab;
        public float3 Offset;
        public float3 Scale;
    }
}
