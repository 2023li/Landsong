using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(Persistence.CheckpointSystem))]
    public partial class IntelligenceSystem : SystemBase
    {
        double next;
        protected override void OnCreate() => RequireForUpdate<SimulationReady>();
        protected override void OnUpdate()
        {
            if (SystemAPI.Time.ElapsedTime < next) return; next = SystemAPI.Time.ElapsedTime + .25;
            var root = Sim.Root(EntityManager); if (root == Entity.Null) return;
            var s = EntityManager.GetComponentData<Session>(root);
            var grid = EntityManager.GetComponentData<GridData>(root);
            var projection = EntityManager.HasComponent<IntelProjection>(root) ? EntityManager.GetComponentData<IntelProjection>(root) : default;
            if (s.Phase == Phase.Day && (projection.Revision != grid.Revision || projection.Seed != s.NightSeed))
            {
                NightPlanOps.Reproject(EntityManager, root);
                Sim.Set(EntityManager, root, new IntelProjection { Revision = grid.Revision, Seed = s.NightSeed });
            }
            IntelOps.Refresh(EntityManager, root);
        }
    }
    public struct IntelProjection : IComponentData { public int Revision; public uint Seed; }
    [InternalBufferCapacity(0)] public struct IntelGeometry : IBufferElementData { public int Region, Count, Revision, Tier; public float3 Position; public IntelArea Area; }
}
