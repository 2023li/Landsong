using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local PolicyGroup catalog index. The default value is an absent reference.</summary>
    public readonly struct PolicyGroupId : IEquatable<PolicyGroupId>, IComparable<PolicyGroupId>
    {
        readonly int encoded;
        PolicyGroupId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static PolicyGroupId None => default;

        public static PolicyGroupId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new PolicyGroupId(index);
        }

        public int CompareTo(PolicyGroupId other) => encoded.CompareTo(other.encoded);
        public bool Equals(PolicyGroupId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is PolicyGroupId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "PolicyGroup:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "PolicyGroup:None";
        public static bool operator ==(PolicyGroupId left, PolicyGroupId right) => left.Equals(right);
        public static bool operator !=(PolicyGroupId left, PolicyGroupId right) => !left.Equals(right);
    }

    public struct PolicyGroupDefinition
    {
        public DefinitionMetadata Metadata;
    }

    public struct PolicyGroupCatalogBlob
    {
        public BlobArray<PolicyGroupDefinition> Definitions;
    }

    public struct PolicyGroupCatalog : IComponentData
    {
        public BlobAssetReference<PolicyGroupCatalogBlob> Value;
    }

    public static class PolicyGroupDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<PolicyGroupCatalog>(root))
                return 0;
            var blob = em.GetComponentData<PolicyGroupCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, PolicyGroupId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref PolicyGroupDefinition Get(EntityManager em, Entity root, PolicyGroupId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<PolicyGroupCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static PolicyGroupId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<PolicyGroupCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return PolicyGroupId.FromIndex(i);
            return default;
        }
    }
}
