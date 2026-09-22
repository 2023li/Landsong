using System;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct UnlockedFeature : IBufferElementData
    {
        public FeatureId Feature;
    }

    public static class FeatureUnlocks
    {
        public static bool Has(EntityManager em, Entity root, FeatureId feature)
        {
            if (!FeatureDefinitions.IsValid(em, root, feature))
                return false;
            foreach (var entry in em.GetBuffer<UnlockedFeature>(root))
                if (entry.Feature == feature)
                    return true;
            return false;
        }

        public static void Unlock(EntityManager em, Entity root, FeatureId feature)
        {
            if (!FeatureDefinitions.IsValid(em, root, feature))
                throw new ArgumentOutOfRangeException(nameof(feature));
            if (!Has(em, root, feature))
                em.GetBuffer<UnlockedFeature>(root).Add(new UnlockedFeature { Feature = feature });
        }
    }
}
