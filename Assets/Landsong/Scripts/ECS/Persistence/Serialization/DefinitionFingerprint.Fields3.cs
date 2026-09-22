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
        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingQuestCapacity value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Slots);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingQuestInvitation value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Slots);
            SnapshotBinary.Write(writer, value.Type);
            SnapshotBinary.Write(writer, value.MinimumRefreshTurns);
            SnapshotBinary.Write(writer, value.MaximumRefreshTurns);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingQuestRecruitCost value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingQuests value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.InvitationCosts.Length);
            for (int i = 0; i < value.InvitationCosts.Length; i++)
            {
                Write(writer, ref value.InvitationCosts[i]);
            }

            writer.Write(value.Capacity.Length);
            for (int i = 0; i < value.Capacity.Length; i++)
            {
                Write(writer, ref value.Capacity[i]);
            }

            writer.Write(value.Invitations.Length);
            for (int i = 0; i < value.Invitations.Length; i++)
            {
                Write(writer, ref value.Invitations[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingRareProduction value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
            SnapshotBinary.Write(writer, value.Probability);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingRepairMaterial value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
            SnapshotBinary.Write(writer, value.RepairTurns);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingRequirement value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.Required);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingResearch value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Levels.Length);
            for (int i = 0; i < value.Levels.Length; i++)
            {
                Write(writer, ref value.Levels[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingResearchLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.PointsPerTurn);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingResidenceLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Capacity);
            SnapshotBinary.Write(writer, value.InitialResidents);
            SnapshotBinary.Write(writer, value.StarvationThreshold);
            SnapshotBinary.Write(writer, value.GrowthInterval);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingResidenceTax value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.PerResident);
            SnapshotBinary.Write(writer, value.Interval);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingResidentFood value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.FoodGroup);
            SnapshotBinary.Write(writer, value.Varieties);
            SnapshotBinary.Write(writer, value.AmountPerResident);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingResourceProvider value)
        {
            SnapshotBinary.Write(writer, value.Level);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingSanctum value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Levels.Length);
            for (int i = 0; i < value.Levels.Length; i++)
            {
                Write(writer, ref value.Levels[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingSanctumLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Hero);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingSpatialEffect value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.Magnitude);
            SnapshotBinary.Write(writer, value.Type);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
            SnapshotBinary.Write(writer, value.Radius);
            SnapshotBinary.Write(writer, value.Stacking);
            SnapshotBinary.Write(writer, value.Group);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingStorage value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Providers.Length);
            for (int i = 0; i < value.Providers.Length; i++)
            {
                Write(writer, ref value.Providers[i]);
            }

            writer.Write(value.Warehouses.Length);
            for (int i = 0; i < value.Warehouses.Length; i++)
            {
                Write(writer, ref value.Warehouses[i]);
            }

            writer.Write(value.Conditions.Length);
            for (int i = 0; i < value.Conditions.Length; i++)
            {
                Write(writer, ref value.Conditions[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingStorageCondition value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
            SnapshotBinary.Write(writer, value.MaintenanceLossPercent);
            SnapshotBinary.Write(writer, value.AttractionPenalty);
            SnapshotBinary.Write(writer, value.UnderstaffedLossMultiplier);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingTerrainConnection value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            SnapshotBinary.Write(writer, value.Rise);
            SnapshotBinary.Write(writer, value.Bidirectional);
            SnapshotBinary.Write(writer, value.Clearance);
            SnapshotBinary.Write(writer, value.DamagedCost);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingUpgrade value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Costs.Length);
            for (int i = 0; i < value.Costs.Length; i++)
            {
                Write(writer, ref value.Costs[i]);
            }

            writer.Write(value.Workers.Length);
            for (int i = 0; i < value.Workers.Length; i++)
            {
                Write(writer, ref value.Workers[i]);
            }

            writer.Write(value.Residents.Length);
            for (int i = 0; i < value.Residents.Length; i++)
            {
                Write(writer, ref value.Residents[i]);
            }

            writer.Write(value.MaintenanceRequirements.Length);
            for (int i = 0; i < value.MaintenanceRequirements.Length; i++)
            {
                Write(writer, ref value.MaintenanceRequirements[i]);
            }

            writer.Write(value.Experience.Length);
            for (int i = 0; i < value.Experience.Length; i++)
            {
                Write(writer, ref value.Experience[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingUpgradeCost value)
        {
            SnapshotBinary.Write(writer, value.TargetLevel);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingUpgradeMaintenance value)
        {
            SnapshotBinary.Write(writer, value.TargetLevel);
        }
    }
}
