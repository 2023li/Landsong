using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class SimulationEvents
    {
        public static void Emit(EntityManager em, Entity root, EventKind kind, FixedString128Bytes message, ulong target = 0, int amount = 0, HistoryCategory? category = null)
        {
            var historyCategory = category ?? HistoryOps.DefaultCategory(kind);
            em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = kind, Category = historyCategory, Message = message, Target = target, Amount = amount });
            HistoryOps.Message(em, root, kind, message, target, historyCategory);
        }
    }
}
