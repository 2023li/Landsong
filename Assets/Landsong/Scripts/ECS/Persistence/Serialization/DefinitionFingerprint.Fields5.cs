using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    internal static partial class DefinitionFingerprint
    {
        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ItemAmount value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ItemDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.PrimaryGroup);
            writer.Write(value.AdditionalGroups.Length);
            for (int i = 0; i < value.AdditionalGroups.Length; i++)
            {
                SnapshotBinary.Write(writer, value.AdditionalGroups[i]);
            }

            SnapshotBinary.Write(writer, value.MaximumStack);
            SnapshotBinary.Write(writer, value.TradeValue);
            SnapshotBinary.Write(writer, value.NaturalLossRate);
            Write(writer, ref value.Theft);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ItemGroupDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.ParentGroup);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ItemGroupLossOverride value)
        {
            SnapshotBinary.Write(writer, value.Group);
            SnapshotBinary.Write(writer, value.Multiplier);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ItemLossOverride value)
        {
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Multiplier);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ItemNumericEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Target);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.Magnitude);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ItemQuantityRange value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.MinimumQuantity);
            SnapshotBinary.Write(writer, value.MaximumQuantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.KingdomNumericEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.Magnitude);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.LeveledItemAmount value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.LootDefinition value)
        {
            Write(writer, ref value.Metadata);
            Write(writer, ref value.Rewards);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.OpportunityDefinition value)
        {
            Write(writer, ref value.Metadata);
            Write(writer, ref value.VisitorProfile);
            Write(writer, ref value.Rewards);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.PolicyDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.PolicyGroup);
            SnapshotBinary.Write(writer, value.PolicyTier);
            SnapshotBinary.Write(writer, value.RequiredPublicOpinion);
            Write(writer, ref value.Prerequisites);
            Write(writer, ref value.Effects);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.PolicyGroupDefinition value)
        {
            Write(writer, ref value.Metadata);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ProjectileDefinition value)
        {
            Write(writer, ref value.Metadata);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestBuildingObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.Count);
            SnapshotBinary.Write(writer, value.MinimumLevel);
            SnapshotBinary.Write(writer, value.CompletedOnly);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestCameraMoveObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestCameraZoomObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestPopulationObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestTechnologyCompletedObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Technology);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.DeadlineTurns);
            SnapshotBinary.Write(writer, value.Behavior);
            SnapshotBinary.Write(writer, value.OfferType);
            SnapshotBinary.Write(writer, value.Intensity);
            SnapshotBinary.Write(writer, value.OfferWeight);
            SnapshotBinary.Write(writer, value.ItemQuantityScale);
            SnapshotBinary.Write(writer, value.NextQuest);
            SnapshotBinary.Write(writer, value.MinimumRefreshTurns);
            SnapshotBinary.Write(writer, value.MaximumRefreshTurns);
            Write(writer, ref value.RefreshPrerequisites);
            Write(writer, ref value.Prerequisites);
            Write(writer, ref value.Objectives);
            Write(writer, ref value.Rewards);
            writer.Write(value.FailurePenalties.Length);
            for (int i = 0; i < value.FailurePenalties.Length; i++)
            {
                Write(writer, ref value.FailurePenalties[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestObjectives value)
        {
            writer.Write(value.BuildingObjectives.Length);
            var BuildingObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.BuildingObjectives.Length; i++)
                BuildingObjectivesOrder.Add(value.BuildingObjectives[i].Key.ToString(), i);
            foreach (int i in BuildingObjectivesOrder.Values)
            {
                Write(writer, ref value.BuildingObjectives[i]);
            }

            writer.Write(value.PlantedBuildingObjectives.Length);
            var PlantedBuildingObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.PlantedBuildingObjectives.Length; i++)
                PlantedBuildingObjectivesOrder.Add(value.PlantedBuildingObjectives[i].Key.ToString(), i);
            foreach (int i in PlantedBuildingObjectivesOrder.Values)
            {
                Write(writer, ref value.PlantedBuildingObjectives[i]);
            }

            writer.Write(value.OwnedItemObjectives.Length);
            var OwnedItemObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.OwnedItemObjectives.Length; i++)
                OwnedItemObjectivesOrder.Add(value.OwnedItemObjectives[i].Key.ToString(), i);
            foreach (int i in OwnedItemObjectivesOrder.Values)
            {
                Write(writer, ref value.OwnedItemObjectives[i]);
            }

            writer.Write(value.SubmittedItemObjectives.Length);
            var SubmittedItemObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.SubmittedItemObjectives.Length; i++)
                SubmittedItemObjectivesOrder.Add(value.SubmittedItemObjectives[i].Key.ToString(), i);
            foreach (int i in SubmittedItemObjectivesOrder.Values)
            {
                Write(writer, ref value.SubmittedItemObjectives[i]);
            }

            writer.Write(value.TechnologyObjectives.Length);
            var TechnologyObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.TechnologyObjectives.Length; i++)
                TechnologyObjectivesOrder.Add(value.TechnologyObjectives[i].Key.ToString(), i);
            foreach (int i in TechnologyObjectivesOrder.Values)
            {
                Write(writer, ref value.TechnologyObjectives[i]);
            }

            writer.Write(value.TechnologyCompletedObjectives.Length);
            var completedOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.TechnologyCompletedObjectives.Length; i++)
                completedOrder.Add(value.TechnologyCompletedObjectives[i].Key.ToString(), i);
            foreach (var i in completedOrder.Values)
                Write(writer, ref value.TechnologyCompletedObjectives[i]);

            writer.Write(value.PopulationObjectives.Length);
            var populationOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.PopulationObjectives.Length; i++)
                populationOrder.Add(value.PopulationObjectives[i].Key.ToString(), i);
            foreach (var i in populationOrder.Values)
                Write(writer, ref value.PopulationObjectives[i]);

            writer.Write(value.CameraMoveObjectives.Length);
            var CameraMoveObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.CameraMoveObjectives.Length; i++)
                CameraMoveObjectivesOrder.Add(value.CameraMoveObjectives[i].Key.ToString(), i);
            foreach (int i in CameraMoveObjectivesOrder.Values)
            {
                Write(writer, ref value.CameraMoveObjectives[i]);
            }

            writer.Write(value.CameraZoomObjectives.Length);
            var CameraZoomObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.CameraZoomObjectives.Length; i++)
                CameraZoomObjectivesOrder.Add(value.CameraZoomObjectives[i].Key.ToString(), i);
            foreach (int i in CameraZoomObjectivesOrder.Values)
            {
                Write(writer, ref value.CameraZoomObjectives[i]);
            }

            writer.Write(value.TurnObjectives.Length);
            var TurnObjectivesOrder = new SortedDictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < value.TurnObjectives.Length; i++)
                TurnObjectivesOrder.Add(value.TurnObjectives[i].Key.ToString(), i);
            foreach (int i in TurnObjectivesOrder.Values)
            {
                Write(writer, ref value.TurnObjectives[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestOwnedItemObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestPlantedBuildingObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestRequirement value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Quest);
            SnapshotBinary.Write(writer, value.Required);
        }
    }
}
