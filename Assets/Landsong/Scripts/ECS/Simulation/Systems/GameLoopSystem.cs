using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct GameLoopSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<Session>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = WorldQueries.Root(em);
            if (root == Entity.Null)
                return;
            if (em.GetComponentData<Session>(root).Initialized == 0)
                WorldInitialization.Initialize(em, root);
            else if (em.GetComponentData<SeasonWeatherState>(root).Initialized == 0)
                SeasonWeatherOps.Initialize(em, root);
            GameplayRequestProcessing.Drain(em, root);
            NightOps.Tick(em, root, SystemAPI.Time.DeltaTime);
            LightningOps.Tick(em, root, SystemAPI.Time.DeltaTime);
            HistoryOps.Trim(em, root);
        }
    }
}
