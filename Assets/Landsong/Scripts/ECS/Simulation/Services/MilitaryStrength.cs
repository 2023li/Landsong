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
            float value = 0;
            var rules = NightPlanOps.Rules(em, root);
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
                var stats = SoldierCombatStats.Current(em, root, d);
                UnitProgression.ApplyGrowth(ref stats, SoldierDefinitions.Get(em, root, d).Growth, s.Experience);
                SoldierCombatStats.ApplyWeapon(em, root, ref stats, s.Weapon);
                value += stats.Health * .05f / (1 - stats.Combat.Reduction) + stats.Combat.Armor + stats.Damage / math.max(.1f, stats.Interval);
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
                var stats = HeroCombatStats.Current(em, root, d);
                UnitProgression.ApplyGrowth(ref stats, HeroDefinitions.Get(em, root, d).Growth.Progression, hero.Experience);
                value += (stats.Health * .05f / (1 - stats.Combat.Reduction) + stats.Combat.Armor + stats.Damage / math.max(.1f, stats.Interval)) * rules.HeroWeight;
            }

            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var e in buildings)
                if (BuildingStatus.Operational(em, e))
                {
                    BuildingGarrisonStats statsGarrison = em.GetComponentData<BuildingGarrisonStats>(e);
                    BuildingBellStats statsBell = em.GetComponentData<BuildingBellStats>(e);
                    if (statsGarrison.Capacity > 0 || statsBell.Radius > 0)
                        value += math.max(1, statsGarrison.Capacity) * rules.FacilityWeight;
                }

            return (int)value;
        }
    }
}
