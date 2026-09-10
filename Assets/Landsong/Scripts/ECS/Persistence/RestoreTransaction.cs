using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS.Persistence
{
    // Synchronous, main-thread staging only. No system/UI update may run inside this scope.
    // Original entities (including combat transients and their Entity references) remain intact
    // until preparation succeeds. Disabled is added only to simulation roots, never visual children.
    public sealed class RestoreTransaction : IDisposable
    {
        readonly EntityManager em;
        readonly Entity original;
        readonly HashSet<Entity> before = new HashSet<Entity>();
        readonly List<Entity> hidden = new List<Entity>();
        readonly List<Entity> retired = new List<Entity>();
        readonly RootState originalState;
        bool committed, disposed;
        public Entity Root { get; private set; }

        public RestoreTransaction(EntityManager manager, Entity root)
        {
            em = manager; original = root; em.CompleteAllTrackedJobs();
            if (Sim.Root(em) != root || em.HasBuffer<LinkedEntityGroup>(root))
                throw new InvalidOperationException("恢复需要一个独立的 ECS 会话根实体。");
            originalState = new RootState(em, root);
            using (var all = All(em)) foreach (var e in all) before.Add(e);
            try
            {
                Root = em.Instantiate(root);
                foreach (var e in before)
                {
                    if (em.HasComponent<Prefab>(e) || (!em.HasComponent<Persistent>(e) && !em.HasComponent<NightTransient>(e))) continue;
                    if (!em.HasComponent<SimulationOwner>(e) || em.GetComponentData<SimulationOwner>(e).Root != root)
                        throw new InvalidOperationException("发现不属于当前会话的运行实体，已取消恢复。");
                    retired.Add(e); Hide(e);
                }
                foreach (var e in retired) if (em.HasBuffer<LinkedEntityGroup>(e)) foreach (var child in em.GetBuffer<LinkedEntityGroup>(e))
                    if (!before.Contains(child.Value) || child.Value == original || em.HasComponent<Prefab>(child.Value))
                        throw new InvalidOperationException("运行实体层级引用无效，已取消恢复。");
                Hide(root);
            }
            catch { Dispose(); throw; }
        }
        void Hide(Entity e)
        {
            if (em.HasComponent<Disabled>(e)) return;
            hidden.Add(e); em.AddComponent<Disabled>(e);
        }
        static NativeArray<Entity> All(EntityManager manager)
        {
            using var query = manager.CreateEntityQuery(new EntityQueryDesc
            { Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab });
            return query.ToEntityArray(Allocator.Temp);
        }
        public void Commit(Action<string> probe = null)
        {
            if (committed || disposed) throw new InvalidOperationException("恢复事务已结束。");
            var prepared = new RootState(em, Root);
            using var toRetire = new NativeArray<Entity>(retired.ToArray(), Allocator.Temp);
            // Copy can allocate buffer capacity; rollback still owns every original entity here.
            prepared.Apply(em, original);
            probe?.Invoke("root-published");
            using (var owners = Sim.Entities<SimulationOwner>(em)) foreach (var e in owners)
                if (em.GetComponentData<SimulationOwner>(e).Root == Root) em.SetComponentData(e, new SimulationOwner { Root = original });
            probe?.Invoke("before-retire");
            // Last irreversible operation: no gameplay callbacks, IO, or fault hooks after this point.
            em.DestroyEntity(toRetire);
            committed = true;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (!committed)
            {
                using var all = All(em);
                foreach (var e in all) if (!before.Contains(e) && em.Exists(e)) em.DestroyEntity(e);
                originalState.Apply(em, original);
            }
            else if (Root != Entity.Null && em.Exists(Root)) em.DestroyEntity(Root);
            foreach (var e in hidden) if (em.Exists(e)) em.RemoveComponent<Disabled>(e);
        }

        // Explicit mutable root schema. Static catalog/map/prefab buffers remain on the baked root.
        // Optional component absence is restored as well as its value; no phantom journals on cancel.
        sealed class RootState
        {
            readonly List<Action<EntityManager, Entity>> apply = new List<Action<EntityManager, Entity>>();
            public RootState(EntityManager manager, Entity root)
            {
                Component<Session>(manager, root); Component<GridData>(manager, root);
                Component<RunPersistence>(manager, root); Component<RecoveryState>(manager, root);
                Component<NightResultState>(manager, root); Component<NightEntryReview>(manager, root);
                Component<PeacefulState>(manager, root); Buffer<OpportunityCount>(manager, root);
                Buffer<BattleHistoryEntry>(manager, root);
                Buffer<HistoryEntry>(manager,root);Component<ManualHistoryContext>(manager,root);
                Component<EconomyJournalState>(manager, root); Component<EconomyForecastState>(manager, root);
                Component<QuestTracking>(manager, root);
                Component<CourtState>(manager, root); Buffer<CourtLogEntry>(manager, root);
                Component<NightPlanState>(manager, root); Buffer<NightEventHistory>(manager, root); Buffer<UnresolvedBoss>(manager, root); Buffer<NightPreparation>(manager, root);
                Component<IntelProjection>(manager, root); Buffer<IntelGeometry>(manager, root);
                Component<QuestCapacityReview>(manager, root);
                Buffer<EconomyEntry>(manager, root); Buffer<EconomyForecastEntry>(manager, root);
                Buffer<Occupancy>(manager, root); Buffer<InventorySlot>(manager, root); Buffer<PendingItem>(manager, root);
                Buffer<Entitlement>(manager, root); Buffer<ResearchEntry>(manager, root); Buffer<PolicyChoice>(manager, root);
                Buffer<NightWave>(manager, root); Buffer<BattleReportEntry>(manager, root); Buffer<NightReward>(manager, root);
                Buffer<Command>(manager, root); Buffer<GameEvent>(manager, root); Buffer<DamageRequest>(manager, root);
                Buffer<NightEntryLoss>(manager, root);
            }
            void Component<T>(EntityManager manager, Entity root) where T : unmanaged, IComponentData
            {
                var exists = manager.HasComponent<T>(root); var value = exists ? manager.GetComponentData<T>(root) : default;
                apply.Add((m, e) => { if (exists) Sim.Set(m, e, value); else if (m.HasComponent<T>(e)) m.RemoveComponent<T>(e); });
            }
            void Buffer<T>(EntityManager manager, Entity root) where T : unmanaged, IBufferElementData
            {
                var exists = manager.HasBuffer<T>(root); T[] values = null;
                if (exists) { using var array = manager.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); values = array.ToArray(); }
                apply.Add((m, e) => { if (exists) { Sim.Buffer<T>(m, e); m.GetBuffer<T>(e).CopyFrom(values); } else if (m.HasBuffer<T>(e)) m.RemoveComponent<T>(e); });
            }
            public void Apply(EntityManager manager, Entity root) { foreach (var action in apply) action(manager, root); }
        }
    }
}
