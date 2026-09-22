using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Expedition catalog index. The default value is an absent reference.</summary>
    public readonly struct ExpeditionId : IEquatable<ExpeditionId>, IComparable<ExpeditionId>
    {
        readonly int encoded;
        ExpeditionId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static ExpeditionId None => default;

        public static ExpeditionId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new ExpeditionId(index);
        }

        public int CompareTo(ExpeditionId other) => encoded.CompareTo(other.encoded);
        public bool Equals(ExpeditionId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is ExpeditionId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Expedition:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Expedition:None";
        public static bool operator ==(ExpeditionId left, ExpeditionId right) => left.Equals(right);
        public static bool operator !=(ExpeditionId left, ExpeditionId right) => !left.Equals(right);
    }

    public struct ExpeditionDefinition
    {
        public DefinitionMetadata Metadata;
        public int MinimumSiteLevel;
        public int MinimumCrew;
        public int MaximumCrew;
        public int TravelTurns;
        public float BaseSuccessChance;
        public float SuccessChancePerCrew;
        public float MaximumSuccessChance;
        public float FailureCasualtyRatio;
        public int BaseCompensation;
        public int CompensationPerCrew;
        public bool Repeatable;
        public DefinitionPrerequisites Prerequisites;
        public DefinitionPrerequisites Visibility;
        public BlobArray<ExpeditionSupply> Supplies;
        public DefinitionRewards Rewards;
        public BlobArray<ItemAmount> FailurePenalties;
    }

    public struct ExpeditionCatalogBlob
    {
        public BlobArray<ExpeditionDefinition> Definitions;
    }

    public struct ExpeditionCatalog : IComponentData
    {
        public BlobAssetReference<ExpeditionCatalogBlob> Value;
    }

    public static class ExpeditionDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<ExpeditionCatalog>(root))
                return 0;
            var blob = em.GetComponentData<ExpeditionCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, ExpeditionId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref ExpeditionDefinition Get(EntityManager em, Entity root, ExpeditionId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<ExpeditionCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static ExpeditionId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<ExpeditionCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return ExpeditionId.FromIndex(i);
            return default;
        }
    }
}
