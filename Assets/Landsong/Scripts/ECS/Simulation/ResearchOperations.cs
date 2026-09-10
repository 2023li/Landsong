using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum ResearchStatus { Locked, Available, Researching, Queued, Paused, Completed, AwaitingRewards, Blocked }
    public sealed class ResearchQuote
    {
        public int Definition, Cost, Progress, Completions, Order, Points;
        public bool Repeatable, PrerequisitesMet, Editable;
        public ResearchStatus Status;
        public ResultCode CanQueue;
        public string Reason;
        public readonly List<int> Prerequisites = new List<int>();
        public readonly List<Rule> Rewards = new List<Rule>();
    }
    public sealed class ResearchPathQuote
    {
        public ResultCode Code = ResultCode.Success;
        public string Reason = "按前置顺序替换当前计划；已投入进度保留，不退款、不立即消耗研究点。";
        public long Remaining;
        public readonly List<int> Definitions = new List<int>();
    }
    public static class ResearchOps
    {
        public const string FeatureId = "feature.Technology";
        // Permission is optional in custom catalogs. Formal Landsong content registers it and starts locked.
        public static bool Unlocked(EntityManager em, Entity root)
        {
            var blob = em.GetComponentData<ContentCatalog>(root).Value;
            for (var i = 0; i < blob.Value.Definitions.Length; i++) if (blob.Value.Definitions[i].Id.ToString() == FeatureId) return Sim.HasGrant(em, root, i);
            return true;
        }
        public static ResearchEntry Entry(EntityManager em, Entity root, int definition)
        { foreach (var r in em.GetBuffer<ResearchEntry>(root)) if (r.Definition == definition) return r; return new ResearchEntry { Definition = definition }; }
        public static List<ResearchEntry> Queue(EntityManager em, Entity root)
        { var list = new List<ResearchEntry>(); foreach (var r in em.GetBuffer<ResearchEntry>(root)) if (r.QueueOrder > 0) list.Add(r); list.Sort((a, b) => a.QueueOrder.CompareTo(b.QueueOrder)); return list; }
        public static int Completed(EntityManager em, Entity root, int definition)
        { var count = Entry(em, root, definition).Completions; foreach (var g in em.GetBuffer<Entitlement>(root)) if (g.Definition == definition) count = math.max(count, g.Level); return count; }
        public static ResultCode Editable(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            return s.Phase != Phase.Day ? ResultCode.WrongPhase : s.CheckpointPending != 0 ? ResultCode.Busy : !Unlocked(em, root) ? ResultCode.Unavailable : ResultCode.Success;
        }
        public static ResearchQuote Quote(EntityManager em, Entity root, int definition)
        {
            var q = new ResearchQuote { Definition = definition, CanQueue = ResultCode.InvalidContent, Reason = "不是有效科技" };
            if (!Sim.ValidDefinition(em, root, definition) || Sim.Definition(em, root, definition).Kind != ContentKind.Technology) return q;
            var d = Sim.Definition(em, root, definition); var r = Entry(em, root, definition);
            q.Cost = d.Cost; q.Progress = r.Progress; q.Completions = Completed(em, root, definition); q.Repeatable = (d.Flags & 1) != 0; q.Order = r.QueueOrder;
            q.Points = em.GetComponentData<Session>(root).ResearchPoints; q.Editable = Editable(em, root) == ResultCode.Success;
            for (var i = 0; i < d.RuleCount; i++) { var rule = Sim.GetRule(em, root, d.RuleStart + i); if (rule.Kind == RuleKind.Prerequisite) q.Prerequisites.Add(rule.Target); if (rule.Kind >= RuleKind.RewardItem && rule.Kind <= RuleKind.RewardFeature) q.Rewards.Add(rule); }
            q.PrerequisitesMet = ProgressionOps.Prerequisites(em, root, definition);
            var first = Queue(em, root).FirstOrDefault();
            q.Status = r.QueueOrder > 0 ? !q.PrerequisitesMet ? ResearchStatus.Blocked : r.Progress >= d.Cost && q.Completions == 0 ? ResearchStatus.AwaitingRewards : first.Definition == definition ? ResearchStatus.Researching : ResearchStatus.Queued
                : q.Completions > 0 && !q.Repeatable ? ResearchStatus.Completed : !q.PrerequisitesMet ? ResearchStatus.Locked : r.Progress > 0 ? ResearchStatus.Paused : ResearchStatus.Available;
            q.CanQueue = !q.Editable ? Editable(em, root) : r.QueueOrder > 0 || q.Completions > 0 && !q.Repeatable ? ResultCode.Unavailable : !q.PrerequisitesMet ? ResultCode.MissingResearch : ResultCode.Success;
            q.Reason = !Unlocked(em, root) ? "科技功能尚未解锁" : !q.Editable ? "仅白天可调整研究，节点准备期间暂停操作" : !q.PrerequisitesMet ? "前置尚未完成；可选择补齐前置的研究路径" : r.QueueOrder > 0 ? q.Status == ResearchStatus.AwaitingRewards ? "满进度待发奖；整理库存后下次白天结算重试，后续队列暂停" : "已在研究计划中，白天结算时按队列消耗点数" : q.Completions > 0 && !q.Repeatable ? "已完成，不可重复研究" : q.Points == 0 ? "可以排队；暂无研究点，等待后续产出" : "可加入队尾，不立即扣点";
            return q;
        }
        static void Put(EntityManager em, Entity root, ResearchEntry entry)
        { var rows = em.GetBuffer<ResearchEntry>(root); for (var i = 0; i < rows.Length; i++) if (rows[i].Definition == entry.Definition) { rows[i] = entry; return; } rows.Add(entry); }
        static void Normalize(EntityManager em, Entity root)
        { var order = 0; foreach (var row in Queue(em, root)) { var r = row; r.QueueOrder = ++order; Put(em, root, r); } }
        public static ResultCode Command(EntityManager em, Entity root, int definition, bool cancel)
        {
            var q = Quote(em, root, definition); if (q.CanQueue == ResultCode.InvalidContent) return q.CanQueue;
            var permission = Editable(em, root); if (permission != ResultCode.Success) return permission;
            var r = Entry(em, root, definition);
            if (cancel) { if (r.QueueOrder == 0) return ResultCode.InvalidTarget; r.QueueOrder = 0; Put(em, root, r); Normalize(em, root); return ResultCode.Success; }
            if (q.CanQueue != ResultCode.Success) return q.CanQueue;
            Normalize(em, root); r.QueueOrder = Queue(em, root).Count + 1; r.Completions = q.Completions; Put(em, root, r); return ResultCode.Success;
        }
        public static ResearchPathQuote Path(EntityManager em, Entity root, int definition)
        {
            var plan = new ResearchPathQuote(); var visited = new HashSet<int>(); var visiting = new HashSet<int>();
            bool Visit(int at, bool target)
            {
                if (!Sim.ValidDefinition(em, root, at) || Sim.Definition(em, root, at).Kind != ContentKind.Technology) return false;
                var d = Sim.Definition(em, root, at); var completed = Completed(em, root, at);
                if (completed > 0 && (!target || (d.Flags & 1) == 0)) return true;
                if (visiting.Contains(at)) return false; if (!visited.Add(at)) return true; visiting.Add(at);
                for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == RuleKind.Prerequisite && !Visit(r.Target, false)) return false; }
                visiting.Remove(at); plan.Definitions.Add(at); plan.Remaining += math.max(0, d.Cost - Entry(em, root, at).Progress); return true;
            }
            if (!Visit(definition, true)) { plan.Definitions.Clear(); plan.Remaining = 0; plan.Code = ResultCode.InvalidContent; plan.Reason = "前置关系无效或存在循环，未更改计划"; }
            else if (plan.Definitions.Count == 0) { plan.Code = ResultCode.Unavailable; plan.Reason = "目标已完成，且不允许重复研究"; }
            return plan;
        }
        public static string Fingerprint(EntityManager em, Entity root)
        {
            // Only the state displayed by a research-plan confirmation participates.
            ulong hash = 14695981039346656037UL;
            void Add(int value) { unchecked { hash = (hash ^ (uint)value) * 1099511628211UL; } }
            var s = em.GetComponentData<Session>(root); Add(s.Turn); Add((int)s.Phase); Add(s.ResearchPoints);
            foreach (var r in em.GetBuffer<ResearchEntry>(root)) { Add(r.Definition); Add(r.Progress); Add(r.Completions); Add(r.QueueOrder); }
            foreach (var g in em.GetBuffer<Entitlement>(root)) { Add(g.Definition); Add(g.Level); }
            return hash.ToString("X16");
        }
        public static ResultCode Plan(EntityManager em, Entity root, int definition, string expected = null)
        {
            var result = Editable(em, root); if (result != ResultCode.Success) return result;
            if (!string.IsNullOrEmpty(expected) && expected != Fingerprint(em, root)) return ResultCode.Unavailable;
            var plan = Path(em, root, definition); if (plan.Code != ResultCode.Success) return plan.Code;
            var rows = em.GetBuffer<ResearchEntry>(root);
            for (var i = 0; i < rows.Length; i++) { var r = rows[i]; r.QueueOrder = 0; rows[i] = r; }
            var order = 0; foreach (var at in plan.Definitions) { var r = Entry(em, root, at); r.Completions = Completed(em, root, at); r.QueueOrder = ++order; Put(em, root, r); }
            return ResultCode.Success;
        }
        public static void Settle(EntityManager em, Entity root)
        {
            if (!Unlocked(em, root)) return;
            using var scope = EconomyJournalOps.For(em, root, Entity.Null, EconomyReason.Research);
            foreach (var queued in Queue(em, root))
            {
                if (!ProgressionOps.Prerequisites(em, root, queued.Definition)) { EconomyJournalOps.Note(em, root, "研究前置未完成，队列暂停且不扣点"); break; }
                var entry = Entry(em, root, queued.Definition); var d = Sim.Definition(em, root, entry.Definition);
                var completed = Completed(em, root, entry.Definition);
                if (completed > 0 && (d.Flags & 1) == 0) { entry.Completions = completed; entry.Progress = entry.QueueOrder = 0; Put(em, root, entry); continue; }
                var s = em.GetComponentData<Session>(root); var spend = math.min(math.max(0, s.ResearchPoints), math.max(0, d.Cost - entry.Progress));
                entry.Progress += spend; s.ResearchPoints -= spend; em.SetComponentData(root, s); Put(em, root, entry);
                if (entry.Progress < d.Cost) break;
                if (completed == 0 && !ProgressionOps.Reward(em, root, entry.Definition)) { EconomyJournalOps.Note(em, root, "研究满进度等待完整奖励入库，后续暂停"); break; }
                entry.Completions = checked(completed + 1); entry.Progress = entry.QueueOrder = 0; Put(em, root, entry); Sim.Grant(em, root, entry.Definition, entry.Completions);
                Sim.Emit(em, root, EventKind.Message, "研究完成", definition: entry.Definition); EconomyJournalOps.Note(em, root, "研究完成；首次奖励只发放一次");
            }
            Normalize(em, root);
        }
        public static void ValidateState(EntityManager em, Entity root, int points, ResearchEntry[] rows, Entitlement[] grants)
        {
            if (points < 0) throw new InvalidDataException("Negative research points"); var definitions = new HashSet<int>(); var orders = new HashSet<int>();
            foreach (var r in rows)
            {
                if (!Sim.ValidDefinition(em, root, r.Definition) || Sim.Definition(em, root, r.Definition).Kind != ContentKind.Technology || !definitions.Add(r.Definition) || r.Progress < 0 || r.Completions < 0 || r.Completions == int.MaxValue || r.QueueOrder < 0 || r.QueueOrder > 0 && !orders.Add(r.QueueOrder)) throw new InvalidDataException("Invalid research entry/queue");
                var d = Sim.Definition(em, root, r.Definition);
                if (r.Progress > d.Cost || (d.Flags & 1) == 0 && (r.Completions > 1 || r.Completions > 0 && (r.Progress > 0 || r.QueueOrder > 0))) throw new InvalidDataException("Invalid research completion/progress");
                if (r.Completions > 0 && !grants.Any(g => g.Definition == r.Definition && g.Level >= r.Completions)) throw new InvalidDataException("Research completion without entitlement");
            }
        }
    }
}
