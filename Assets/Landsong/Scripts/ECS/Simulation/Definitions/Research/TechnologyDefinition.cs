using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Technology catalog index. The default value is an absent reference.</summary>
    public readonly struct TechnologyId : IEquatable<TechnologyId>, IComparable<TechnologyId>
    {
        readonly int encoded;
        TechnologyId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static TechnologyId None => default;

        public static TechnologyId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new TechnologyId(index);
        }

        public int CompareTo(TechnologyId other) => encoded.CompareTo(other.encoded);
        public bool Equals(TechnologyId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is TechnologyId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Technology:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Technology:None";
        public static bool operator ==(TechnologyId left, TechnologyId right) => left.Equals(right);
        public static bool operator !=(TechnologyId left, TechnologyId right) => !left.Equals(right);
    }

    public struct TechnologyDefinition
    {
        public DefinitionMetadata Metadata;
        public int ResearchPointCost;
        public bool Repeatable;
        public DefinitionPrerequisites Prerequisites;
        public DefinitionRewards Rewards;
        public DefinitionEffects Effects;
    }

    public struct TechnologyCatalogBlob
    {
        public BlobArray<TechnologyDefinition> Definitions;
    }

    public struct TechnologyCatalog : IComponentData
    {
        public BlobAssetReference<TechnologyCatalogBlob> Value;
    }

    public static class TechnologyDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<TechnologyCatalog>(root))
                return 0;
            var blob = em.GetComponentData<TechnologyCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, TechnologyId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref TechnologyDefinition Get(EntityManager em, Entity root, TechnologyId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<TechnologyCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static TechnologyId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<TechnologyCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return TechnologyId.FromIndex(i);
            return default;
        }
    }
}
