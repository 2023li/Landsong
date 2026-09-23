using System;
using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class PopulationOps
    {
        public static void RemovePopulation(EntityManager em, Entity root, int amount)
        {
            amount = math.clamp(amount, 0, Population(em, root));
            using var all = WorldQueries.OrderedEntities<Building>(em);
            foreach (var e in all)
            {
                if (amount <= 0)
                    break;
                var burning = em.HasComponent<BuildingFireState>(e) && em.GetComponentData<BuildingFireState>(e).Burning != 0;
                if (!BuildingStatus.Operational(em, e) && !burning && em.GetComponentData<Building>(e).RuinPending == 0)
                    continue;
                BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
                var remove = math.min(amount, bHousing.Population);
                bHousing.Population -= remove;
                amount -= remove;
                {
                    em.SetComponentData(e, bHousing);
                }
            }

            if (amount > 0)
            {
                PopulationState sPopulation = em.GetComponentData<PopulationState>(root);
                // The signed base also offsets fixed population supplied by buildings.
                // Clamping this to zero made citizens supplied by the palace immune to losses.
                sPopulation.BasePopulation -= amount;
                {
                    em.SetComponentData(root, sPopulation);
                }
            }
        }

        public static int Population(EntityManager em, Entity root)
        {
            var count = em.GetComponentData<PopulationState>(root).BasePopulation;
            using var all = WorldQueries.Entities<Building>(em);
            foreach (var e in all)
            {
                var b = em.GetComponentData<Building>(e);
                BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
                var burning = em.HasComponent<BuildingFireState>(e) && em.GetComponentData<BuildingFireState>(e).Burning != 0;
                if (BuildingStatus.Operational(em, e) || burning || b.RuinPending != 0)
                    count += bHousing.Population + em.GetComponentData<BuildingHousingStats>(e).BasePopulation;
            }

            return math.max(0, count);
        }

        public static int Employed(EntityManager em)
        {
            var count = 0;
            using var buildings = WorldQueries.Entities<Building>(em);
            foreach (var e in buildings)
                count += em.GetComponentData<BuildingWorkforceState>(e).Workers;
            using var soldiers = WorldQueries.Entities<Soldier>(em);
            foreach (var e in soldiers)
                count += em.GetComponentData<Soldier>(e).PopulationCost;
            using var heroes = WorldQueries.Entities<Hero>(em);
            var root = WorldQueries.Root(em);
            foreach (var e in heroes)
            {
                var hero = em.GetComponentData<Hero>(e);
                if (hero.Recruited != 0 || hero.DeathPending != 0)
                    count += HeroDefinitions.Get(em, root, em.GetComponentData<HeroDefinitionRef>(e).Definition).PopulationCost;
            }

            return count;
        }
    }
}
