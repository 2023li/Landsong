using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class EntityState
    {
        public static bool Alive(EntityManager em, Entity e) => e != Entity.Null && em.Exists(e) && em.HasComponent<Health>(e) && em.GetComponentData<Health>(e).Current > 0;
        public static void Set<T>(EntityManager em, Entity entity, T value)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                em.SetComponentData(entity, value);
            else
                em.AddComponentData(entity, value);
        }

        public static void Buffer<T>(EntityManager em, Entity entity)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(entity))
                em.AddBuffer<T>(entity);
        }

        public static float3 Position(EntityManager em, Entity e) => em.GetComponentData<LocalTransform>(e).Position;
    }
}
