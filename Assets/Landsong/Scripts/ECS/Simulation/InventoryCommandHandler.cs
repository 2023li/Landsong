using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryCommandHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Command command, out ResultCode result)
        {
            switch (command.Kind)
            {
                case CommandKind.MoveInventoryToPending: case CommandKind.MoveInventory: case CommandKind.SortInventory: case CommandKind.StorePendingSlot:
                case CommandKind.DiscardSlot: case CommandKind.DiscardPending:
                    result = InventoryOps.LayoutCommand(em, root, command); break;
                case CommandKind.StorePending:
                    long before = PendingAmount(em, root);
                    result = InventoryOps.LayoutCommand(em, root, command);
                    if (result == ResultCode.Success)
                    {
                        long remaining = PendingAmount(em, root);
                        Sim.Emit(em, root, EventKind.Message, $"已入库 {before - remaining} 件，待存区剩余 {remaining} 件。");
                    }
                    break;
                case CommandKind.Discard:
                    result = InventoryOps.Remove(em, root, command.Definition, math.max(0, command.Amount), command.Target)
                        ? ResultCode.Success : ResultCode.InsufficientResources; break;
                case CommandKind.ForecastEconomy:
                    try { result = EconomyForecastOps.Create(em, root); }
                    catch (System.Exception error) { UnityEngine.Debug.LogException(error); result = ResultCode.PreparationFailed; }
                    break;
                default: result = ResultCode.Unavailable; return false;
            }
            return true;
        }
        static long PendingAmount(EntityManager em, Entity root)
        { long amount = 0; foreach (var item in em.GetBuffer<PendingItem>(root)) amount += item.Amount; return amount; }
    }
}
