using System;
using Unity.Entities;
using Unity.Collections;

namespace Landsong.ECS
{
    public interface IGameRequest : IComponentData
    {
        CommandKind Kind { get; }

        ulong Target { get; }
    }

    public interface IHistoryNamedRequest
    {
        FixedString128Bytes HistoryName(EntityManager em, Entity root);
    }

    // Only ordering and routing metadata are shared. Each request entity carries its own typed payload.
    [InternalBufferCapacity(0)]
    public struct QueuedGameplayRequest : IBufferElementData
    {
        public ulong RequestId;
        public CommandKind Kind;
        public ulong Target;
        public Entity Payload;
        public FixedString128Bytes SourceName;
    }

    public static class GameplayRequests
    {
        public static void Enqueue<T>(EntityManager em, Entity root, T request, ulong requestId = 0)
            where T : unmanaged, IGameRequest
        {
            if (!em.Exists(root) || !em.HasBuffer<QueuedGameplayRequest>(root))
                throw new InvalidOperationException("Gameplay request queue is not initialized.");
            var payload = em.CreateEntity();
            try
            {
                em.AddComponentData(payload, request);
                em.AddComponentData(payload, new SimulationOwner { Root = root });
                em.GetBuffer<QueuedGameplayRequest>(root).Add(new QueuedGameplayRequest { RequestId = requestId, Kind = request.Kind, Target = request.Target, Payload = payload, SourceName = request is IHistoryNamedRequest named ? named.HistoryName(em, root) : default });
            }
            catch
            {
                if (em.Exists(payload))
                    em.DestroyEntity(payload);
                throw;
            }
        }

        public static void Clear(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasBuffer<QueuedGameplayRequest>(root))
                return;
            // Destroying an entity invalidates buffer references, so copy the queue first.
            using var queue = em.GetBuffer<QueuedGameplayRequest>(root).ToNativeArray(Unity.Collections.Allocator.Temp);
            em.GetBuffer<QueuedGameplayRequest>(root).Clear();
            foreach (var request in queue)
                if (em.Exists(request.Payload))
                    em.DestroyEntity(request.Payload);
        }
    }
}
