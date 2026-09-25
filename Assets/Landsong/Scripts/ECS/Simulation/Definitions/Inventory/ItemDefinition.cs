using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Item catalog index. The default value is an absent reference.</summary>
    public readonly struct ItemId : IEquatable<ItemId>, IComparable<ItemId>
    {
        readonly int encoded;
        ItemId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static ItemId None => default;

        public static ItemId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new ItemId(index);
        }

        public int CompareTo(ItemId other) => encoded.CompareTo(other.encoded);
        public bool Equals(ItemId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is ItemId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Item:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Item:None";
        public static bool operator ==(ItemId left, ItemId right) => left.Equals(right);
        public static bool operator !=(ItemId left, ItemId right) => !left.Equals(right);
    }

    public struct ItemDefinition
    {
        public DefinitionMetadata Metadata;
        public ItemGroupId PrimaryGroup;
        public BlobArray<ItemGroupId> AdditionalGroups;
        public int MaximumStack;
        public int TradeValue;
        public float NaturalLossRate;
        public EquipmentProfile Equipment;
        public TheftProfile Theft;
    }

    [Serializable]
    public struct EquipmentProfile
    {
        public SoldierWeaponKind Weapon;
        public float StrengthMultiplier;
        public float BreakChance;
    }

    public struct ItemCatalogBlob
    {
        public BlobArray<ItemDefinition> Definitions;
    }

    public struct ItemCatalog : IComponentData
    {
        public BlobAssetReference<ItemCatalogBlob> Value;
    }

    public static class ItemDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<ItemCatalog>(root))
                return 0;
            var blob = em.GetComponentData<ItemCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, ItemId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref ItemDefinition Get(EntityManager em, Entity root, ItemId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<ItemCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static ItemId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<ItemCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return ItemId.FromIndex(i);
            return default;
        }
    }
}
