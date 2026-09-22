using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Opportunity catalog index. The default value is an absent reference.</summary>
    public readonly struct OpportunityId : IEquatable<OpportunityId>, IComparable<OpportunityId>
    {
        readonly int encoded;
        OpportunityId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static OpportunityId None => default;

        public static OpportunityId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new OpportunityId(index);
        }

        public int CompareTo(OpportunityId other) => encoded.CompareTo(other.encoded);
        public bool Equals(OpportunityId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is OpportunityId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Opportunity:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Opportunity:None";
        public static bool operator ==(OpportunityId left, OpportunityId right) => left.Equals(right);
        public static bool operator !=(OpportunityId left, OpportunityId right) => !left.Equals(right);
    }

    public struct OpportunityDefinition
    {
        public DefinitionMetadata Metadata;
        public OpportunityProfile VisitorProfile;
        public DefinitionRewards Rewards;
    }

    public struct OpportunityCatalogBlob
    {
        public BlobArray<OpportunityDefinition> Definitions;
    }

    public struct OpportunityCatalog : IComponentData
    {
        public BlobAssetReference<OpportunityCatalogBlob> Value;
    }

    [InternalBufferCapacity(0)]
    public struct OpportunityPrefab : IBufferElementData
    {
        public OpportunityId Definition;
        public Entity Prefab;
    }

    public static class OpportunityDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<OpportunityCatalog>(root))
                return 0;
            var blob = em.GetComponentData<OpportunityCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, OpportunityId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref OpportunityDefinition Get(EntityManager em, Entity root, OpportunityId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<OpportunityCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static OpportunityId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<OpportunityCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return OpportunityId.FromIndex(i);
            return default;
        }
    }
}
