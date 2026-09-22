using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct ResearchCompletedEvent : IBufferElementData
    {
        public TechnologyId Technology;
        public FixedString128Bytes Name;
    }
}
