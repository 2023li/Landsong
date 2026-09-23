using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Updated by the active game camera. UI panels do not clip this viewport.
    public struct LightningViewport : IComponentData
    {
        public float4x4 ViewProjection;
        public float3 CameraPosition;
        public float3 Forward;
        public float Near;
        public float Far;
        public byte Available;
    }

    public enum LightningVisualKind : byte { Flash, Ground, Building, Unit }

    [InternalBufferCapacity(0)]
    public struct LightningVisualEvent : IBufferElementData
    {
        public LightningVisualKind Kind;
        public float3 Position;
        public ulong Target;
    }
}
