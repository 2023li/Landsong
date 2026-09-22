using System.Collections.Generic;
using System.IO;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS.Persistence
{
    internal static class ProgressionStateValidation
    {
        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data)
        {
            if (data.Blueprints == null || data.Buffs == null || data.Features == null || data.ClaimedQuests == null || data.CompletedExpeditions == null || data.Research == null)
                throw new InvalidDataException("Missing progression state");
            var buildings = new HashSet<BuildingId>();
            foreach (var row in data.Blueprints)
                if (!BuildingDefinitions.IsValid(em, root, row.Building) || !buildings.Add(row.Building) || row.MaximumLevel < 1 || row.MaximumLevel > BuildingDefinitions.Get(em, root, row.Building).MaximumLevel)
                    throw new InvalidDataException("Invalid building blueprint");
            var buffs = new HashSet<BuffId>();
            foreach (var row in data.Buffs)
                if (!BuffDefinitions.IsValid(em, root, row.Buff) || !buffs.Add(row.Buff) || row.Level < 1)
                    throw new InvalidDataException("Invalid permanent buff");
            var features = new HashSet<FeatureId>();
            foreach (var row in data.Features)
                if (!FeatureDefinitions.IsValid(em, root, row.Feature) || !features.Add(row.Feature))
                    throw new InvalidDataException("Invalid unlocked feature");
            var quests = new HashSet<QuestId>();
            foreach (var row in data.ClaimedQuests)
                if (!QuestDefinitions.IsValid(em, root, row.Quest) || !quests.Add(row.Quest))
                    throw new InvalidDataException("Invalid claimed quest");
            var expeditions = new HashSet<ExpeditionId>();
            foreach (var row in data.CompletedExpeditions)
                if (!ExpeditionDefinitions.IsValid(em, root, row.Expedition) || !expeditions.Add(row.Expedition))
                    throw new InvalidDataException("Invalid completed expedition");
            ResearchOps.ValidateState(em, root, data.ResearchState.Points, data.Research);
        }
    }
}
