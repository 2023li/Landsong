using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class MilitaryStrength
    {
        public static int Calculate(EntityManager em, Entity root)
        {
            long value = 0;
            using var soldiers = WorldQueries.OrderedEntities<Soldier>(em);
            using var capacity = new NativeHashMap<ulong, int>(math.max(1, soldiers.Length), Allocator.Temp);
            var usedSlots = capacity;
            foreach (var e in soldiers)
            {
                var s = em.GetComponentData<Soldier>(e);
                var site = WorldQueries.Find(em, s.Garrison);
                if (!EntityState.Alive(em, e) || !BuildingStatus.Operational(em, site))
                    continue;
                if (!capacity.TryGetValue(s.Garrison, out var used))
                    used = 0;
                if (used >= em.GetComponentData<BuildingGarrisonStats>(site).Capacity)
                    continue;
                usedSlots[s.Garrison] = used + 1;
                var d = em.GetComponentData<SoldierDefinitionRef>(e).Definition;
                value += math.max(0, SoldierDefinitions.Get(em, root, d).NightPower);
            }

            using var heroes = WorldQueries.OrderedEntities<Hero>(em);
            foreach (var e in heroes)
            {
                var hero = em.GetComponentData<Hero>(e);
                var site = WorldQueries.Find(em, hero.Sanctum);
                if (hero.Recruited == 0 || hero.DeathPending != 0 || !EntityState.Alive(em, e) || !BuildingStatus.Operational(em, site) || em.GetComponentData<BuildingWorkforceState>(site).Workers < em.GetComponentData<BuildingSanctumStats>(site).RequiredWorkers)
                    continue;
                // Potentially awakenable, independent of offering toggle: turning supply off must not hide a titan.
                var d = em.GetComponentData<HeroDefinitionRef>(e).Definition;
                value += math.max(0, HeroDefinitions.Get(em, root, d).NightPower);
            }

            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var e in buildings)
                if (BuildingStatus.Operational(em, e))
                {
                    ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition);
                    if (definition.Faction == BuildingFaction.Settlement)
                        value += math.max(0, definition.NightPower);
                }

            return (int)Math.Min(int.MaxValue, value);
        }
    }
}
