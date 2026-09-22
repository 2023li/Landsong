using System;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct OwnedBuff : IBufferElementData
    {
        public BuffId Buff;
        public int Level;
    }

    public static class PermanentBuffs
    {
        public static int Level(EntityManager em, Entity root, BuffId buff)
        {
            if (!BuffDefinitions.IsValid(em, root, buff))
                return 0;
            foreach (var entry in em.GetBuffer<OwnedBuff>(root))
                if (entry.Buff == buff)
                    return entry.Level;
            return 0;
        }

        public static void Grant(EntityManager em, Entity root, BuffId buff, int level)
        {
            if (!BuffDefinitions.IsValid(em, root, buff))
                throw new ArgumentOutOfRangeException(nameof(buff));
            if (level <= 0)
                throw new ArgumentOutOfRangeException(nameof(level));
            var entries = em.GetBuffer<OwnedBuff>(root);
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                if (entry.Buff != buff)
                    continue;
                if (entry.Level < level)
                {
                    entry.Level = level;
                    entries[index] = entry;
                }

                return;
            }

            entries.Add(new OwnedBuff { Buff = buff, Level = level });
        }
    }
}
