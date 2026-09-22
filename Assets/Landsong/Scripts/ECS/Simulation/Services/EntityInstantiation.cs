using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    internal static class EntityInstantiation
    {
        internal static void Initialize(EntityManager em, Entity root, Entity entity, FixedString128Bytes name, float3 position, bool persistent)
        {
            EntityState.Set(em, entity, new Identity { Id = EntityIdentityAllocator.AllocateId(em, root), Name = name });
            EntityState.Set(em, entity, LocalTransform.FromPosition(position));
            EntityState.Set(em, entity, new SimulationOwner { Root = root });
            if (persistent)
                EntityState.Set(em, entity, new Persistent());
            else
                EntityState.Set(em, entity, new NightTransient());
        }
    }
}
