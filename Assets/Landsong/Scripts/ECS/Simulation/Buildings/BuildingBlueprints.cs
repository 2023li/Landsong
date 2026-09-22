using System;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct BlueprintUnlock : IBufferElementData
    {
        public BuildingId Building;
        public int MaximumLevel;
    }

    public static class BuildingBlueprints
    {
        public static int Level(EntityManager em, Entity root, BuildingId building)
        {
            if (!BuildingDefinitions.IsValid(em, root, building))
                return 0;
            foreach (var entry in em.GetBuffer<BlueprintUnlock>(root))
                if (entry.Building == building)
                    return entry.MaximumLevel;
            return 0;
        }

        public static bool Has(EntityManager em, Entity root, BuildingId building, int requiredLevel = 1) => requiredLevel > 0 && Level(em, root, building) >= requiredLevel;
        public static void Grant(EntityManager em, Entity root, BuildingId building, int level)
        {
            ref var definition = ref BuildingDefinitions.Get(em, root, building);
            if (level <= 0 || level > definition.MaximumLevel)
                throw new ArgumentOutOfRangeException(nameof(level), "蓝图等级必须处于建筑支持的等级范围。");
            var entries = em.GetBuffer<BlueprintUnlock>(root);
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry.Building != building)
                    continue;
                if (entry.MaximumLevel < level)
                {
                    entry.MaximumLevel = level;
                    entries[index] = entry;
                }

                return;
            }

            entries.Add(new BlueprintUnlock { Building = building, MaximumLevel = level });
        }
    }
}
