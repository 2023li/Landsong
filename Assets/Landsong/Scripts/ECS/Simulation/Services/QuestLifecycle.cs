using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public enum QuestAction
    {
        Accept,
        Reject,
        Claim,
        Abandon
    }

    public static class QuestLifecycle
    {
        public static int QuestCapacity(EntityManager em)
        {
            var capacity = 0;
            using var all = WorldQueries.OrderedEntities<Building>(em);
            foreach (var e in all)
                capacity += QuestLifecycle.QuestContainerCapacity(em, e);
            return capacity;
        }

        public static int QuestContainerCapacity(EntityManager em, Entity e)
        {
            if (!BuildingStatus.Operational(em, e) || !em.HasComponent<BuildingHousingStats>(e))
                return 0;
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
            BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(e);
            BuildingWorkforceStats statsWorkforce = em.GetComponentData<BuildingWorkforceStats>(e);
            BuildingQuestStats statsQuests = em.GetComponentData<BuildingQuestStats>(e);
            return bMaintenance.Maintained != 0 && bWorkforce.Workers >= statsWorkforce.Capacity ? math.max(0, statsQuests.Capacity) : 0;
        }

        public static bool QuestSlotFree(EntityManager em, ulong container, int slot)
        {
            using var all = WorldQueries.OrderedEntities<Quest>(em);
            foreach (var e in all)
            {
                var q = em.GetComponentData<Quest>(e);
                if ((q.Status == QuestStatus.Active || q.Status == QuestStatus.Completed) && q.Container == container && q.ContainerSlot == slot)
                    return false;
            }

            return true;
        }

        public static bool BindQuestContainer(EntityManager em, ref Quest quest)
        {
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            // Prefer the invitation's provider, then the first available stable building/slot ID.
            for (var pass = 0; pass < 2; pass++)
                foreach (var e in buildings)
                {
                    var id = em.GetComponentData<Identity>(e).Id;
                    if ((id == quest.Source) != (pass == 0))
                        continue;
                    for (var i = 0; i < QuestLifecycle.QuestContainerCapacity(em, e); i++)
                        if (QuestLifecycle.QuestSlotFree(em, id, i))
                        {
                            quest.Container = id;
                            quest.ContainerSlot = i;
                            return true;
                        }
                }

            return false;
        }

        public static void ReconcileQuestContainers(EntityManager em, Entity root)
        {
            using var quests = WorldQueries.OrderedEntities<Quest>(em);
            foreach (var e in quests)
            {
                var q = em.GetComponentData<Quest>(e);
                if (q.Status != QuestStatus.Active && q.Status != QuestStatus.Completed)
                    continue;
                if (q.Container == 0 || q.ContainerSlot < 0 || q.ContainerSlot >= QuestLifecycle.QuestContainerCapacity(em, WorldQueries.Find(em, q.Container)))
                    QuestLifecycle.FailQuest(em, root, e, "承接槽位失效", true);
            }
        }

        public static int QuestCount(EntityManager em)
        {
            var count = 0;
            using var all = WorldQueries.OrderedEntities<Quest>(em);
            foreach (var e in all)
            {
                var q = em.GetComponentData<Quest>(e);
                if (q.Status == QuestStatus.Active || q.Status == QuestStatus.Completed)
                    count++;
            }

            return count;
        }

        public static Entity CreateQuest(EntityManager em, Entity root, QuestId definition, ulong source = 0, int slot = 0, ulong container = 0, int containerSlot = 0)
        {
            ref var d = ref QuestDefinitions.Get(em, root, definition);
            var mainline = (d.Behavior & QuestBehaviorFlags.Mainline) != 0;
            var turn = em.GetComponentData<GameClock>(root).Turn;
            var accepted = mainline || container != 0;
            var quest = new Quest
            {
                Mainline = (byte)(mainline ? 1 : 0),
                Status = accepted ? QuestStatus.Active : QuestStatus.Offered,
                Source = source,
                Slot = slot,
                Container = container,
                ContainerSlot = containerSlot,
                StartTurn = accepted ? turn : 0,
                Deadline = accepted && d.DeadlineTurns > 0 ? turn + d.DeadlineTurns : 0
            };
            if (container != 0)
            {
                if (containerSlot < 0 || containerSlot >= QuestLifecycle.QuestContainerCapacity(em, WorldQueries.Find(em, container)) || !QuestLifecycle.QuestSlotFree(em, container, containerSlot))
                    return Entity.Null;
            }
            else if (mainline)
            {
                if (quest.Source == 0)
                {
                    using var buildings = WorldQueries.OrderedEntities<Building>(em);
                    foreach (var building in buildings)
                        if (em.GetComponentData<BuildingHousingStats>(building).IsCore != 0)
                        {
                            quest.Source = em.GetComponentData<Identity>(building).Id;
                            break;
                        }
                }

                // No entity, timer or ID is allocated until a real shared slot is available.
                if (!QuestLifecycle.BindQuestContainer(em, ref quest))
                    return Entity.Null;
            }

            var e = QuestEntities.Spawn(em, root, definition, default, true);
            EntityState.Set(em, e, quest);
            QuestObjectiveProgress.Initialize(em, e, ref d.Objectives);
            return e;
        }

        internal static ulong ContinueQuest(EntityManager em, Entity root, QuestId predecessor, Quest previous)
        {
            var existing = new System.Collections.Generic.HashSet<QuestId>();
            using (var quests = WorldQueries.OrderedEntities<Quest>(em))
                foreach (var e in quests)
                    existing.Add(em.GetComponentData<QuestDefinitionRef>(e).Definition);
            var count = QuestDefinitions.Count(em, root);
            var best = QuestDefinitions.Get(em, root, predecessor).NextQuest;
            long bestValue = -1;
            var bestReady = best.IsValid && QuestOps.Prerequisites(em, root, best);
            for (var index = 0; !best.IsValid && index < count; index++)
            {
                var i = QuestId.FromIndex(index);
                ref var d = ref QuestDefinitions.Get(em, root, i);
                if ((d.Behavior & QuestBehaviorFlags.Draft) != 0 || (d.Behavior & QuestBehaviorFlags.Mainline) != 0 && existing.Contains(i) || !QuestOps.HasQuestPredecessor(em, root, i, predecessor))
                    continue;
                var ready = QuestOps.Prerequisites(em, root, i);
                var value = QuestOps.RewardValue(em, root, i);
                if (!best.IsValid || ready && !bestReady || ready == bestReady && value > bestValue)
                {
                    best = i;
                    bestValue = value;
                    bestReady = ready;
                }
            }

            if (!best.IsValid)
                return 0;
            if (existing.Contains(best))
                return 0;
            // Both ordinary and mainline steps retain their invitation provenance and exact accepted slot.
            var next = QuestLifecycle.CreateQuest(em, root, best, previous.Source, previous.Slot, previous.Container, previous.ContainerSlot);
            if (next == Entity.Null)
                return 0;
            if (!bestReady)
            {
                // A step with extra prerequisites keeps the slot but starts its timer only when ready.
                var waiting = em.GetComponentData<Quest>(next);
                waiting.StartTurn = 0;
                waiting.Deadline = 0;
                em.SetComponentData(next, waiting);
            }

            return em.GetComponentData<Identity>(next).Id;
        }

        public static System.Collections.Generic.List<QuestId> WaitingMainlines(EntityManager em, Entity root)
        {
            var existing = new System.Collections.Generic.HashSet<QuestId>();
            using (var quests = WorldQueries.OrderedEntities<Quest>(em))
                foreach (var e in quests)
                    existing.Add(em.GetComponentData<QuestDefinitionRef>(e).Definition);
            var waiting = new System.Collections.Generic.List<QuestId>();
            var count = QuestDefinitions.Count(em, root);
            for (var index = 0; index < count; index++)
            {
                var i = QuestId.FromIndex(index);
                ref var d = ref QuestDefinitions.Get(em, root, i);
                if ((d.Behavior & QuestBehaviorFlags.Mainline) != 0 && (d.Behavior & QuestBehaviorFlags.Draft) == 0 && !existing.Contains(i) && QuestOps.Prerequisites(em, root, i))
                    waiting.Add(i);
            }

            return waiting;
        }

        public static void DiscoverQuests(EntityManager em, Entity root)
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase != Phase.Day && phase != Phase.Settlement)
                return;
            foreach (var definition in QuestLifecycle.WaitingMainlines(em, root))
                if (QuestLifecycle.CreateQuest(em, root, definition) == Entity.Null)
                    break;
        }

        public static ResultCode ChangeStatus(EntityManager em, Entity root, ulong questId, QuestAction action)
        {
            var e = WorldQueries.Find(em, questId);
            if (e == Entity.Null || !em.HasComponent<Quest>(e))
                return ResultCode.InvalidTarget;
            var q = em.GetComponentData<Quest>(e);
            var definition = em.GetComponentData<QuestDefinitionRef>(e).Definition;
            if (action == QuestAction.Accept)
            {
                if (q.Status != QuestStatus.Offered)
                    return ResultCode.Unavailable;
                if (WorldQueries.Find(em, q.Source) == Entity.Null)
                    return ResultCode.InvalidTarget;
                if (QuestLifecycle.QuestCount(em) >= QuestLifecycle.QuestCapacity(em) || !QuestLifecycle.BindQuestContainer(em, ref q))
                    return ResultCode.QuestOverflow;
                q.Status = QuestStatus.Active;
                q.StartTurn = em.GetComponentData<GameClock>(root).Turn;
                var duration = QuestDefinitions.Get(em, root, definition).DeadlineTurns;
                q.Deadline = duration > 0 ? q.StartTurn + duration : 0;
                em.SetComponentData(e, q);
                QuestLifecycle.RestartOfferCooldown(em, root, q);
                QuestLifecycle.EvaluateQuests(em, root);
                return ResultCode.Success;
            }

            if (action == QuestAction.Reject || action == QuestAction.Abandon)
            {
                if (q.Mainline != 0 || (action == QuestAction.Reject ? q.Status != QuestStatus.Offered : q.Status != QuestStatus.Active))
                    return ResultCode.Unavailable;
                if (action == QuestAction.Abandon)
                    return QuestLifecycle.FailQuest(em, root, e, "主动放弃");
                QuestLifecycle.RestartOfferCooldown(em, root, q);
                em.DestroyEntity(e);
                return ResultCode.Success;
            }

            if (action == QuestAction.Claim)
            {
                if (q.Status != QuestStatus.Active && q.Status != QuestStatus.Completed)
                    return ResultCode.Unavailable;
                QuestLifecycle.EvaluateQuests(em, root);
                if (!em.Exists(e))
                    return ResultCode.Unavailable;
                q = em.GetComponentData<Quest>(e);
                if (q.Status != QuestStatus.Completed)
                    return ResultCode.Unavailable;
                var follow = QuestOps.IsTracked(em, root, questId);
                ref var source = ref QuestDefinitions.Get(em, root, definition);
                if (!RewardDelivery.Apply(em, root, ref source.Rewards, source.Metadata.Name, 1, false, () => QuestCompletions.RecordClaim(em, root, definition)))
                    return ResultCode.NoCapacity;
                var previous = q;
                if (q.Mainline != 0)
                {
                    q.Status = QuestStatus.Claimed;
                    q.Container = 0;
                    q.ContainerSlot = 0;
                    em.SetComponentData(e, q);
                }
                else
                    em.DestroyEntity(e);
                QuestOfferOps.RestartQuestCooldown(em, root, definition);
                // Hand off before discovery can allocate the vacated slot to another quest.
                var continuation = QuestLifecycle.ContinueQuest(em, root, definition, previous);
                QuestLifecycle.DiscoverQuests(em, root);
                QuestLifecycle.EvaluateQuests(em, root);
                if (follow)
                    QuestOps.FollowClaim(em, root, definition, continuation);
                return ResultCode.Success;
            }

            return ResultCode.Unavailable;
        }

        public static void TrackInput(EntityManager em, Entity root, bool zoom) => QuestObjectiveProgress.ObserveCamera(em, root, zoom);
        public static void EvaluateQuests(EntityManager em, Entity root)
        {
            QuestLifecycle.ReconcileQuestContainers(em, root);
            QuestLifecycle.DiscoverQuests(em, root);
            using var all = WorldQueries.OrderedEntities<Quest>(em);
            foreach (var e in all)
            {
                var q = em.GetComponentData<Quest>(e);
                if (q.Status == QuestStatus.Offered)
                {
                    if (WorldQueries.Find(em, q.Source) == Entity.Null)
                        em.DestroyEntity(e);
                    continue;
                }

                if (q.Status == QuestStatus.Claimed || q.Status == QuestStatus.Completed)
                    continue;
                var definition = em.GetComponentData<QuestDefinitionRef>(e).Definition;
                if (!QuestOps.Prerequisites(em, root, definition))
                    continue;
                if (q.StartTurn == 0 && QuestOps.HasQuestPredecessor(em, root, definition))
                {
                    q.StartTurn = em.GetComponentData<GameClock>(root).Turn;
                    var duration = QuestDefinitions.Get(em, root, definition).DeadlineTurns;
                    q.Deadline = duration > 0 ? q.StartTurn + duration : 0;
                }

                ref var source = ref QuestDefinitions.Get(em, root, definition);
                var complete = QuestObjectiveProgress.Evaluate(em, root, e, ref source.Objectives, q.StartTurn);
                q.Status = complete ? QuestStatus.Completed : QuestStatus.Active;
                em.SetComponentData(e, q);
                if (complete)
                    SimulationEvents.Emit(em, root, EventKind.Message, "任务已完成", em.GetComponentData<Identity>(e).Id);
            }

            QuestOps.RefreshTracking(em, root);
        }

        public static List<BuildingCost> FailureCosts(EntityManager em, Entity root, QuestId definition)
        {
            ref var source = ref QuestDefinitions.Get(em, root, definition);
            var costs = new List<BuildingCost>();
            for (int i = 0; i < source.FailurePenalties.Length; i++)
            {
                var item = source.FailurePenalties[i];
                costs.Add(new BuildingCost { Item = item.Item, Amount = item.Quantity });
            }

            return costs;
        }

        public static ResultCode FailQuest(EntityManager em, Entity root, Entity entity, string reason, bool containerLost = false)
        {
            if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<Quest>(entity))
                return ResultCode.InvalidTarget;
            var quest = em.GetComponentData<Quest>(entity);
            if (quest.Mainline != 0 && !containerLost || (quest.Status != QuestStatus.Active && !(containerLost && quest.Status == QuestStatus.Completed)))
                return ResultCode.Unavailable;
            using var scope = EconomyJournalOps.For(em, root, entity, EconomyReason.QuestPenalty);
            var id = em.GetComponentData<Identity>(entity);
            foreach (var cost in QuestLifecycle.FailureCosts(em, root, em.GetComponentData<QuestDefinitionRef>(entity).Definition))
            {
                var amount = math.min(cost.Amount, InventoryOps.Count(em, root, cost.Item));
                InventoryOps.Remove(em, root, cost.Item, amount);
                if (amount > 0)
                    SimulationEvents.Emit(em, root, EventKind.Message, "任务惩罚已扣除", id.Id, amount: amount, category: HistoryCategory.Economy);
            }

            // Removal is the one-shot commit marker. Repeat commands and timeout cannot charge again.
            QuestOfferOps.RestartQuestCooldown(em, root, em.GetComponentData<QuestDefinitionRef>(entity).Definition);
            em.DestroyEntity(entity);
            QuestOps.RefreshTracking(em, root);
            SimulationEvents.Emit(em, root, EventKind.Message, "任务结束：" + reason, id.Id);
            return ResultCode.Success;
        }

        internal static void RestartOfferCooldown(EntityManager em, Entity root, Quest quest)
        {
            var source = WorldQueries.Find(em, quest.Source);
            if (source == Entity.Null || !em.HasBuffer<QuestOfferSlot>(source))
                return;
            var slots = em.GetBuffer<QuestOfferSlot>(source);
            if (quest.Slot < 0 || quest.Slot >= slots.Length)
                return;
            QuestOfferOps.Restart(em, root, source, slots[quest.Slot].Type, em.GetComponentData<GameClock>(root).Turn);
        }
    }
}
