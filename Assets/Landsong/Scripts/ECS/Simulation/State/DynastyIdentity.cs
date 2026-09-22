using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct DynastyIdentity : IComponentData
    {
        public FixedString128Bytes Name;
    }
}
