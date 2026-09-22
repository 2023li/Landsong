using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingChangeNotifications
    {
        public static void Publish(EntityManager em, Entity root)
        {
            // Only targets are projected again; quantities, types, timing, seeds and threat stay locked.
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase != Phase.Day && phase != Phase.Settlement)
                return;
            NightPlanOps.Reproject(em, root);
            QuestLifecycle.EvaluateQuests(em, root);
        }
    }
}
