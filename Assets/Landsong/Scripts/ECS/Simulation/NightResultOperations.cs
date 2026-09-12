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
            if (source == 0 || kind == RuleKind.RewardItem && amount <= 0) return;
            RewardOps.Validate(em, root, new RewardGrant(kind, definition, amount, source, sourceName));
            if (!em.HasComponent<NightResultState>(root)) Reset(em, root);
            if (em.GetComponentData<NightResultState>(root).Committed != 0 || source == 0 || amount <= 0) return;
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
                if (r.Kind >= RuleKind.RewardItem && r.Kind <= RuleKind.RewardFeature)
                    RewardOps.Validate(em, root, new RewardGrant(r.Kind, r.Target, r.Amount));
            }
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
            var rewards = new System.Collections.Generic.List<RewardGrant>(journal.Length);
            foreach (var reward in journal) rewards.Add(new RewardGrant(reward.Kind, reward.Definition, reward.Amount, reward.Source, reward.SourceName));
            RewardOps.ApplyBatch(em, root, rewards, true,
                () => { state.Committed = 1; em.SetComponentData(root, state); }, reportOverflow: true);
        }
    }
}
