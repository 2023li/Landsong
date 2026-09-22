using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingLevelConfiguration
    {
        public static void Apply(EntityManager em, Entity root, Entity e, bool initialize)
        {
            var state = em.GetComponentData<Building>(e);
            BuildingWorkforceState stateWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingHousingState stateHousing = em.GetComponentData<BuildingHousingState>(e);
            BuildingGatheringState stateGathering = em.GetComponentData<BuildingGatheringState>(e);
            ref var source = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition);
            ref var capabilities = ref source.Capabilities;
            var stats = new BuildingHousingStats
            {
            };
            BuildingWorkforceStats statsWorkforce = new BuildingWorkforceStats()
            {
            };
            BuildingStorageStats statsStorage = new BuildingStorageStats()
            {
            };
            BuildingGarrisonStats statsGarrison = new BuildingGarrisonStats()
            {
            };
            BuildingQuestStats statsQuests = new BuildingQuestStats()
            {
            };
            BuildingIntelligenceStats statsIntelligence = new BuildingIntelligenceStats()
            {
            };
            BuildingSanctumStats statsSanctum = new BuildingSanctumStats()
            {
                Hero = HeroId.None
            };
            BuildingBellStats statsBell = new BuildingBellStats()
            {
            };
            bool Matches(int level) => level == 0 || level == state.Level;
            for (int i = 0; i < capabilities.Workforce.Levels.Length; i++)
            {
                var row = capabilities.Workforce.Levels[i];
                if (!Matches(row.Level))
                    continue;
                statsWorkforce.Capacity = row.Capacity;
                if (initialize)
                {
                    stateWorkforce.Workers = state.Stage == LifeStage.Operational ? math.min(row.InitialWorkers, math.max(0, PopulationOps.Population(em, root) - PopulationOps.Employed(em))) : 0;
                    stateWorkforce.Subsidy = (byte)(row.InitialSubsidy ? 1 : 0);
                    stateWorkforce.SubsidyBudget = row.InitialSubsidy ? WorkforceOps.SubsidyCost(row.Capacity, row.BaseAttraction, row.Capacity) : 0;
                    stateWorkforce.StableWorkers = row.InitialWorkers;
                }
            }

            for (int i = 0; i < capabilities.Housing.Population.Length; i++)
            {
                var row = capabilities.Housing.Population[i];
                if (!Matches(row.Level))
                    continue;
                stats.BasePopulation += row.Population;
                stats.IsCore = (byte)(row.IsCore ? 1 : 0);
            }

            for (int i = 0; i < capabilities.Housing.Residences.Length; i++)
            {
                var row = capabilities.Housing.Residences[i];
                if (!Matches(row.Level))
                    continue;
                stats.MaxPopulation = row.Capacity;
                if (initialize && state.Stage == LifeStage.Operational)
                    stateHousing.Population = math.min(row.Capacity, math.max(0, row.InitialResidents));
            }

            for (int i = 0; i < capabilities.Storage.Warehouses.Length; i++)
                if (Matches(capabilities.Storage.Warehouses[i].Level))
                    statsStorage.Capacity += capabilities.Storage.Warehouses[i].Slots;
            for (int i = 0; i < capabilities.Storage.Providers.Length; i++)
                if (Matches(capabilities.Storage.Providers[i].Level))
                    statsStorage.IsProvider = 1;
            for (int i = 0; i < capabilities.Garrison.Levels.Length; i++)
            {
                var row = capabilities.Garrison.Levels[i];
                if (!Matches(row.Level))
                    continue;
                statsGarrison.Capacity = row.Capacity;
                statsGarrison.BatchSize = math.max(1, row.DeploymentBatchSize);
            }

            for (int i = 0; i < capabilities.Defence.Bells.Length; i++)
                if (Matches(capabilities.Defence.Bells[i].Level))
                    statsBell.Radius = capabilities.Defence.Bells[i].Radius;
            for (int i = 0; i < capabilities.Defence.Intelligence.Length; i++)
                if (Matches(capabilities.Defence.Intelligence[i].Level))
                    statsIntelligence.Points += capabilities.Defence.Intelligence[i].Points;
            for (int i = 0; i < capabilities.Sanctum.Levels.Length; i++)
            {
                var row = capabilities.Sanctum.Levels[i];
                if (!Matches(row.Level))
                    continue;
                statsSanctum.Hero = row.Hero;
                statsSanctum.RequiredWorkers = row.RequiredWorkers;
            }

            for (int i = 0; i < capabilities.Quests.Capacity.Length; i++)
                if (Matches(capabilities.Quests.Capacity[i].Level))
                    statsQuests.Capacity += capabilities.Quests.Capacity[i].Slots;
            for (int i = 0; i < capabilities.Gathering.Levels.Length; i++)
                if (initialize && Matches(capabilities.Gathering.Levels[i].Level))
                    stateGathering.RemainingUses = capabilities.Gathering.Levels[i].Uses;
            var slots = em.GetBuffer<QuestOfferSlot>(e);
            for (int i = 0; i < capabilities.Quests.Invitations.Length; i++)
            {
                var row = capabilities.Quests.Invitations[i];
                if (!Matches(row.Level))
                    continue;
                for (int index = 0; index < row.Slots; index++)
                {
                    bool found = false;
                    foreach (var old in slots)
                        if (old.Type == (int)row.Type && old.Index == index)
                        {
                            found = true;
                            break;
                        }

                    if (!found)
                        slots.Add(new QuestOfferSlot { Type = (int)row.Type, Index = index, NextTurn = 0 });
                }
            }

            stateWorkforce.SubsidyBudget = math.clamp(stateWorkforce.SubsidyBudget, 0, statsWorkforce.Capacity);
            stateWorkforce.Workers = math.min(stateWorkforce.Workers, statsWorkforce.Capacity);
            stateHousing.Population = math.min(stateHousing.Population, stats.MaxPopulation);
            {
                em.SetComponentData(e, state);
                em.SetComponentData(e, stateWorkforce);
                em.SetComponentData(e, stateHousing);
                em.SetComponentData(e, stateGathering);
            }

            {
                em.SetComponentData(e, stats);
                em.SetComponentData(e, statsWorkforce);
                em.SetComponentData(e, statsStorage);
                em.SetComponentData(e, statsGarrison);
                em.SetComponentData(e, statsQuests);
                em.SetComponentData(e, statsIntelligence);
                em.SetComponentData(e, statsSanctum);
                em.SetComponentData(e, statsBell);
            }

            em.SetComponentData(e, new BuildingRangeStats { ActionPower = source.ResourceConnectionActionPower });
            em.SetComponentData(e, new BuildingNavigationStats { MovementCost = source.MovementCost });
            if (!initialize)
                QuestOfferOps.Synchronize(em, root, e);
            if (state.Stage == LifeStage.Operational)
                InventoryProviders.Provision(em, root, e);
        }
    }
}
