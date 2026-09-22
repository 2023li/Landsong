using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryCommandHandler
    {
        public static ResultCode Execute(EntityManager em, Entity root, InventoryLayoutRequest request)
        {
            if (request.Action == InventoryLayoutAction.DiscardStored)
                return InventoryOps.Remove(em, root, request.Item, math.max(0, request.Quantity), request.SourceProvider) ? ResultCode.Success : ResultCode.InsufficientResources;
            long before = request.Action == InventoryLayoutAction.StorePending ? PendingAmount(em, root) : 0;
            var result = InventoryLayout.Execute(em, root, request);
            if (result == ResultCode.Success && request.Action == InventoryLayoutAction.StorePending)
            {
                long remaining = PendingAmount(em, root);
                SimulationEvents.Emit(em, root, EventKind.Message, $"已入库 {before - remaining} 件，待存区剩余 {remaining} 件。");
            }

            return result;
        }

        public static ResultCode Execute(EntityManager em, Entity root, InventoryForecastRequest request)
        {
            try
            {
                return EconomyForecastOps.Create(em, root);
            }
            catch (System.Exception error)
            {
                UnityEngine.Debug.LogException(error);
                return ResultCode.PreparationFailed;
            }
        }

        static long PendingAmount(EntityManager em, Entity root)
        {
            long amount = 0;
            foreach (var item in em.GetBuffer<PendingItem>(root))
                amount += item.Amount;
            return amount;
        }
    }
}
