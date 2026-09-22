using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local StorageSlot catalog index. The default value is an absent reference.</summary>
    public readonly struct StorageSlotId : IEquatable<StorageSlotId>, IComparable<StorageSlotId>
    {
        readonly int encoded;
        StorageSlotId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static StorageSlotId None => default;

        public static StorageSlotId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new StorageSlotId(index);
        }

        public int CompareTo(StorageSlotId other) => encoded.CompareTo(other.encoded);
        public bool Equals(StorageSlotId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is StorageSlotId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "StorageSlot:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "StorageSlot:None";
        public static bool operator ==(StorageSlotId left, StorageSlotId right) => left.Equals(right);
        public static bool operator !=(StorageSlotId left, StorageSlotId right) => !left.Equals(right);
    }

    public struct StorageSlotDefinition
    {
        public DefinitionMetadata Metadata;
        public float DefaultLossMultiplier;
        public BlobArray<ItemId> AcceptedItems;
        public BlobArray<ItemGroupId> AcceptedGroups;
        public BlobArray<ItemLossOverride> ItemLosses;
        public BlobArray<ItemGroupLossOverride> GroupLosses;
    }

    public struct StorageSlotCatalogBlob
    {
        public BlobArray<StorageSlotDefinition> Definitions;
    }

    public struct StorageSlotCatalog : IComponentData
    {
        public BlobAssetReference<StorageSlotCatalogBlob> Value;
    }

    public static class StorageSlotDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<StorageSlotCatalog>(root))
                return 0;
            var blob = em.GetComponentData<StorageSlotCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, StorageSlotId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref StorageSlotDefinition Get(EntityManager em, Entity root, StorageSlotId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<StorageSlotCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static StorageSlotId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<StorageSlotCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return StorageSlotId.FromIndex(i);
            return default;
        }
    }
}
