using Landsong.ECS.Definitions;
using Unity.Collections;

namespace Landsong.ECS
{
    public enum InventoryLayoutAction : byte
    {
        SortInventory,
        StorePending,
        StorePendingSlot,
        DiscardPending,
        MoveInventoryToPending,
        DiscardSlot,
        MoveInventory,
        DiscardStored
    }

    public struct InventoryLayoutRequest : IGameRequest
    {
        public InventoryLayoutAction Action;
        public ulong SourceProvider, DestinationProvider;
        public int SourceSlot, DestinationSlot;
        public ItemId Item;
        public int Quantity;
        public FixedString128Bytes ExpectedInventory;
        public ulong Target => SourceProvider;
        public CommandKind Kind => Action switch
        {
            InventoryLayoutAction.SortInventory => CommandKind.SortInventory,
            InventoryLayoutAction.StorePending => CommandKind.StorePending,
            InventoryLayoutAction.StorePendingSlot => CommandKind.StorePendingSlot,
            InventoryLayoutAction.DiscardPending => CommandKind.DiscardPending,
            InventoryLayoutAction.MoveInventoryToPending => CommandKind.MoveInventoryToPending,
            InventoryLayoutAction.DiscardSlot => CommandKind.DiscardSlot,
            InventoryLayoutAction.MoveInventory => CommandKind.MoveInventory,
            _ => CommandKind.Discard
        };
    }

    public struct InventoryForecastRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.ForecastEconomy;
        public ulong Target => 0;
    }
}
