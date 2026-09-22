using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local BuildingLimitGroup catalog index. The default value is an absent reference.</summary>
    public readonly struct BuildingLimitGroupId : IEquatable<BuildingLimitGroupId>, IComparable<BuildingLimitGroupId>
    {
        readonly int encoded;
        BuildingLimitGroupId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static BuildingLimitGroupId None => default;

        public static BuildingLimitGroupId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new BuildingLimitGroupId(index);
        }

        public int CompareTo(BuildingLimitGroupId other) => encoded.CompareTo(other.encoded);
        public bool Equals(BuildingLimitGroupId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is BuildingLimitGroupId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "BuildingLimitGroup:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "BuildingLimitGroup:None";
        public static bool operator ==(BuildingLimitGroupId left, BuildingLimitGroupId right) => left.Equals(right);
        public static bool operator !=(BuildingLimitGroupId left, BuildingLimitGroupId right) => !left.Equals(right);
    }

    public struct BuildingLimitGroupDefinition
    {
        public DefinitionMetadata Metadata;
    }

    public struct BuildingLimitGroupCatalogBlob
    {
        public BlobArray<BuildingLimitGroupDefinition> Definitions;
    }

    public struct BuildingLimitGroupCatalog : IComponentData
    {
        public BlobAssetReference<BuildingLimitGroupCatalogBlob> Value;
    }

    public static class BuildingLimitGroupDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<BuildingLimitGroupCatalog>(root))
                return 0;
            var blob = em.GetComponentData<BuildingLimitGroupCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, BuildingLimitGroupId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref BuildingLimitGroupDefinition Get(EntityManager em, Entity root, BuildingLimitGroupId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<BuildingLimitGroupCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static BuildingLimitGroupId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<BuildingLimitGroupCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return BuildingLimitGroupId.FromIndex(i);
            return default;
        }
    }
}
