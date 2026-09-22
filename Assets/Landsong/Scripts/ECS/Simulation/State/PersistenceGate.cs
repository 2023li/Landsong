using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct PersistenceGate : IComponentData
    {
        public byte CheckpointPending;
    }
}
