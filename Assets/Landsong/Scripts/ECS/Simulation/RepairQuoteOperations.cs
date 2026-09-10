using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct RepairPayment { public int Item, Required, Pending, Normal, Available, Missing; }
    public sealed class RepairQuote
    {
        public int Duration, Step;
        public Entity Provider;
        public bool NeedsNetwork;
        public ResultCode Code;
        public string Reason;
        public readonly List<BuildingCost> Total = new List<BuildingCost>(), Remaining = new List<BuildingCost>(), Costs = new List<BuildingCost>();
        public readonly List<RepairPayment> Payments = new List<RepairPayment>();
    }
    public static partial class BuildingCostOps
    {
        // Used by both the next-payment preview and the actual settlement, including frozen repair plans.
        public static RepairQuote QuoteRepair(EntityManager em, Entity root, Entity building)
        {
            var q = new RepairQuote { Code = ResultCode.Success, Reason = "按当前库存可支付下一期；实际在白天结算时重新核对" };
            var b = em.GetComponentData<Building>(building);
            if (b.Stage == LifeStage.Repairing)
            {
                q.Duration = math.max(1, b.RepairDuration); q.Step = b.Progress;
                foreach (var r in em.GetBuffer<RepairMaterial>(building)) Add(q.Total, r.Item, r.Amount);
            }
            else { q.Total.AddRange(RepairTotal(em, root, building, out q.Duration)); q.Step = 0; }
            foreach (var c in q.Total)
            {
                var paid = (long)(c.Amount / q.Duration) * q.Step + math.min(q.Step, c.Amount % q.Duration);
                Add(q.Remaining, c.Item, (int)math.max(0L, c.Amount - paid));
                var required = q.Step < q.Duration ? c.Amount / q.Duration + (q.Step < c.Amount % q.Duration ? 1 : 0) : 0;
                Add(q.Costs, c.Item, required);
                var pending = math.min(required, InventoryOps.PendingCount(em, root, c.Item)); var available = InventoryOps.Count(em, root, c.Item);
                var normal = required - pending; var missing = math.max(0, normal - available);
                q.Payments.Add(new RepairPayment { Item = c.Item, Required = required, Pending = pending, Normal = normal, Available = available, Missing = missing });
                q.NeedsNetwork |= normal > 0;
                if (missing > 0) { q.Code = ResultCode.InsufficientResources; q.Reason = "当期材料不足，整期不扣款、不推进"; }
            }
            if (q.NeedsNetwork) q.Provider = ResourceNetworkOps.Provider(em, root, building);
            if (q.NeedsNetwork && q.Provider == Entity.Null) { q.Code = ResultCode.Unavailable; q.Reason = "需要正常库存，但没有有效资源连接；整期暂停"; }
            if (b.RuinPending != 0) { q.Code = ResultCode.Busy; q.Reason = "等待黎明提交本夜荒废，尚不能修复"; }
            if (b.Stage != LifeStage.Ruined && b.Stage != LifeStage.Repairing) { q.Code = ResultCode.InvalidTarget; q.Reason = "建筑不处于荒废或修复状态"; }
            return q;
        }
        public static bool PayRepairStep(EntityManager em, Entity root, Entity building)
        {
            var q = QuoteRepair(em, root, building);
            if (q.Code != ResultCode.Success) { EconomyJournalOps.Note(em, root, q.Reason); return false; }
            using var payment = new InventoryTransaction(em, root);
            if (!Pay(em, root, q.Costs, true)) return false;
            foreach (var c in q.Payments) ResourceNetworkOps.Record(em, root, q.Provider, c.Item, c.Normal);
            payment.Commit(); return true;
        }
    }
}
