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
        internal static void Write(BinaryWriter writer, ref AuthoredConnection value)
        {
            SnapshotBinary.Write(writer, value.Id);
            Write(writer, ref value.Cell);
            Write(writer, ref value.Size);
            SnapshotBinary.Write(writer, value.Rotation);
            SnapshotBinary.Write(writer, value.EntrySurface);
            SnapshotBinary.Write(writer, value.ExitSurface);
            SnapshotBinary.Write(writer, value.EntryElevation);
            SnapshotBinary.Write(writer, value.Rise);
            SnapshotBinary.Write(writer, value.Bidirectional);
            SnapshotBinary.Write(writer, value.ProtrudingSlope);
        }

        internal static void Write(BinaryWriter writer, ref CombatProfile value)
        {
            SnapshotBinary.Write(writer, value.DetectionRadius);
            SnapshotBinary.Write(writer, value.ChaseRadius);
            SnapshotBinary.Write(writer, value.ChaseSeconds);
            SnapshotBinary.Write(writer, value.BodyRadius);
            SnapshotBinary.Write(writer, value.Armor);
            SnapshotBinary.Write(writer, value.Reduction);
            SnapshotBinary.Write(writer, value.Penetration);
            SnapshotBinary.Write(writer, value.BlastRadius);
            SnapshotBinary.Write(writer, value.WarningSeconds);
            SnapshotBinary.Write(writer, value.ProjectileLifetime);
            SnapshotBinary.Write(writer, value.ProjectileMode);
            SnapshotBinary.Write(writer, value.Traits);
            SnapshotBinary.Write(writer, value.BlocksProjectile);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BlueprintReward value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.GrantedLevel);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuffDefinition value)
        {
            Write(writer, ref value.Metadata);
            Write(writer, ref value.Effects);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuffRequirement value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Buff);
            SnapshotBinary.Write(writer, value.Required);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuffReward value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Buff);
            SnapshotBinary.Write(writer, value.GrantedLevel);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingAllowedCrop value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Crop);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingAttraction value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Radius);
            SnapshotBinary.Write(writer, value.PerResidentBonus);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingBasePopulation value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Population);
            SnapshotBinary.Write(writer, value.IsCore);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingBellLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Radius);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingCapabilities value)
        {
            Write(writer, ref value.Construction);
            Write(writer, ref value.Upgrade);
            Write(writer, ref value.Maintenance);
            Write(writer, ref value.Production);
            Write(writer, ref value.Quests);
            Write(writer, ref value.Placement);
            Write(writer, ref value.Connection);
            Write(writer, ref value.Storage);
            Write(writer, ref value.Workforce);
            Write(writer, ref value.Housing);
            Write(writer, ref value.Research);
            Write(writer, ref value.Farming);
            Write(writer, ref value.Gathering);
            Write(writer, ref value.Garrison);
            Write(writer, ref value.Sanctum);
            Write(writer, ref value.Market);
            Write(writer, ref value.Effects);
            Write(writer, ref value.Defence);
            Write(writer, ref value.Expeditions);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingConstruction value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.PlacementCosts.Length);
            for (int i = 0; i < value.PlacementCosts.Length; i++)
            {
                Write(writer, ref value.PlacementCosts[i]);
            }

            writer.Write(value.StageCosts.Length);
            for (int i = 0; i < value.StageCosts.Length; i++)
            {
                Write(writer, ref value.StageCosts[i]);
            }

            writer.Write(value.StageOutputs.Length);
            for (int i = 0; i < value.StageOutputs.Length; i++)
            {
                Write(writer, ref value.StageOutputs[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingConstructionCost value)
        {
            SnapshotBinary.Write(writer, value.Stage);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingConstructionOutput value)
        {
            SnapshotBinary.Write(writer, value.Stage);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingDefence value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Intelligence.Length);
            for (int i = 0; i < value.Intelligence.Length; i++)
            {
                Write(writer, ref value.Intelligence[i]);
            }

            writer.Write(value.Bells.Length);
            for (int i = 0; i < value.Bells.Length; i++)
            {
                Write(writer, ref value.Bells[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.LimitGroup);
            SnapshotBinary.Write(writer, value.MaximumLevel);
            SnapshotBinary.Write(writer, value.ConstructionTurns);
            SnapshotBinary.Write(writer, value.ResourceConnectionActionPower);
            SnapshotBinary.Write(writer, value.MaximumCount);
            Write(writer, ref value.Footprint);
            SnapshotBinary.Write(writer, value.MaximumDurability);
            SnapshotBinary.Write(writer, value.MovementCost);
            Write(writer, ref value.DefenseStats);
            Write(writer, ref value.PlacementAndVisuals);
            Write(writer, ref value.Capabilities);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingEffects value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Spatial.Length);
            for (int i = 0; i < value.Spatial.Length; i++)
            {
                Write(writer, ref value.Spatial[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingEnvironmentRequirement value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.RequiredValue);
            SnapshotBinary.Write(writer, value.Type);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingExpeditionSiteLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.MinimumCrew);
            SnapshotBinary.Write(writer, value.MaximumCrew);
            SnapshotBinary.Write(writer, value.FullCrewRewardBonus);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingExpeditions value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Levels.Length);
            for (int i = 0; i < value.Levels.Length; i++)
            {
                Write(writer, ref value.Levels[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingExperience value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.ExperiencePerTurn);
            SnapshotBinary.Write(writer, value.UpgradeExperience);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingFarming value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
            SnapshotBinary.Write(writer, value.FullCycleBonusWorkers);
            SnapshotBinary.Write(writer, value.FullCycleYieldBonusPercent);
            writer.Write(value.Crops.Length);
            for (int i = 0; i < value.Crops.Length; i++)
            {
                Write(writer, ref value.Crops[i]);
            }
        }
    }
}
