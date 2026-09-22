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
        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingGarrison value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            SnapshotBinary.Write(writer, value.RecruitmentLimitPerTurn);
            writer.Write(value.Levels.Length);
            for (int i = 0; i < value.Levels.Length; i++)
            {
                Write(writer, ref value.Levels[i]);
            }

            writer.Write(value.InitialUnits.Length);
            for (int i = 0; i < value.InitialUnits.Length; i++)
            {
                Write(writer, ref value.InitialUnits[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingGarrisonLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Capacity);
            SnapshotBinary.Write(writer, value.DeploymentBatchSize);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingGathering value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Levels.Length);
            for (int i = 0; i < value.Levels.Length; i++)
            {
                Write(writer, ref value.Levels[i]);
            }

            writer.Write(value.Rewards.Length);
            for (int i = 0; i < value.Rewards.Length; i++)
            {
                Write(writer, ref value.Rewards[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingGatheringLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Uses);
            SnapshotBinary.Write(writer, value.AmountPerUse);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingGatheringReward value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingHousing value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Population.Length);
            for (int i = 0; i < value.Population.Length; i++)
            {
                Write(writer, ref value.Population[i]);
            }

            writer.Write(value.Residences.Length);
            for (int i = 0; i < value.Residences.Length; i++)
            {
                Write(writer, ref value.Residences[i]);
            }

            writer.Write(value.Food.Length);
            for (int i = 0; i < value.Food.Length; i++)
            {
                Write(writer, ref value.Food[i]);
            }

            writer.Write(value.Taxes.Length);
            for (int i = 0; i < value.Taxes.Length; i++)
            {
                Write(writer, ref value.Taxes[i]);
            }

            writer.Write(value.Environment.Length);
            for (int i = 0; i < value.Environment.Length; i++)
            {
                Write(writer, ref value.Environment[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingInitialGarrison value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Soldier);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingInput value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingIntelligenceLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Technology);
            SnapshotBinary.Write(writer, value.Points);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingLimitGroupDefinition value)
        {
            Write(writer, ref value.Metadata);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingMaintenance value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            SnapshotBinary.Write(writer, value.RepairTurns);
            writer.Write(value.Costs.Length);
            for (int i = 0; i < value.Costs.Length; i++)
            {
                Write(writer, ref value.Costs[i]);
            }

            writer.Write(value.Repairs.Length);
            for (int i = 0; i < value.Repairs.Length; i++)
            {
                Write(writer, ref value.Repairs[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingMaintenanceCost value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingMarket value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Levels.Length);
            for (int i = 0; i < value.Levels.Length; i++)
            {
                Write(writer, ref value.Levels[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingMarketLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Currency);
            SnapshotBinary.Write(writer, value.ValuePerMarketPoint);
            SnapshotBinary.Write(writer, value.IncomeRatio);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingNumericEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Target);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.Magnitude);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingPlacement value)
        {
            SnapshotBinary.Write(writer, value.AllowedTerrains);
            SnapshotBinary.Write(writer, value.ExcludedTerrains);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingPlacementAndVisuals value)
        {
            SnapshotBinary.Write(writer, value.Category);
            SnapshotBinary.Write(writer, value.SpawnExclusionPadding);
            SnapshotBinary.Write(writer, value.ProviderPriority);
            SnapshotBinary.Write(writer, value.CanMove);
            SnapshotBinary.Write(writer, value.CanRotate);
            SnapshotBinary.Write(writer, value.MoveMaterialRatio);
            SnapshotBinary.Write(writer, value.MoveExperienceRatio);
            SnapshotBinary.Write(writer, value.RuinMovementCost);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingPlacementCost value)
        {
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingProcessingTier value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Interval);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingProduction value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Inputs.Length);
            for (int i = 0; i < value.Inputs.Length; i++)
            {
                Write(writer, ref value.Inputs[i]);
            }

            writer.Write(value.Cycles.Length);
            for (int i = 0; i < value.Cycles.Length; i++)
            {
                Write(writer, ref value.Cycles[i]);
            }

            writer.Write(value.ProcessingTiers.Length);
            for (int i = 0; i < value.ProcessingTiers.Length; i++)
            {
                Write(writer, ref value.ProcessingTiers[i]);
            }

            writer.Write(value.Outputs.Length);
            for (int i = 0; i < value.Outputs.Length; i++)
            {
                Write(writer, ref value.Outputs[i]);
            }

            writer.Write(value.RareOutputs.Length);
            for (int i = 0; i < value.RareOutputs.Length; i++)
            {
                Write(writer, ref value.RareOutputs[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingProductionCycle value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Interval);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingProductionOutput value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
            SnapshotBinary.Write(writer, value.MinimumWorkers);
            SnapshotBinary.Write(writer, value.MaximumWorkers);
        }
    }
}
