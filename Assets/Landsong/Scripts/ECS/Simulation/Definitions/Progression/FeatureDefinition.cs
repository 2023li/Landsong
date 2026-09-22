using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Feature catalog index. The default value is an absent reference.</summary>
    public readonly struct FeatureId : IEquatable<FeatureId>, IComparable<FeatureId>
    {
        readonly int encoded;
        FeatureId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static FeatureId None => default;

        public static FeatureId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new FeatureId(index);
        }

        public int CompareTo(FeatureId other) => encoded.CompareTo(other.encoded);
        public bool Equals(FeatureId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is FeatureId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Feature:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Feature:None";
        public static bool operator ==(FeatureId left, FeatureId right) => left.Equals(right);
        public static bool operator !=(FeatureId left, FeatureId right) => !left.Equals(right);
    }

    public struct FeatureDefinition
    {
        public DefinitionMetadata Metadata;
    }

    public struct FeatureCatalogBlob
    {
        public BlobArray<FeatureDefinition> Definitions;
    }

    public struct FeatureCatalog : IComponentData
    {
        public BlobAssetReference<FeatureCatalogBlob> Value;
    }

    public static class FeatureDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<FeatureCatalog>(root))
                return 0;
            var blob = em.GetComponentData<FeatureCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, FeatureId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref FeatureDefinition Get(EntityManager em, Entity root, FeatureId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<FeatureCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static FeatureId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<FeatureCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return FeatureId.FromIndex(i);
            return default;
        }
    }
}
