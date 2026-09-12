using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Landsong.ECS
{
    // ConfigureCombatant changes transient navigation/order data as well as health. A failed
    // recruit/wake must restore all of it, including absence of optional components/buffers.
    sealed class HeroMutationTransaction : IDisposable
    {
        readonly EntityManager manager;
        readonly Entity entity;
        readonly List<Action> rollback = new List<Action>();
        bool committed;
        public HeroMutationTransaction(EntityManager em, Entity hero)
        {
            manager = em; entity = hero;
            Capture<Hero>(); Capture<Combatant>(); Capture<Health>(); Capture<LocalTransform>();
            Capture<UnitOrder>(); Capture<Steering>(); Capture<HeroCombat>(); Capture<TacticalState>();
            Capture<NavigationState>(); Capture<Perception>(); Capture<VisualState>();
            bool exists = em.HasBuffer<Waypoint>(hero); Waypoint[] points = null;
            if (exists) { using var array = em.GetBuffer<Waypoint>(hero).ToNativeArray(Allocator.Temp); points = array.ToArray(); }
            rollback.Add(() => { if (exists) { Sim.Buffer<Waypoint>(manager, entity); manager.GetBuffer<Waypoint>(entity).CopyFrom(points); } else if (manager.HasBuffer<Waypoint>(entity)) manager.RemoveComponent<Waypoint>(entity); });
        }
        void Capture<T>() where T : unmanaged, IComponentData
        {
            bool exists = manager.HasComponent<T>(entity); var value = exists ? manager.GetComponentData<T>(entity) : default;
            rollback.Add(() => { if (exists) Sim.Set(manager, entity, value); else if (manager.HasComponent<T>(entity)) manager.RemoveComponent<T>(entity); });
        }
        public void Commit() => committed = true;
        public void Dispose() { if (!committed) foreach (var restore in rollback) restore(); }
    }
}
