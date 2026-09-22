using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local RoyalTrait catalog index. The default value is an absent reference.</summary>
    public readonly struct RoyalTraitId : IEquatable<RoyalTraitId>, IComparable<RoyalTraitId>
    {
        readonly int encoded;
        RoyalTraitId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static RoyalTraitId None => default;

        public static RoyalTraitId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new RoyalTraitId(index);
        }

        public int CompareTo(RoyalTraitId other) => encoded.CompareTo(other.encoded);
        public bool Equals(RoyalTraitId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is RoyalTraitId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "RoyalTrait:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "RoyalTrait:None";
        public static bool operator ==(RoyalTraitId left, RoyalTraitId right) => left.Equals(right);
        public static bool operator !=(RoyalTraitId left, RoyalTraitId right) => !left.Equals(right);
    }

    public struct RoyalTraitDefinition
    {
        public DefinitionMetadata Metadata;
        public int RevealAge;
        public int MinimumActivationAge;
        public bool Heritable;
        public float InheritanceChance;
        public BlobArray<RoyalTraitId> GrantedTraits;
        public BlobArray<RoyalTraitId> ConflictingTraits;
        public BlobArray<RoyalTraitId> RequiredTraits;
        public DefinitionPrerequisites Prerequisites;
        public DefinitionEffects Effects;
    }

    public struct RoyalTraitCatalogBlob
    {
        public BlobArray<RoyalTraitDefinition> Definitions;
    }

    public struct RoyalTraitCatalog : IComponentData
    {
        public BlobAssetReference<RoyalTraitCatalogBlob> Value;
    }

    public static class RoyalTraitDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<RoyalTraitCatalog>(root))
                return 0;
            var blob = em.GetComponentData<RoyalTraitCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, RoyalTraitId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref RoyalTraitDefinition Get(EntityManager em, Entity root, RoyalTraitId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<RoyalTraitCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static RoyalTraitId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<RoyalTraitCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return RoyalTraitId.FromIndex(i);
            return default;
        }
    }
}
