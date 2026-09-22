using System;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class WorldInitialization
    {
        public static void Initialize(EntityManager em, Entity root)
        {
            var starting = em.GetComponentData<StartingRewards>(root).Value;
            RewardDelivery.Validate(em, root, ref starting.Value.Rewards);
            HistoryOps.Ensure(em, root);
            using (var initial = em.GetBuffer<InitialBuilding>(root).ToNativeArray(Allocator.Temp))
                foreach (var building in initial)
                {
                    if (!GridOps.CanPlace(em, root, building.Definition, building.Cell, building.Rotation))
                        throw new InvalidOperationException("Invalid initial building footprint: " + BuildingDefinitions.Get(em, root, building.Definition).Metadata.Id);
                    var entity = BuildingCreation.Create(em, root, building.Definition, building.Cell, building.Rotation, building.Level, true);
                    if (!building.Name.IsEmpty)
                    {
                        var identity = em.GetComponentData<Identity>(entity);
                        identity.Name = building.Name;
                        em.SetComponentData(entity, identity);
                    }
                }

            if (!RewardDelivery.Apply(em, root, ref starting.Value.Rewards))
                throw new InvalidOperationException("Starting resources exceed authored storage capacity.");
            using (var family = em.GetBuffer<InitialRoyal>(root).ToNativeArray(Allocator.Temp))
                foreach (var initial in family)
                {
                    var person = DynastyOps.CreateRoyal(em, root, initial.Name, initial.Role, initial.Age);
                    if (initial.Gender != PersonGender.Unspecified)
                    {
                        var state = em.GetComponentData<Royal>(person);
                        state.Gender = initial.Gender;
                        em.SetComponentData(person, state);
                    }

                    foreach (var trait in initial.Traits)
                        em.GetBuffer<TraitEntry>(person).Add(new TraitEntry { Definition = trait });
                }

            CourtOps.Initialize(em, root);
            SocialOps.EnsureContacts(em, root);
            GarrisonOps.InitializeGarrisons(em, root);
            PortraitOps.EnsurePeople(em, root);
            PortraitOps.Announce(em, root);
            var session = em.GetComponentData<Session>(root);
            session.Initialized = 1;
            em.SetComponentData(root, session);
            EntityState.Set(em, root, new SimulationReady());
            QuestLifecycle.DiscoverQuests(em, root);
            QuestLifecycle.EvaluateQuests(em, root);
            NightOps.Plan(em, root, false);
            SimulationEvents.Emit(em, root, EventKind.DayCheckpoint, "白天节点");
        }
    }
}
