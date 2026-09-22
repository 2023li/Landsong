using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct Session : IComponentData
    {
        public Phase Phase;
        public byte Initialized;
    }
}
