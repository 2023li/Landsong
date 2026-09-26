using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public enum BuildingFaction : byte
    {
        [Sirenix.OdinInspector.LabelText("己方")] Settlement = 0,
        [Sirenix.OdinInspector.LabelText("敌方")] Invaders = 1,
        [Sirenix.OdinInspector.LabelText("中立")] Neutral = 2
    }

    public static class BuildingFactionOps
    {
        public static byte Of(EntityManager em, Entity root, Entity building)
            => (byte)BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(building).Definition).Faction;

        public static bool Hostile(byte attacker, byte target)
            => target != (byte)BuildingFaction.Neutral && attacker != target;
    }

    /// <summary>A local Building catalog index. The default value is an absent reference.</summary>
    public readonly struct BuildingId : IEquatable<BuildingId>, IComparable<BuildingId>
    {
        readonly int encoded;
        BuildingId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static BuildingId None => default;

        public static BuildingId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new BuildingId(index);
        }

        public int CompareTo(BuildingId other) => encoded.CompareTo(other.encoded);
        public bool Equals(BuildingId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is BuildingId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Building:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Building:None";
        public static bool operator ==(BuildingId left, BuildingId right) => left.Equals(right);
        public static bool operator !=(BuildingId left, BuildingId right) => !left.Equals(right);
    }

    public struct BuildingDefinition
    {
        public DefinitionMetadata Metadata;
        public BuildingFaction Faction;
        public BuildingLimitGroupId LimitGroup;
        public int MaximumLevel;
        public int ConstructionTurns;
        public int ResourceConnectionActionPower;
        public int MaximumCount;
        public int2 Footprint;
        public float MaximumDurability;
        public int NightPower;
        public float MovementCost;
        public UnitCombatStats DefenseStats;
        public BuildingPlacementAndVisuals PlacementAndVisuals;
        public BuildingCapabilities Capabilities;
    }

    public struct BuildingCatalogBlob
    {
        public BlobArray<BuildingDefinition> Definitions;
    }

    public struct BuildingCatalog : IComponentData
    {
        public BlobAssetReference<BuildingCatalogBlob> Value;
    }

    [InternalBufferCapacity(0)]
    public struct BuildingPrefab : IBufferElementData
    {
        public BuildingId Definition;
        public Entity Prefab;
    }

    public static class BuildingDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<BuildingCatalog>(root))
                return 0;
            var blob = em.GetComponentData<BuildingCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, BuildingId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref BuildingDefinition Get(EntityManager em, Entity root, BuildingId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<BuildingCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static BuildingId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<BuildingCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return BuildingId.FromIndex(i);
            return default;
        }
    }
}
