using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct ItemPickupEvent : IBufferElementData
    {
        public ItemId Item;
        public int Quantity;
        public float3 Position;
    }
}
