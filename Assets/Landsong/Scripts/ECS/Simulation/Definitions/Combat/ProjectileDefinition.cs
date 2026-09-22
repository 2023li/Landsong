using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Projectile catalog index. The default value is an absent reference.</summary>
    public readonly struct ProjectileId : IEquatable<ProjectileId>, IComparable<ProjectileId>
    {
        readonly int encoded;
        ProjectileId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static ProjectileId None => default;

        public static ProjectileId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new ProjectileId(index);
        }

        public int CompareTo(ProjectileId other) => encoded.CompareTo(other.encoded);
        public bool Equals(ProjectileId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is ProjectileId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Projectile:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Projectile:None";
        public static bool operator ==(ProjectileId left, ProjectileId right) => left.Equals(right);
        public static bool operator !=(ProjectileId left, ProjectileId right) => !left.Equals(right);
    }

    public struct ProjectileDefinition
    {
        public DefinitionMetadata Metadata;
    }

    public struct ProjectileCatalogBlob
    {
        public BlobArray<ProjectileDefinition> Definitions;
    }

    public struct ProjectileCatalog : IComponentData
    {
        public BlobAssetReference<ProjectileCatalogBlob> Value;
    }

    [InternalBufferCapacity(0)]
    public struct ProjectilePrefab : IBufferElementData
    {
        public ProjectileId Definition;
        public Entity Prefab;
    }

    public static class ProjectileDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<ProjectileCatalog>(root))
                return 0;
            var blob = em.GetComponentData<ProjectileCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, ProjectileId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref ProjectileDefinition Get(EntityManager em, Entity root, ProjectileId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<ProjectileCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static ProjectileId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<ProjectileCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return ProjectileId.FromIndex(i);
            return default;
        }
    }
}
