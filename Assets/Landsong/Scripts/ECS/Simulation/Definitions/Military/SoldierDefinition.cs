using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Soldier catalog index. The default value is an absent reference.</summary>
    public readonly struct SoldierId : IEquatable<SoldierId>, IComparable<SoldierId>
    {
        readonly int encoded;
        SoldierId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static SoldierId None => default;

        public static SoldierId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new SoldierId(index);
        }

        public bool Equals(SoldierId other) => encoded == other.encoded;
        public int CompareTo(SoldierId other) => encoded.CompareTo(other.encoded);
        public override bool Equals(object other) => other is SoldierId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Soldier:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Soldier:None";
        public static bool operator ==(SoldierId left, SoldierId right) => left.Equals(right);
        public static bool operator !=(SoldierId left, SoldierId right) => !left.Equals(right);
    }

    public struct SoldierDefinition
    {
        public DefinitionMetadata Metadata;
        public UnitCombatStats CombatStats;
        public int ThreatValue;
        public byte TargetMode;
        public int PopulationCost;
        public int FallbackRecruitGold;
        public SoldierGrowth Growth;
        public BlobArray<LeveledItemAmount> RecruitmentCosts;
    }

    public struct SoldierCatalogBlob
    {
        public BlobArray<SoldierDefinition> Definitions;
    }

    public struct SoldierCatalog : IComponentData
    {
        public BlobAssetReference<SoldierCatalogBlob> Value;
    }

    [InternalBufferCapacity(0)]
    public struct SoldierPrefab : IBufferElementData
    {
        public SoldierId Definition;
        public Entity Prefab;
    }

    public static class SoldierDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<SoldierCatalog>(root))
                return 0;
            var blob = em.GetComponentData<SoldierCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, SoldierId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref SoldierDefinition Get(EntityManager em, Entity root, SoldierId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<SoldierCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static SoldierId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<SoldierCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return SoldierId.FromIndex(i);
            return default;
        }
    }
}
