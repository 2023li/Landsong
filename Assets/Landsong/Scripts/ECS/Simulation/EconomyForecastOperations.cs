using System;
using System.Security.Cryptography;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class EconomyForecastOps
    {
        public static void Clear(EntityManager em, Entity root)
        {
            if (em.HasComponent<EconomyForecastState>(root)) em.SetComponentData(root, new EconomyForecastState());
            if (em.HasBuffer<EconomyForecastEntry>(root)) em.GetBuffer<EconomyForecastEntry>(root).Clear();
        }
        public static string Fingerprint(EntityManager em, Entity root)
        { using var hash = SHA256.Create(); return Convert.ToBase64String(hash.ComputeHash(SnapshotCodec.Capture(em, root))); }
        public static ResultCode Create(EntityManager em, Entity root, Action<string> probe = null)
        {
            var state = em.GetComponentData<Session>(root);
            if (state.Phase != Phase.Day) return ResultCode.WrongPhase;
            if (state.LastSettledTurn == state.Turn) return ResultCode.Unavailable;
            var bytes = SnapshotCodec.Capture(em, root);
            string fingerprint; using (var hash = SHA256.Create()) fingerprint = Convert.ToBase64String(hash.ComputeHash(bytes));
            EconomyEntry[] entries;
            using (var transaction = new RestoreTransaction(em, root))
            {
                var candidate = transaction.Root;
                SnapshotCodec.Rebuild(em, candidate, SnapshotCodec.Decode(em, candidate, bytes));
                var s = em.GetComponentData<Session>(candidate); s.Phase = Phase.Settlement; em.SetComponentData(candidate, s);
                EconomyOps.Settle(em, candidate, true); probe?.Invoke("forecast-settled");
                using var rows = em.GetBuffer<EconomyEntry>(candidate).ToNativeArray(Allocator.Temp); entries = rows.ToArray();
            }
            // Read-model only; the real journal, RNG and entire day have been restored.
            Sim.Buffer<EconomyForecastEntry>(em, root); em.GetBuffer<EconomyForecastEntry>(root).Clear();
            foreach (var entry in entries) em.GetBuffer<EconomyForecastEntry>(root).Add(new EconomyForecastEntry { Value = entry });
            Sim.Set(em, root, new EconomyForecastState { Turn = state.Turn, Fingerprint = fingerprint });
            return ResultCode.Success;
        }
    }
}
