using Unity.Collections;
using Unity.Entities;
using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    public static class FeatureOps
    {
        public static bool Unlocked(EntityManager em, Entity root, string feature)
        {
            var index = FeatureDefinitions.Find(em, root, new FixedString128Bytes("feature." + feature));
            return IsUnlocked(em, root, index);
        }

        public static bool IsUnlocked(EntityManager em, Entity root, FeatureId definition) => FeatureUnlocks.Has(em, root, definition);
        public static void Unlock(EntityManager em, Entity root, FeatureId definition) => FeatureUnlocks.Unlock(em, root, definition);
        public static string Required(CommandKind kind)
        {
            switch (kind)
            {
                case CommandKind.Build:
                case CommandKind.BuildRoad:
                    return "Building";
                case CommandKind.MoveInventoryToPending:
                case CommandKind.MoveInventory:
                case CommandKind.SortInventory:
                case CommandKind.StorePendingSlot:
                case CommandKind.DiscardSlot:
                case CommandKind.DiscardPending:
                case CommandKind.StorePending:
                case CommandKind.Discard:
                    return "Inventory";
                case CommandKind.StartExpedition:
                case CommandKind.ClaimExpedition:
                case CommandKind.AbandonExpedition:
                    return "Expedition";
                case CommandKind.RecruitTalent:
                case CommandKind.AssignTalent:
                case CommandKind.DismissTalent:
                case CommandKind.RefreshTalents:
                case CommandKind.Abdicate:
                case CommandKind.SelectPolicy:
                case CommandKind.CancelPolicy:
                case CommandKind.GiftPerson:
                case CommandKind.CompleteSocialTask:
                case CommandKind.ProposeMarriage:
                case CommandKind.DesignateHeir:
                case CommandKind.ExecuteHeir:
                case CommandKind.RoyalVisit:
                case CommandKind.ResolveMarriage:
                case CommandKind.PrepareMarriage:
                case CommandKind.ArrangeMarriage:
                case CommandKind.RefusePersonRequest:
                case CommandKind.CustomizePortrait:
                    return "Royal";
                default:
                    return null;
            }
        }

        public static bool Allowed(EntityManager em, Entity root, CommandKind kind)
        {
            var feature = Required(kind);
            return feature == null || Unlocked(em, root, feature);
        }
    }
}
