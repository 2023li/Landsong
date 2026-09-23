using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Crop catalog index. The default value is an absent reference.</summary>
    public readonly struct CropId : IEquatable<CropId>, IComparable<CropId>
    {
        readonly int encoded;
        CropId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static CropId None => default;

        public static CropId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new CropId(index);
        }

        public int CompareTo(CropId other) => encoded.CompareTo(other.encoded);
        public bool Equals(CropId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is CropId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Crop:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Crop:None";
        public static bool operator ==(CropId left, CropId right) => left.Equals(right);
        public static bool operator !=(CropId left, CropId right) => !left.Equals(right);
    }

    public struct CropDefinition
    {
        public DefinitionMetadata Metadata;
        public int GrowthTurns;
        public BlobArray<ItemAmount> PlantingCosts;
        public BlobArray<ItemAmount> AutomaticHarvestCosts;
        public BlobArray<ItemQuantityRange> HarvestOutputs;
    }

    public struct CropCatalogBlob
    {
        public BlobArray<CropDefinition> Definitions;
    }

    public struct CropCatalog : IComponentData
    {
        public BlobAssetReference<CropCatalogBlob> Value;
    }

    public static class CropDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<CropCatalog>(root))
                return 0;
            var blob = em.GetComponentData<CropCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, CropId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref CropDefinition Get(EntityManager em, Entity root, CropId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<CropCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static CropId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<CropCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return CropId.FromIndex(i);
            return default;
        }
    }
}
