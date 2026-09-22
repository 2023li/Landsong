using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct IdentitySequence : IComponentData
    {
        public ulong NextId;
    }
}
