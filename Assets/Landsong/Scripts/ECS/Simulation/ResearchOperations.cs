using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum ResearchStatus
    {
        Locked,
        Available,
        Researching,
        Queued,
        Paused,
        Completed,
        AwaitingRewards,
        Blocked
    }

    public sealed class ResearchQuote
    {
        public TechnologyId Definition;
        public int Cost, Progress, Completions, Order, Points;
        public bool Repeatable, PrerequisitesMet, Editable;
        public ResearchStatus Status;
        public ResultCode CanQueue;
        public string Reason;
    }

    public sealed class ResearchPathQuote
    {
        public ResultCode Code = ResultCode.Success;
        public string Reason = "按前置顺序替换当前计划；已投入进度保留，不退款、不立即消耗研究点。";
        public long Remaining;
        public readonly List<TechnologyId> Definitions = new List<TechnologyId>();
    }

    public static class ResearchOps
    {
        public const string FeatureId = "feature.Technology";
        public const int MaximumCompletions = int.MaxValue - 1;
        // Permission is optional in custom catalogs. Formal Landsong content registers it and starts locked.
        public static bool Unlocked(EntityManager em, Entity root)
        {
            var feature = FeatureDefinitions.Find(em, root, new Unity.Collections.FixedString128Bytes(FeatureId));
            return !feature.IsValid || FeatureUnlocks.Has(em, root, feature);
        }

        public static bool Prerequisites(EntityManager em, Entity root, TechnologyId technology)
        {
            ref var definition = ref TechnologyDefinitions.Get(em, root, technology);
            return PrerequisiteEvaluation.Satisfied(em, root, ref definition.Prerequisites);
        }

        public static TechnologyProgress Entry(EntityManager em, Entity root, TechnologyId definition)
        {
            foreach (var r in em.GetBuffer<TechnologyProgress>(root))
                if (r.Technology == definition)
                    return r;
            return new TechnologyProgress
            {
                Technology = definition
            };
        }

        public static List<TechnologyProgress> Queue(EntityManager em, Entity root)
        {
            var list = new List<TechnologyProgress>();
            foreach (var r in em.GetBuffer<TechnologyProgress>(root))
                if (r.QueueOrder > 0)
                    list.Add(r);
            list.Sort((a, b) => a.QueueOrder.CompareTo(b.QueueOrder));
            return list;
        }

        public static int Completed(EntityManager em, Entity root, TechnologyId definition) => Entry(em, root, definition).Completions;
        public static ResultCode Editable(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            IntelligenceModeState sIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            return s.Phase != Phase.Day ? ResultCode.WrongPhase : sPersistence.CheckpointPending != 0 || sControl.Paused != 0 || sIntelligenceMode.Enabled != 0 ? ResultCode.Busy : !Unlocked(em, root) ? ResultCode.Unavailable : ResultCode.Success;
        }

        public static ResearchQuote Quote(EntityManager em, Entity root, TechnologyId definition)
        {
            var q = new ResearchQuote
            {
                Definition = definition,
                CanQueue = ResultCode.InvalidContent,
                Reason = "不是有效科技"
            };
            if (!TechnologyDefinitions.IsValid(em, root, definition))
                return q;
            ref var d = ref TechnologyDefinitions.Get(em, root, definition);
            var r = Entry(em, root, definition);
            q.Cost = d.ResearchPointCost;
            q.Progress = r.ResearchPoints;
            q.Completions = Completed(em, root, definition);
            q.Repeatable = d.Repeatable;
            q.Order = r.QueueOrder;
            q.Points = em.GetComponentData<ResearchState>(root).Points;
            q.Editable = Editable(em, root) == ResultCode.Success;
            q.PrerequisitesMet = Prerequisites(em, root, definition);
            var first = Queue(em, root).FirstOrDefault();
            q.Status = r.QueueOrder > 0 ? !q.PrerequisitesMet ? ResearchStatus.Blocked : r.ResearchPoints >= d.ResearchPointCost && q.Completions == 0 ? ResearchStatus.AwaitingRewards : first.Technology == definition ? ResearchStatus.Researching : ResearchStatus.Queued : q.Completions > 0 && !q.Repeatable ? ResearchStatus.Completed : !q.PrerequisitesMet ? ResearchStatus.Locked : r.ResearchPoints > 0 ? ResearchStatus.Paused : ResearchStatus.Available;
            q.CanQueue = !q.Editable ? Editable(em, root) : r.QueueOrder > 0 || q.Completions >= MaximumCompletions || q.Completions > 0 && !q.Repeatable ? ResultCode.Unavailable : !q.PrerequisitesMet ? ResultCode.MissingResearch : ResultCode.Success;
            q.Reason = !Unlocked(em, root) ? "科技功能尚未解锁" : !q.Editable ? "仅可在白天非暂停、非情报模式且节点准备结束后调整研究" : q.Completions >= MaximumCompletions ? "研究完成次数已达到支持上限" : !q.PrerequisitesMet ? "前置尚未完成；可选择补齐前置的研究路径" : r.QueueOrder > 0 ? q.Status == ResearchStatus.AwaitingRewards ? "满进度待发奖；整理库存后下次白天结算重试，后续队列暂停" : "已在研究计划中，白天结算时按队列消耗点数" : q.Completions > 0 && !q.Repeatable ? "已完成，不可重复研究" : q.Points == 0 ? "可以排队；暂无研究点，等待后续产出" : "可加入队尾，不立即扣点";
            return q;
        }

        static void Put(EntityManager em, Entity root, TechnologyProgress entry)
        {
            var rows = em.GetBuffer<TechnologyProgress>(root);
            for (var i = 0; i < rows.Length; i++)
                if (rows[i].Technology == entry.Technology)
                {
                    rows[i] = entry;
                    return;
                }

            rows.Add(entry);
        }

        static void Normalize(EntityManager em, Entity root)
        {
            var order = 0;
            foreach (var row in Queue(em, root))
            {
                var r = row;
                r.QueueOrder = ++order;
                Put(em, root, r);
            }
        }

        public static ResultCode Command(EntityManager em, Entity root, TechnologyId definition, bool cancel)
        {
            var q = Quote(em, root, definition);
            if (q.CanQueue == ResultCode.InvalidContent)
                return q.CanQueue;
            var permission = Editable(em, root);
            if (permission != ResultCode.Success)
                return permission;
            var r = Entry(em, root, definition);
            if (cancel)
            {
                if (r.QueueOrder == 0)
                    return ResultCode.InvalidTarget;
                r.QueueOrder = 0;
                Put(em, root, r);
                Normalize(em, root);
                return ResultCode.Success;
            }

            if (q.CanQueue != ResultCode.Success)
                return q.CanQueue;
            Normalize(em, root);
            r.QueueOrder = Queue(em, root).Count + 1;
            r.Completions = q.Completions;
            Put(em, root, r);
            return ResultCode.Success;
        }

        public static ResearchPathQuote Path(EntityManager em, Entity root, TechnologyId definition)
        {
            var plan = new ResearchPathQuote();
            var visited = new HashSet<TechnologyId>();
            var visiting = new HashSet<TechnologyId>();
            if (TechnologyDefinitions.IsValid(em, root, definition) && Completed(em, root, definition) >= MaximumCompletions)
            {
                plan.Code = ResultCode.Unavailable;
                plan.Reason = "研究完成次数已达到支持上限";
                return plan;
            }

            bool Visit(TechnologyId at, bool target)
            {
                if (!TechnologyDefinitions.IsValid(em, root, at))
                    return false;
                ref var d = ref TechnologyDefinitions.Get(em, root, at);
                var completed = Completed(em, root, at);
                if (completed > 0 && (!target || !d.Repeatable))
                    return true;
                if (visiting.Contains(at))
                    return false;
                if (!visited.Add(at))
                    return true;
                visiting.Add(at);
                for (var i = 0; i < d.Prerequisites.TechnologyRequirements.Length; i++)
                {
                    var requirement = d.Prerequisites.TechnologyRequirements[i];
                    if (Completed(em, root, requirement.Technology) < requirement.Required && !Visit(requirement.Technology, false))
                        return false;
                }

                visiting.Remove(at);
                plan.Definitions.Add(at);
                plan.Remaining += math.max(0, d.ResearchPointCost - Entry(em, root, at).ResearchPoints);
                return true;
            }

            if (!Visit(definition, true))
            {
                plan.Definitions.Clear();
                plan.Remaining = 0;
                plan.Code = ResultCode.InvalidContent;
                plan.Reason = "前置关系无效或存在循环，未更改计划";
            }
            else if (plan.Definitions.Count == 0)
            {
                plan.Code = ResultCode.Unavailable;
                plan.Reason = "目标已完成，且不允许重复研究";
            }

            return plan;
        }

        public static string Fingerprint(EntityManager em, Entity root)
        {
            // Only the state displayed by a research-plan confirmation participates.
            ulong hash = 14695981039346656037UL;
            void Add(int value)
            {
                unchecked
                {
                    hash = (hash ^ (uint)value) * 1099511628211UL;
                }
            }

            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            ResearchState sResearchState = em.GetComponentData<ResearchState>(root);
            Add(sClock.Turn);
            Add((int)s.Phase);
            Add(sResearchState.Points);
            Add(1);
            Add(em.GetBuffer<TechnologyProgress>(root).Length);
            foreach (var r in em.GetBuffer<TechnologyProgress>(root))
            {
                Add(r.Technology.Index);
                Add(r.ResearchPoints);
                Add(r.Completions);
                Add(r.QueueOrder);
            }

            Add(2);
            Add(em.GetBuffer<BlueprintUnlock>(root).Length);
            foreach (var g in em.GetBuffer<BlueprintUnlock>(root))
            {
                Add(g.Building.Index);
                Add(g.MaximumLevel);
            }

            Add(3);
            Add(em.GetBuffer<OwnedBuff>(root).Length);
            foreach (var g in em.GetBuffer<OwnedBuff>(root))
            {
                Add(g.Buff.Index);
                Add(g.Level);
            }

            Add(4);
            Add(em.GetBuffer<UnlockedFeature>(root).Length);
            foreach (var g in em.GetBuffer<UnlockedFeature>(root))
                Add(g.Feature.Index);
            Add(5);
            Add(em.GetBuffer<ClaimedQuest>(root).Length);
            foreach (var g in em.GetBuffer<ClaimedQuest>(root))
                Add(g.Quest.Index);
            Add(6);
            Add(em.GetBuffer<CompletedExpedition>(root).Length);
            foreach (var g in em.GetBuffer<CompletedExpedition>(root))
                Add(g.Expedition.Index);
            return hash.ToString("X16");
        }

        public static ResultCode Plan(EntityManager em, Entity root, TechnologyId definition, string expected = null)
        {
            var result = Editable(em, root);
            if (result != ResultCode.Success)
                return result;
            if (!string.IsNullOrEmpty(expected) && expected != Fingerprint(em, root))
                return ResultCode.Unavailable;
            var plan = Path(em, root, definition);
            if (plan.Code != ResultCode.Success)
                return plan.Code;
            var rows = em.GetBuffer<TechnologyProgress>(root);
            for (var i = 0; i < rows.Length; i++)
            {
                var r = rows[i];
                r.QueueOrder = 0;
                rows[i] = r;
            }

            var order = 0;
            foreach (var at in plan.Definitions)
            {
                var r = Entry(em, root, at);
                r.Completions = Completed(em, root, at);
                r.QueueOrder = ++order;
                Put(em, root, r);
            }

            return ResultCode.Success;
        }

        public static void Settle(EntityManager em, Entity root, Action<int> rewardProbe = null)
        {
            var state = em.GetComponentData<Session>(root);
            SimulationControl stateControl = em.GetComponentData<SimulationControl>(root);
            IntelligenceModeState stateIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
            PersistenceGate statePersistence = em.GetComponentData<PersistenceGate>(root);
            if ((state.Phase != Phase.Day && state.Phase != Phase.Settlement) || stateControl.Paused != 0 || stateIntelligenceMode.Enabled != 0 || statePersistence.CheckpointPending != 0 || !Unlocked(em, root))
                return;
            using var scope = EconomyJournalOps.For(em, root, Entity.Null, EconomyReason.Research);
            foreach (var queued in Queue(em, root))
            {
                if (!Prerequisites(em, root, queued.Technology))
                {
                    EconomyJournalOps.Note(em, root, "研究前置未完成，队列暂停且不扣点");
                    break;
                }

                var entry = Entry(em, root, queued.Technology);
                ref var d = ref TechnologyDefinitions.Get(em, root, entry.Technology);
                var completed = Completed(em, root, entry.Technology);
                if (completed >= MaximumCompletions)
                {
                    entry.QueueOrder = 0;
                    Put(em, root, entry);
                    continue;
                }

                if (completed > 0 && !d.Repeatable)
                {
                    entry.Completions = completed;
                    entry.ResearchPoints = entry.QueueOrder = 0;
                    Put(em, root, entry);
                    continue;
                }

                var s = em.GetComponentData<Session>(root);
                ResearchState sResearchState = em.GetComponentData<ResearchState>(root);
                var spend = math.min(math.max(0, sResearchState.Points), math.max(0, d.ResearchPointCost - entry.ResearchPoints));
                entry.ResearchPoints += spend;
                sResearchState.Points -= spend;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sResearchState);
                }

                Put(em, root, entry);
                if (entry.ResearchPoints < d.ResearchPointCost)
                    break;
                var completedName = d.Metadata.Name;
                void Complete()
                {
                    entry.Completions = checked(completed + 1);
                    entry.ResearchPoints = entry.QueueOrder = 0;
                    Put(em, root, entry);
                    em.GetBuffer<ResearchCompletedEvent>(root).Add(new ResearchCompletedEvent { Technology = entry.Technology, Name = completedName });
                    SimulationEvents.Emit(em, root, EventKind.Message, "研究完成：" + completedName);
                    EconomyJournalOps.Note(em, root, "研究完成；首次奖励只发放一次");
                }

                bool awarded = completed == 0 ? RewardDelivery.Apply(em, root, ref d.Rewards, d.Metadata.Name, 1, false, Complete, rewardProbe) : RewardDelivery.Commit(em, root, Complete, rewardProbe);
                if (!awarded)
                {
                    EconomyJournalOps.Note(em, root, "研究满进度等待完整奖励入库，后续暂停");
                    break;
                }
            }

            Normalize(em, root);
        }

        public static void ValidateState(EntityManager em, Entity root, int points, TechnologyProgress[] rows) => ValidateEntries(em, root, points, rows);
        static void ValidateEntries(EntityManager em, Entity root, int points, TechnologyProgress[] rows)
        {
            if (points < 0 || rows == null)
                throw new InvalidDataException("Invalid research points or missing entries");
            var definitions = new HashSet<TechnologyId>();
            var orders = new HashSet<int>();
            foreach (var r in rows)
            {
                if (!TechnologyDefinitions.IsValid(em, root, r.Technology) || !definitions.Add(r.Technology) || r.ResearchPoints < 0 || r.Completions < 0 || r.Completions == int.MaxValue || r.QueueOrder < 0 || r.QueueOrder > 0 && !orders.Add(r.QueueOrder))
                    throw new InvalidDataException("Invalid research entry/queue");
                ref var d = ref TechnologyDefinitions.Get(em, root, r.Technology);
                if (r.ResearchPoints > d.ResearchPointCost || !d.Repeatable && (r.Completions > 1 || r.Completions > 0 && (r.ResearchPoints > 0 || r.QueueOrder > 0)))
                    throw new InvalidDataException("Invalid research completion/progress");
                if (r.Completions >= MaximumCompletions && r.QueueOrder > 0)
                    throw new InvalidDataException("Research queue exceeds supported completion count");
            }
        }
    }
}
