using System;
using System.Collections.Generic;
using System.IO;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class QuestObjectiveProgress
    {
        public static void Initialize(EntityManager em, Entity entity, ref QuestObjectives objectives)
        {
            var keys = Keys(ref objectives);
            keys.Sort((left, right) => left.Order.CompareTo(right.Order));
            EntityState.Buffer<QuestProgress>(em, entity);
            var progress = em.GetBuffer<QuestProgress>(entity);
            foreach (var key in keys)
                progress.Add(new QuestProgress { Key = key.Key });
        }

        public static int Index(DynamicBuffer<QuestProgress> progress, FixedString64Bytes key)
        {
            for (int i = 0; i < progress.Length; i++)
                if (progress[i].Key == key)
                    return i;
            return -1;
        }

        public static int Amount(DynamicBuffer<QuestProgress> progress, FixedString64Bytes key)
        {
            int index = Index(progress, key);
            if (index < 0)
                throw new InvalidOperationException("Quest progress key is missing: " + key);
            return progress[index].Amount;
        }

        public static bool Evaluate(EntityManager em, Entity root, Entity entity, ref QuestObjectives objectives, int acceptedTurn)
        {
            bool complete = true;
            var progress = em.GetBuffer<QuestProgress>(entity);
            void Observe(FixedString64Bytes key, int count, int required)
            {
                int index = Index(progress, key);
                if (index < 0)
                    throw new InvalidOperationException("Quest progress key is missing: " + key);
                var row = progress[index];
                row.Amount = math.clamp(count, 0, required);
                progress[index] = row;
                if (count < required)
                    complete = false;
            }

            for (int i = 0; i < objectives.OwnedItemObjectives.Length; i++)
            {
                var requirement = objectives.OwnedItemObjectives[i];
                Observe(requirement.Key, InventoryOps.Count(em, root, requirement.Item), requirement.Quantity);
            }

            for (int i = 0; i < objectives.SubmittedItemObjectives.Length; i++)
            {
                var requirement = objectives.SubmittedItemObjectives[i];
                Observe(requirement.Key, Amount(progress, requirement.Key), requirement.Quantity);
            }

            for (int i = 0; i < objectives.CameraMoveObjectives.Length; i++)
            {
                var requirement = objectives.CameraMoveObjectives[i];
                Observe(requirement.Key, Amount(progress, requirement.Key), requirement.Count);
            }

            for (int i = 0; i < objectives.CameraZoomObjectives.Length; i++)
            {
                var requirement = objectives.CameraZoomObjectives[i];
                Observe(requirement.Key, Amount(progress, requirement.Key), requirement.Count);
            }

            for (int i = 0; i < objectives.TurnObjectives.Length; i++)
            {
                var requirement = objectives.TurnObjectives[i];
                Observe(requirement.Key, em.GetComponentData<GameClock>(root).Turn - (requirement.SinceAccepted ? acceptedTurn : 0), requirement.Turns);
            }

            var queue = ResearchOps.Queue(em, root);
            for (int i = 0; i < objectives.TechnologyObjectives.Length; i++)
            {
                var requirement = objectives.TechnologyObjectives[i];
                int count = queue.Count > 0 && (!requirement.Technology.IsValid || queue[0].Technology == requirement.Technology) && ResearchOps.Prerequisites(em, root, queue[0].Technology) ? 1 : 0;
                Observe(requirement.Key, count, requirement.Count);
            }

            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            for (int i = 0; i < objectives.BuildingObjectives.Length; i++)
            {
                var requirement = objectives.BuildingObjectives[i];
                int count = 0;
                foreach (var building in buildings)
                {
                    var state = em.GetComponentData<Building>(building);
                    if (state.Stage != LifeStage.Operational && (requirement.CompletedOnly || state.Stage != LifeStage.Construction))
                        continue;
                    if (em.GetComponentData<BuildingDefinitionRef>(building).Definition == requirement.Building && state.Level >= math.max(1, requirement.MinimumLevel))
                        count++;
                }

                Observe(requirement.Key, count, requirement.Count);
            }

            for (int i = 0; i < objectives.PlantedBuildingObjectives.Length; i++)
            {
                var requirement = objectives.PlantedBuildingObjectives[i];
                int count = 0;
                foreach (var building in buildings)
                {
                    var state = em.GetComponentData<Building>(building);
                    BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(building);
                    if (state.Stage == LifeStage.Operational && stateFarming.Crop.IsValid && (!requirement.Building.IsValid || em.GetComponentData<BuildingDefinitionRef>(building).Definition == requirement.Building))
                        count++;
                }

                Observe(requirement.Key, count, requirement.Count);
            }

            return complete;
        }

        public static void ObserveCamera(EntityManager em, Entity root, bool zoom)
        {
            using var quests = WorldQueries.OrderedEntities<Quest>(em);
            foreach (var entity in quests)
            {
                if (em.GetComponentData<Quest>(entity).Status != QuestStatus.Active)
                    continue;
                var id = em.GetComponentData<QuestDefinitionRef>(entity).Definition;
                ref var definition = ref QuestDefinitions.Get(em, root, id);
                if (!PrerequisiteEvaluation.Satisfied(em, root, ref definition.Prerequisites))
                    continue;
                var progress = em.GetBuffer<QuestProgress>(entity);
                void Increment(FixedString64Bytes key, int required)
                {
                    int index = Index(progress, key);
                    if (index < 0)
                        throw new InvalidOperationException("Quest progress key is missing: " + key);
                    var value = progress[index];
                    if (value.Amount < required)
                    {
                        value.Amount++;
                        progress[index] = value;
                    }
                }

                if (zoom)
                    for (int i = 0; i < definition.Objectives.CameraZoomObjectives.Length; i++)
                    {
                        var objective = definition.Objectives.CameraZoomObjectives[i];
                        Increment(objective.Key, objective.Count);
                    }
                else
                    for (int i = 0; i < definition.Objectives.CameraMoveObjectives.Length; i++)
                    {
                        var objective = definition.Objectives.CameraMoveObjectives[i];
                        Increment(objective.Key, objective.Count);
                    }
            }
        }

        public static void Validate(ref QuestObjectives objectives, QuestProgress[] progress)
        {
            var expected = Keys(ref objectives);
            if (progress == null || progress.Length != expected.Count)
                throw new InvalidDataException("Quest objective count mismatch.");
            var limits = new Dictionary<FixedString64Bytes, int>();
            foreach (var objective in expected)
                if (objective.Key.IsEmpty || objective.Maximum < 0 || !limits.TryAdd(objective.Key, objective.Maximum))
                    throw new InvalidDataException("Invalid or duplicate quest objective key.");
            foreach (var row in progress)
            {
                if (!limits.TryGetValue(row.Key, out int maximum) || row.Amount < 0 || row.Amount > maximum)
                    throw new InvalidDataException("Invalid quest progress.");
                limits.Remove(row.Key);
            }
        }

        static List<(int Order, FixedString64Bytes Key, int Maximum)> Keys(ref QuestObjectives objectives)
        {
            var keys = new List<(int, FixedString64Bytes, int)>();
            for (int i = 0; i < objectives.BuildingObjectives.Length; i++)
            {
                var r = objectives.BuildingObjectives[i];
                keys.Add((r.Order, r.Key, r.Count));
            }

            for (int i = 0; i < objectives.PlantedBuildingObjectives.Length; i++)
            {
                var r = objectives.PlantedBuildingObjectives[i];
                keys.Add((r.Order, r.Key, r.Count));
            }

            for (int i = 0; i < objectives.OwnedItemObjectives.Length; i++)
            {
                var r = objectives.OwnedItemObjectives[i];
                keys.Add((r.Order, r.Key, r.Quantity));
            }

            for (int i = 0; i < objectives.SubmittedItemObjectives.Length; i++)
            {
                var r = objectives.SubmittedItemObjectives[i];
                keys.Add((r.Order, r.Key, r.Quantity));
            }

            for (int i = 0; i < objectives.TechnologyObjectives.Length; i++)
            {
                var r = objectives.TechnologyObjectives[i];
                keys.Add((r.Order, r.Key, r.Count));
            }

            for (int i = 0; i < objectives.CameraMoveObjectives.Length; i++)
            {
                var r = objectives.CameraMoveObjectives[i];
                keys.Add((r.Order, r.Key, r.Count));
            }

            for (int i = 0; i < objectives.CameraZoomObjectives.Length; i++)
            {
                var r = objectives.CameraZoomObjectives[i];
                keys.Add((r.Order, r.Key, r.Count));
            }

            for (int i = 0; i < objectives.TurnObjectives.Length; i++)
            {
                var r = objectives.TurnObjectives[i];
                keys.Add((r.Order, r.Key, r.Turns));
            }

            return keys;
        }
    }
}
