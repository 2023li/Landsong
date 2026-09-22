using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Policy catalog index. The default value is an absent reference.</summary>
    public readonly struct PolicyId : IEquatable<PolicyId>, IComparable<PolicyId>
    {
        readonly int encoded;
        PolicyId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static PolicyId None => default;

        public static PolicyId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new PolicyId(index);
        }

        public int CompareTo(PolicyId other) => encoded.CompareTo(other.encoded);
        public bool Equals(PolicyId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is PolicyId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Policy:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Policy:None";
        public static bool operator ==(PolicyId left, PolicyId right) => left.Equals(right);
        public static bool operator !=(PolicyId left, PolicyId right) => !left.Equals(right);
    }

    public struct PolicyDefinition
    {
        public DefinitionMetadata Metadata;
        public PolicyGroupId PolicyGroup;
        public int PolicyTier;
        public int RequiredPublicOpinion;
        public DefinitionPrerequisites Prerequisites;
        public DefinitionEffects Effects;
    }

    public struct PolicyCatalogBlob
    {
        public BlobArray<PolicyDefinition> Definitions;
    }

    public struct PolicyCatalog : IComponentData
    {
        public BlobAssetReference<PolicyCatalogBlob> Value;
    }

    public static class PolicyDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<PolicyCatalog>(root))
                return 0;
            var blob = em.GetComponentData<PolicyCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, PolicyId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref PolicyDefinition Get(EntityManager em, Entity root, PolicyId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<PolicyCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static PolicyId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<PolicyCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return PolicyId.FromIndex(i);
            return default;
        }
    }
}
