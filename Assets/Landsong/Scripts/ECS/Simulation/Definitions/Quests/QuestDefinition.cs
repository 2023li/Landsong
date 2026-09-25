using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    /// <summary>A local Quest catalog index. The default value is an absent reference.</summary>
    public readonly struct QuestId : IEquatable<QuestId>, IComparable<QuestId>
    {
        readonly int encoded;
        QuestId(int index)
        {
            encoded = checked(index + 1);
        }

        public int Index => encoded - 1;
        public bool IsValid => encoded > 0;
        public static QuestId None => default;

        public static QuestId FromIndex(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            return new QuestId(index);
        }

        public int CompareTo(QuestId other) => encoded.CompareTo(other.encoded);
        public bool Equals(QuestId other) => encoded == other.encoded;
        public override bool Equals(object other) => other is QuestId id && Equals(id);
        public override int GetHashCode() => encoded;
        public override string ToString() => IsValid ? "Quest:" + Index.ToString(System.Globalization.CultureInfo.InvariantCulture) : "Quest:None";
        public static bool operator ==(QuestId left, QuestId right) => left.Equals(right);
        public static bool operator !=(QuestId left, QuestId right) => !left.Equals(right);
    }

    public struct QuestDefinition
    {
        public DefinitionMetadata Metadata;
        public int DeadlineTurns;
        public QuestBehaviorFlags Behavior;
        public QuestOfferType OfferType;
        public int Intensity;
        public float OfferWeight;
        public float ItemQuantityScale;
        public QuestId NextQuest;
        public DefinitionPrerequisites RefreshPrerequisites;
        public int MinimumRefreshTurns;
        public int MaximumRefreshTurns;
        public DefinitionPrerequisites Prerequisites;
        public QuestObjectives Objectives;
        public DefinitionRewards Rewards;
        public BlobArray<ItemAmount> FailurePenalties;
    }

    public struct QuestCatalogBlob
    {
        public BlobArray<QuestDefinition> Definitions;
    }

    public struct QuestCatalog : IComponentData
    {
        public BlobAssetReference<QuestCatalogBlob> Value;
    }

    public static class QuestDefinitions
    {
        public static int Count(EntityManager em, Entity root)
        {
            if (!em.Exists(root) || !em.HasComponent<QuestCatalog>(root))
                return 0;
            var blob = em.GetComponentData<QuestCatalog>(root).Value;
            return blob.IsCreated ? blob.Value.Definitions.Length : 0;
        }

        public static bool IsValid(EntityManager em, Entity root, QuestId id) => id.IsValid && id.Index < Count(em, root);
        // BlobArray stores relative pointers: Unity requires a mutable ref return even for read-only callers.
        public static ref QuestDefinition Get(EntityManager em, Entity root, QuestId id)
        {
            if (!IsValid(em, root, id))
                throw new ArgumentOutOfRangeException(nameof(id));
            var blob = em.GetComponentData<QuestCatalog>(root).Value;
            return ref blob.Value.Definitions[id.Index];
        }

        public static QuestId Find(EntityManager em, Entity root, FixedString128Bytes stableId)
        {
            if (stableId.IsEmpty || Count(em, root) == 0)
                return default;
            var blob = em.GetComponentData<QuestCatalog>(root).Value;
            for (int i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Metadata.Id == stableId)
                    return QuestId.FromIndex(i);
            return default;
        }
    }
}
