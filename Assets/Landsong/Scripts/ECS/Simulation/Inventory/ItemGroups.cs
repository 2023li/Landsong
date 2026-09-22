using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class ItemGroups
    {
        public static bool Matches(EntityManager em, Entity root, ItemId item, ItemGroupId group)
        {
            if (!group.IsValid)
                return true;
            if (!ItemDefinitions.IsValid(em, root, item))
                return false;
            ref var definition = ref ItemDefinitions.Get(em, root, item);
            if (ItemGroups.GroupDescendsFrom(em, root, definition.PrimaryGroup, group))
                return true;
            for (int i = 0; i < definition.AdditionalGroups.Length; i++)
                if (ItemGroups.GroupDescendsFrom(em, root, definition.AdditionalGroups[i], group))
                    return true;
            return false;
        }

        internal static bool GroupDescendsFrom(EntityManager em, Entity root, ItemGroupId candidate, ItemGroupId group)
        {
            int guard = 0;
            while (ItemGroupDefinitions.IsValid(em, root, candidate) && guard++ < 64)
            {
                if (candidate == group)
                    return true;
                candidate = ItemGroupDefinitions.Get(em, root, candidate).ParentGroup;
            }

            return false;
        }
    }
}
