using System;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    // A claimed drop changes only this journal. Inventory and entitlements change at dawn.
    public static class NightResultOps
    {
        public static void Reset(EntityManager em, Entity root)
        {
            Sim.Buffer<NightReward>(em, root);
            em.GetBuffer<NightReward>(root).Clear();
            Sim.Set(em, root, new NightResultState { Turn = em.GetComponentData<Session>(root).Turn });
            PeacefulOps.Reset(em, root);
        }

        public static void Record(EntityManager em, Entity root, ulong source, int entry, RuleKind kind, int definition, int amount, FixedString128Bytes sourceName = default)
        {
            if (kind != RuleKind.RewardItem) amount = Unity.Mathematics.math.max(1, amount);
            if (!em.HasComponent<NightResultState>(root)) Reset(em, root);
            if (em.GetComponentData<NightResultState>(root).Committed != 0 || source == 0 || amount <= 0) return;
            if (!Sim.ValidDefinition(em, root, definition)) throw new InvalidOperationException("夜间奖励引用无效。");
            var journal = em.GetBuffer<NightReward>(root);
            foreach (var reward in journal) if (reward.Source == source && reward.Entry == entry) return;
            if (sourceName.IsEmpty) { var entity = Sim.Find(em, source); if (entity != Entity.Null) sourceName = em.GetComponentData<Identity>(entity).Name; }
            journal.Add(new NightReward { Source = source, Entry = entry, Kind = kind, Definition = definition, Amount = amount, Collected = 1, SourceName = sourceName });
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Reward, Id = source, Definition = definition, Amount = amount, SourceName = sourceName });
        }

        public static void RecordDefinition(EntityManager em, Entity root, ulong source, int definition)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind == RuleKind.RewardItem || r.Kind == RuleKind.RewardBlueprint || r.Kind == RuleKind.RewardBuff || r.Kind == RuleKind.RewardFeature)
                    Record(em, root, source, i, r.Kind, r.Target, r.Amount);
            }
        }

        public static void Commit(EntityManager em, Entity root)
        {
            if (!em.HasComponent<NightResultState>(root)) return;
            var state = em.GetComponentData<NightResultState>(root);
            if (state.Committed != 0 || state.Turn != em.GetComponentData<Session>(root).Turn) return;
            using var journal = em.GetBuffer<NightReward>(root).ToNativeArray(Allocator.Temp);
            // Roll back this small transaction if a content/runtime error occurs mid-commit.
            using var stock = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp);
            using var pending = em.GetBuffer<PendingItem>(root).ToNativeArray(Allocator.Temp);
            using var grants = em.GetBuffer<Entitlement>(root).ToNativeArray(Allocator.Temp);
            var reportCount = em.GetBuffer<BattleReportEntry>(root).Length;
            try
            {
                foreach (var reward in journal)
                {
                    if (reward.Kind == RuleKind.RewardItem)
                    {
                        var stored = InventoryOps.Add(em, root, reward.Definition, reward.Amount, true);
                        if (stored < reward.Amount) em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.RewardOverflow, Id = reward.Source, Definition = reward.Definition, Amount = reward.Amount - stored, SourceName = reward.SourceName });
                    }
                    else Sim.Grant(em, root, reward.Definition, Unity.Mathematics.math.max(1, reward.Amount));
                }
                state.Committed = 1; em.SetComponentData(root, state);
            }
            catch
            {
                em.GetBuffer<InventorySlot>(root).CopyFrom(stock); em.GetBuffer<PendingItem>(root).CopyFrom(pending); em.GetBuffer<Entitlement>(root).CopyFrom(grants);
                em.GetBuffer<BattleReportEntry>(root).ResizeUninitialized(reportCount); throw;
            }
        }
    }
}
