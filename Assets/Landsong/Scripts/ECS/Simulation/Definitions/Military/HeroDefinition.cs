using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Hero catalog index. The default value is an absent reference.</summary>
    public readonly struct HeroId : IEquatable<HeroId>, IComparable<HeroId>
    {
        readonly int encoded;
        HeroId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static HeroId None => default;

        public static HeroId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new HeroId(index);
        }

        public bool Equals(HeroId other) => encoded == other.encoded;
        public int CompareTo(HeroId other) => encoded.CompareTo(other.encoded);
        public override bool Equals(object other) => other is HeroId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Hero:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Hero:None";
        public static bool operator ==(HeroId left, HeroId right) => left.Equals(right);
        public static bool operator !=(HeroId left, HeroId right) => !left.Equals(right);
    }

    public struct HeroDefinition
    {
        public DefinitionMetadata Metadata;
        public UnitCombatStats CombatStats;
        public int ThreatValue;
        public int NightPower;
        public byte TargetMode;
        public int PopulationCost;
        public int FallbackWakeGold;
        public int RevivalCooldownTurns;
        public HeroGrowth Growth;
        public BlobArray<LeveledItemAmount> AwakeningCosts;
        public BlobArray<LeveledItemAmount> OfferingCosts;
    }

    public struct HeroCatalogBlob
    {
        public BlobArray<HeroDefinition> Definitions;
    }

    public struct HeroCatalog : IComponentData
    {
        public BlobAssetReference<HeroCatalogBlob> Value;
    }

    [InternalBufferCapacity(0)]
    public struct HeroPrefab : IBufferElementData
    {
        public HeroId Definition;
        public Entity Prefab;
    }

    public static class HeroDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<HeroCatalog>(root))
                return 0;
            var blob = em.GetComponentData<HeroCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, HeroId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref HeroDefinition Get(EntityManager em, Entity root, HeroId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<HeroCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static HeroId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<HeroCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return HeroId.FromIndex(i);
            return default;
        }
    }
}
