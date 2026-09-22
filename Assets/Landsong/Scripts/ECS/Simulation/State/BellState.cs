using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct BellState : IComponentData
    {
        public ulong ActiveBell;
    }
}
