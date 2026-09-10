using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class FeatureOps
    {
        public static bool Unlocked(EntityManager em, Entity root, string feature)
        {
            var index = Sim.FindDefinition(em, root, new FixedString128Bytes("feature." + feature));
            return index >= 0 && Sim.HasGrant(em, root, index);
        }
        public static string Required(CommandKind kind)
        {
            switch (kind)
            {
                case CommandKind.Build: case CommandKind.BuildRoad: return "Building";
                case CommandKind.MoveInventory: case CommandKind.SortInventory: case CommandKind.StorePendingSlot: case CommandKind.DiscardSlot: case CommandKind.DiscardPending: case CommandKind.StorePending: case CommandKind.Discard: return "Inventory";
                case CommandKind.StartExpedition: case CommandKind.ClaimExpedition: case CommandKind.AbandonExpedition: return "Expedition";
                default: return null;
            }
        }
        public static bool Allowed(EntityManager em, Entity root, CommandKind kind) { var feature = Required(kind); return feature == null || Unlocked(em, root, feature); }
    }
}
