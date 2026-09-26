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
        static void WriteEconomyBillEntry(BinaryWriter writer, EconomyBillEntry value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Item);
            Write(writer, value.Source);
            Write(writer, value.Income);
            Write(writer, value.Expense);
            Write(writer, value.Stored);
            Write(writer, value.Pending);
        }

        static EconomyBillEntry ReadEconomyBillEntry(BinaryReader reader)
        {
            return new EconomyBillEntry
            {
                Turn = Read<int>(reader),
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Source = Read<ulong>(reader),
                Income = Read<long>(reader),
                Expense = Read<long>(reader),
                Stored = Read<long>(reader),
                Pending = Read<long>(reader),
            };
        }

        static void WriteEconomyEntry(BinaryWriter writer, EconomyEntry value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Delta);
            Write(writer, value.Item);
            Write(writer, value.Source);
            Write(writer, value.Reason);
            Write(writer, value.Pending);
            Write(writer, value.SourceName);
            Write(writer, value.Note);
        }

        static EconomyEntry ReadEconomyEntry(BinaryReader reader)
        {
            return new EconomyEntry
            {
                Turn = Read<int>(reader),
                Delta = Read<int>(reader),
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Source = Read<ulong>(reader),
                Reason = Read<EconomyReason>(reader),
                Pending = Read<byte>(reader),
                SourceName = Read<FixedString128Bytes>(reader),
                Note = Read<FixedString128Bytes>(reader),
            };
        }

        static void WriteExpedition(BinaryWriter writer, Expedition value)
        {
            Write(writer, value.Site);
            Write(writer, value.Captain);
            Write(writer, value.SourceName);
            Write(writer, value.Crew);
            Write(writer, value.Departure);
            Write(writer, value.Arrival);
            Write(writer, value.SourceLevel);
            Write(writer, value.Casualties);
            Write(writer, value.SubsidyRequired);
            Write(writer, value.SubsidyPaid);
            Write(writer, value.PenaltyStacks);
            Write(writer, value.RewardBonus);
            Write(writer, value.SuccessChance);
            Write(writer, value.Status);
        }

        static Expedition ReadExpedition(BinaryReader reader)
        {
            return new Expedition
            {
                Site = Read<ulong>(reader),
                Captain = Read<ulong>(reader),
                SourceName = Read<FixedString128Bytes>(reader),
                Crew = Read<int>(reader),
                Departure = Read<int>(reader),
                Arrival = Read<int>(reader),
                SourceLevel = Read<int>(reader),
                Casualties = Read<int>(reader),
                SubsidyRequired = Read<int>(reader),
                SubsidyPaid = Read<int>(reader),
                PenaltyStacks = Read<int>(reader),
                RewardBonus = Read<float>(reader),
                SuccessChance = Read<float>(reader),
                Status = Read<ExpeditionStatus>(reader),
            };
        }

        static void WriteExpeditionDestinationHistory(BinaryWriter writer, ExpeditionDestinationHistory value)
        {
            Write(writer, value.Definition);
        }

        static ExpeditionDestinationHistory ReadExpeditionDestinationHistory(BinaryReader reader)
        {
            return new ExpeditionDestinationHistory
            {
                Definition = Read<Landsong.ECS.Definitions.ExpeditionId>(reader),
            };
        }

        static void WriteExpeditionPenaltyState(BinaryWriter writer, ExpeditionPenaltyState value)
        {
            Write(writer, value.Stacks);
            Write(writer, value.UntilTurn);
        }

        static ExpeditionPenaltyState ReadExpeditionPenaltyState(BinaryReader reader)
        {
            return new ExpeditionPenaltyState
            {
                Stacks = Read<int>(reader),
                UntilTurn = Read<int>(reader),
            };
        }

        static void WriteExpeditionSettings(BinaryWriter writer, ExpeditionSettings value)
        {
            Write(writer, value.PenaltyTurns);
            Write(writer, value.AttractionPerStack);
        }

        static ExpeditionSettings ReadExpeditionSettings(BinaryReader reader)
        {
            return new ExpeditionSettings
            {
                PenaltyTurns = Read<int>(reader),
                AttractionPerStack = Read<float>(reader),
            };
        }

        static void WriteExpeditionSupply(BinaryWriter writer, ExpeditionSupply value)
        {
            Write(writer, value.Item);
            Write(writer, value.Amount);
        }

        static ExpeditionSupply ReadExpeditionSupply(BinaryReader reader)
        {
            return new ExpeditionSupply
            {
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Amount = Read<int>(reader),
            };
        }

        static void WriteFoodSelection(BinaryWriter writer, FoodSelection value)
        {
            Write(writer, value.Group);
            Write(writer, value.Item);
            Write(writer, value.Amount);
        }

        static FoodSelection ReadFoodSelection(BinaryReader reader)
        {
            return new FoodSelection
            {
                Group = Read<Landsong.ECS.Definitions.ItemGroupId>(reader),
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Amount = Read<int>(reader),
            };
        }

        static void WriteGameClock(BinaryWriter writer, GameClock value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Time);
            Write(writer, value.PhaseTime);
            Write(writer, value.DawnRemaining);
            Write(writer, value.DawnSourceNightTime);
        }

        static GameClock ReadGameClock(BinaryReader reader)
        {
            return new GameClock
            {
                Turn = Read<int>(reader),
                Time = Read<float>(reader),
                PhaseTime = Read<float>(reader),
                DawnRemaining = Read<float>(reader),
                DawnSourceNightTime = Read<float>(reader),
            };
        }

        static void WriteHealth(BinaryWriter writer, Health value)
        {
            Write(writer, value.Current);
            Write(writer, value.Maximum);
        }

        static Health ReadHealth(BinaryReader reader)
        {
            return new Health
            {
                Current = Read<float>(reader),
                Maximum = Read<float>(reader),
            };
        }

        static void WriteHero(BinaryWriter writer, Hero value)
        {
            Write(writer, value.Sanctum);
            Write(writer, value.CooldownUntil);
            Write(writer, value.Experience);
            Write(writer, value.LastCombatTurn);
            Write(writer, value.Recruited);
            Write(writer, value.DeathPending);
        }

        static Hero ReadHero(BinaryReader reader)
        {
            return new Hero
            {
                Sanctum = Read<ulong>(reader),
                CooldownUntil = Read<int>(reader),
                Experience = Read<int>(reader),
                LastCombatTurn = Read<int>(reader),
                Recruited = Read<byte>(reader),
                DeathPending = Read<byte>(reader),
            };
        }

        static void WriteHeroSelection(BinaryWriter writer, HeroSelection value)
        {
            Write(writer, value.SelectedHero);
        }

        static HeroSelection ReadHeroSelection(BinaryReader reader)
        {
            return new HeroSelection
            {
                SelectedHero = Read<Entity>(reader),
            };
        }

        static void WriteHistoryEntry(BinaryWriter writer, HistoryEntry value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Delta);
            Write(writer, value.Count);
            Write(writer, value.Item);
            Write(writer, value.Source);
            Write(writer, value.Pending);
            Write(writer, value.HasPosition);
            Write(writer, value.Transfer);
            Write(writer, value.Category);
            Write(writer, value.Position);
            Write(writer, value.SourceName);
            Write(writer, value.Text);
        }

        static HistoryEntry ReadHistoryEntry(BinaryReader reader)
        {
            return new HistoryEntry
            {
                Turn = Read<int>(reader),
                Delta = Read<int>(reader),
                Count = Read<int>(reader),
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Source = Read<ulong>(reader),
                Pending = Read<byte>(reader),
                HasPosition = Read<byte>(reader),
                Transfer = Read<byte>(reader),
                Category = Read<HistoryCategory>(reader),
                Position = Read<float3>(reader),
                SourceName = Read<FixedString128Bytes>(reader),
                Text = Read<FixedString128Bytes>(reader),
            };
        }

        static void WriteIdentity(BinaryWriter writer, Identity value)
        {
            Write(writer, value.Id);
            Write(writer, value.Name);
        }

        static Identity ReadIdentity(BinaryReader reader)
        {
            return new Identity
            {
                Id = Read<ulong>(reader),
                Name = Read<FixedString128Bytes>(reader),
            };
        }

        static void WriteIdentitySequence(BinaryWriter writer, IdentitySequence value)
        {
            Write(writer, value.NextId);
        }

        static IdentitySequence ReadIdentitySequence(BinaryReader reader)
        {
            return new IdentitySequence
            {
                NextId = Read<ulong>(reader),
            };
        }

        static void WriteInitialBuilding(BinaryWriter writer, InitialBuilding value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Level);
            Write(writer, value.Rotation);
            Write(writer, value.Cell);
            Write(writer, value.Name);
        }

        static InitialBuilding ReadInitialBuilding(BinaryReader reader)
        {
            return new InitialBuilding
            {
                Definition = Read<Landsong.ECS.Definitions.BuildingId>(reader),
                Level = Read<int>(reader),
                Rotation = Read<int>(reader),
                Cell = Read<int2>(reader),
                Name = Read<FixedString128Bytes>(reader),
            };
        }

        static void WriteInitialRoyal(BinaryWriter writer, InitialRoyal value)
        {
            Write(writer, value.Name);
            Write(writer, value.Age);
            Write(writer, value.Role);
            Write(writer, value.Gender);
            Write(writer, value.Traits);
        }

        static InitialRoyal ReadInitialRoyal(BinaryReader reader)
        {
            return new InitialRoyal
            {
                Name = Read<FixedString128Bytes>(reader),
                Age = Read<int>(reader),
                Role = Read<byte>(reader),
                Gender = Read<PersonGender>(reader),
                Traits = Read<FixedList128Bytes<Landsong.ECS.Definitions.RoyalTraitId>>(reader),
            };
        }

        static void WriteIntelligenceModeState(BinaryWriter writer, IntelligenceModeState value)
        {
            Write(writer, value.Enabled);
        }

        static IntelligenceModeState ReadIntelligenceModeState(BinaryReader reader)
        {
            return new IntelligenceModeState
            {
                Enabled = Read<byte>(reader),
            };
        }

        static void WriteIntelligenceSettings(BinaryWriter writer, IntelligenceSettings value)
        {
            Write(writer, value.LowIntel);
            Write(writer, value.MediumIntel);
            Write(writer, value.HighIntel);
            Write(writer, value.MediumIntelLead);
            Write(writer, value.HighIntelLead);
        }

        static IntelligenceSettings ReadIntelligenceSettings(BinaryReader reader)
        {
            return new IntelligenceSettings
            {
                LowIntel = Read<int>(reader),
                MediumIntel = Read<int>(reader),
                HighIntel = Read<int>(reader),
                MediumIntelLead = Read<float>(reader),
                HighIntelLead = Read<float>(reader),
            };
        }

        static void WriteInventorySlot(BinaryWriter writer, InventorySlot value)
        {
            Write(writer, value.Provider);
            Write(writer, value.Index);
            Write(writer, value.Count);
            Write(writer, value.SlotType);
            Write(writer, value.Item);
            Write(writer, value.LossRemainder);
            Write(writer, value.Unavailable);
        }

        static InventorySlot ReadInventorySlot(BinaryReader reader)
        {
            return new InventorySlot
            {
                Provider = Read<ulong>(reader),
                Index = Read<int>(reader),
                Count = Read<int>(reader),
                SlotType = Read<Landsong.ECS.Definitions.StorageSlotId>(reader),
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                LossRemainder = Read<float>(reader),
                Unavailable = Read<byte>(reader),
            };
        }

        static void WriteNightEventHistory(BinaryWriter writer, NightEventHistory value)
        {
            Write(writer, value.Event);
            Write(writer, value.LastTurn);
            Write(writer, value.Count);
        }

        static NightEventHistory ReadNightEventHistory(BinaryReader reader)
        {
            return new NightEventHistory
            {
                Event = Read<FixedString64Bytes>(reader),
                LastTurn = Read<int>(reader),
                Count = Read<int>(reader),
            };
        }

        static void WriteNightPlanState(BinaryWriter writer, NightPlanState value)
        {
            Write(writer, value.Event);
            Write(writer, value.Turn);
            Write(writer, value.BaseThreat);
            Write(writer, value.PreparedTurn);
            Write(writer, value.BossDefinition);
            Write(writer, value.DifficultyScale);
            Write(writer, value.CombatElapsed);
            Write(writer, value.FirstActionAt);
            Write(writer, value.ClockStarted);
            Write(writer, value.Committed);
            Write(writer, value.AnySpawned);
            Write(writer, value.BossKilled);
            Write(writer, value.BossEscaped);
        }

        static NightPlanState ReadNightPlanState(BinaryReader reader)
        {
            return new NightPlanState
            {
                Event = Read<FixedString64Bytes>(reader),
                Turn = Read<int>(reader),
                BaseThreat = Read<int>(reader),
                PreparedTurn = Read<int>(reader),
                BossDefinition = Read<Landsong.ECS.Definitions.EnemyId>(reader),
                DifficultyScale = Read<float>(reader),
                CombatElapsed = Read<float>(reader),
                FirstActionAt = Read<float>(reader),
                ClockStarted = Read<byte>(reader),
                Committed = Read<byte>(reader),
                AnySpawned = Read<byte>(reader),
                BossKilled = Read<byte>(reader),
                BossEscaped = Read<byte>(reader),
            };
        }

        static void WriteNightRules(BinaryWriter writer, NightRules value)
        {
            Write(writer, value.WarningSeconds);
            Write(writer, value.ProtectionSeconds);
            Write(writer, value.MinSpawnRegions);
            Write(writer, value.MaxSpawnRegions);
            Write(writer, value.SpawnRegionSize);
            Write(writer, value.SpawnRegionGap);
            Write(writer, value.HeroWeight);
            Write(writer, value.FacilityWeight);
            Write(writer, value.TargetRadius);
            Write(writer, value.ThreatFloor);
            Write(writer, value.ThreatPerStrengthCap);
        }

        static NightRules ReadNightRules(BinaryReader reader)
        {
            return new NightRules
            {
                WarningSeconds = Read<float>(reader),
                ProtectionSeconds = Read<float>(reader),
                MinSpawnRegions = Read<int>(reader),
                MaxSpawnRegions = Read<int>(reader),
                SpawnRegionSize = Read<int>(reader),
                SpawnRegionGap = Read<int>(reader),
                HeroWeight = Read<float>(reader),
                FacilityWeight = Read<float>(reader),
                TargetRadius = Read<float>(reader),
                ThreatFloor = Read<float>(reader),
                ThreatPerStrengthCap = Read<float>(reader),
            };
        }

        static void WriteNightRuntimeState(BinaryWriter writer, NightRuntimeState value)
        {
            Write(writer, value.Kind);
            Write(writer, value.Seed);
            Write(writer, value.Duration);
            Write(writer, value.Speed);
            Write(writer, value.Threat);
            Write(writer, value.StartCombatStrength);
            Write(writer, value.DeploymentTime);
            Write(writer, value.BossEscaped);
            Write(writer, value.BossReturnTurn);
            Write(writer, value.Intelligence);
        }

        static NightRuntimeState ReadNightRuntimeState(BinaryReader reader)
        {
            return new NightRuntimeState
            {
                Kind = Read<NightKind>(reader),
                Seed = Read<uint>(reader),
                Duration = Read<float>(reader),
                Speed = Read<byte>(reader),
                Threat = Read<int>(reader),
                StartCombatStrength = Read<int>(reader),
                DeploymentTime = Read<float>(reader),
                BossEscaped = Read<byte>(reader),
                BossReturnTurn = Read<int>(reader),
                Intelligence = Read<int>(reader),
            };
        }
    }
}
