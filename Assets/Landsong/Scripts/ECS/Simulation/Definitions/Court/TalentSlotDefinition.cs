using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local TalentSlot catalog index. The default value is an absent reference.</summary>
    public readonly struct TalentSlotId : IEquatable<TalentSlotId>, IComparable<TalentSlotId>
    {
        readonly int encoded;
        TalentSlotId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static TalentSlotId None => default;

        public static TalentSlotId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new TalentSlotId(index);
        }

        public bool Equals(TalentSlotId other) => encoded == other.encoded;
        public int CompareTo(TalentSlotId other) => encoded.CompareTo(other.encoded);
        public override bool Equals(object other) => other is TalentSlotId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "TalentSlot:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "TalentSlot:None";
        public static bool operator ==(TalentSlotId left, TalentSlotId right) => left.Equals(right);
        public static bool operator !=(TalentSlotId left, TalentSlotId right) => !left.Equals(right);
    }

    public struct TalentSlotDefinition
    {
        public DefinitionMetadata Metadata;
        public int AcceptedSpecialty;
        public BlobArray<RoyalTraitId> RequiredTraits;
        public TalentJobEffects JobEffects;
        public DefinitionEffects Effects;
    }

    public struct TalentSlotCatalogBlob
    {
        public BlobArray<TalentSlotDefinition> Definitions;
    }

    public struct TalentSlotCatalog : IComponentData
    {
        public BlobAssetReference<TalentSlotCatalogBlob> Value;
    }

    public static class TalentSlotDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<TalentSlotCatalog>(root))
                return 0;
            var blob = em.GetComponentData<TalentSlotCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, TalentSlotId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref TalentSlotDefinition Get(EntityManager em, Entity root, TalentSlotId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<TalentSlotCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static TalentSlotId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<TalentSlotCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return TalentSlotId.FromIndex(i);
            return default;
        }
    }
}
