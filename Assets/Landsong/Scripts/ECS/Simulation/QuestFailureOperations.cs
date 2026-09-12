using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static partial class ProgressionOps
    {
        public static List<BuildingCost> FailureCosts(EntityManager em, Entity root, int definition)
            => BuildingCostOps.Rules(em, root, definition, RuleKind.FailureItem, 1);

        public static ResultCode FailQuest(EntityManager em, Entity root, Entity entity, string reason, bool containerLost = false)
        {
            if (entity == Entity.Null || !em.Exists(entity) || !em.HasComponent<Quest>(entity)) return ResultCode.InvalidTarget;
            var quest = em.GetComponentData<Quest>(entity);
            if (quest.Mainline != 0 && !containerLost || (quest.Status != QuestStatus.Active && !(containerLost && quest.Status == QuestStatus.Completed))) return ResultCode.Unavailable;
            using var scope = EconomyJournalOps.For(em, root, entity, EconomyReason.QuestPenalty);
            var id = em.GetComponentData<Identity>(entity);
            foreach (var cost in FailureCosts(em, root, id.Definition))
            {
                var amount = math.min(cost.Amount, InventoryOps.Count(em, root, cost.Item));
                InventoryOps.Remove(em, root, cost.Item, amount);
                if (amount > 0) Sim.Emit(em, root, EventKind.Message, "任务惩罚已扣除", id.Id, cost.Item, amount, category: HistoryCategory.Economy);
            }
            // Removal is the one-shot commit marker. Repeat commands and timeout cannot charge again.
            em.DestroyEntity(entity);
            QuestOps.RefreshTracking(em, root);
            Sim.Emit(em, root, EventKind.Message, "任务结束：" + reason, id.Id);
            return ResultCode.Success;
        }

        static void RestartOfferCooldown(EntityManager em, Entity root, Quest quest)
        {
            var source = Sim.Find(em, quest.Source);
            if (source == Entity.Null || !em.HasBuffer<QuestOfferSlot>(source)) return;
            var slots = em.GetBuffer<QuestOfferSlot>(source);
            if (quest.Slot < 0 || quest.Slot >= slots.Length) return;
            QuestOfferOps.Restart(em, root, source, slots[quest.Slot].Type, em.GetComponentData<Session>(root).Turn);
        }
    }
}
