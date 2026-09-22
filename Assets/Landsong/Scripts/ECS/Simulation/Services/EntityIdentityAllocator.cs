using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class EntityIdentityAllocator
    {
        public static ulong AllocateId(EntityManager em, Entity root)
        {
            IdentitySequence sIds = em.GetComponentData<IdentitySequence>(root);
            var id = ++sIds.NextId;
            {
                em.SetComponentData(root, sIds);
            }

            return id;
        }
    }
}
