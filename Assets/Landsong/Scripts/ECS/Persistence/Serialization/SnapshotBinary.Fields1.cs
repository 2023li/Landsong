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
    public static partial class SnapshotBinary
    {
        static void WriteBattleHistoryEntry(BinaryWriter writer, BattleHistoryEntry value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Entry);
        }

        static BattleHistoryEntry ReadBattleHistoryEntry(BinaryReader reader)
        {
            return new BattleHistoryEntry
            {
                Turn = Read<int>(reader),
                Entry = Read<BattleReportEntry>(reader),
            };
        }

        static void WriteBattleReportEntry(BinaryWriter writer, BattleReportEntry value)
        {
            Write(writer, value.Kind);
            Write(writer, value.Id);
            Write(writer, value.Building);
            Write(writer, value.Item);
            Write(writer, value.Amount);
            Write(writer, value.Value);
            Write(writer, value.SourceName);
        }

        static BattleReportEntry ReadBattleReportEntry(BinaryReader reader)
        {
            return new BattleReportEntry
            {
                Kind = Read<EventKind>(reader),
                Id = Read<ulong>(reader),
                Building = Read<Landsong.ECS.Definitions.BuildingId>(reader),
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Amount = Read<int>(reader),
                Value = Read<float>(reader),
                SourceName = Read<FixedString128Bytes>(reader),
            };
        }

        static void WriteBellState(BinaryWriter writer, BellState value)
        {
            Write(writer, value.ActiveBell);
        }

        static BellState ReadBellState(BinaryReader reader)
        {
            return new BellState
            {
                ActiveBell = Read<ulong>(reader),
            };
        }

        static void WriteBlueprintUnlock(BinaryWriter writer, BlueprintUnlock value)
        {
            Write(writer, value.Building);
            Write(writer, value.MaximumLevel);
        }

        static BlueprintUnlock ReadBlueprintUnlock(BinaryReader reader)
        {
            return new BlueprintUnlock
            {
                Building = Read<Landsong.ECS.Definitions.BuildingId>(reader),
                MaximumLevel = Read<int>(reader),
            };
        }

        static void WriteBuilding(BinaryWriter writer, Building value)
        {
            Write(writer, value.Stage);
            Write(writer, value.Level);
            Write(writer, value.RuinPending);
        }

        static Building ReadBuilding(BinaryReader reader)
        {
            return new Building
            {
                Stage = Read<LifeStage>(reader),
                Level = Read<int>(reader),
                RuinPending = Read<byte>(reader),
            };
        }

        static void WriteBuildingAppearanceState(BinaryWriter writer, BuildingAppearanceState value)
        {
            Write(writer, value.Skin);
        }

        static BuildingAppearanceState ReadBuildingAppearanceState(BinaryReader reader)
        {
            return new BuildingAppearanceState
            {
                Skin = Read<FixedString64Bytes>(reader),
            };
        }

        static void WriteBuildingConstructionState(BinaryWriter writer, BuildingConstructionState value)
        {
            Write(writer, value.Progress);
            Write(writer, value.RepairDuration);
            Write(writer, value.RepairCompletedTurn);
        }

        static BuildingConstructionState ReadBuildingConstructionState(BinaryReader reader)
        {
            return new BuildingConstructionState
            {
                Progress = Read<int>(reader),
                RepairDuration = Read<int>(reader),
                RepairCompletedTurn = Read<int>(reader),
            };
        }

        static void WriteBuildingExperienceState(BinaryWriter writer, BuildingExperienceState value)
        {
            Write(writer, value.Experience);
        }

        static BuildingExperienceState ReadBuildingExperienceState(BinaryReader reader)
        {
            return new BuildingExperienceState
            {
                Experience = Read<int>(reader),
            };
        }

        static void WriteBuildingFarmingState(BinaryWriter writer, BuildingFarmingState value)
        {
            Write(writer, value.Crop);
            Write(writer, value.Progress);
            Write(writer, value.Seed);
            Write(writer, value.FullCycle);
            Write(writer, value.AutoHarvest);
        }

        static BuildingFarmingState ReadBuildingFarmingState(BinaryReader reader)
        {
            return new BuildingFarmingState
            {
                Crop = Read<Landsong.ECS.Definitions.CropId>(reader),
                Progress = Read<int>(reader),
                Seed = Read<uint>(reader),
                FullCycle = Read<byte>(reader),
                AutoHarvest = Read<byte>(reader),
            };
        }

        static void WriteBuildingGatheringState(BinaryWriter writer, BuildingGatheringState value)
        {
            Write(writer, value.RemainingUses);
        }

        static BuildingGatheringState ReadBuildingGatheringState(BinaryReader reader)
        {
            return new BuildingGatheringState
            {
                RemainingUses = Read<int>(reader),
            };
        }

        static void WriteBuildingHousingState(BinaryWriter writer, BuildingHousingState value)
        {
            Write(writer, value.Population);
            Write(writer, value.Growth);
            Write(writer, value.FoodFailures);
            Write(writer, value.TaxProgress);
            Write(writer, value.DeferredResidents);
        }

        static BuildingHousingState ReadBuildingHousingState(BinaryReader reader)
        {
            return new BuildingHousingState
            {
                Population = Read<int>(reader),
                Growth = Read<int>(reader),
                FoodFailures = Read<int>(reader),
                TaxProgress = Read<int>(reader),
                DeferredResidents = Read<int>(reader),
            };
        }

        static void WriteBuildingInvestment(BinaryWriter writer, BuildingInvestment value)
        {
            Write(writer, value.Item);
            Write(writer, value.Amount);
        }

        static BuildingInvestment ReadBuildingInvestment(BinaryReader reader)
        {
            return new BuildingInvestment
            {
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Amount = Read<int>(reader),
            };
        }

        static void WriteBuildingMaintenanceState(BinaryWriter writer, BuildingMaintenanceState value)
        {
            Write(writer, value.Maintained);
        }

        static BuildingMaintenanceState ReadBuildingMaintenanceState(BinaryReader reader)
        {
            return new BuildingMaintenanceState
            {
                Maintained = Read<byte>(reader),
            };
        }

        static void WriteBuildingMarketState(BinaryWriter writer, BuildingMarketState value)
        {
            Write(writer, value.TurnValue);
            Write(writer, value.LifetimeValue);
        }

        static BuildingMarketState ReadBuildingMarketState(BinaryReader reader)
        {
            return new BuildingMarketState
            {
                TurnValue = Read<long>(reader),
                LifetimeValue = Read<long>(reader),
            };
        }

        static void WriteBuildingPlacementState(BinaryWriter writer, BuildingPlacementState value)
        {
            Write(writer, value.Cell);
            Write(writer, value.Size);
            Write(writer, value.Rotation);
            Write(writer, value.Elevation);
            Write(writer, value.Surface);
        }

        static BuildingPlacementState ReadBuildingPlacementState(BinaryReader reader)
        {
            return new BuildingPlacementState
            {
                Cell = Read<int2>(reader),
                Size = Read<int2>(reader),
                Rotation = Read<int>(reader),
                Elevation = Read<int>(reader),
                Surface = Read<int>(reader),
            };
        }

        static void WriteBuildingProductionState(BinaryWriter writer, BuildingProductionState value)
        {
            Write(writer, value.Progress);
        }

        static BuildingProductionState ReadBuildingProductionState(BinaryReader reader)
        {
            return new BuildingProductionState
            {
                Progress = Read<int>(reader),
            };
        }

        static void WriteBuildingRecruitmentState(BinaryWriter writer, BuildingRecruitmentState value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Count);
        }

        static BuildingRecruitmentState ReadBuildingRecruitmentState(BinaryReader reader)
        {
            return new BuildingRecruitmentState
            {
                Turn = Read<int>(reader),
                Count = Read<int>(reader),
            };
        }

        static void WriteBuildingSanctumState(BinaryWriter writer, BuildingSanctumState value)
        {
            Write(writer, value.Offering);
            Write(writer, value.PaidOfferingTurn);
            Write(writer, value.WokenTurn);
        }

        static BuildingSanctumState ReadBuildingSanctumState(BinaryReader reader)
        {
            return new BuildingSanctumState
            {
                Offering = Read<byte>(reader),
                PaidOfferingTurn = Read<int>(reader),
                WokenTurn = Read<int>(reader),
            };
        }

        static void WriteBuildingWorkforceState(BinaryWriter writer, BuildingWorkforceState value)
        {
            Write(writer, value.Workers);
            Write(writer, value.StableWorkers);
            Write(writer, value.WorkerTarget);
            Write(writer, value.ProtectionUntil);
            Write(writer, value.PaidSubsidy);
            Write(writer, value.PaidSubsidyTurn);
            Write(writer, value.SubsidyBudget);
            Write(writer, value.Subsidy);
        }

        static BuildingWorkforceState ReadBuildingWorkforceState(BinaryReader reader)
        {
            return new BuildingWorkforceState
            {
                Workers = Read<int>(reader),
                StableWorkers = Read<int>(reader),
                WorkerTarget = Read<int>(reader),
                ProtectionUntil = Read<int>(reader),
                PaidSubsidy = Read<int>(reader),
                PaidSubsidyTurn = Read<int>(reader),
                SubsidyBudget = Read<int>(reader),
                Subsidy = Read<byte>(reader),
            };
        }

        static void WriteClaimedQuest(BinaryWriter writer, ClaimedQuest value)
        {
            Write(writer, value.Quest);
        }

        static ClaimedQuest ReadClaimedQuest(BinaryReader reader)
        {
            return new ClaimedQuest
            {
                Quest = Read<Landsong.ECS.Definitions.QuestId>(reader),
            };
        }

        static void WriteCombatProfile(BinaryWriter writer, CombatProfile value)
        {
            Write(writer, value.DetectionRadius);
            Write(writer, value.ChaseRadius);
            Write(writer, value.ChaseSeconds);
            Write(writer, value.BodyRadius);
            Write(writer, value.Armor);
            Write(writer, value.Reduction);
            Write(writer, value.Penetration);
            Write(writer, value.BlastRadius);
            Write(writer, value.WarningSeconds);
            Write(writer, value.ProjectileLifetime);
            Write(writer, value.ProjectileMode);
            Write(writer, value.Traits);
            Write(writer, value.BlocksProjectile);
        }

        static CombatProfile ReadCombatProfile(BinaryReader reader)
        {
            return new CombatProfile
            {
                DetectionRadius = Read<float>(reader),
                ChaseRadius = Read<float>(reader),
                ChaseSeconds = Read<float>(reader),
                BodyRadius = Read<float>(reader),
                Armor = Read<float>(reader),
                Reduction = Read<float>(reader),
                Penetration = Read<float>(reader),
                BlastRadius = Read<float>(reader),
                WarningSeconds = Read<float>(reader),
                ProjectileLifetime = Read<float>(reader),
                ProjectileMode = Read<ProjectileMode>(reader),
                Traits = Read<TacticalTraits>(reader),
                BlocksProjectile = Read<bool>(reader),
            };
        }

        static void WriteCombatStatsSnapshot(BinaryWriter writer, CombatStatsSnapshot value)
        {
            Write(writer, value.Health);
            Write(writer, value.Damage);
            Write(writer, value.Speed);
            Write(writer, value.Range);
            Write(writer, value.Interval);
            Write(writer, value.ProjectileSpeed);
            Write(writer, value.Combat);
        }

        static CombatStatsSnapshot ReadCombatStatsSnapshot(BinaryReader reader)
        {
            return new CombatStatsSnapshot
            {
                Health = Read<float>(reader),
                Damage = Read<float>(reader),
                Speed = Read<float>(reader),
                Range = Read<float>(reader),
                Interval = Read<float>(reader),
                ProjectileSpeed = Read<float>(reader),
                Combat = Read<CombatProfile>(reader),
            };
        }

        static void WriteCompletedExpedition(BinaryWriter writer, CompletedExpedition value)
        {
            Write(writer, value.Expedition);
        }

        static CompletedExpedition ReadCompletedExpedition(BinaryReader reader)
        {
            return new CompletedExpedition
            {
                Expedition = Read<Landsong.ECS.Definitions.ExpeditionId>(reader),
            };
        }

        static void WriteCourtLogEntry(BinaryWriter writer, CourtLogEntry value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Person);
            Write(writer, value.Message);
        }

        static CourtLogEntry ReadCourtLogEntry(BinaryReader reader)
        {
            return new CourtLogEntry
            {
                Turn = Read<int>(reader),
                Person = Read<ulong>(reader),
                Message = Read<FixedString128Bytes>(reader),
            };
        }
    }
}
