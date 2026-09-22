using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class WorldQueries
    {
        public static Entity Root(EntityManager em)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<Session>());
            return q.CalculateEntityCount() == 1 ? q.GetSingletonEntity() : Entity.Null;
        }

        public static NativeArray<Entity> Entities<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return q.ToEntityArray(Allocator.Temp);
        }

        public static Entity Find(EntityManager em, ulong id)
        {
            if (id == 0)
                return Entity.Null;
            using var all = WorldQueries.Entities<Identity>(em);
            foreach (var e in all)
                if (em.GetComponentData<Identity>(e).Id == id)
                    return e;
            return Entity.Null;
        }

        public static NativeArray<Entity> OrderedEntities<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            var all = WorldQueries.Entities<T>(em);
            all.Sort(new StableIdentityComparer { Manager = em });
            return all;
        }

        struct StableIdentityComparer : System.Collections.Generic.IComparer<Entity>
        {
            public EntityManager Manager;
            public int Compare(Entity a, Entity b) => Manager.GetComponentData<Identity>(a).Id.CompareTo(Manager.GetComponentData<Identity>(b).Id);
        }
    }
}
