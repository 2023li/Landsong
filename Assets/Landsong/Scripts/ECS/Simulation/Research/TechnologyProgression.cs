using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct TechnologyProgress : IBufferElementData
    {
        public TechnologyId Technology;
        public int ResearchPoints;
        public int Completions;
        public int QueueOrder;
    }

    public static class TechnologyProgression
    {
        public static TechnologyProgress Read(EntityManager em, Entity root, TechnologyId technology)
        {
            foreach (var entry in em.GetBuffer<TechnologyProgress>(root))
                if (entry.Technology == technology)
                    return entry;
            return new TechnologyProgress
            {
                Technology = technology
            };
        }

        internal static void Write(EntityManager em, Entity root, TechnologyProgress progress)
        {
            var entries = em.GetBuffer<TechnologyProgress>(root);
            for (var index = 0; index < entries.Length; index++)
                if (entries[index].Technology == progress.Technology)
                {
                    entries[index] = progress;
                    return;
                }

            entries.Add(progress);
        }
    }
}
