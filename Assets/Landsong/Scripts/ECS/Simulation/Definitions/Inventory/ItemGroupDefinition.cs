using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local ItemGroup catalog index. The default value is an absent reference.</summary>
    public readonly struct ItemGroupId : IEquatable<ItemGroupId>, IComparable<ItemGroupId>
    {
        readonly int encoded;
        ItemGroupId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static ItemGroupId None => default;

        public static ItemGroupId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new ItemGroupId(index);
        }

        public int CompareTo(ItemGroupId other) => encoded.CompareTo(other.encoded);
        public bool Equals(ItemGroupId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is ItemGroupId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "ItemGroup:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "ItemGroup:None";
        public static bool operator ==(ItemGroupId left, ItemGroupId right) => left.Equals(right);
        public static bool operator !=(ItemGroupId left, ItemGroupId right) => !left.Equals(right);
    }

    public struct ItemGroupDefinition
    {
        public DefinitionMetadata Metadata;
        public ItemGroupId ParentGroup;
    }

    public struct ItemGroupCatalogBlob
    {
        public BlobArray<ItemGroupDefinition> Definitions;
    }

    public struct ItemGroupCatalog : IComponentData
    {
        public BlobAssetReference<ItemGroupCatalogBlob> Value;
    }

    public static class ItemGroupDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<ItemGroupCatalog>(root))
                return 0;
            var blob = em.GetComponentData<ItemGroupCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, ItemGroupId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref ItemGroupDefinition Get(EntityManager em, Entity root, ItemGroupId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<ItemGroupCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static ItemGroupId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<ItemGroupCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return ItemGroupId.FromIndex(i);
            return default;
        }
    }
}
