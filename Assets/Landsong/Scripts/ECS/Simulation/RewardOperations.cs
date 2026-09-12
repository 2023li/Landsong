using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public readonly struct RewardGrant
    {
        public readonly RuleKind Kind;
        public readonly int Definition, Amount;
        public readonly ulong Source;
        public readonly FixedString128Bytes SourceName;
        public RewardGrant(RuleKind kind, int definition, int amount, ulong source = 0, FixedString128Bytes sourceName = default)
        { Kind = kind; Definition = definition; Amount = amount; Source = source; SourceName = sourceName; }
    }

    // A reward batch may mutate root facts, but never spawn/destroy entities or perform external I/O.
    internal sealed class RewardTransaction : IDisposable
    {
        readonly EntityManager em;
        readonly Entity root;
        readonly InventoryTransaction inventory;
        readonly Entitlement[] grants;
        readonly ResearchEntry[] research;
        readonly GameEvent[] events;
        readonly BattleReportEntry[] report;
        readonly HistoryEntry[] history;
        readonly Session session;
        readonly bool hadNightResult;
        readonly NightResultState nightResult;
        bool committed;

        internal RewardTransaction(EntityManager manager, Entity owner)
        {
            em = manager; root = owner;
            grants = Capture<Entitlement>(); research = Capture<ResearchEntry>();
            events = Capture<GameEvent>(); report = Capture<BattleReportEntry>(); history = Capture<HistoryEntry>();
            session = em.GetComponentData<Session>(root);
            hadNightResult = em.HasComponent<NightResultState>(root);
            nightResult = hadNightResult ? em.GetComponentData<NightResultState>(root) : default;
            inventory = new InventoryTransaction(em, root);
        }
        T[] Capture<T>() where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(root)) return null;
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }
        void Restore<T>(T[] rows) where T : unmanaged, IBufferElementData
        {
            if (rows == null) { if (em.HasBuffer<T>(root)) em.RemoveComponent<T>(root); return; }
            if (!em.HasBuffer<T>(root)) em.AddBuffer<T>(root);
            em.GetBuffer<T>(root).CopyFrom(rows);
        }
        internal void Commit() { inventory.Commit(); committed = true; }
        public void Dispose()
        {
            try
            {
                if (!committed)
                {
                    Restore(grants); Restore(research); Restore(events); Restore(report);
                    em.SetComponentData(root, session);
                    if (hadNightResult) em.SetComponentData(root, nightResult);
                    else if (em.HasComponent<NightResultState>(root)) em.RemoveComponent<NightResultState>(root);
                }
            }
            finally
            {
                try { inventory.Dispose(); }
                finally { if (!committed) Restore(history); } // Message coalescing can modify an existing row.
            }
        }
    }

    public static class RewardOps
    {
        public static void Validate(EntityManager em, Entity root, RewardGrant reward)
        {
            if (!Sim.ValidDefinition(em, root, reward.Definition)) throw new InvalidOperationException("奖励目标定义无效：" + reward.Definition);
            if (reward.Amount <= 0) throw new InvalidOperationException("奖励数量或许可等级必须大于零。");
            var kind = Sim.Definition(em, root, reward.Definition).Kind;
            switch (reward.Kind)
            {
                case RuleKind.RewardItem:
                    if (kind != ContentKind.Item) throw new InvalidOperationException("物品奖励目标不是物品。");
                    return;
                case RuleKind.RewardBlueprint: EntitlementStore.Validate(em, root, reward.Definition, reward.Amount, ContentKind.Building); return;
                case RuleKind.RewardBuff: EntitlementStore.Validate(em, root, reward.Definition, reward.Amount, ContentKind.Buff); return;
                case RuleKind.RewardFeature: EntitlementStore.Validate(em, root, reward.Definition, reward.Amount, ContentKind.Feature); return;
                default: throw new InvalidOperationException("不支持的奖励类型：" + reward.Kind);
            }
        }

        public static bool ApplyDefinition(EntityManager em, Entity root, int definition, float multiplier = 1, bool pending = false, int level = -1, Action<int> probe = null)
            => ApplyDefinitionAndCommit(em, root, definition, multiplier, pending, level, null, probe);

        internal static bool ApplyDefinitionAndCommit(EntityManager em, Entity root, int definition, float multiplier, bool pending, int level, Action commit, Action<int> probe = null)
        {
            if (!Sim.ValidDefinition(em, root, definition)) throw new InvalidOperationException("奖励来源定义无效。");
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0) throw new InvalidOperationException("奖励倍率必须是非负有限数值。");
            var d = Sim.Definition(em, root, definition);
            var rewards = new List<RewardGrant>();
            var licenses = new List<RewardGrant>();
            for (var i = 0; i < d.RuleCount; i++)
            {
                var rule = Sim.GetRule(em, root, d.RuleStart + i);
                if (rule.Kind < RuleKind.RewardItem || rule.Kind > RuleKind.RewardFeature || level >= 0 && rule.Level != 0 && rule.Level != level) continue;
                Validate(em, root, new RewardGrant(rule.Kind, rule.Target, rule.Amount));
                var quantity = rule.Amount;
                if (rule.Kind == RuleKind.RewardItem)
                {
                    // Preserve the existing float multiplication before flooring; widening first changes awards.
                    double scaled = math.floor(quantity * multiplier);
                    if (scaled > int.MaxValue) throw new InvalidOperationException("物品奖励数量溢出。");
                    quantity = (int)scaled;
                }
                if (quantity > 0)
                {
                    var reward = new RewardGrant(rule.Kind, rule.Target, quantity, sourceName: d.Name);
                    if (rule.Kind == RuleKind.RewardItem) rewards.Add(reward); else licenses.Add(reward);
                }
            }
            // Definition rewards historically store items before activating any new modifiers.
            // Keep authored order within each pass, so a new loss Buff cannot change this batch's storage order.
            rewards.AddRange(licenses);
            return ApplyBatch(em, root, rewards, pending, commit, false, probe);
        }

        internal static bool ApplyBatch(EntityManager em, Entity root, IReadOnlyList<RewardGrant> rewards, bool pending, Action commit = null, bool reportOverflow = false, Action<int> probe = null)
        {
            foreach (var reward in rewards) Validate(em, root, reward);
            using (var transaction = new RewardTransaction(em, root))
            {
                var sequence = 0;
                foreach (var reward in rewards)
                {
                    if (reward.Kind == RuleKind.RewardItem)
                    {
                        var stored = InventoryOps.Add(em, root, reward.Definition, reward.Amount, pending);
                        if (!pending && stored != reward.Amount) return false;
                        if (reportOverflow && stored < reward.Amount)
                            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.RewardOverflow, Id = reward.Source, Definition = reward.Definition, Amount = reward.Amount - stored, SourceName = reward.SourceName });
                    }
                    else GrantEntitlement(em, root, reward.Definition, reward.Amount);
                    probe?.Invoke(++sequence);
                }
                commit?.Invoke();
                probe?.Invoke(++sequence);
                transaction.Commit();
            }
            return true;
        }

        internal static void GrantEntitlement(EntityManager em, Entity root, int definition, int level)
        {
            ValidateEntitlement(em, root, definition, level);
            switch (Sim.Definition(em, root, definition).Kind)
            {
                case ContentKind.Building: BlueprintOps.Grant(em, root, definition, level); break;
                case ContentKind.Buff: PermanentBuffOps.Grant(em, root, definition, level); break;
                case ContentKind.Feature: FeatureOps.Unlock(em, root, definition); break;
            }
        }

        internal static void ValidateEntitlement(EntityManager em, Entity root, int definition, int level)
        {
            if (!Sim.ValidDefinition(em, root, definition)) throw new InvalidOperationException("奖励许可定义无效。");
            switch (Sim.Definition(em, root, definition).Kind)
            {
                case ContentKind.Building:
                case ContentKind.Buff:
                case ContentKind.Feature:
                    EntitlementStore.Validate(em, root, definition, level); break;
                default: throw new InvalidOperationException("奖励不能直接写入科技、任务或远征完成状态。");
            }
        }
    }
}
