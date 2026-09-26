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
        static void WriteNightSettings(BinaryWriter writer, NightSettings value)
        {
            Write(writer, value.NightSeconds);
            Write(writer, value.DeployInterval);
            Write(writer, value.NightPreparationSeconds);
            Write(writer, value.NightClosureSeconds);
            Write(writer, value.RetreatDelaySeconds);
            Write(writer, value.VictoryCaptionDelaySeconds);
            Write(writer, value.CelebrationDelaySeconds);
            Write(writer, value.VictoryAdvanceDelaySeconds);
            Write(writer, value.WaveIntervalSeconds);
            Write(writer, value.DawnSeconds);
            Write(writer, value.InvasionChance);
            Write(writer, value.StrengthRatio);
            Write(writer, value.RetryStep);
            Write(writer, value.RetryCap);
            Write(writer, value.FirstInvasion);
            Write(writer, value.FirstBoss);
            Write(writer, value.BossInterval);
            Write(writer, value.ThreatPerTurn);
            Write(writer, value.DifficultyPerTurn);
            Write(writer, value.ExpectedPowerAtTurnOne);
            Write(writer, value.ExpectedPowerPerTurn);
            Write(writer, value.PlayerPowerSensitivity);
        }

        static NightSettings ReadNightSettings(BinaryReader reader)
        {
            return new NightSettings
            {
                NightSeconds = Read<float>(reader),
                DeployInterval = Read<float>(reader),
                NightPreparationSeconds = Read<float>(reader),
                NightClosureSeconds = Read<float>(reader),
                RetreatDelaySeconds = Read<float>(reader),
                VictoryCaptionDelaySeconds = Read<float>(reader),
                CelebrationDelaySeconds = Read<float>(reader),
                VictoryAdvanceDelaySeconds = Read<float>(reader),
                WaveIntervalSeconds = Read<float>(reader),
                DawnSeconds = Read<float>(reader),
                InvasionChance = Read<float>(reader),
                StrengthRatio = Read<float>(reader),
                RetryStep = Read<float>(reader),
                RetryCap = Read<float>(reader),
                FirstInvasion = Read<int>(reader),
                FirstBoss = Read<int>(reader),
                BossInterval = Read<int>(reader),
                ThreatPerTurn = Read<int>(reader),
                DifficultyPerTurn = Read<float>(reader),
                ExpectedPowerAtTurnOne = Read<float>(reader),
                ExpectedPowerPerTurn = Read<float>(reader),
                PlayerPowerSensitivity = Read<float>(reader),
            };
        }

        static void WriteNightWave(BinaryWriter writer, NightWave value)
        {
            Write(writer, value.At);
            Write(writer, value.PowerScale);
            Write(writer, value.WarnedAt);
            Write(writer, value.WaveIndex);
            Write(writer, value.Definition);
            Write(writer, value.Count);
            Write(writer, value.Direction);
            Write(writer, value.Region);
            Write(writer, value.Position);
            Write(writer, value.Target);
            Write(writer, value.Spawned);
            Write(writer, value.Warned);
            Write(writer, value.SpatiallyBlocked);
        }

        static NightWave ReadNightWave(BinaryReader reader)
        {
            return new NightWave
            {
                At = Read<float>(reader),
                PowerScale = Read<float>(reader),
                WarnedAt = Read<float>(reader),
                WaveIndex = Read<int>(reader),
                Definition = Read<Landsong.ECS.Definitions.EnemyId>(reader),
                Count = Read<int>(reader),
                Direction = Read<int>(reader),
                Region = Read<int>(reader),
                Position = Read<float3>(reader),
                Target = Read<ulong>(reader),
                Spawned = Read<byte>(reader),
                Warned = Read<byte>(reader),
                SpatiallyBlocked = Read<byte>(reader),
            };
        }

        static void WriteOwnedBuff(BinaryWriter writer, OwnedBuff value)
        {
            Write(writer, value.Buff);
            Write(writer, value.Level);
        }

        static OwnedBuff ReadOwnedBuff(BinaryReader reader)
        {
            return new OwnedBuff
            {
                Buff = Read<Landsong.ECS.Definitions.BuffId>(reader),
                Level = Read<int>(reader),
            };
        }

        static void WritePeacefulRules(BinaryWriter writer, PeacefulRules value)
        {
            Write(writer, value.MaximumPerNight);
            Write(writer, value.MaximumConcurrent);
            Write(writer, value.TheftValueBudget);
            Write(writer, value.FirstOpportunity);
            Write(writer, value.Interval);
        }

        static PeacefulRules ReadPeacefulRules(BinaryReader reader)
        {
            return new PeacefulRules
            {
                MaximumPerNight = Read<int>(reader),
                MaximumConcurrent = Read<int>(reader),
                TheftValueBudget = Read<int>(reader),
                FirstOpportunity = Read<float>(reader),
                Interval = Read<float>(reader),
            };
        }

        static void WritePendingItem(BinaryWriter writer, PendingItem value)
        {
            Write(writer, value.Item);
            Write(writer, value.Amount);
            Write(writer, value.LossRemainder);
        }

        static PendingItem ReadPendingItem(BinaryReader reader)
        {
            return new PendingItem
            {
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Amount = Read<int>(reader),
                LossRemainder = Read<float>(reader),
            };
        }

        static void WritePersistenceGate(BinaryWriter writer, PersistenceGate value)
        {
            Write(writer, value.CheckpointPending);
        }

        static PersistenceGate ReadPersistenceGate(BinaryReader reader)
        {
            return new PersistenceGate
            {
                CheckpointPending = Read<byte>(reader),
            };
        }

        static void WritePersonRequestEntry(BinaryWriter writer, PersonRequestEntry value)
        {
            Write(writer, value.Kind);
            Write(writer, value.Status);
            Write(writer, value.CreatedTurn);
            Write(writer, value.ResolvedTurn);
            Write(writer, value.Journey);
        }

        static PersonRequestEntry ReadPersonRequestEntry(BinaryReader reader)
        {
            return new PersonRequestEntry
            {
                Kind = Read<PersonRequestKind>(reader),
                Status = Read<PersonRequestStatus>(reader),
                CreatedTurn = Read<int>(reader),
                ResolvedTurn = Read<int>(reader),
                Journey = Read<ulong>(reader),
            };
        }

        static void WritePolicyChoice(BinaryWriter writer, PolicyChoice value)
        {
            Write(writer, value.Definition);
        }

        static PolicyChoice ReadPolicyChoice(BinaryReader reader)
        {
            return new PolicyChoice
            {
                Definition = Read<Landsong.ECS.Definitions.PolicyId>(reader),
            };
        }

        static void WritePopulationState(BinaryWriter writer, PopulationState value)
        {
            Write(writer, value.BasePopulation);
        }

        static PopulationState ReadPopulationState(BinaryReader reader)
        {
            return new PopulationState
            {
                BasePopulation = Read<int>(reader),
            };
        }

        static void WritePortraitDNA(BinaryWriter writer, PortraitDNA value)
        {
            Write(writer, value.Parts);
            Write(writer, value.SkinDetails);
            Write(writer, value.Skin);
            Write(writer, value.Hair);
            Write(writer, value.Eyes);
            Write(writer, value.Seed);
            Write(writer, value.Customized);
            Write(writer, value.InvitationAnnounced);
        }

        static PortraitDNA ReadPortraitDNA(BinaryReader reader)
        {
            return new PortraitDNA
            {
                Parts = Read<FixedList128Bytes<int>>(reader),
                SkinDetails = Read<FixedList64Bytes<int>>(reader),
                Skin = Read<Color32>(reader),
                Hair = Read<Color32>(reader),
                Eyes = Read<Color32>(reader),
                Seed = Read<uint>(reader),
                Customized = Read<byte>(reader),
                InvitationAnnounced = Read<byte>(reader),
            };
        }

        static void WritePortraitSettings(BinaryWriter writer, PortraitSettings value)
        {
            Write(writer, value.YouthAge);
            Write(writer, value.GreyAge);
            Write(writer, value.ElderAge);
            Write(writer, value.SoldierRecruitMinAge);
            Write(writer, value.SoldierRecruitMaxAge);
            Write(writer, value.SoldierLifeMin);
            Write(writer, value.SoldierLifeMax);
            Write(writer, value.ColorMutation);
        }

        static PortraitSettings ReadPortraitSettings(BinaryReader reader)
        {
            return new PortraitSettings
            {
                YouthAge = Read<int>(reader),
                GreyAge = Read<int>(reader),
                ElderAge = Read<int>(reader),
                SoldierRecruitMinAge = Read<int>(reader),
                SoldierRecruitMaxAge = Read<int>(reader),
                SoldierLifeMin = Read<int>(reader),
                SoldierLifeMax = Read<int>(reader),
                ColorMutation = Read<float>(reader),
            };
        }

        static void WritePreparedBuildingDefense(BinaryWriter writer, PreparedBuildingDefense value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Profile);
        }

        static PreparedBuildingDefense ReadPreparedBuildingDefense(BinaryReader reader)
        {
            return new PreparedBuildingDefense
            {
                Definition = Read<Landsong.ECS.Definitions.BuildingId>(reader),
                Profile = Read<CombatProfile>(reader),
            };
        }

        static void WritePreparedHero(BinaryWriter writer, PreparedHero value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Stats);
        }

        static PreparedHero ReadPreparedHero(BinaryReader reader)
        {
            return new PreparedHero
            {
                Definition = Read<Landsong.ECS.Definitions.HeroId>(reader),
                Stats = Read<CombatStatsSnapshot>(reader),
            };
        }

        static void WritePreparedSoldier(BinaryWriter writer, PreparedSoldier value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Stats);
        }

        static PreparedSoldier ReadPreparedSoldier(BinaryReader reader)
        {
            return new PreparedSoldier
            {
                Definition = Read<Landsong.ECS.Definitions.SoldierId>(reader),
                Stats = Read<CombatStatsSnapshot>(reader),
            };
        }

        static void WritePublicOpinionState(BinaryWriter writer, PublicOpinionState value)
        {
            Write(writer, value.Value);
        }

        static PublicOpinionState ReadPublicOpinionState(BinaryReader reader)
        {
            return new PublicOpinionState
            {
                Value = Read<int>(reader),
            };
        }

        static void WriteQuest(BinaryWriter writer, Quest value)
        {
            Write(writer, value.Status);
            Write(writer, value.StartTurn);
            Write(writer, value.Deadline);
            Write(writer, value.Source);
            Write(writer, value.Container);
            Write(writer, value.Slot);
            Write(writer, value.ContainerSlot);
            Write(writer, value.Mainline);
        }

        static Quest ReadQuest(BinaryReader reader)
        {
            return new Quest
            {
                Status = Read<QuestStatus>(reader),
                StartTurn = Read<int>(reader),
                Deadline = Read<int>(reader),
                Source = Read<ulong>(reader),
                Container = Read<ulong>(reader),
                Slot = Read<int>(reader),
                ContainerSlot = Read<int>(reader),
                Mainline = Read<byte>(reader),
            };
        }

        static void WriteQuestGenerationSettings(BinaryWriter writer, QuestGenerationSettings value)
        {
            Write(writer, value.StrengthStep);
            Write(writer, value.MarketValuePerStrength);
            Write(writer, value.Low);
            Write(writer, value.Medium);
            Write(writer, value.High);
            Write(writer, value.Maximum);
        }

        static QuestGenerationSettings ReadQuestGenerationSettings(BinaryReader reader)
        {
            return new QuestGenerationSettings
            {
                StrengthStep = Read<int>(reader),
                MarketValuePerStrength = Read<int>(reader),
                Low = Read<int4>(reader),
                Medium = Read<int4>(reader),
                High = Read<int4>(reader),
                Maximum = Read<int4>(reader),
            };
        }

        static void WriteQuestOfferSlot(BinaryWriter writer, QuestOfferSlot value)
        {
            Write(writer, value.Type);
            Write(writer, value.Index);
            Write(writer, value.NextTurn);
        }

        static QuestOfferSlot ReadQuestOfferSlot(BinaryReader reader)
        {
            return new QuestOfferSlot
            {
                Type = Read<int>(reader),
                Index = Read<int>(reader),
                NextTurn = Read<int>(reader),
            };
        }

        static void WriteQuestProgress(BinaryWriter writer, QuestProgress value)
        {
            Write(writer, value.Amount);
            Write(writer, value.Key);
        }

        static QuestProgress ReadQuestProgress(BinaryReader reader)
        {
            return new QuestProgress
            {
                Amount = Read<int>(reader),
                Key = Read<FixedString64Bytes>(reader),
            };
        }

        static void WriteQuestTracking(BinaryWriter writer, QuestTracking value)
        {
            Write(writer, value.Target);
            Write(writer, value.Mode);
        }

        static QuestTracking ReadQuestTracking(BinaryReader reader)
        {
            return new QuestTracking
            {
                Target = Read<ulong>(reader),
                Mode = Read<byte>(reader),
            };
        }

        static void WriteRepairMaterial(BinaryWriter writer, RepairMaterial value)
        {
            Write(writer, value.Item);
            Write(writer, value.Amount);
        }

        static RepairMaterial ReadRepairMaterial(BinaryReader reader)
        {
            return new RepairMaterial
            {
                Item = Read<Landsong.ECS.Definitions.ItemId>(reader),
                Amount = Read<int>(reader),
            };
        }

        static void WriteResearchState(BinaryWriter writer, ResearchState value)
        {
            Write(writer, value.Points);
        }

        static ResearchState ReadResearchState(BinaryReader reader)
        {
            return new ResearchState
            {
                Points = Read<int>(reader),
            };
        }

        static void WriteRetryState(BinaryWriter writer, RetryState value)
        {
            Write(writer, value.Count);
        }

        static RetryState ReadRetryState(BinaryReader reader)
        {
            return new RetryState
            {
                Count = Read<int>(reader),
            };
        }

        static void WriteRoyal(BinaryWriter writer, Royal value)
        {
            Write(writer, value.Role);
            Write(writer, value.Alive);
            Write(writer, value.Retired);
            Write(writer, value.FateUsed);
            Write(writer, value.Evidence);
            Write(writer, value.TaskClaimed);
            Write(writer, value.EverMonarch);
            Write(writer, value.Gender);
            Write(writer, value.MarriageRequestTurn);
            Write(writer, value.MarriageCooldownUntil);
            Write(writer, value.RequestedSpouse);
            Write(writer, value.MarriageRequestMonarch);
            Write(writer, value.Age);
            Write(writer, value.Generation);
            Write(writer, value.ReignSince);
            Write(writer, value.FateUntil);
            Write(writer, value.LastGiftTurn);
            Write(writer, value.Affection);
            Write(writer, value.VisitUntil);
            Write(writer, value.Parent);
            Write(writer, value.SecondParent);
            Write(writer, value.Spouse);
            Write(writer, value.Influence);
            Write(writer, value.Growth);
            Write(writer, value.Ambition);
            Write(writer, value.Grievance);
            Write(writer, value.InfluenceSource);
        }

        static Royal ReadRoyal(BinaryReader reader)
        {
            return new Royal
            {
                Role = Read<byte>(reader),
                Alive = Read<byte>(reader),
                Retired = Read<byte>(reader),
                FateUsed = Read<byte>(reader),
                Evidence = Read<byte>(reader),
                TaskClaimed = Read<byte>(reader),
                EverMonarch = Read<byte>(reader),
                Gender = Read<PersonGender>(reader),
                MarriageRequestTurn = Read<int>(reader),
                MarriageCooldownUntil = Read<int>(reader),
                RequestedSpouse = Read<ulong>(reader),
                MarriageRequestMonarch = Read<ulong>(reader),
                Age = Read<int>(reader),
                Generation = Read<int>(reader),
                ReignSince = Read<int>(reader),
                FateUntil = Read<int>(reader),
                LastGiftTurn = Read<int>(reader),
                Affection = Read<int>(reader),
                VisitUntil = Read<int>(reader),
                Parent = Read<ulong>(reader),
                SecondParent = Read<ulong>(reader),
                Spouse = Read<ulong>(reader),
                Influence = Read<float>(reader),
                Growth = Read<float>(reader),
                Ambition = Read<float>(reader),
                Grievance = Read<float>(reader),
                InfluenceSource = Read<FixedString128Bytes>(reader),
            };
        }
    }
}
