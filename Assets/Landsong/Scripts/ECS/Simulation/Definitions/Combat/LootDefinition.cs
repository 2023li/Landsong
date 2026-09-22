using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Loot catalog index. The default value is an absent reference.</summary>
    public readonly struct LootId : IEquatable<LootId>, IComparable<LootId>
    {
        readonly int encoded;
        LootId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static LootId None => default;

        public static LootId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new LootId(index);
        }

        public int CompareTo(LootId other) => encoded.CompareTo(other.encoded);
        public bool Equals(LootId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is LootId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Loot:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Loot:None";
        public static bool operator ==(LootId left, LootId right) => left.Equals(right);
        public static bool operator !=(LootId left, LootId right) => !left.Equals(right);
    }

    public struct LootDefinition
    {
        public DefinitionMetadata Metadata;
        public DefinitionRewards Rewards;
    }

    public struct LootCatalogBlob
    {
        public BlobArray<LootDefinition> Definitions;
    }

    public struct LootCatalog : IComponentData
    {
        public BlobAssetReference<LootCatalogBlob> Value;
    }

    [InternalBufferCapacity(0)]
    public struct LootPrefab : IBufferElementData
    {
        public LootId Definition;
        public Entity Prefab;
    }

    public static class LootDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<LootCatalog>(root))
                return 0;
            var blob = em.GetComponentData<LootCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, LootId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref LootDefinition Get(EntityManager em, Entity root, LootId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<LootCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static LootId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<LootCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return LootId.FromIndex(i);
            return default;
        }
    }
}
