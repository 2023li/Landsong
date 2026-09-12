using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public static class FeatureOps
    {
        public static bool Unlocked(EntityManager em, Entity root, string feature)
        {
            var index = Sim.FindDefinition(em, root, new FixedString128Bytes("feature." + feature));
            return IsUnlocked(em, root, index);
        }
        public static bool IsUnlocked(EntityManager em, Entity root, int definition)
            => Sim.ValidDefinition(em, root, definition) && Sim.Definition(em, root, definition).Kind == ContentKind.Feature
                && EntitlementStore.Level(em, root, definition) == 1;
        public static void Unlock(EntityManager em, Entity root, int definition)
            => EntitlementStore.Put(em, root, definition, 1, ContentKind.Feature);
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
