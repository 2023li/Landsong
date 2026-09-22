using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Buff catalog index. The default value is an absent reference.</summary>
    public readonly struct BuffId : IEquatable<BuffId>, IComparable<BuffId>
    {
        readonly int encoded;
        BuffId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static BuffId None => default;

        public static BuffId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new BuffId(index);
        }

        public int CompareTo(BuffId other) => encoded.CompareTo(other.encoded);
        public bool Equals(BuffId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is BuffId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Buff:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Buff:None";
        public static bool operator ==(BuffId left, BuffId right) => left.Equals(right);
        public static bool operator !=(BuffId left, BuffId right) => !left.Equals(right);
    }

    public struct BuffDefinition
    {
        public DefinitionMetadata Metadata;
        public DefinitionEffects Effects;
    }

    public struct BuffCatalogBlob
    {
        public BlobArray<BuffDefinition> Definitions;
    }

    public struct BuffCatalog : IComponentData
    {
        public BlobAssetReference<BuffCatalogBlob> Value;
    }

    public static class BuffDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<BuffCatalog>(root))
                return 0;
            var blob = em.GetComponentData<BuffCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, BuffId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref BuffDefinition Get(EntityManager em, Entity root, BuffId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<BuffCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static BuffId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<BuffCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return BuffId.FromIndex(i);
            return default;
        }
    }
}
