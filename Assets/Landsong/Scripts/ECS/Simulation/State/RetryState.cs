using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct RetryState : IComponentData
    {
        public int Count;
    }
}
