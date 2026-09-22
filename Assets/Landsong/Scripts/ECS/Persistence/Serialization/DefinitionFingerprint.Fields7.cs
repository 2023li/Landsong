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
        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentSlotDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.AcceptedSpecialty);
            writer.Write(value.RequiredTraits.Length);
            for (int i = 0; i < value.RequiredTraits.Length; i++)
            {
                SnapshotBinary.Write(writer, value.RequiredTraits[i]);
            }

            Write(writer, ref value.JobEffects);
            Write(writer, ref value.Effects);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentSocialTask value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
            SnapshotBinary.Write(writer, value.AffectionReward);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentSoldierJobEffect value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Recipient);
            SnapshotBinary.Write(writer, value.Effect);
            SnapshotBinary.Write(writer, value.BaseMagnitude);
            SnapshotBinary.Write(writer, value.PerLevel);
            Write(writer, ref value.Scaling);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TalentWage value)
        {
            SnapshotBinary.Write(writer, value.BaseAmount);
            SnapshotBinary.Write(writer, value.PerLevel);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TechnologyDefinition value)
        {
            Write(writer, ref value.Metadata);
            SnapshotBinary.Write(writer, value.ResearchPointCost);
            SnapshotBinary.Write(writer, value.Repeatable);
            Write(writer, ref value.Prerequisites);
            Write(writer, ref value.Rewards);
            Write(writer, ref value.Effects);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.TechnologyRequirement value)
        {
            SnapshotBinary.Write(writer, value.Order);
            SnapshotBinary.Write(writer, value.Technology);
            SnapshotBinary.Write(writer, value.Required);
        }

        internal static void Write(BinaryWriter writer, ref Landsong.ECS.Definitions.UnitCombatStats value)
        {
            SnapshotBinary.Write(writer, value.MaximumHealth);
            SnapshotBinary.Write(writer, value.Damage);
            SnapshotBinary.Write(writer, value.AttackRange);
            SnapshotBinary.Write(writer, value.AttackIntervalSeconds);
            SnapshotBinary.Write(writer, value.MovementSpeed);
            SnapshotBinary.Write(writer, value.ProjectileSpeed);
            Write(writer, ref value.Profile);
        }

        internal static void Write(BinaryWriter writer, ref GridBlob value)
        {
            Write(writer, ref value.Min);
            Write(writer, ref value.Size);
            writer.Write(value.Cells.Length);
            for (int i = 0; i < value.Cells.Length; i++)
            {
                Write(writer, ref value.Cells[i]);
            }

            SnapshotBinary.Write(writer, value.ElevationStep);
            writer.Write(value.NavigationSurfaces.Length);
            for (int i = 0; i < value.NavigationSurfaces.Length; i++)
            {
                Write(writer, ref value.NavigationSurfaces[i]);
            }

            writer.Write(value.Connections.Length);
            for (int i = 0; i < value.Connections.Length; i++)
            {
                Write(writer, ref value.Connections[i]);
            }
        }

        internal static void Write(BinaryWriter writer, ref GridCell value)
        {
            SnapshotBinary.Write(writer, value.Exists);
            SnapshotBinary.Write(writer, value.Buildable);
            SnapshotBinary.Write(writer, value.Traversable);
            SnapshotBinary.Write(writer, value.BlocksProjectile);
            SnapshotBinary.Write(writer, value.EdgeZone);
            SnapshotBinary.Write(writer, value.Elevation);
            SnapshotBinary.Write(writer, value.Surface);
            SnapshotBinary.Write(writer, value.Height);
            SnapshotBinary.Write(writer, value.Terrain);
        }

        internal static void Write(BinaryWriter writer, ref HeroGrowth value)
        {
            SnapshotBinary.Write(writer, value.MaxLevel);
            SnapshotBinary.Write(writer, value.FirstLevelExperience);
            SnapshotBinary.Write(writer, value.ExperienceStep);
            SnapshotBinary.Write(writer, value.HealthPerLevel);
            SnapshotBinary.Write(writer, value.DamagePerLevel);
            SnapshotBinary.Write(writer, value.OfferingExperience);
            SnapshotBinary.Write(writer, value.ContactSeconds);
            SnapshotBinary.Write(writer, value.ExperiencePerSecond);
            SnapshotBinary.Write(writer, value.ThreatReference);
            SnapshotBinary.Write(writer, value.MaximumThreatMultiplier);
        }

        internal static void Write(BinaryWriter writer, ref NavigationSurface value)
        {
            Write(writer, ref value.Cell);
            SnapshotBinary.Write(writer, value.Surface);
            SnapshotBinary.Write(writer, value.Elevation);
        }

        internal static void Write(BinaryWriter writer, ref NightBuildingCondition value)
        {
            SnapshotBinary.Write(writer, value.Building);
            SnapshotBinary.Write(writer, value.Count);
            SnapshotBinary.Write(writer, value.MinimumLevel);
        }

        internal static void Write(BinaryWriter writer, ref NightEnemyChoice value)
        {
            SnapshotBinary.Write(writer, value.Definition);
            SnapshotBinary.Write(writer, value.Weight);
        }

        internal static void Write(BinaryWriter writer, ref NightEventConditions value)
        {
            SnapshotBinary.Write(writer, value.MinimumTurn);
            writer.Write(value.Buildings.Length);
            for (int i = 0; i < value.Buildings.Length; i++)
            {
                Write(writer, ref value.Buildings[i]);
            }

            writer.Write(value.Items.Length);
            for (int i = 0; i < value.Items.Length; i++)
            {
                Write(writer, ref value.Items[i]);
            }

            writer.Write(value.Technologies.Length);
            for (int i = 0; i < value.Technologies.Length; i++)
            {
                Write(writer, ref value.Technologies[i]);
            }

            Write(writer, ref value.Completions);
        }

        internal static void Write(BinaryWriter writer, ref NightEventDefinition value)
        {
            SnapshotBinary.Write(writer, value.Id);
            SnapshotBinary.Write(writer, value.FollowUp);
            SnapshotBinary.Write(writer, value.Kind);
            SnapshotBinary.Write(writer, value.Priority);
            SnapshotBinary.Write(writer, value.MinTurn);
            SnapshotBinary.Write(writer, value.MaxTurn);
            SnapshotBinary.Write(writer, value.Interval);
            SnapshotBinary.Write(writer, value.Cooldown);
            SnapshotBinary.Write(writer, value.WaveCount);
            SnapshotBinary.Write(writer, value.ReturnDelay);
            SnapshotBinary.Write(writer, value.Weight);
            SnapshotBinary.Write(writer, value.BudgetScale);
            SnapshotBinary.Write(writer, value.Once);
            SnapshotBinary.Write(writer, value.ReturnOnly);
            SnapshotBinary.Write(writer, value.Forced);
            SnapshotBinary.Write(writer, value.WaveTimes);
            writer.Write(value.Enemies.Length);
            for (int i = 0; i < value.Enemies.Length; i++)
            {
                Write(writer, ref value.Enemies[i]);
            }

            Write(writer, ref value.Conditions);
        }

        internal static void Write(BinaryWriter writer, ref NightItemCondition value)
        {
            SnapshotBinary.Write(writer, value.Item);
            SnapshotBinary.Write(writer, value.Quantity);
        }

        internal static void Write(BinaryWriter writer, ref NightTechnologyCondition value)
        {
            SnapshotBinary.Write(writer, value.Technology);
            SnapshotBinary.Write(writer, value.Count);
        }

        internal static void Write(BinaryWriter writer, ref OpportunityProfile value)
        {
            SnapshotBinary.Write(writer, value.Kind);
            SnapshotBinary.Write(writer, value.Weight);
            SnapshotBinary.Write(writer, value.MaximumPerNight);
            SnapshotBinary.Write(writer, value.StartFraction);
            SnapshotBinary.Write(writer, value.EndFraction);
            SnapshotBinary.Write(writer, value.Speed);
            SnapshotBinary.Write(writer, value.MinimumResponse);
            SnapshotBinary.Write(writer, value.CaptureRadius);
            SnapshotBinary.Write(writer, value.ResponseRadius);
            SnapshotBinary.Write(writer, value.RouteLength);
            SnapshotBinary.Write(writer, value.Soldiers);
            SnapshotBinary.Write(writer, value.Heroes);
        }

        internal static void Write(BinaryWriter writer, ref SoldierGrowth value)
        {
            SnapshotBinary.Write(writer, value.MaxLevel);
            SnapshotBinary.Write(writer, value.FirstLevelExperience);
            SnapshotBinary.Write(writer, value.ExperienceStep);
            SnapshotBinary.Write(writer, value.BattleExperience);
            SnapshotBinary.Write(writer, value.HealthPerLevel);
            SnapshotBinary.Write(writer, value.DamagePerLevel);
        }

        internal static void Write(BinaryWriter writer, ref TheftProfile value)
        {
            SnapshotBinary.Write(writer, value.Protection);
            SnapshotBinary.Write(writer, value.Weight);
            SnapshotBinary.Write(writer, value.Maximum);
            SnapshotBinary.Write(writer, value.UnitValue);
        }

        internal static void Write(BinaryWriter writer, ref int2 value)
        {
            SnapshotBinary.Write(writer, value.x);
            SnapshotBinary.Write(writer, value.y);
        }
    }
}
