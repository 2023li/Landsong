using Unity.Entities;

namespace Landsong.ECS
{
    // Runtime instances are not automatically owned by the SubScene that supplied their prefab.
    // An explicit owner prevents a second map/session from inheriting the previous dynasty.
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SimulationLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state) => Cleanup(state.EntityManager);

        public static void Cleanup(EntityManager em)
        {
            using var entities = Sim.Entities<SimulationOwner>(em);
            foreach (var e in entities)
            {
                var owner = em.GetComponentData<SimulationOwner>(e).Root;
                if (owner == Entity.Null || !em.Exists(owner) || !em.HasComponent<Session>(owner)) em.DestroyEntity(e);
            }
        }
    }
}
