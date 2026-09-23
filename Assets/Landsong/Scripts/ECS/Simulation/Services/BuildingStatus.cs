using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingStatus
    {
        public static bool Operational(EntityManager em, Entity e) => e != Entity.Null && em.Exists(e) && em.HasComponent<Building>(e)
            && em.GetComponentData<Building>(e).Stage == LifeStage.Operational
            && (!em.HasComponent<BuildingFireState>(e) || em.GetComponentData<BuildingFireState>(e).Burning == 0);
    }
}
