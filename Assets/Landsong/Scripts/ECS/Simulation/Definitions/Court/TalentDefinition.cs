using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Talent catalog index. The default value is an absent reference.</summary>
    public readonly struct TalentId : IEquatable<TalentId>, IComparable<TalentId>
    {
        readonly int encoded;
        TalentId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static TalentId None => default;

        public static TalentId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new TalentId(index);
        }

        public bool Equals(TalentId other) => encoded == other.encoded;
        public int CompareTo(TalentId other) => encoded.CompareTo(other.encoded);
        public override bool Equals(object other) => other is TalentId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Talent:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Talent:None";
        public static bool operator ==(TalentId left, TalentId right) => left.Equals(right);
        public static bool operator !=(TalentId left, TalentId right) => !left.Equals(right);
    }

    public struct TalentDefinition
    {
        public DefinitionMetadata Metadata;
        public int InitialLevel;
        public int MaximumLevel;
        public int BaseLevelExperience;
        public int LevelExperienceIncrement;
        public int Specialty;
        public TalentWage Wage;
        public BlobArray<RoyalTraitId> InitialTraits;
        public BlobArray<RoyalTraitId> ConflictingTraits;
        public BlobArray<RoyalTraitId> RequiredTraits;
        public DefinitionPrerequisites Prerequisites;
        public BlobArray<TalentSocialTask> SocialTasks;
        public TalentPeriodicIncome PeriodicIncome;
        public TalentJobEffects JobEffects;
        public DefinitionEffects Effects;
    }

    public struct TalentCatalogBlob
    {
        public BlobArray<TalentDefinition> Definitions;
    }

    public struct TalentCatalog : IComponentData
    {
        public BlobAssetReference<TalentCatalogBlob> Value;
    }

    public static class TalentDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<TalentCatalog>(root))
                return 0;
            var blob = em.GetComponentData<TalentCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, TalentId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref TalentDefinition Get(EntityManager em, Entity root, TalentId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<TalentCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static TalentId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<TalentCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return TalentId.FromIndex(i);
            return default;
        }
    }
}
