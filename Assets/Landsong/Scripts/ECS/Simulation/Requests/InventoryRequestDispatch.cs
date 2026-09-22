using Unity.Entities;

namespace Landsong.ECS
{
    internal static class InventoryRequestDispatch
    {
        internal static bool TryExecute(EntityManager em, Entity root, Entity payload, out ResultCode result)
        {
            if (em.HasComponent<InventoryLayoutRequest>(payload))
            {
                result = InventoryCommandHandler.Execute(em, root, em.GetComponentData<InventoryLayoutRequest>(payload));
                return true;
            }

            if (em.HasComponent<InventoryForecastRequest>(payload))
            {
                result = InventoryCommandHandler.Execute(em, root, default(InventoryForecastRequest));
                return true;
            }

            result = ResultCode.Unavailable;
            return false;
        }
    }
}
