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
        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestSubmittedItemObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestTechnologyObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Technology);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.QuestTurnObjective value)
        {
            SnapshotBinary.Write(writer, value.Key);
            SnapshotBinary.Write(writer, value.Turns);
            SnapshotBinary.Write(writer, value.SinceAccepted);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.RoyalTraitDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.RevealAge);
            SnapshotBinary.Write(writer, value.MinimumActivationAge);
            SnapshotBinary.Write(writer, value.Heritable);
            SnapshotBinary.Write(writer, value.InheritanceChance);
            writer.Write(value.GrantedTraits.Length);
            for (int i = 0; i < value.GrantedTraits.Length; i++)
            {
                SnapshotBinary.Write(writer, value.GrantedTraits[i]);
            }

            writer.Write(value.ConflictingTraits.Length);
            for (int i = 0; i < value.ConflictingTraits.Length; i++)
            {
                SnapshotBinary.Write(writer, value.ConflictingTraits[i]);
            }

            writer.Write(value.RequiredTraits.Length);
            for (int i = 0; i < value.RequiredTraits.Length; i++)
            {
                SnapshotBinary.Write(writer, value.RequiredTraits[i]);
            }

            Write(writer, ref value.Prerequisites);
            Write(writer, ref value.Effects);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.SoldierDefinition value)
        {
            Write(writer, ref value.Metadata);
            Write(writer, ref value.CombatStats);
            SnapshotBinary.Write(writer, value.ThreatValue);
            SnapshotBinary.Write(writer, value.NightPower);
            SnapshotBinary.Write(writer, value.TargetMode);
            SnapshotBinary.Write(writer, value.PopulationCost);
            SnapshotBinary.Write(writer, value.FallbackRecruitGold);
            Write(writer, ref value.Growth);
            writer.Write(value.RecruitmentCosts.Length);
            for (int i = 0; i < value.RecruitmentCosts.Length; i++)
            {
                Write(writer, ref value.RecruitmentCosts[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.SoldierNumericEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Target);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.Magnitude);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.SpecialItemDrop value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
            SnapshotBinary.Write(writer, value.Rarity);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.StorageSlotDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.DefaultLossMultiplier);
            writer.Write(value.AcceptedItems.Length);
            for (int i = 0; i < value.AcceptedItems.Length; i++)
            {
                SnapshotBinary.Write(writer, value.AcceptedItems[i]);
            }

            writer.Write(value.AcceptedGroups.Length);
            for (int i = 0; i < value.AcceptedGroups.Length; i++)
            {
                SnapshotBinary.Write(writer, value.AcceptedGroups[i]);
            }

            writer.Write(value.ItemLosses.Length);
            for (int i = 0; i < value.ItemLosses.Length; i++)
            {
                Write(writer, ref value.ItemLosses[i]);
            }

            writer.Write(value.GroupLosses.Length);
            for (int i = 0; i < value.GroupLosses.Length; i++)
            {
                Write(writer, ref value.GroupLosses[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentBlueprintIncome value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.BaseLevel);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentBuffIncome value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Buff);
            SnapshotBinary.Write(writer, value.BaseLevel);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.InitialLevel);
            SnapshotBinary.Write(writer, value.MaximumLevel);
            SnapshotBinary.Write(writer, value.BaseLevelExperience);
            SnapshotBinary.Write(writer, value.LevelExperienceIncrement);
            SnapshotBinary.Write(writer, value.Specialty);
            Write(writer, ref value.Wage);
            writer.Write(value.InitialTraits.Length);
            for (int i = 0; i < value.InitialTraits.Length; i++)
            {
                SnapshotBinary.Write(writer, value.InitialTraits[i]);
            }

            writer.Write(value.ConflictingTraits.Length);
            for (int i = 0; i < value.ConflictingTraits.Length; i++)
            {
                SnapshotBinary.Write(writer, value.ConflictingTraits[i]);
            }

            writer.Write(value.RequiredTraits.Length);
            for (int i = 0; i < value.RequiredTraits.Length; i++)
            {
                SnapshotBinary.Write(writer, value.RequiredTraits[i]);
            }

            Write(writer, ref value.Prerequisites);
            writer.Write(value.SocialTasks.Length);
            for (int i = 0; i < value.SocialTasks.Length; i++)
            {
                Write(writer, ref value.SocialTasks[i]);
            }

            Write(writer, ref value.PeriodicIncome);
            Write(writer, ref value.JobEffects);
            Write(writer, ref value.Effects);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentEffectScaling value)
        {
            SnapshotBinary.Write(writer, value.Kind);
            SnapshotBinary.Write(writer, value.SourceItem);
            SnapshotBinary.Write(writer, value.SourceBuilding);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentFeatureIncome value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Feature);
            SnapshotBinary.Write(writer, value.BaseLevel);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentHeroJobEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Recipient);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.BaseMagnitude);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentItemIncome value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.BaseQuantity);
            SnapshotBinary.Write(writer, value.PerLevel);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentItemJobEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Recipient);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.BaseMagnitude);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentJobEffects value)
        {
            writer.Write(value.Items.Length);
            for (int i = 0; i < value.Items.Length; i++)
            {
                Write(writer, ref value.Items[i]);
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

            writer.Write(value.Kingdom.Length);
            for (int i = 0; i < value.Kingdom.Length; i++)
            {
                Write(writer, ref value.Kingdom[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentKingdomJobEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.BaseMagnitude);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentNumericEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Level);
            SnapshotBinary.Write(writer, value.Target);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.Magnitude);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentPeriodicIncome value)
        {
            writer.Write(value.Items.Length);
            for (int i = 0; i < value.Items.Length; i++)
            {
                Write(writer, ref value.Items[i]);
            }

            writer.Write(value.ScaledItems.Length);
            for (int i = 0; i < value.ScaledItems.Length; i++)
            {
                Write(writer, ref value.ScaledItems[i]);
            }

            writer.Write(value.ResearchPoints.Length);
            for (int i = 0; i < value.ResearchPoints.Length; i++)
            {
                Write(writer, ref value.ResearchPoints[i]);
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

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentResearchIncome value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.BasePoints);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentScaledItemIncome value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.BaseQuantity);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }
    }
}
