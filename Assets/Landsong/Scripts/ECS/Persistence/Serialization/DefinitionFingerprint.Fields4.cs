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
        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingUpgradePopulation value)
        {
            SnapshotBinary.Write(writer, value.TargetLevel);
            SnapshotBinary.Write(writer, value.Required);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingUpgradeWorkers value)
        {
            SnapshotBinary.Write(writer, value.TargetLevel);
            SnapshotBinary.Write(writer, value.Required);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingWarehouseLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.SlotType);
            SnapshotBinary.Write(writer, value.Slots);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingWorkerEfficiencyTier value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.MinimumWorkers);
            SnapshotBinary.Write(writer, value.MaximumWorkers);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingWorkforce value)
        {
            SnapshotBinary.Write(writer, value.Enabled);
            writer.Write(value.Levels.Length);
            for (int i = 0; i < value.Levels.Length; i++)
            {
                Write(writer, ref value.Levels[i]);
            }

            writer.Write(value.Attraction.Length);
            for (int i = 0; i < value.Attraction.Length; i++)
            {
                Write(writer, ref value.Attraction[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.BuildingWorkforceLevel value)
        {
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Currency);
            SnapshotBinary.Write(writer, value.Capacity);
            SnapshotBinary.Write(writer, value.InitialWorkers);
            SnapshotBinary.Write(writer, value.InitialSubsidy);
            SnapshotBinary.Write(writer, value.BaseAttraction);
            SnapshotBinary.Write(writer, value.RecruitmentCost);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.CropDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.GrowthTurns);
            SnapshotBinary.Write(writer, value.RequiredWorkers);
            SnapshotBinary.Write(writer, value.FullStaffBonusWorkers);
            SnapshotBinary.Write(writer, value.FullStaffYieldBonus);
            writer.Write(value.PlantingCosts.Length);
            for (int i = 0; i < value.PlantingCosts.Length; i++)
            {
                Write(writer, ref value.PlantingCosts[i]);
            }

            writer.Write(value.AutomaticHarvestCosts.Length);
            for (int i = 0; i < value.AutomaticHarvestCosts.Length; i++)
            {
                Write(writer, ref value.AutomaticHarvestCosts[i]);
            }

            writer.Write(value.HarvestOutputs.Length);
            for (int i = 0; i < value.HarvestOutputs.Length; i++)
            {
                Write(writer, ref value.HarvestOutputs[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.DefinitionEffects value)
        {
            writer.Write(value.Items.Length);
            for (int i = 0; i < value.Items.Length; i++)
            {
                Write(writer, ref value.Items[i]);
            }

            writer.Write(value.Buildings.Length);
            for (int i = 0; i < value.Buildings.Length; i++)
            {
                Write(writer, ref value.Buildings[i]);
            }

            writer.Write(value.Soldiers.Length);
            for (int i = 0; i < value.Soldiers.Length; i++)
            {
                Write(writer, ref value.Soldiers[i]);
            }

            writer.Write(value.Heroes.Length);
            for (int i = 0; i < value.Heroes.Length; i++)
            {
                Write(writer, ref value.Heroes[i]);
            }

            writer.Write(value.Talents.Length);
            for (int i = 0; i < value.Talents.Length; i++)
            {
                Write(writer, ref value.Talents[i]);
            }

            writer.Write(value.Kingdom.Length);
            for (int i = 0; i < value.Kingdom.Length; i++)
            {
                Write(writer, ref value.Kingdom[i]);
            }

            writer.Write(value.Intelligence.Length);
            for (int i = 0; i < value.Intelligence.Length; i++)
            {
                Write(writer, ref value.Intelligence[i]);
            }

            writer.Write(value.FlatProduction.Length);
            for (int i = 0; i < value.FlatProduction.Length; i++)
            {
                Write(writer, ref value.FlatProduction[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.DefinitionMetadata value)
        {
            SnapshotBinary.Write(writer, value.Id);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.DefinitionPrerequisites value)
        {
            writer.Write(value.BuildingRequirements.Length);
            for (int i = 0; i < value.BuildingRequirements.Length; i++)
            {
                Write(writer, ref value.BuildingRequirements[i]);
            }

            writer.Write(value.TechnologyRequirements.Length);
            for (int i = 0; i < value.TechnologyRequirements.Length; i++)
            {
                Write(writer, ref value.TechnologyRequirements[i]);
            }

            writer.Write(value.BuffRequirements.Length);
            for (int i = 0; i < value.BuffRequirements.Length; i++)
            {
                Write(writer, ref value.BuffRequirements[i]);
            }

            writer.Write(value.FeatureRequirements.Length);
            for (int i = 0; i < value.FeatureRequirements.Length; i++)
            {
                Write(writer, ref value.FeatureRequirements[i]);
            }

            writer.Write(value.QuestRequirements.Length);
            for (int i = 0; i < value.QuestRequirements.Length; i++)
            {
                Write(writer, ref value.QuestRequirements[i]);
            }

            writer.Write(value.ExpeditionRequirements.Length);
            for (int i = 0; i < value.ExpeditionRequirements.Length; i++)
            {
                Write(writer, ref value.ExpeditionRequirements[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.DefinitionRewards value)
        {
            writer.Write(value.Items.Length);
            for (int i = 0; i < value.Items.Length; i++)
            {
                Write(writer, ref value.Items[i]);
            }

            writer.Write(value.Blueprints.Length);
            for (int i = 0; i < value.Blueprints.Length; i++)
            {
                Write(writer, ref value.Blueprints[i]);
            }

            writer.Write(value.Buffs.Length);
            for (int i = 0; i < value.Buffs.Length; i++)
            {
                Write(writer, ref value.Buffs[i]);
            }

            writer.Write(value.Features.Length);
            for (int i = 0; i < value.Features.Length; i++)
            {
                Write(writer, ref value.Features[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.EnemyDefinition value)
        {
            Write(writer, ref value.Metadata);
            Write(writer, ref value.CombatStats);
            SnapshotBinary.Write(writer, value.ThreatValue);
            SnapshotBinary.Write(writer, value.Behavior);
            SnapshotBinary.Write(writer, value.PreferredTargetCategory);
            Write(writer, ref value.KillRewards);
            writer.Write(value.SpecialDrops.Length);
            for (int i = 0; i < value.SpecialDrops.Length; i++)
            {
                Write(writer, ref value.SpecialDrops[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ExpeditionDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.MinimumSiteLevel);
            SnapshotBinary.Write(writer, value.MinimumCrew);
            SnapshotBinary.Write(writer, value.MaximumCrew);
            SnapshotBinary.Write(writer, value.TravelTurns);
            SnapshotBinary.Write(writer, value.BaseSuccessChance);
            SnapshotBinary.Write(writer, value.SuccessChancePerCrew);
            SnapshotBinary.Write(writer, value.MaximumSuccessChance);
            SnapshotBinary.Write(writer, value.FailureCasualtyRatio);
            SnapshotBinary.Write(writer, value.BaseCompensation);
            SnapshotBinary.Write(writer, value.CompensationPerCrew);
            SnapshotBinary.Write(writer, value.Repeatable);
            Write(writer, ref value.Prerequisites);
            Write(writer, ref value.Visibility);
            writer.Write(value.Supplies.Length);
            for (int i = 0; i < value.Supplies.Length; i++)
            {
                Write(writer, ref value.Supplies[i]);
            }

            Write(writer, ref value.Rewards);
            writer.Write(value.FailurePenalties.Length);
            for (int i = 0; i < value.FailurePenalties.Length; i++)
            {
                Write(writer, ref value.FailurePenalties[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ExpeditionRequirement value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Expedition);
            SnapshotBinary.Write(writer, value.Required);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.ExpeditionSupply value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.MinimumQuantity);
            SnapshotBinary.Write(writer, value.ExtraLimit);
            SnapshotBinary.Write(writer, value.SuccessPerExtra);
            SnapshotBinary.Write(writer, value.RewardPerExtra);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.FeatureDefinition value)
        {
            Write(writer, ref value.Metadata);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.FeatureRequirement value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Feature);
            SnapshotBinary.Write(writer, value.Required);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.FeatureReward value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Feature);
            SnapshotBinary.Write(writer, value.GrantedLevel);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.FlatProductionEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.HeroDefinition value)
        {
            Write(writer, ref value.Metadata);
            Write(writer, ref value.CombatStats);
            SnapshotBinary.Write(writer, value.ThreatValue);
            SnapshotBinary.Write(writer, value.TargetMode);
            SnapshotBinary.Write(writer, value.PopulationCost);
            SnapshotBinary.Write(writer, value.FallbackWakeGold);
            SnapshotBinary.Write(writer, value.RevivalCooldownTurns);
            Write(writer, ref value.Growth);
            writer.Write(value.AwakeningCosts.Length);
            for (int i = 0; i < value.AwakeningCosts.Length; i++)
            {
                Write(writer, ref value.AwakeningCosts[i]);
            }

            writer.Write(value.OfferingCosts.Length);
            for (int i = 0; i < value.OfferingCosts.Length; i++)
            {
                Write(writer, ref value.OfferingCosts[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.HeroNumericEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Target);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.Magnitude);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.IntelligenceEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.RequiredTechnology);
            SnapshotBinary.Write(writer, value.Points);
        }
    }
}
