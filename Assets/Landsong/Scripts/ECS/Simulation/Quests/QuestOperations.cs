using System;
using System.IO;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class QuestSubmissionQuote
    {
        public ResultCode Code = ResultCode.InvalidTarget;
        public ItemId Item;
        public int Available, Remaining, Maximum, ProgressIndex = -1;
        public string Key, Stamp, Reason = "任务或要求已不存在";
    }

    public static class QuestOps
    {
        public static bool Prerequisites(EntityManager em, Entity root, QuestId quest)
        {
            ref var definition = ref QuestDefinitions.Get(em, root, quest);
            return PrerequisiteEvaluation.Satisfied(em, root, ref definition.Prerequisites);
        }

        public static long RewardValue(EntityManager em, Entity root, QuestId quest)
        {
            ref var definition = ref QuestDefinitions.Get(em, root, quest);
            long value = 0;
            for (int i = 0; i < definition.Rewards.Items.Length; i++)
            {
                var reward = definition.Rewards.Items[i];
                long amount = (long)math.max(0, reward.Quantity) * math.max(0, ItemDefinitions.Get(em, root, reward.Item).TradeValue);
                value = amount > long.MaxValue - value ? long.MaxValue : value + amount;
            }

            return value;
        }

        public static bool HasQuestPredecessor(EntityManager em, Entity root, QuestId quest, QuestId predecessor = default)
        {
            ref var definition = ref QuestDefinitions.Get(em, root, quest);
            for (int i = 0; i < definition.Prerequisites.QuestRequirements.Length; i++)
                if (!predecessor.IsValid || definition.Prerequisites.QuestRequirements[i].Quest == predecessor)
                    return true;
            return false;
        }

        public static QuestTracking Tracking(EntityManager em, Entity root) => em.HasComponent<QuestTracking>(root) ? em.GetComponentData<QuestTracking>(root) : default;
        public static bool Trackable(EntityManager em, Entity entity) => entity != Entity.Null && em.Exists(entity) && em.HasComponent<Quest>(entity) && (em.GetComponentData<Quest>(entity).Status == QuestStatus.Active || em.GetComponentData<Quest>(entity).Status == QuestStatus.Completed);
        public static int CompareValue(EntityManager em, Entity root, Entity left, Entity right)
        {
            int order = RewardValue(em, root, em.GetComponentData<QuestDefinitionRef>(right).Definition).CompareTo(RewardValue(em, root, em.GetComponentData<QuestDefinitionRef>(left).Definition));
            return order != 0 ? order : em.GetComponentData<Identity>(left).Id.CompareTo(em.GetComponentData<Identity>(right).Id);
        }

        static ulong BestAccepted(EntityManager em, Entity root, QuestId predecessor = default)
        {
            Entity best = Entity.Null;
            using var quests = WorldQueries.OrderedEntities<Quest>(em);
            foreach (var entity in quests)
            {
                if (!Trackable(em, entity))
                    continue;
                if (predecessor.IsValid && !HasQuestPredecessor(em, root, em.GetComponentData<QuestDefinitionRef>(entity).Definition, predecessor))
                    continue;
                if (best == Entity.Null || CompareValue(em, root, entity, best) < 0)
                    best = entity;
            }

            return best == Entity.Null ? 0 : em.GetComponentData<Identity>(best).Id;
        }

        public static void FollowClaim(EntityManager em, Entity root, QuestId quest, ulong continuation = 0)
        {
            ulong next = Trackable(em, WorldQueries.Find(em, continuation)) ? continuation : BestAccepted(em, root, quest);
            if (next == 0)
                next = BestAccepted(em, root);
            EntityState.Set(em, root, new QuestTracking { Target = next });
        }

        public static void RefreshTracking(EntityManager em, Entity root)
        {
            var state = Tracking(em, root);
            ulong prior = state.Target;
            if (state.Mode == 0 && !Trackable(em, WorldQueries.Find(em, state.Target)))
                state.Target = BestAccepted(em, root);
            else if (state.Mode == 2 || !Trackable(em, WorldQueries.Find(em, state.Target)))
                state.Target = 0;
            if (prior != state.Target || !em.HasComponent<QuestTracking>(root))
                EntityState.Set(em, root, state);
        }

        public static ResultCode Track(EntityManager em, Entity root, ulong target, int mode)
        {
            if (mode < 0 || mode > 2 || mode == 1 && !Trackable(em, WorldQueries.Find(em, target)))
                return ResultCode.InvalidTarget;
            if (mode == 2 && target != 0 && Tracking(em, root).Target != target)
                return ResultCode.Success;
            EntityState.Set(em, root, new QuestTracking { Mode = (byte)mode, Target = mode == 1 ? target : 0 });
            RefreshTracking(em, root);
            return ResultCode.Success;
        }

        public static string Fingerprint(EntityManager em, Entity root, Entity entity)
        {
            ulong hash = 14695981039346656037UL;
            void Add(ulong value)
            {
                unchecked
                {
                    hash = (hash ^ value) * 1099511628211UL;
                }
            }

            var quest = em.GetComponentData<Quest>(entity);
            Add(em.GetComponentData<Identity>(entity).Id);
            Add((ulong)quest.Status);
            Add((ulong)quest.StartTurn);
            foreach (var row in em.GetBuffer<QuestProgress>(entity))
            {
                foreach (char c in row.Key.ToString())
                    Add(c);
                Add((ulong)row.Amount);
            }

            foreach (var slot in em.GetBuffer<InventorySlot>(root))
            {
                Add(slot.Provider);
                Add((ulong)slot.Index);
                Add((ulong)(slot.Item.Index + 1));
                Add((ulong)slot.Count);
                Add(slot.Unavailable);
            }

            return hash.ToString("X16");
        }

        public static QuestSubmissionQuote Submission(EntityManager em, Entity root, Entity entity, string key)
        {
            var quote = new QuestSubmissionQuote
            {
                Key = key
            };
            if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<Quest>(entity))
                return quote;
            var quest = em.GetComponentData<Quest>(entity);
            if (em.GetComponentData<Session>(root).Phase != Phase.Day || em.GetComponentData<PersistenceGate>(root).CheckpointPending != 0)
            {
                quote.Code = ResultCode.WrongPhase;
                quote.Reason = "仅白天可提交";
                return quote;
            }

            if (quest.Status != QuestStatus.Active)
            {
                quote.Code = ResultCode.Unavailable;
                quote.Reason = "仅进行中的任务可提交";
                return quote;
            }

            var id = em.GetComponentData<QuestDefinitionRef>(entity).Definition;
            if (!Prerequisites(em, root, id))
            {
                quote.Code = ResultCode.Unavailable;
                quote.Reason = "等待前置条件，原承接槽位保留";
                return quote;
            }

            ref var definition = ref QuestDefinitions.Get(em, root, id);
            var progress = em.GetBuffer<QuestProgress>(entity);
            for (int i = 0; i < definition.Objectives.SubmittedItemObjectives.Length; i++)
            {
                var objective = definition.Objectives.SubmittedItemObjectives[i];
                if (objective.Key.ToString() != key)
                    continue;
                int index = QuestObjectiveProgress.Index(progress, objective.Key);
                if (index < 0)
                    return quote;
                quote.Item = objective.Item;
                quote.ProgressIndex = index;
                quote.Remaining = math.max(0, objective.Quantity - progress[index].Amount);
                quote.Available = InventoryOps.Count(em, root, objective.Item);
                quote.Maximum = math.min(quote.Remaining, quote.Available);
                quote.Stamp = Fingerprint(em, root, entity);
                quote.Code = quote.Maximum > 0 ? ResultCode.Success : ResultCode.InsufficientResources;
                quote.Reason = quote.Remaining == 0 ? "该项已经提交完毕" : quote.Maximum == 0 ? "正常库存不足；待存放不用于任务提交" : "只提交此项，确认后不可退还";
                return quote;
            }

            return quote;
        }

        public static ResultCode Submit(EntityManager em, Entity root, Entity entity, ItemId item, int quantity, string key, string expectedQuote)
        {
            var quote = Submission(em, root, entity, key);
            if (quote.Code != ResultCode.Success)
                return quote.Code;
            if (expectedQuote != quote.Stamp || item != quote.Item)
                return ResultCode.Unavailable;
            if (quantity <= 0 || quantity > quote.Maximum || !InventoryOps.Remove(em, root, item, quantity))
                return ResultCode.InsufficientResources;
            var progress = em.GetBuffer<QuestProgress>(entity);
            var row = progress[quote.ProgressIndex];
            row.Amount += quantity;
            progress[quote.ProgressIndex] = row;
            QuestLifecycle.EvaluateQuests(em, root);
            return ResultCode.Success;
        }

        public static void ValidateProgress(EntityManager em, Entity root, QuestId definition, Quest quest, QuestProgress[] progress, int turn)
        {
            if (!QuestDefinitions.IsValid(em, root, definition) || quest.Status > QuestStatus.Claimed || quest.StartTurn < 0 || quest.StartTurn > turn || quest.Deadline < 0 || quest.Mainline > 1)
                throw new InvalidDataException("Invalid quest state.");
            ref var source = ref QuestDefinitions.Get(em, root, definition);
            QuestObjectiveProgress.Validate(ref source.Objectives, progress);
        }
    }
}
