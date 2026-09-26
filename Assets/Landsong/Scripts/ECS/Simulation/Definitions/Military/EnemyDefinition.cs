using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Enemy catalog index. The default value is an absent reference.</summary>
    public readonly struct EnemyId : IEquatable<EnemyId>, IComparable<EnemyId>
    {
        readonly int encoded;
        EnemyId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static EnemyId None => default;

        public static EnemyId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new EnemyId(index);
        }

        public int CompareTo(EnemyId other) => encoded.CompareTo(other.encoded);
        public bool Equals(EnemyId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is EnemyId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Enemy:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Enemy:None";
        public static bool operator ==(EnemyId left, EnemyId right) => left.Equals(right);
        public static bool operator !=(EnemyId left, EnemyId right) => !left.Equals(right);
    }

    public struct EnemyDefinition
    {
        public DefinitionMetadata Metadata;
        public UnitCombatStats CombatStats;
        public int ThreatValue;
        public int NightPower;
        public EnemyBehaviorFlags Behavior;
        public BuildingCategory PreferredTargetCategory;
        public DefinitionRewards KillRewards;
        public BlobArray<SpecialItemDrop> SpecialDrops;
    }

    public struct EnemyCatalogBlob
    {
        public BlobArray<EnemyDefinition> Definitions;
    }

    public struct EnemyCatalog : IComponentData
    {
        public BlobAssetReference<EnemyCatalogBlob> Value;
    }

    [InternalBufferCapacity(0)]
    public struct EnemyPrefab : IBufferElementData
    {
        public EnemyId Definition;
        public Entity Prefab;
    }

    public static class EnemyDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<EnemyCatalog>(root))
                return 0;
            var blob = em.GetComponentData<EnemyCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, EnemyId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref EnemyDefinition Get(EntityManager em, Entity root, EnemyId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<EnemyCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static EnemyId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<EnemyCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return EnemyId.FromIndex(i);
            return default;
        }
    }
}
