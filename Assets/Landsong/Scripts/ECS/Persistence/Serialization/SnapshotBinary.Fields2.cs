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
        static void WriteCourtSettings(BinaryWriter writer, CourtSettings value)
        {
            Write(writer, value.MarriageAge);
            Write(writer, value.CaptainAge);
            Write(writer, value.GiftCost);
            Write(writer, value.GiftAffection);
            Write(writer, value.RecruitAffection);
            Write(writer, value.MarriageAffection);
            Write(writer, value.StableDesignationTurns);
            Write(writer, value.MinimumReign);
            Write(writer, value.TemporaryTurns);
            Write(writer, value.ElectionTurns);
            Write(writer, value.DisorderTurns);
            Write(writer, value.VisitInterval);
            Write(writer, value.VisitDuration);
            Write(writer, value.VisitCost);
            Write(writer, value.MarriageRequestCooldown);
            Write(writer, value.ExpeditionRequestCooldown);
            Write(writer, value.ExpeditionRequestChance);
            Write(writer, value.MarriageRequestChance);
            Write(writer, value.MarriageRefusalGrievanceChance);
            Write(writer, value.MarriageRefusalGrievance);
            Write(writer, value.InitialOpinion);
            Write(writer, value.OpinionRecovery);
            Write(writer, value.DisorderOpinionCost);
            Write(writer, value.PrinceGrowth);
            Write(writer, value.StrongInfluence);
            Write(writer, value.UsurpGap);
            Write(writer, value.UsurpChance);
            Write(writer, value.RegicideGap);
            Write(writer, value.RegicideChance);
            Write(writer, value.PrinceRisk);
            Write(writer, value.StableProduction);
            Write(writer, value.StableAttack);
            Write(writer, value.WeakProduction);
            Write(writer, value.WeakAttack);
            Write(writer, value.ElectionProduction);
            Write(writer, value.ElectionAttack);
            Write(writer, value.UsurpProduction);
            Write(writer, value.UsurpAttack);
            Write(writer, value.RegicideProduction);
            Write(writer, value.RegicideAttack);
            Write(writer, value.DisorderPerStack);
            Write(writer, value.DisorderCap);
            Write(writer, value.DeathYoung);
            Write(writer, value.DeathAdult);
            Write(writer, value.DeathMature);
            Write(writer, value.DeathOld);
            Write(writer, value.DeathAncient);
        }

        static CourtSettings ReadCourtSettings(BinaryReader reader)
        {
            return new CourtSettings
            {
                MarriageAge = Read<int>(reader),
                CaptainAge = Read<int>(reader),
                GiftCost = Read<int>(reader),
                GiftAffection = Read<int>(reader),
                RecruitAffection = Read<int>(reader),
                MarriageAffection = Read<int>(reader),
                StableDesignationTurns = Read<int>(reader),
                MinimumReign = Read<int>(reader),
                TemporaryTurns = Read<int>(reader),
                ElectionTurns = Read<int>(reader),
                DisorderTurns = Read<int>(reader),
                VisitInterval = Read<int>(reader),
                VisitDuration = Read<int>(reader),
                VisitCost = Read<int>(reader),
                MarriageRequestCooldown = Read<int>(reader),
                ExpeditionRequestCooldown = Read<int>(reader),
                ExpeditionRequestChance = Read<float>(reader),
                MarriageRequestChance = Read<float>(reader),
                MarriageRefusalGrievanceChance = Read<float>(reader),
                MarriageRefusalGrievance = Read<float>(reader),
                InitialOpinion = Read<int>(reader),
                OpinionRecovery = Read<int>(reader),
                DisorderOpinionCost = Read<int>(reader),
                PrinceGrowth = Read<float>(reader),
                StrongInfluence = Read<float>(reader),
                UsurpGap = Read<float>(reader),
                UsurpChance = Read<float>(reader),
                RegicideGap = Read<float>(reader),
                RegicideChance = Read<float>(reader),
                PrinceRisk = Read<float>(reader),
                StableProduction = Read<float>(reader),
                StableAttack = Read<float>(reader),
                WeakProduction = Read<float>(reader),
                WeakAttack = Read<float>(reader),
                ElectionProduction = Read<float>(reader),
                ElectionAttack = Read<float>(reader),
                UsurpProduction = Read<float>(reader),
                UsurpAttack = Read<float>(reader),
                RegicideProduction = Read<float>(reader),
                RegicideAttack = Read<float>(reader),
                DisorderPerStack = Read<float>(reader),
                DisorderCap = Read<float>(reader),
                DeathYoung = Read<float>(reader),
                DeathAdult = Read<float>(reader),
                DeathMature = Read<float>(reader),
                DeathOld = Read<float>(reader),
                DeathAncient = Read<float>(reader),
            };
        }

        static void WriteCourtState(BinaryWriter writer, CourtState value)
        {
            Write(writer, value.Crown);
            Write(writer, value.LegacyFounder);
            Write(writer, value.CrownSince);
            Write(writer, value.LastSettledTurn);
            Write(writer, value.TemporaryUntil);
            Write(writer, value.DisorderUntil);
            Write(writer, value.LegacyGeneration);
            Write(writer, value.VisitOfferTurn);
            Write(writer, value.TemporaryProduction);
            Write(writer, value.TemporaryAttack);
            Write(writer, value.LegacyProduction);
            Write(writer, value.LegacyAttack);
            Write(writer, value.Disorder);
            Write(writer, value.Extinction);
            Write(writer, value.LegacySeverity);
            Write(writer, value.VisitResolved);
        }

        static CourtState ReadCourtState(BinaryReader reader)
        {
            return new CourtState
            {
                Crown = Read<ulong>(reader),
                LegacyFounder = Read<ulong>(reader),
                CrownSince = Read<int>(reader),
                LastSettledTurn = Read<int>(reader),
                TemporaryUntil = Read<int>(reader),
                DisorderUntil = Read<int>(reader),
                LegacyGeneration = Read<int>(reader),
                VisitOfferTurn = Read<int>(reader),
                TemporaryProduction = Read<float>(reader),
                TemporaryAttack = Read<float>(reader),
                LegacyProduction = Read<float>(reader),
                LegacyAttack = Read<float>(reader),
                Disorder = Read<float>(reader),
                Extinction = Read<byte>(reader),
                LegacySeverity = Read<byte>(reader),
                VisitResolved = Read<byte>(reader),
            };
        }

        static void WriteCurrencySettings(BinaryWriter writer, CurrencySettings value)
        {
            Write(writer, value.Gold);
        }

        static CurrencySettings ReadCurrencySettings(BinaryReader reader)
        {
            return new CurrencySettings
            {
                Gold = Read<Landsong.ECS.Definitions.ItemId>(reader),
            };
        }

        static void WriteDaySettlementState(BinaryWriter writer, DaySettlementState value)
        {
            Write(writer, value.LastSettledTurn);
        }

        static DaySettlementState ReadDaySettlementState(BinaryReader reader)
        {
            return new DaySettlementState
            {
                LastSettledTurn = Read<int>(reader),
            };
        }

        static Landsong.ECS.Definitions.BuffId ReadLandsong_ECS_Definitions_BuffId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.BuffId");
            return index < 0 ? default : Landsong.ECS.Definitions.BuffId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.BuildingId ReadLandsong_ECS_Definitions_BuildingId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.BuildingId");
            return index < 0 ? default : Landsong.ECS.Definitions.BuildingId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.BuildingLimitGroupId ReadLandsong_ECS_Definitions_BuildingLimitGroupId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.BuildingLimitGroupId");
            return index < 0 ? default : Landsong.ECS.Definitions.BuildingLimitGroupId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.CropId ReadLandsong_ECS_Definitions_CropId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.CropId");
            return index < 0 ? default : Landsong.ECS.Definitions.CropId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.EnemyId ReadLandsong_ECS_Definitions_EnemyId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.EnemyId");
            return index < 0 ? default : Landsong.ECS.Definitions.EnemyId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.ExpeditionId ReadLandsong_ECS_Definitions_ExpeditionId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.ExpeditionId");
            return index < 0 ? default : Landsong.ECS.Definitions.ExpeditionId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.FeatureId ReadLandsong_ECS_Definitions_FeatureId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.FeatureId");
            return index < 0 ? default : Landsong.ECS.Definitions.FeatureId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.HeroId ReadLandsong_ECS_Definitions_HeroId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.HeroId");
            return index < 0 ? default : Landsong.ECS.Definitions.HeroId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.ItemGroupId ReadLandsong_ECS_Definitions_ItemGroupId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.ItemGroupId");
            return index < 0 ? default : Landsong.ECS.Definitions.ItemGroupId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.ItemId ReadLandsong_ECS_Definitions_ItemId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.ItemId");
            return index < 0 ? default : Landsong.ECS.Definitions.ItemId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.PolicyGroupId ReadLandsong_ECS_Definitions_PolicyGroupId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.PolicyGroupId");
            return index < 0 ? default : Landsong.ECS.Definitions.PolicyGroupId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.PolicyId ReadLandsong_ECS_Definitions_PolicyId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.PolicyId");
            return index < 0 ? default : Landsong.ECS.Definitions.PolicyId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.QuestId ReadLandsong_ECS_Definitions_QuestId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.QuestId");
            return index < 0 ? default : Landsong.ECS.Definitions.QuestId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.RoyalTraitId ReadLandsong_ECS_Definitions_RoyalTraitId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.RoyalTraitId");
            return index < 0 ? default : Landsong.ECS.Definitions.RoyalTraitId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.SoldierId ReadLandsong_ECS_Definitions_SoldierId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.SoldierId");
            return index < 0 ? default : Landsong.ECS.Definitions.SoldierId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.StorageSlotId ReadLandsong_ECS_Definitions_StorageSlotId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.StorageSlotId");
            return index < 0 ? default : Landsong.ECS.Definitions.StorageSlotId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.TalentId ReadLandsong_ECS_Definitions_TalentId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.TalentId");
            return index < 0 ? default : Landsong.ECS.Definitions.TalentId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.TalentSlotId ReadLandsong_ECS_Definitions_TalentSlotId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.TalentSlotId");
            return index < 0 ? default : Landsong.ECS.Definitions.TalentSlotId.FromIndex(index);
        }

        static Landsong.ECS.Definitions.TechnologyId ReadLandsong_ECS_Definitions_TechnologyId(BinaryReader reader)
        {
            int index = reader.ReadInt32();
            if (index < -1)
                throw new InvalidDataException("Invalid Landsong.ECS.Definitions.TechnologyId");
            return index < 0 ? default : Landsong.ECS.Definitions.TechnologyId.FromIndex(index);
        }

        static void WriteDynastyIdentity(BinaryWriter writer, DynastyIdentity value)
        {
            Write(writer, value.Name);
        }

        static DynastyIdentity ReadDynastyIdentity(BinaryReader reader)
        {
            return new DynastyIdentity
            {
                Name = Read<FixedString128Bytes>(reader),
            };
        }
    }
}
