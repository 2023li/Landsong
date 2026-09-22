using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class SimulationRandom
    {
        public static uint NextRandom(EntityManager em, Entity root)
        {
            SimulationRandomState sRandom = em.GetComponentData<SimulationRandomState>(root);
            var r = new Unity.Mathematics.Random(math.max(1u, sRandom.State));
            var result = r.NextUInt();
            sRandom.State = r.state;
            {
                em.SetComponentData(root, sRandom);
            }

            return result;
        }
    }
}
