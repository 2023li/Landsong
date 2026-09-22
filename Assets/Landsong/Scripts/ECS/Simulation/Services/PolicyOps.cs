using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class PolicyOps
    {
        public static ResultCode Policy(EntityManager em, Entity root, PolicyId definition)
        {
            if (!PolicyDefinitions.IsValid(em, root, definition))
                return ResultCode.InvalidContent;
            ref var d = ref PolicyDefinitions.Get(em, root, definition);
            if (!PrerequisiteEvaluation.Satisfied(em, root, ref d.Prerequisites))
                return ResultCode.Unavailable;
            if (em.GetComponentData<PublicOpinionState>(root).Value < d.RequiredPublicOpinion)
                return ResultCode.Unavailable;
            var policies = em.GetBuffer<PolicyChoice>(root);
            for (var i = 0; i < policies.Length; i++)
            {
                ref var old = ref PolicyDefinitions.Get(em, root, policies[i].Definition);
                if (old.PolicyGroup == d.PolicyGroup && old.PolicyTier == d.PolicyTier)
                {
                    policies[i] = new PolicyChoice
                    {
                        Definition = definition
                    };
                    return ResultCode.Success;
                }
            }

            policies.Add(new PolicyChoice { Definition = definition });
            return ResultCode.Success;
        }
    }
}
