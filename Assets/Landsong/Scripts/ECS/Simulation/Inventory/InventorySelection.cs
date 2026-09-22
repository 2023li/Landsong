using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    // A displayed inventory source is immutable while selecting a destination or confirming loss.
    // It is a transient application request, never a persisted ECS state component.
    public readonly struct InventorySelection
    {
        public bool Pending { get; }
        public ulong Provider { get; }
        public int Slot { get; }
        public ItemId Item { get; }
        public int Quantity { get; }
        public string ExpectedInventory { get; }

        public InventorySelection(bool pending, ulong provider, int slot, ItemId item, int quantity, string expectedInventory)
        {
            Pending = pending;
            Provider = provider;
            Slot = slot;
            Item = item;
            Quantity = quantity;
            ExpectedInventory = expectedInventory;
        }
    }
}
