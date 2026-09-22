using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class GameplayRequestProcessing
    {
        public static void Drain(EntityManager em, Entity root)
        {
            using var requests = em.GetBuffer<QueuedGameplayRequest>(root).ToNativeArray(Allocator.Temp);
            em.GetBuffer<QueuedGameplayRequest>(root).Clear();
            try
            {
                foreach (var request in requests)
                {
                    var result = GameRequestExecution.Execute(em, root, request);
                    em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = EventKind.CommandResult, RequestId = request.RequestId, Result = result, Target = request.Target, Amount = (int)request.Kind });
                }
            }
            finally
            {
                foreach (var request in requests)
                    if (em.Exists(request.Payload))
                        em.DestroyEntity(request.Payload);
            }
        }
    }
}
