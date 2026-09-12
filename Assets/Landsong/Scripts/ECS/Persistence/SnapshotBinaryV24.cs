using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    // The field order below IS the v24 disk schema. Do not regenerate it from runtime layouts.
    // Runtime fields can be reordered without changing these bytes. A format change needs a new codec.
    public static class SnapshotBinaryV24
    {
        public static void Write<T>(BinaryWriter writer, T value) where T : unmanaged
        {
            if (typeof(T) == typeof(BattleHistoryEntry)) { WriteBattleHistoryEntry(writer, (BattleHistoryEntry)(object)value); return; }
            if (typeof(T) == typeof(BattleReportEntry)) { WriteBattleReportEntry(writer, (BattleReportEntry)(object)value); return; }
            if (typeof(T) == typeof(Building)) { WriteBuilding(writer, (Building)(object)value); return; }
            if (typeof(T) == typeof(BuildingCategory)) { writer.Write((int)((BuildingCategory)(object)value)); return; }
            if (typeof(T) == typeof(BuildingInvestment)) { WriteBuildingInvestment(writer, (BuildingInvestment)(object)value); return; }
            if (typeof(T) == typeof(BuildingPolicy)) { WriteBuildingPolicy(writer, (BuildingPolicy)(object)value); return; }
            if (typeof(T) == typeof(Color32)) { WriteColor32(writer, (Color32)(object)value); return; }
            if (typeof(T) == typeof(CombatProfile)) { WriteCombatProfile(writer, (CombatProfile)(object)value); return; }
            if (typeof(T) == typeof(ContentDefinition)) { WriteContentDefinition(writer, (ContentDefinition)(object)value); return; }
            if (typeof(T) == typeof(ContentKind)) { writer.Write((byte)((ContentKind)(object)value)); return; }
            if (typeof(T) == typeof(CourtLogEntry)) { WriteCourtLogEntry(writer, (CourtLogEntry)(object)value); return; }
            if (typeof(T) == typeof(CourtSettings)) { WriteCourtSettings(writer, (CourtSettings)(object)value); return; }
            if (typeof(T) == typeof(CourtState)) { WriteCourtState(writer, (CourtState)(object)value); return; }
            if (typeof(T) == typeof(DynastySettings)) { WriteDynastySettings(writer, (DynastySettings)(object)value); return; }
            if (typeof(T) == typeof(EconomyEntry)) { WriteEconomyEntry(writer, (EconomyEntry)(object)value); return; }
            if (typeof(T) == typeof(EconomyReason)) { writer.Write((byte)((EconomyReason)(object)value)); return; }
            if (typeof(T) == typeof(Entitlement)) { WriteEntitlement(writer, (Entitlement)(object)value); return; }
            if (typeof(T) == typeof(Entity)) { WriteEntity(writer, (Entity)(object)value); return; }
            if (typeof(T) == typeof(EventKind)) { writer.Write((byte)((EventKind)(object)value)); return; }
            if (typeof(T) == typeof(Expedition)) { WriteExpedition(writer, (Expedition)(object)value); return; }
            if (typeof(T) == typeof(ExpeditionDestinationHistory)) { WriteExpeditionDestinationHistory(writer, (ExpeditionDestinationHistory)(object)value); return; }
            if (typeof(T) == typeof(ExpeditionSettings)) { WriteExpeditionSettings(writer, (ExpeditionSettings)(object)value); return; }
            if (typeof(T) == typeof(ExpeditionStatus)) { writer.Write((byte)((ExpeditionStatus)(object)value)); return; }
            if (typeof(T) == typeof(ExpeditionSupply)) { WriteExpeditionSupply(writer, (ExpeditionSupply)(object)value); return; }
            if (typeof(T) == typeof(FixedList128Bytes<int>)) { WriteFixedList128Bytes_int_(writer, (FixedList128Bytes<int>)(object)value); return; }
            if (typeof(T) == typeof(FixedList512Bytes<float>)) { WriteFixedList512Bytes_float_(writer, (FixedList512Bytes<float>)(object)value); return; }
            if (typeof(T) == typeof(FixedList64Bytes<int>)) { WriteFixedList64Bytes_int_(writer, (FixedList64Bytes<int>)(object)value); return; }
            if (typeof(T) == typeof(FixedString128Bytes)) { writer.Write(((FixedString128Bytes)(object)value).ToString()); return; }
            if (typeof(T) == typeof(FixedString64Bytes)) { writer.Write(((FixedString64Bytes)(object)value).ToString()); return; }
            if (typeof(T) == typeof(FoodSelection)) { WriteFoodSelection(writer, (FoodSelection)(object)value); return; }
            if (typeof(T) == typeof(GameSettings)) { WriteGameSettings(writer, (GameSettings)(object)value); return; }
            if (typeof(T) == typeof(GridCell)) { WriteGridCell(writer, (GridCell)(object)value); return; }
            if (typeof(T) == typeof(Health)) { WriteHealth(writer, (Health)(object)value); return; }
            if (typeof(T) == typeof(Hero)) { WriteHero(writer, (Hero)(object)value); return; }
            if (typeof(T) == typeof(HeroGrowth)) { WriteHeroGrowth(writer, (HeroGrowth)(object)value); return; }
            if (typeof(T) == typeof(HistoryCategory)) { writer.Write((byte)((HistoryCategory)(object)value)); return; }
            if (typeof(T) == typeof(HistoryEntry)) { WriteHistoryEntry(writer, (HistoryEntry)(object)value); return; }
            if (typeof(T) == typeof(Identity)) { WriteIdentity(writer, (Identity)(object)value); return; }
            if (typeof(T) == typeof(InitialBuilding)) { WriteInitialBuilding(writer, (InitialBuilding)(object)value); return; }
            if (typeof(T) == typeof(InitialRoyal)) { WriteInitialRoyal(writer, (InitialRoyal)(object)value); return; }
            if (typeof(T) == typeof(InventorySlot)) { WriteInventorySlot(writer, (InventorySlot)(object)value); return; }
            if (typeof(T) == typeof(ItemProtection)) { writer.Write((byte)((ItemProtection)(object)value)); return; }
            if (typeof(T) == typeof(LifeStage)) { writer.Write((byte)((LifeStage)(object)value)); return; }
            if (typeof(T) == typeof(LocalTransform)) { WriteLocalTransform(writer, (LocalTransform)(object)value); return; }
            if (typeof(T) == typeof(NightEnemyChoice)) { WriteNightEnemyChoice(writer, (NightEnemyChoice)(object)value); return; }
            if (typeof(T) == typeof(NightEventDefinition)) { WriteNightEventDefinition(writer, (NightEventDefinition)(object)value); return; }
            if (typeof(T) == typeof(NightEventHistory)) { WriteNightEventHistory(writer, (NightEventHistory)(object)value); return; }
            if (typeof(T) == typeof(NightKind)) { writer.Write((byte)((NightKind)(object)value)); return; }
            if (typeof(T) == typeof(NightPlanState)) { WriteNightPlanState(writer, (NightPlanState)(object)value); return; }
            if (typeof(T) == typeof(NightPreparation)) { WriteNightPreparation(writer, (NightPreparation)(object)value); return; }
            if (typeof(T) == typeof(NightRules)) { WriteNightRules(writer, (NightRules)(object)value); return; }
            if (typeof(T) == typeof(NightWave)) { WriteNightWave(writer, (NightWave)(object)value); return; }
            if (typeof(T) == typeof(OpportunityProfile)) { WriteOpportunityProfile(writer, (OpportunityProfile)(object)value); return; }
            if (typeof(T) == typeof(PeacefulRules)) { WritePeacefulRules(writer, (PeacefulRules)(object)value); return; }
            if (typeof(T) == typeof(PendingItem)) { WritePendingItem(writer, (PendingItem)(object)value); return; }
            if (typeof(T) == typeof(PersonGender)) { writer.Write((byte)((PersonGender)(object)value)); return; }
            if (typeof(T) == typeof(PersonRequestEntry)) { WritePersonRequestEntry(writer, (PersonRequestEntry)(object)value); return; }
            if (typeof(T) == typeof(PersonRequestKind)) { writer.Write((byte)((PersonRequestKind)(object)value)); return; }
            if (typeof(T) == typeof(PersonRequestStatus)) { writer.Write((byte)((PersonRequestStatus)(object)value)); return; }
            if (typeof(T) == typeof(Phase)) { writer.Write((byte)((Phase)(object)value)); return; }
            if (typeof(T) == typeof(PolicyChoice)) { WritePolicyChoice(writer, (PolicyChoice)(object)value); return; }
            if (typeof(T) == typeof(PortraitDNA)) { WritePortraitDNA(writer, (PortraitDNA)(object)value); return; }
            if (typeof(T) == typeof(PortraitSettings)) { WritePortraitSettings(writer, (PortraitSettings)(object)value); return; }
            if (typeof(T) == typeof(ProjectileMode)) { writer.Write((byte)((ProjectileMode)(object)value)); return; }
            if (typeof(T) == typeof(Quest)) { WriteQuest(writer, (Quest)(object)value); return; }
            if (typeof(T) == typeof(QuestGenerationSettings)) { WriteQuestGenerationSettings(writer, (QuestGenerationSettings)(object)value); return; }
            if (typeof(T) == typeof(QuestOfferSlot)) { WriteQuestOfferSlot(writer, (QuestOfferSlot)(object)value); return; }
            if (typeof(T) == typeof(QuestProgress)) { WriteQuestProgress(writer, (QuestProgress)(object)value); return; }
            if (typeof(T) == typeof(QuestStatus)) { writer.Write((byte)((QuestStatus)(object)value)); return; }
            if (typeof(T) == typeof(QuestTracking)) { WriteQuestTracking(writer, (QuestTracking)(object)value); return; }
            if (typeof(T) == typeof(RepairMaterial)) { WriteRepairMaterial(writer, (RepairMaterial)(object)value); return; }
            if (typeof(T) == typeof(ResearchEntry)) { WriteResearchEntry(writer, (ResearchEntry)(object)value); return; }
            if (typeof(T) == typeof(Royal)) { WriteRoyal(writer, (Royal)(object)value); return; }
            if (typeof(T) == typeof(Rule)) { WriteRule(writer, (Rule)(object)value); return; }
            if (typeof(T) == typeof(RuleKind)) { writer.Write((byte)((RuleKind)(object)value)); return; }
            if (typeof(T) == typeof(Session)) { WriteSession(writer, (Session)(object)value); return; }
            if (typeof(T) == typeof(Soldier)) { WriteSoldier(writer, (Soldier)(object)value); return; }
            if (typeof(T) == typeof(SoldierGrowth)) { WriteSoldierGrowth(writer, (SoldierGrowth)(object)value); return; }
            if (typeof(T) == typeof(SoldierPerson)) { WriteSoldierPerson(writer, (SoldierPerson)(object)value); return; }
            if (typeof(T) == typeof(SpawnRegion)) { WriteSpawnRegion(writer, (SpawnRegion)(object)value); return; }
            if (typeof(T) == typeof(TacticalTraits)) { writer.Write((byte)((TacticalTraits)(object)value)); return; }
            if (typeof(T) == typeof(Talent)) { WriteTalent(writer, (Talent)(object)value); return; }
            if (typeof(T) == typeof(TheftProfile)) { WriteTheftProfile(writer, (TheftProfile)(object)value); return; }
            if (typeof(T) == typeof(TraitEntry)) { WriteTraitEntry(writer, (TraitEntry)(object)value); return; }
            if (typeof(T) == typeof(UnresolvedBoss)) { WriteUnresolvedBoss(writer, (UnresolvedBoss)(object)value); return; }
            if (typeof(T) == typeof(VisitorKind)) { writer.Write((byte)((VisitorKind)(object)value)); return; }
            if (typeof(T) == typeof(bool)) { writer.Write((bool)(object)value); return; }
            if (typeof(T) == typeof(byte)) { writer.Write((byte)(object)value); return; }
            if (typeof(T) == typeof(float)) { writer.Write((float)(object)value); return; }
            if (typeof(T) == typeof(float3)) { Writefloat3(writer, (float3)(object)value); return; }
            if (typeof(T) == typeof(float4)) { Writefloat4(writer, (float4)(object)value); return; }
            if (typeof(T) == typeof(int)) { writer.Write((int)(object)value); return; }
            if (typeof(T) == typeof(int2)) { Writeint2(writer, (int2)(object)value); return; }
            if (typeof(T) == typeof(int4)) { Writeint4(writer, (int4)(object)value); return; }
            if (typeof(T) == typeof(long)) { writer.Write((long)(object)value); return; }
            if (typeof(T) == typeof(quaternion)) { Writequaternion(writer, (quaternion)(object)value); return; }
            if (typeof(T) == typeof(uint)) { writer.Write((uint)(object)value); return; }
            if (typeof(T) == typeof(ulong)) { writer.Write((ulong)(object)value); return; }
            throw new InvalidDataException("Unregistered v24 field type: " + typeof(T));
        }
        public static T Read<T>(BinaryReader reader) where T : unmanaged
        {
            if (typeof(T) == typeof(BattleHistoryEntry)) return (T)(object)(ReadBattleHistoryEntry(reader));
            if (typeof(T) == typeof(BattleReportEntry)) return (T)(object)(ReadBattleReportEntry(reader));
            if (typeof(T) == typeof(Building)) return (T)(object)(ReadBuilding(reader));
            if (typeof(T) == typeof(BuildingCategory)) return (T)(object)((BuildingCategory)reader.ReadInt32());
            if (typeof(T) == typeof(BuildingInvestment)) return (T)(object)(ReadBuildingInvestment(reader));
            if (typeof(T) == typeof(BuildingPolicy)) return (T)(object)(ReadBuildingPolicy(reader));
            if (typeof(T) == typeof(Color32)) return (T)(object)(ReadColor32(reader));
            if (typeof(T) == typeof(CombatProfile)) return (T)(object)(ReadCombatProfile(reader));
            if (typeof(T) == typeof(ContentDefinition)) return (T)(object)(ReadContentDefinition(reader));
            if (typeof(T) == typeof(ContentKind)) return (T)(object)((ContentKind)reader.ReadByte());
            if (typeof(T) == typeof(CourtLogEntry)) return (T)(object)(ReadCourtLogEntry(reader));
            if (typeof(T) == typeof(CourtSettings)) return (T)(object)(ReadCourtSettings(reader));
            if (typeof(T) == typeof(CourtState)) return (T)(object)(ReadCourtState(reader));
            if (typeof(T) == typeof(DynastySettings)) return (T)(object)(ReadDynastySettings(reader));
            if (typeof(T) == typeof(EconomyEntry)) return (T)(object)(ReadEconomyEntry(reader));
            if (typeof(T) == typeof(EconomyReason)) return (T)(object)((EconomyReason)reader.ReadByte());
            if (typeof(T) == typeof(Entitlement)) return (T)(object)(ReadEntitlement(reader));
            if (typeof(T) == typeof(Entity)) return (T)(object)(ReadEntity(reader));
            if (typeof(T) == typeof(EventKind)) return (T)(object)((EventKind)reader.ReadByte());
            if (typeof(T) == typeof(Expedition)) return (T)(object)(ReadExpedition(reader));
            if (typeof(T) == typeof(ExpeditionDestinationHistory)) return (T)(object)(ReadExpeditionDestinationHistory(reader));
            if (typeof(T) == typeof(ExpeditionSettings)) return (T)(object)(ReadExpeditionSettings(reader));
            if (typeof(T) == typeof(ExpeditionStatus)) return (T)(object)((ExpeditionStatus)reader.ReadByte());
            if (typeof(T) == typeof(ExpeditionSupply)) return (T)(object)(ReadExpeditionSupply(reader));
            if (typeof(T) == typeof(FixedList128Bytes<int>)) return (T)(object)(ReadFixedList128Bytes_int_(reader));
            if (typeof(T) == typeof(FixedList512Bytes<float>)) return (T)(object)(ReadFixedList512Bytes_float_(reader));
            if (typeof(T) == typeof(FixedList64Bytes<int>)) return (T)(object)(ReadFixedList64Bytes_int_(reader));
            if (typeof(T) == typeof(FixedString128Bytes)) return (T)(object)(new FixedString128Bytes(reader.ReadString()));
            if (typeof(T) == typeof(FixedString64Bytes)) return (T)(object)(new FixedString64Bytes(reader.ReadString()));
            if (typeof(T) == typeof(FoodSelection)) return (T)(object)(ReadFoodSelection(reader));
            if (typeof(T) == typeof(GameSettings)) return (T)(object)(ReadGameSettings(reader));
            if (typeof(T) == typeof(GridCell)) return (T)(object)(ReadGridCell(reader));
            if (typeof(T) == typeof(Health)) return (T)(object)(ReadHealth(reader));
            if (typeof(T) == typeof(Hero)) return (T)(object)(ReadHero(reader));
            if (typeof(T) == typeof(HeroGrowth)) return (T)(object)(ReadHeroGrowth(reader));
            if (typeof(T) == typeof(HistoryCategory)) return (T)(object)((HistoryCategory)reader.ReadByte());
            if (typeof(T) == typeof(HistoryEntry)) return (T)(object)(ReadHistoryEntry(reader));
            if (typeof(T) == typeof(Identity)) return (T)(object)(ReadIdentity(reader));
            if (typeof(T) == typeof(InitialBuilding)) return (T)(object)(ReadInitialBuilding(reader));
            if (typeof(T) == typeof(InitialRoyal)) return (T)(object)(ReadInitialRoyal(reader));
            if (typeof(T) == typeof(InventorySlot)) return (T)(object)(ReadInventorySlot(reader));
            if (typeof(T) == typeof(ItemProtection)) return (T)(object)((ItemProtection)reader.ReadByte());
            if (typeof(T) == typeof(LifeStage)) return (T)(object)((LifeStage)reader.ReadByte());
            if (typeof(T) == typeof(LocalTransform)) return (T)(object)(ReadLocalTransform(reader));
            if (typeof(T) == typeof(NightEnemyChoice)) return (T)(object)(ReadNightEnemyChoice(reader));
            if (typeof(T) == typeof(NightEventDefinition)) return (T)(object)(ReadNightEventDefinition(reader));
            if (typeof(T) == typeof(NightEventHistory)) return (T)(object)(ReadNightEventHistory(reader));
            if (typeof(T) == typeof(NightKind)) return (T)(object)((NightKind)reader.ReadByte());
            if (typeof(T) == typeof(NightPlanState)) return (T)(object)(ReadNightPlanState(reader));
            if (typeof(T) == typeof(NightPreparation)) return (T)(object)(ReadNightPreparation(reader));
            if (typeof(T) == typeof(NightRules)) return (T)(object)(ReadNightRules(reader));
            if (typeof(T) == typeof(NightWave)) return (T)(object)(ReadNightWave(reader));
            if (typeof(T) == typeof(OpportunityProfile)) return (T)(object)(ReadOpportunityProfile(reader));
            if (typeof(T) == typeof(PeacefulRules)) return (T)(object)(ReadPeacefulRules(reader));
            if (typeof(T) == typeof(PendingItem)) return (T)(object)(ReadPendingItem(reader));
            if (typeof(T) == typeof(PersonGender)) return (T)(object)((PersonGender)reader.ReadByte());
            if (typeof(T) == typeof(PersonRequestEntry)) return (T)(object)(ReadPersonRequestEntry(reader));
            if (typeof(T) == typeof(PersonRequestKind)) return (T)(object)((PersonRequestKind)reader.ReadByte());
            if (typeof(T) == typeof(PersonRequestStatus)) return (T)(object)((PersonRequestStatus)reader.ReadByte());
            if (typeof(T) == typeof(Phase)) return (T)(object)((Phase)reader.ReadByte());
            if (typeof(T) == typeof(PolicyChoice)) return (T)(object)(ReadPolicyChoice(reader));
            if (typeof(T) == typeof(PortraitDNA)) return (T)(object)(ReadPortraitDNA(reader));
            if (typeof(T) == typeof(PortraitSettings)) return (T)(object)(ReadPortraitSettings(reader));
            if (typeof(T) == typeof(ProjectileMode)) return (T)(object)((ProjectileMode)reader.ReadByte());
            if (typeof(T) == typeof(Quest)) return (T)(object)(ReadQuest(reader));
            if (typeof(T) == typeof(QuestGenerationSettings)) return (T)(object)(ReadQuestGenerationSettings(reader));
            if (typeof(T) == typeof(QuestOfferSlot)) return (T)(object)(ReadQuestOfferSlot(reader));
            if (typeof(T) == typeof(QuestProgress)) return (T)(object)(ReadQuestProgress(reader));
            if (typeof(T) == typeof(QuestStatus)) return (T)(object)((QuestStatus)reader.ReadByte());
            if (typeof(T) == typeof(QuestTracking)) return (T)(object)(ReadQuestTracking(reader));
            if (typeof(T) == typeof(RepairMaterial)) return (T)(object)(ReadRepairMaterial(reader));
            if (typeof(T) == typeof(ResearchEntry)) return (T)(object)(ReadResearchEntry(reader));
            if (typeof(T) == typeof(Royal)) return (T)(object)(ReadRoyal(reader));
            if (typeof(T) == typeof(Rule)) return (T)(object)(ReadRule(reader));
            if (typeof(T) == typeof(RuleKind)) return (T)(object)((RuleKind)reader.ReadByte());
            if (typeof(T) == typeof(Session)) return (T)(object)(ReadSession(reader));
            if (typeof(T) == typeof(Soldier)) return (T)(object)(ReadSoldier(reader));
            if (typeof(T) == typeof(SoldierGrowth)) return (T)(object)(ReadSoldierGrowth(reader));
            if (typeof(T) == typeof(SoldierPerson)) return (T)(object)(ReadSoldierPerson(reader));
            if (typeof(T) == typeof(SpawnRegion)) return (T)(object)(ReadSpawnRegion(reader));
            if (typeof(T) == typeof(TacticalTraits)) return (T)(object)((TacticalTraits)reader.ReadByte());
            if (typeof(T) == typeof(Talent)) return (T)(object)(ReadTalent(reader));
            if (typeof(T) == typeof(TheftProfile)) return (T)(object)(ReadTheftProfile(reader));
            if (typeof(T) == typeof(TraitEntry)) return (T)(object)(ReadTraitEntry(reader));
            if (typeof(T) == typeof(UnresolvedBoss)) return (T)(object)(ReadUnresolvedBoss(reader));
            if (typeof(T) == typeof(VisitorKind)) return (T)(object)((VisitorKind)reader.ReadByte());
            if (typeof(T) == typeof(bool)) return (T)(object)(reader.ReadBoolean());
            if (typeof(T) == typeof(byte)) return (T)(object)(reader.ReadByte());
            if (typeof(T) == typeof(float)) return (T)(object)(reader.ReadSingle());
            if (typeof(T) == typeof(float3)) return (T)(object)(Readfloat3(reader));
            if (typeof(T) == typeof(float4)) return (T)(object)(Readfloat4(reader));
            if (typeof(T) == typeof(int)) return (T)(object)(reader.ReadInt32());
            if (typeof(T) == typeof(int2)) return (T)(object)(Readint2(reader));
            if (typeof(T) == typeof(int4)) return (T)(object)(Readint4(reader));
            if (typeof(T) == typeof(long)) return (T)(object)(reader.ReadInt64());
            if (typeof(T) == typeof(quaternion)) return (T)(object)(Readquaternion(reader));
            if (typeof(T) == typeof(uint)) return (T)(object)(reader.ReadUInt32());
            if (typeof(T) == typeof(ulong)) return (T)(object)(reader.ReadUInt64());
            throw new InvalidDataException("Unregistered v24 field type: " + typeof(T));
        }
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
            Write(writer, value.Definition);
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
                Definition = Read<int>(reader),
                Amount = Read<int>(reader),
                Value = Read<float>(reader),
                SourceName = Read<FixedString128Bytes>(reader),
            };
        }
        static void WriteBuilding(BinaryWriter writer, Building value)
        {
            Write(writer, value.Stage);
            Write(writer, value.Level);
            Write(writer, value.Progress);
            Write(writer, value.Workers);
            Write(writer, value.StableWorkers);
            Write(writer, value.Experience);
            Write(writer, value.Population);
            Write(writer, value.Growth);
            Write(writer, value.FoodFailures);
            Write(writer, value.TaxProgress);
            Write(writer, value.ProductionProgress);
            Write(writer, value.Crop);
            Write(writer, value.CropProgress);
            Write(writer, value.WorkerTarget);
            Write(writer, value.ProtectionUntil);
            Write(writer, value.PaidOfferingTurn);
            Write(writer, value.WokenTurn);
            Write(writer, value.HarvestRemaining);
            Write(writer, value.PaidSubsidy);
            Write(writer, value.PaidSubsidyTurn);
            Write(writer, value.SubsidyBudget);
            Write(writer, value.SoldierRecruitTurn);
            Write(writer, value.SoldiersRecruited);
            Write(writer, value.CropSeed);
            Write(writer, value.Skin);
            Write(writer, value.RepairDuration);
            Write(writer, value.RepairCompletedTurn);
            Write(writer, value.DeferredResidents);
            Write(writer, value.RuinPending);
            Write(writer, value.MarketValue);
            Write(writer, value.MarketLifetimeValue);
            Write(writer, value.Offering);
            Write(writer, value.Subsidy);
            Write(writer, value.Maintained);
            Write(writer, value.CropFullCycle);
            Write(writer, value.AutoHarvest);
            Write(writer, value.Cell);
            Write(writer, value.Size);
            Write(writer, value.Rotation);
            Write(writer, value.Elevation);
            Write(writer, value.Surface);
        }
        static Building ReadBuilding(BinaryReader reader)
        {
            return new Building
            {
                Stage = Read<LifeStage>(reader),
                Level = Read<int>(reader),
                Progress = Read<int>(reader),
                Workers = Read<int>(reader),
                StableWorkers = Read<int>(reader),
                Experience = Read<int>(reader),
                Population = Read<int>(reader),
                Growth = Read<int>(reader),
                FoodFailures = Read<int>(reader),
                TaxProgress = Read<int>(reader),
                ProductionProgress = Read<int>(reader),
                Crop = Read<int>(reader),
                CropProgress = Read<int>(reader),
                WorkerTarget = Read<int>(reader),
                ProtectionUntil = Read<int>(reader),
                PaidOfferingTurn = Read<int>(reader),
                WokenTurn = Read<int>(reader),
                HarvestRemaining = Read<int>(reader),
                PaidSubsidy = Read<int>(reader),
                PaidSubsidyTurn = Read<int>(reader),
                SubsidyBudget = Read<int>(reader),
                SoldierRecruitTurn = Read<int>(reader),
                SoldiersRecruited = Read<int>(reader),
                CropSeed = Read<uint>(reader),
                Skin = Read<FixedString64Bytes>(reader),
                RepairDuration = Read<int>(reader),
                RepairCompletedTurn = Read<int>(reader),
                DeferredResidents = Read<int>(reader),
                RuinPending = Read<byte>(reader),
                MarketValue = Read<long>(reader),
                MarketLifetimeValue = Read<long>(reader),
                Offering = Read<byte>(reader),
                Subsidy = Read<byte>(reader),
                Maintained = Read<byte>(reader),
                CropFullCycle = Read<byte>(reader),
                AutoHarvest = Read<byte>(reader),
                Cell = Read<int2>(reader),
                Size = Read<int2>(reader),
                Rotation = Read<int>(reader),
                Elevation = Read<int>(reader),
                Surface = Read<int>(reader),
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
                Item = Read<int>(reader),
                Amount = Read<int>(reader),
            };
        }
        static void WriteBuildingPolicy(BinaryWriter writer, BuildingPolicy value)
        {
            Write(writer, value.Category);
            Write(writer, value.MenuOrder);
            Write(writer, value.ProviderPriority);
            Write(writer, value.RepairTurns);
            Write(writer, value.SoldierRecruitLimit);
            Write(writer, value.MoveMaterialRatio);
            Write(writer, value.MoveExperienceRatio);
            Write(writer, value.RuinMovementCost);
            Write(writer, value.CanMove);
            Write(writer, value.CanRotate);
        }
        static BuildingPolicy ReadBuildingPolicy(BinaryReader reader)
        {
            return new BuildingPolicy
            {
                Category = Read<BuildingCategory>(reader),
                MenuOrder = Read<int>(reader),
                ProviderPriority = Read<int>(reader),
                RepairTurns = Read<int>(reader),
                SoldierRecruitLimit = Read<int>(reader),
                MoveMaterialRatio = Read<float>(reader),
                MoveExperienceRatio = Read<float>(reader),
                RuinMovementCost = Read<float>(reader),
                CanMove = Read<byte>(reader),
                CanRotate = Read<byte>(reader),
            };
        }
        static void WriteColor32(BinaryWriter writer, Color32 value)
        {
            Write(writer, value.r);
            Write(writer, value.g);
            Write(writer, value.b);
            Write(writer, value.a);
        }
        static Color32 ReadColor32(BinaryReader reader)
        {
            return new Color32
            {
                r = Read<byte>(reader),
                g = Read<byte>(reader),
                b = Read<byte>(reader),
                a = Read<byte>(reader),
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
        static void WriteContentDefinition(BinaryWriter writer, ContentDefinition value)
        {
            Write(writer, value.Id);
            Write(writer, value.Name);
            Write(writer, value.Kind);
            Write(writer, value.RuleStart);
            Write(writer, value.RuleCount);
            Write(writer, value.Level);
            Write(writer, value.Group);
            Write(writer, value.Capacity);
            Write(writer, value.Duration);
            Write(writer, value.Value);
            Write(writer, value.Limit);
            Write(writer, value.Size);
            Write(writer, value.Health);
            Write(writer, value.Damage);
            Write(writer, value.Range);
            Write(writer, value.Interval);
            Write(writer, value.Speed);
            Write(writer, value.ProjectileSpeed);
            Write(writer, value.Chance);
            Write(writer, value.Loss);
            Write(writer, value.Population);
            Write(writer, value.Cost);
            Write(writer, value.Flags);
            Write(writer, value.TargetCategory);
            Write(writer, value.QuestIntensity);
            Write(writer, value.QuestWeight);
            Write(writer, value.ItemQuantityScale);
            Write(writer, value.BuildingPolicy);
            Write(writer, value.SoldierGrowth);
            Write(writer, value.HeroGrowth);
            Write(writer, value.Combat);
            Write(writer, value.Opportunity);
            Write(writer, value.Theft);
            Write(writer, value.DefaultSkin);
        }
        static ContentDefinition ReadContentDefinition(BinaryReader reader)
        {
            return new ContentDefinition
            {
                Id = Read<FixedString128Bytes>(reader),
                Name = Read<FixedString128Bytes>(reader),
                Kind = Read<ContentKind>(reader),
                RuleStart = Read<int>(reader),
                RuleCount = Read<int>(reader),
                Level = Read<int>(reader),
                Group = Read<int>(reader),
                Capacity = Read<int>(reader),
                Duration = Read<int>(reader),
                Value = Read<int>(reader),
                Limit = Read<int>(reader),
                Size = Read<int2>(reader),
                Health = Read<float>(reader),
                Damage = Read<float>(reader),
                Range = Read<float>(reader),
                Interval = Read<float>(reader),
                Speed = Read<float>(reader),
                ProjectileSpeed = Read<float>(reader),
                Chance = Read<float>(reader),
                Loss = Read<float>(reader),
                Population = Read<int>(reader),
                Cost = Read<int>(reader),
                Flags = Read<int>(reader),
                TargetCategory = Read<BuildingCategory>(reader),
                QuestIntensity = Read<int>(reader),
                QuestWeight = Read<float>(reader),
                ItemQuantityScale = Read<float>(reader),
                BuildingPolicy = Read<BuildingPolicy>(reader),
                SoldierGrowth = Read<SoldierGrowth>(reader),
                HeroGrowth = Read<HeroGrowth>(reader),
                Combat = Read<CombatProfile>(reader),
                Opportunity = Read<OpportunityProfile>(reader),
                Theft = Read<TheftProfile>(reader),
                DefaultSkin = Read<FixedString64Bytes>(reader),
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
        static void WriteDynastySettings(BinaryWriter writer, DynastySettings value)
        {
            Write(writer, value.MaxChildren);
            Write(writer, value.TalentCapacity);
            Write(writer, value.TalentRecruitCost);
            Write(writer, value.TalentExperience);
            Write(writer, value.BirthChance);
            Write(writer, value.MutationChance);
        }
        static DynastySettings ReadDynastySettings(BinaryReader reader)
        {
            return new DynastySettings
            {
                MaxChildren = Read<int>(reader),
                TalentCapacity = Read<int>(reader),
                TalentRecruitCost = Read<int>(reader),
                TalentExperience = Read<int>(reader),
                BirthChance = Read<float>(reader),
                MutationChance = Read<float>(reader),
            };
        }
        static void WriteEconomyEntry(BinaryWriter writer, EconomyEntry value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Item);
            Write(writer, value.Delta);
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
                Item = Read<int>(reader),
                Delta = Read<int>(reader),
                Source = Read<ulong>(reader),
                Reason = Read<EconomyReason>(reader),
                Pending = Read<byte>(reader),
                SourceName = Read<FixedString128Bytes>(reader),
                Note = Read<FixedString128Bytes>(reader),
            };
        }
        static void WriteEntitlement(BinaryWriter writer, Entitlement value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Level);
        }
        static Entitlement ReadEntitlement(BinaryReader reader)
        {
            return new Entitlement
            {
                Definition = Read<int>(reader),
                Level = Read<int>(reader),
            };
        }
        static void WriteEntity(BinaryWriter writer, Entity value)
        {
            Write(writer, value.Index);
            Write(writer, value.Version);
        }
        static Entity ReadEntity(BinaryReader reader)
        {
            return new Entity
            {
                Index = Read<int>(reader),
                Version = Read<int>(reader),
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
                Definition = Read<int>(reader),
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
                Item = Read<int>(reader),
                Amount = Read<int>(reader),
            };
        }
        static void WriteFixedList128Bytes_int_(BinaryWriter writer, FixedList128Bytes<int> value)
        {
            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++) writer.Write(value[i]);
        }
        static FixedList128Bytes<int> ReadFixedList128Bytes_int_(BinaryReader reader)
        {
            var value = new FixedList128Bytes<int>(); int count = reader.ReadInt32();
            if (count < 0 || count > value.Capacity) throw new InvalidDataException("Invalid fixed list length");
            for (int i = 0; i < count; i++) value.Add(reader.ReadInt32());
            return value;
        }
        static void WriteFixedList512Bytes_float_(BinaryWriter writer, FixedList512Bytes<float> value)
        {
            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++) writer.Write(value[i]);
        }
        static FixedList512Bytes<float> ReadFixedList512Bytes_float_(BinaryReader reader)
        {
            var value = new FixedList512Bytes<float>(); int count = reader.ReadInt32();
            if (count < 0 || count > value.Capacity) throw new InvalidDataException("Invalid fixed list length");
            for (int i = 0; i < count; i++) value.Add(reader.ReadSingle());
            return value;
        }
        static void WriteFixedList64Bytes_int_(BinaryWriter writer, FixedList64Bytes<int> value)
        {
            writer.Write(value.Length);
            for (int i = 0; i < value.Length; i++) writer.Write(value[i]);
        }
        static FixedList64Bytes<int> ReadFixedList64Bytes_int_(BinaryReader reader)
        {
            var value = new FixedList64Bytes<int>(); int count = reader.ReadInt32();
            if (count < 0 || count > value.Capacity) throw new InvalidDataException("Invalid fixed list length");
            for (int i = 0; i < count; i++) value.Add(reader.ReadInt32());
            return value;
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
                Group = Read<int>(reader),
                Item = Read<int>(reader),
                Amount = Read<int>(reader),
            };
        }
        static void WriteGameSettings(BinaryWriter writer, GameSettings value)
        {
            Write(writer, value.PeacefulSeconds);
            Write(writer, value.BattleSeconds);
            Write(writer, value.DeployInterval);
            Write(writer, value.RetreatSeconds);
            Write(writer, value.InvasionChance);
            Write(writer, value.StrengthRatio);
            Write(writer, value.RetryStep);
            Write(writer, value.RetryCap);
            Write(writer, value.FirstInvasion);
            Write(writer, value.FirstBoss);
            Write(writer, value.BossInterval);
            Write(writer, value.ThreatPerTurn);
            Write(writer, value.Gold);
            Write(writer, value.LowIntel);
            Write(writer, value.MediumIntel);
            Write(writer, value.HighIntel);
            Write(writer, value.MediumIntelLead);
            Write(writer, value.HighIntelLead);
        }
        static GameSettings ReadGameSettings(BinaryReader reader)
        {
            return new GameSettings
            {
                PeacefulSeconds = Read<float>(reader),
                BattleSeconds = Read<float>(reader),
                DeployInterval = Read<float>(reader),
                RetreatSeconds = Read<float>(reader),
                InvasionChance = Read<float>(reader),
                StrengthRatio = Read<float>(reader),
                RetryStep = Read<float>(reader),
                RetryCap = Read<float>(reader),
                FirstInvasion = Read<int>(reader),
                FirstBoss = Read<int>(reader),
                BossInterval = Read<int>(reader),
                ThreatPerTurn = Read<int>(reader),
                Gold = Read<int>(reader),
                LowIntel = Read<int>(reader),
                MediumIntel = Read<int>(reader),
                HighIntel = Read<int>(reader),
                MediumIntelLead = Read<float>(reader),
                HighIntelLead = Read<float>(reader),
            };
        }
        static void WriteGridCell(BinaryWriter writer, GridCell value)
        {
            Write(writer, value.Exists);
            Write(writer, value.Buildable);
            Write(writer, value.Traversable);
            Write(writer, value.BlocksProjectile);
            Write(writer, value.Elevation);
            Write(writer, value.Surface);
            Write(writer, value.Height);
            Write(writer, value.Terrain);
        }
        static GridCell ReadGridCell(BinaryReader reader)
        {
            return new GridCell
            {
                Exists = Read<byte>(reader),
                Buildable = Read<byte>(reader),
                Traversable = Read<byte>(reader),
                BlocksProjectile = Read<byte>(reader),
                Elevation = Read<int>(reader),
                Surface = Read<int>(reader),
                Height = Read<float>(reader),
                Terrain = Read<ulong>(reader),
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
        static void WriteHeroGrowth(BinaryWriter writer, HeroGrowth value)
        {
            Write(writer, value.MaxLevel);
            Write(writer, value.FirstLevelExperience);
            Write(writer, value.ExperienceStep);
            Write(writer, value.HealthPerLevel);
            Write(writer, value.DamagePerLevel);
            Write(writer, value.OfferingExperience);
            Write(writer, value.ContactSeconds);
            Write(writer, value.ExperiencePerSecond);
            Write(writer, value.ThreatReference);
            Write(writer, value.MaximumThreatMultiplier);
        }
        static HeroGrowth ReadHeroGrowth(BinaryReader reader)
        {
            return new HeroGrowth
            {
                MaxLevel = Read<int>(reader),
                FirstLevelExperience = Read<int>(reader),
                ExperienceStep = Read<int>(reader),
                HealthPerLevel = Read<float>(reader),
                DamagePerLevel = Read<float>(reader),
                OfferingExperience = Read<int>(reader),
                ContactSeconds = Read<float>(reader),
                ExperiencePerSecond = Read<float>(reader),
                ThreatReference = Read<float>(reader),
                MaximumThreatMultiplier = Read<float>(reader),
            };
        }
        static void WriteHistoryEntry(BinaryWriter writer, HistoryEntry value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Item);
            Write(writer, value.Delta);
            Write(writer, value.Count);
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
                Item = Read<int>(reader),
                Delta = Read<int>(reader),
                Count = Read<int>(reader),
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
            Write(writer, value.Definition);
            Write(writer, value.Name);
        }
        static Identity ReadIdentity(BinaryReader reader)
        {
            return new Identity
            {
                Id = Read<ulong>(reader),
                Definition = Read<int>(reader),
                Name = Read<FixedString128Bytes>(reader),
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
                Definition = Read<int>(reader),
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
                Traits = Read<FixedList128Bytes<int>>(reader),
            };
        }
        static void WriteInventorySlot(BinaryWriter writer, InventorySlot value)
        {
            Write(writer, value.Provider);
            Write(writer, value.Index);
            Write(writer, value.SlotType);
            Write(writer, value.Item);
            Write(writer, value.Count);
            Write(writer, value.LossRemainder);
            Write(writer, value.Unavailable);
        }
        static InventorySlot ReadInventorySlot(BinaryReader reader)
        {
            return new InventorySlot
            {
                Provider = Read<ulong>(reader),
                Index = Read<int>(reader),
                SlotType = Read<int>(reader),
                Item = Read<int>(reader),
                Count = Read<int>(reader),
                LossRemainder = Read<float>(reader),
                Unavailable = Read<byte>(reader),
            };
        }
        static void WriteLocalTransform(BinaryWriter writer, LocalTransform value)
        {
            Write(writer, value.Position);
            Write(writer, value.Scale);
            Write(writer, value.Rotation);
        }
        static LocalTransform ReadLocalTransform(BinaryReader reader)
        {
            return new LocalTransform
            {
                Position = Read<float3>(reader),
                Scale = Read<float>(reader),
                Rotation = Read<quaternion>(reader),
            };
        }
        static void WriteNightEnemyChoice(BinaryWriter writer, NightEnemyChoice value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Weight);
        }
        static NightEnemyChoice ReadNightEnemyChoice(BinaryReader reader)
        {
            return new NightEnemyChoice
            {
                Definition = Read<int>(reader),
                Weight = Read<float>(reader),
            };
        }
        static void WriteNightEventDefinition(BinaryWriter writer, NightEventDefinition value)
        {
            Write(writer, value.Id);
            Write(writer, value.FollowUp);
            Write(writer, value.Kind);
            Write(writer, value.Priority);
            Write(writer, value.MinTurn);
            Write(writer, value.MaxTurn);
            Write(writer, value.Interval);
            Write(writer, value.Cooldown);
            Write(writer, value.WaveCount);
            Write(writer, value.ReturnDelay);
            Write(writer, value.PoolStart);
            Write(writer, value.PoolCount);
            Write(writer, value.ConditionStart);
            Write(writer, value.ConditionCount);
            Write(writer, value.Weight);
            Write(writer, value.BudgetScale);
            Write(writer, value.Duration);
            Write(writer, value.Once);
            Write(writer, value.ReturnOnly);
            Write(writer, value.Forced);
            Write(writer, value.WaveTimes);
        }
        static NightEventDefinition ReadNightEventDefinition(BinaryReader reader)
        {
            return new NightEventDefinition
            {
                Id = Read<FixedString64Bytes>(reader),
                FollowUp = Read<FixedString64Bytes>(reader),
                Kind = Read<NightKind>(reader),
                Priority = Read<int>(reader),
                MinTurn = Read<int>(reader),
                MaxTurn = Read<int>(reader),
                Interval = Read<int>(reader),
                Cooldown = Read<int>(reader),
                WaveCount = Read<int>(reader),
                ReturnDelay = Read<int>(reader),
                PoolStart = Read<int>(reader),
                PoolCount = Read<int>(reader),
                ConditionStart = Read<int>(reader),
                ConditionCount = Read<int>(reader),
                Weight = Read<float>(reader),
                BudgetScale = Read<float>(reader),
                Duration = Read<float>(reader),
                Once = Read<byte>(reader),
                ReturnOnly = Read<byte>(reader),
                Forced = Read<byte>(reader),
                WaveTimes = Read<FixedList512Bytes<float>>(reader),
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
                BossDefinition = Read<int>(reader),
                CombatElapsed = Read<float>(reader),
                FirstActionAt = Read<float>(reader),
                ClockStarted = Read<byte>(reader),
                Committed = Read<byte>(reader),
                AnySpawned = Read<byte>(reader),
                BossKilled = Read<byte>(reader),
                BossEscaped = Read<byte>(reader),
            };
        }
        static void WriteNightPreparation(BinaryWriter writer, NightPreparation value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Health);
            Write(writer, value.Damage);
            Write(writer, value.Speed);
            Write(writer, value.Range);
            Write(writer, value.Interval);
            Write(writer, value.ProjectileSpeed);
            Write(writer, value.Combat);
        }
        static NightPreparation ReadNightPreparation(BinaryReader reader)
        {
            return new NightPreparation
            {
                Definition = Read<int>(reader),
                Health = Read<float>(reader),
                Damage = Read<float>(reader),
                Speed = Read<float>(reader),
                Range = Read<float>(reader),
                Interval = Read<float>(reader),
                ProjectileSpeed = Read<float>(reader),
                Combat = Read<CombatProfile>(reader),
            };
        }
        static void WriteNightRules(BinaryWriter writer, NightRules value)
        {
            Write(writer, value.EntryLeadSeconds);
            Write(writer, value.WarningSeconds);
            Write(writer, value.ProtectionSeconds);
            Write(writer, value.SpawnSafety);
            Write(writer, value.BorderBuffer);
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
                EntryLeadSeconds = Read<float>(reader),
                WarningSeconds = Read<float>(reader),
                ProtectionSeconds = Read<float>(reader),
                SpawnSafety = Read<float>(reader),
                BorderBuffer = Read<float>(reader),
                HeroWeight = Read<float>(reader),
                FacilityWeight = Read<float>(reader),
                TargetRadius = Read<float>(reader),
                ThreatFloor = Read<float>(reader),
                ThreatPerStrengthCap = Read<float>(reader),
            };
        }
        static void WriteNightWave(BinaryWriter writer, NightWave value)
        {
            Write(writer, value.At);
            Write(writer, value.PowerScale);
            Write(writer, value.WarnedAt);
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
                Definition = Read<int>(reader),
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
        static void WriteOpportunityProfile(BinaryWriter writer, OpportunityProfile value)
        {
            Write(writer, value.Kind);
            Write(writer, value.Weight);
            Write(writer, value.MaximumPerNight);
            Write(writer, value.StartFraction);
            Write(writer, value.EndFraction);
            Write(writer, value.Speed);
            Write(writer, value.MinimumResponse);
            Write(writer, value.CaptureRadius);
            Write(writer, value.ResponseRadius);
            Write(writer, value.RouteLength);
            Write(writer, value.Soldiers);
            Write(writer, value.Heroes);
        }
        static OpportunityProfile ReadOpportunityProfile(BinaryReader reader)
        {
            return new OpportunityProfile
            {
                Kind = Read<VisitorKind>(reader),
                Weight = Read<int>(reader),
                MaximumPerNight = Read<int>(reader),
                StartFraction = Read<float>(reader),
                EndFraction = Read<float>(reader),
                Speed = Read<float>(reader),
                MinimumResponse = Read<float>(reader),
                CaptureRadius = Read<float>(reader),
                ResponseRadius = Read<float>(reader),
                RouteLength = Read<float>(reader),
                Soldiers = Read<bool>(reader),
                Heroes = Read<bool>(reader),
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
                Item = Read<int>(reader),
                Amount = Read<int>(reader),
                LossRemainder = Read<float>(reader),
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
                Definition = Read<int>(reader),
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
            Write(writer, value.RuleIndex);
            Write(writer, value.Amount);
            Write(writer, value.Key);
        }
        static QuestProgress ReadQuestProgress(BinaryReader reader)
        {
            return new QuestProgress
            {
                RuleIndex = Read<int>(reader),
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
                Item = Read<int>(reader),
                Amount = Read<int>(reader),
            };
        }
        static void WriteResearchEntry(BinaryWriter writer, ResearchEntry value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Progress);
            Write(writer, value.Completions);
            Write(writer, value.QueueOrder);
        }
        static ResearchEntry ReadResearchEntry(BinaryReader reader)
        {
            return new ResearchEntry
            {
                Definition = Read<int>(reader),
                Progress = Read<int>(reader),
                Completions = Read<int>(reader),
                QueueOrder = Read<int>(reader),
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
        static void WriteRule(BinaryWriter writer, Rule value)
        {
            Write(writer, value.Kind);
            Write(writer, value.Target);
            Write(writer, value.Secondary);
            Write(writer, value.Level);
            Write(writer, value.Amount);
            Write(writer, value.B);
            Write(writer, value.C);
            Write(writer, value.Value);
            Write(writer, value.Extra);
            Write(writer, value.Key);
        }
        static Rule ReadRule(BinaryReader reader)
        {
            return new Rule
            {
                Kind = Read<RuleKind>(reader),
                Target = Read<int>(reader),
                Secondary = Read<int>(reader),
                Level = Read<int>(reader),
                Amount = Read<int>(reader),
                B = Read<int>(reader),
                C = Read<int>(reader),
                Value = Read<float>(reader),
                Extra = Read<float>(reader),
                Key = Read<FixedString64Bytes>(reader),
            };
        }
        static void WriteSession(BinaryWriter writer, Session value)
        {
            Write(writer, value.Turn);
            Write(writer, value.Stage);
            Write(writer, value.BasePopulation);
            Write(writer, value.PublicOpinion);
            Write(writer, value.ExpeditionPenaltyStacks);
            Write(writer, value.ExpeditionPenaltyUntil);
            Write(writer, value.Phase);
            Write(writer, value.NightKind);
            Write(writer, value.RandomState);
            Write(writer, value.NightSeed);
            Write(writer, value.NextId);
            Write(writer, value.RetryCount);
            Write(writer, value.Threat);
            Write(writer, value.StartCombatStrength);
            Write(writer, value.LastSettledTurn);
            Write(writer, value.ResearchPoints);
            Write(writer, value.BossReturnTurn);
            Write(writer, value.IntelAtNight);
            Write(writer, value.Time);
            Write(writer, value.PhaseTime);
            Write(writer, value.NightDuration);
            Write(writer, value.DeploymentTime);
            Write(writer, value.Paused);
            Write(writer, value.Initialized);
            Write(writer, value.BossEscaped);
            Write(writer, value.CheckpointPending);
            Write(writer, value.NightSpeed);
            Write(writer, value.IntelligenceMode);
            Write(writer, value.SelectedHero);
            Write(writer, value.ActiveBell);
            Write(writer, value.DynastyName);
        }
        static Session ReadSession(BinaryReader reader)
        {
            return new Session
            {
                Turn = Read<int>(reader),
                Stage = Read<int>(reader),
                BasePopulation = Read<int>(reader),
                PublicOpinion = Read<int>(reader),
                ExpeditionPenaltyStacks = Read<int>(reader),
                ExpeditionPenaltyUntil = Read<int>(reader),
                Phase = Read<Phase>(reader),
                NightKind = Read<NightKind>(reader),
                RandomState = Read<uint>(reader),
                NightSeed = Read<uint>(reader),
                NextId = Read<ulong>(reader),
                RetryCount = Read<int>(reader),
                Threat = Read<int>(reader),
                StartCombatStrength = Read<int>(reader),
                LastSettledTurn = Read<int>(reader),
                ResearchPoints = Read<int>(reader),
                BossReturnTurn = Read<int>(reader),
                IntelAtNight = Read<int>(reader),
                Time = Read<float>(reader),
                PhaseTime = Read<float>(reader),
                NightDuration = Read<float>(reader),
                DeploymentTime = Read<float>(reader),
                Paused = Read<byte>(reader),
                Initialized = Read<byte>(reader),
                BossEscaped = Read<byte>(reader),
                CheckpointPending = Read<byte>(reader),
                NightSpeed = Read<byte>(reader),
                IntelligenceMode = Read<byte>(reader),
                SelectedHero = Read<Entity>(reader),
                ActiveBell = Read<ulong>(reader),
                DynastyName = Read<FixedString128Bytes>(reader),
            };
        }
        static void WriteSoldier(BinaryWriter writer, Soldier value)
        {
            Write(writer, value.Garrison);
            Write(writer, value.Slot);
            Write(writer, value.PopulationCost);
            Write(writer, value.PendingSince);
            Write(writer, value.Experience);
            Write(writer, value.LastExperienceTurn);
            Write(writer, value.RecallState);
        }
        static Soldier ReadSoldier(BinaryReader reader)
        {
            return new Soldier
            {
                Garrison = Read<ulong>(reader),
                Slot = Read<int>(reader),
                PopulationCost = Read<int>(reader),
                PendingSince = Read<int>(reader),
                Experience = Read<int>(reader),
                LastExperienceTurn = Read<int>(reader),
                RecallState = Read<byte>(reader),
            };
        }
        static void WriteSoldierGrowth(BinaryWriter writer, SoldierGrowth value)
        {
            Write(writer, value.MaxLevel);
            Write(writer, value.FirstLevelExperience);
            Write(writer, value.ExperienceStep);
            Write(writer, value.BattleExperience);
            Write(writer, value.HealthPerLevel);
            Write(writer, value.DamagePerLevel);
        }
        static SoldierGrowth ReadSoldierGrowth(BinaryReader reader)
        {
            return new SoldierGrowth
            {
                MaxLevel = Read<int>(reader),
                FirstLevelExperience = Read<int>(reader),
                ExperienceStep = Read<int>(reader),
                BattleExperience = Read<int>(reader),
                HealthPerLevel = Read<float>(reader),
                DamagePerLevel = Read<float>(reader),
            };
        }
        static void WriteSoldierPerson(BinaryWriter writer, SoldierPerson value)
        {
            Write(writer, value.Age);
            Write(writer, value.Lifespan);
            Write(writer, value.LastAgeTurn);
            Write(writer, value.Incarnation);
            Write(writer, value.Gender);
            Write(writer, value.SpecialAttention);
            Write(writer, value.DeathNotified);
        }
        static SoldierPerson ReadSoldierPerson(BinaryReader reader)
        {
            return new SoldierPerson
            {
                Age = Read<int>(reader),
                Lifespan = Read<int>(reader),
                LastAgeTurn = Read<int>(reader),
                Incarnation = Read<int>(reader),
                Gender = Read<PersonGender>(reader),
                SpecialAttention = Read<byte>(reader),
                DeathNotified = Read<byte>(reader),
            };
        }
        static void WriteSpawnRegion(BinaryWriter writer, SpawnRegion value)
        {
            Write(writer, value.Direction);
            Write(writer, value.Center);
            Write(writer, value.Size);
        }
        static SpawnRegion ReadSpawnRegion(BinaryReader reader)
        {
            return new SpawnRegion
            {
                Direction = Read<int>(reader),
                Center = Read<float3>(reader),
                Size = Read<float3>(reader),
            };
        }
        static void WriteTalent(BinaryWriter writer, Talent value)
        {
            Write(writer, value.Slot);
            Write(writer, value.Experience);
            Write(writer, value.Level);
            Write(writer, value.AssignedTurns);
            Write(writer, value.WageTurn);
            Write(writer, value.LastBenefitTurn);
            Write(writer, value.Recruited);
            Write(writer, value.Paid);
        }
        static Talent ReadTalent(BinaryReader reader)
        {
            return new Talent
            {
                Slot = Read<int>(reader),
                Experience = Read<int>(reader),
                Level = Read<int>(reader),
                AssignedTurns = Read<int>(reader),
                WageTurn = Read<int>(reader),
                LastBenefitTurn = Read<int>(reader),
                Recruited = Read<byte>(reader),
                Paid = Read<byte>(reader),
            };
        }
        static void WriteTheftProfile(BinaryWriter writer, TheftProfile value)
        {
            Write(writer, value.Protection);
            Write(writer, value.Weight);
            Write(writer, value.Maximum);
            Write(writer, value.UnitValue);
        }
        static TheftProfile ReadTheftProfile(BinaryReader reader)
        {
            return new TheftProfile
            {
                Protection = Read<ItemProtection>(reader),
                Weight = Read<int>(reader),
                Maximum = Read<int>(reader),
                UnitValue = Read<int>(reader),
            };
        }
        static void WriteTraitEntry(BinaryWriter writer, TraitEntry value)
        {
            Write(writer, value.Definition);
            Write(writer, value.Revealed);
            Write(writer, value.Active);
        }
        static TraitEntry ReadTraitEntry(BinaryReader reader)
        {
            return new TraitEntry
            {
                Definition = Read<int>(reader),
                Revealed = Read<byte>(reader),
                Active = Read<byte>(reader),
            };
        }
        static void WriteUnresolvedBoss(BinaryWriter writer, UnresolvedBoss value)
        {
            Write(writer, value.Event);
            Write(writer, value.Definition);
            Write(writer, value.DueTurn);
        }
        static UnresolvedBoss ReadUnresolvedBoss(BinaryReader reader)
        {
            return new UnresolvedBoss
            {
                Event = Read<FixedString64Bytes>(reader),
                Definition = Read<int>(reader),
                DueTurn = Read<int>(reader),
            };
        }
        static void Writefloat3(BinaryWriter writer, float3 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
            Write(writer, value.z);
        }
        static float3 Readfloat3(BinaryReader reader)
        {
            return new float3
            {
                x = Read<float>(reader),
                y = Read<float>(reader),
                z = Read<float>(reader),
            };
        }
        static void Writefloat4(BinaryWriter writer, float4 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
            Write(writer, value.z);
            Write(writer, value.w);
        }
        static float4 Readfloat4(BinaryReader reader)
        {
            return new float4
            {
                x = Read<float>(reader),
                y = Read<float>(reader),
                z = Read<float>(reader),
                w = Read<float>(reader),
            };
        }
        static void Writeint2(BinaryWriter writer, int2 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
        }
        static int2 Readint2(BinaryReader reader)
        {
            return new int2
            {
                x = Read<int>(reader),
                y = Read<int>(reader),
            };
        }
        static void Writeint4(BinaryWriter writer, int4 value)
        {
            Write(writer, value.x);
            Write(writer, value.y);
            Write(writer, value.z);
            Write(writer, value.w);
        }
        static int4 Readint4(BinaryReader reader)
        {
            return new int4
            {
                x = Read<int>(reader),
                y = Read<int>(reader),
                z = Read<int>(reader),
                w = Read<int>(reader),
            };
        }
        static void Writequaternion(BinaryWriter writer, quaternion value)
        {
            Write(writer, value.value);
        }
        static quaternion Readquaternion(BinaryReader reader)
        {
            return new quaternion
            {
                value = Read<float4>(reader),
            };
        }
        // A newly added runtime field must be reviewed deliberately, never silently omitted.
        public static void ValidateFieldCoverage()
        {
            Fields<BattleHistoryEntry>("Turn:int|Entry:BattleReportEntry");
            Fields<BattleReportEntry>("Kind:EventKind|Id:ulong|Definition:int|Amount:int|Value:float|SourceName:FixedString128Bytes");
            Fields<Building>("Stage:LifeStage|Level:int|Progress:int|Workers:int|StableWorkers:int|Experience:int|Population:int|Growth:int|FoodFailures:int|TaxProgress:int|ProductionProgress:int|Crop:int|CropProgress:int|WorkerTarget:int|ProtectionUntil:int|PaidOfferingTurn:int|WokenTurn:int|HarvestRemaining:int|PaidSubsidy:int|PaidSubsidyTurn:int|SubsidyBudget:int|SoldierRecruitTurn:int|SoldiersRecruited:int|CropSeed:uint|Skin:FixedString64Bytes|RepairDuration:int|RepairCompletedTurn:int|DeferredResidents:int|RuinPending:byte|MarketValue:long|MarketLifetimeValue:long|Offering:byte|Subsidy:byte|Maintained:byte|CropFullCycle:byte|AutoHarvest:byte|Cell:int2|Size:int2|Rotation:int|Elevation:int|Surface:int");
            Fields<BuildingInvestment>("Item:int|Amount:int");
            Fields<BuildingPolicy>("Category:BuildingCategory|MenuOrder:int|ProviderPriority:int|RepairTurns:int|SoldierRecruitLimit:int|MoveMaterialRatio:float|MoveExperienceRatio:float|RuinMovementCost:float|CanMove:byte|CanRotate:byte");
            Fields<Color32>("r:byte|g:byte|b:byte|a:byte");
            Fields<CombatProfile>("DetectionRadius:float|ChaseRadius:float|ChaseSeconds:float|BodyRadius:float|Armor:float|Reduction:float|Penetration:float|BlastRadius:float|WarningSeconds:float|ProjectileLifetime:float|ProjectileMode:ProjectileMode|Traits:TacticalTraits|BlocksProjectile:bool");
            Fields<ContentDefinition>("Id:FixedString128Bytes|Name:FixedString128Bytes|Kind:ContentKind|RuleStart:int|RuleCount:int|Level:int|Group:int|Capacity:int|Duration:int|Value:int|Limit:int|Size:int2|Health:float|Damage:float|Range:float|Interval:float|Speed:float|ProjectileSpeed:float|Chance:float|Loss:float|Population:int|Cost:int|Flags:int|TargetCategory:BuildingCategory|QuestIntensity:int|QuestWeight:float|ItemQuantityScale:float|BuildingPolicy:BuildingPolicy|SoldierGrowth:SoldierGrowth|HeroGrowth:HeroGrowth|Combat:CombatProfile|Opportunity:OpportunityProfile|Theft:TheftProfile|DefaultSkin:FixedString64Bytes");
            Fields<CourtLogEntry>("Turn:int|Person:ulong|Message:FixedString128Bytes");
            Fields<CourtSettings>("MarriageAge:int|CaptainAge:int|GiftCost:int|GiftAffection:int|RecruitAffection:int|MarriageAffection:int|StableDesignationTurns:int|MinimumReign:int|TemporaryTurns:int|ElectionTurns:int|DisorderTurns:int|VisitInterval:int|VisitDuration:int|VisitCost:int|MarriageRequestCooldown:int|ExpeditionRequestCooldown:int|ExpeditionRequestChance:float|MarriageRequestChance:float|MarriageRefusalGrievanceChance:float|MarriageRefusalGrievance:float|InitialOpinion:int|OpinionRecovery:int|DisorderOpinionCost:int|PrinceGrowth:float|StrongInfluence:float|UsurpGap:float|UsurpChance:float|RegicideGap:float|RegicideChance:float|PrinceRisk:float|StableProduction:float|StableAttack:float|WeakProduction:float|WeakAttack:float|ElectionProduction:float|ElectionAttack:float|UsurpProduction:float|UsurpAttack:float|RegicideProduction:float|RegicideAttack:float|DisorderPerStack:float|DisorderCap:float|DeathYoung:float|DeathAdult:float|DeathMature:float|DeathOld:float|DeathAncient:float");
            Fields<CourtState>("Crown:ulong|LegacyFounder:ulong|CrownSince:int|LastSettledTurn:int|TemporaryUntil:int|DisorderUntil:int|LegacyGeneration:int|VisitOfferTurn:int|TemporaryProduction:float|TemporaryAttack:float|LegacyProduction:float|LegacyAttack:float|Disorder:float|Extinction:byte|LegacySeverity:byte|VisitResolved:byte");
            Fields<DynastySettings>("MaxChildren:int|TalentCapacity:int|TalentRecruitCost:int|TalentExperience:int|BirthChance:float|MutationChance:float");
            Fields<EconomyEntry>("Turn:int|Item:int|Delta:int|Source:ulong|Reason:EconomyReason|Pending:byte|SourceName:FixedString128Bytes|Note:FixedString128Bytes");
            Fields<Entitlement>("Definition:int|Level:int");
            Fields<Entity>("Index:int|Version:int");
            Fields<Expedition>("Site:ulong|Captain:ulong|SourceName:FixedString128Bytes|Crew:int|Departure:int|Arrival:int|SourceLevel:int|Casualties:int|SubsidyRequired:int|SubsidyPaid:int|PenaltyStacks:int|RewardBonus:float|SuccessChance:float|Status:ExpeditionStatus");
            Fields<ExpeditionDestinationHistory>("Definition:int");
            Fields<ExpeditionSettings>("PenaltyTurns:int|AttractionPerStack:float");
            Fields<ExpeditionSupply>("Item:int|Amount:int");
            Fields<FoodSelection>("Group:int|Item:int|Amount:int");
            Fields<GameSettings>("PeacefulSeconds:float|BattleSeconds:float|DeployInterval:float|RetreatSeconds:float|InvasionChance:float|StrengthRatio:float|RetryStep:float|RetryCap:float|FirstInvasion:int|FirstBoss:int|BossInterval:int|ThreatPerTurn:int|Gold:int|LowIntel:int|MediumIntel:int|HighIntel:int|MediumIntelLead:float|HighIntelLead:float");
            Fields<GridCell>("Exists:byte|Buildable:byte|Traversable:byte|BlocksProjectile:byte|Elevation:int|Surface:int|Height:float|Terrain:ulong");
            Fields<Health>("Current:float|Maximum:float");
            Fields<Hero>("Sanctum:ulong|CooldownUntil:int|Experience:int|LastCombatTurn:int|Recruited:byte|DeathPending:byte");
            Fields<HeroGrowth>("MaxLevel:int|FirstLevelExperience:int|ExperienceStep:int|HealthPerLevel:float|DamagePerLevel:float|OfferingExperience:int|ContactSeconds:float|ExperiencePerSecond:float|ThreatReference:float|MaximumThreatMultiplier:float");
            Fields<HistoryEntry>("Turn:int|Item:int|Delta:int|Count:int|Source:ulong|Pending:byte|HasPosition:byte|Transfer:byte|Category:HistoryCategory|Position:float3|SourceName:FixedString128Bytes|Text:FixedString128Bytes");
            Fields<Identity>("Id:ulong|Definition:int|Name:FixedString128Bytes");
            Fields<InitialBuilding>("Definition:int|Level:int|Rotation:int|Cell:int2|Name:FixedString128Bytes");
            Fields<InitialRoyal>("Name:FixedString128Bytes|Age:int|Role:byte|Gender:PersonGender|Traits:FixedList128Bytes<int>");
            Fields<InventorySlot>("Provider:ulong|Index:int|SlotType:int|Item:int|Count:int|LossRemainder:float|Unavailable:byte");
            Fields<LocalTransform>("Position:float3|Scale:float|Rotation:quaternion");
            Fields<NightEnemyChoice>("Definition:int|Weight:float");
            Fields<NightEventDefinition>("Id:FixedString64Bytes|FollowUp:FixedString64Bytes|Kind:NightKind|Priority:int|MinTurn:int|MaxTurn:int|Interval:int|Cooldown:int|WaveCount:int|ReturnDelay:int|PoolStart:int|PoolCount:int|ConditionStart:int|ConditionCount:int|Weight:float|BudgetScale:float|Duration:float|Once:byte|ReturnOnly:byte|Forced:byte|WaveTimes:FixedList512Bytes<float>");
            Fields<NightEventHistory>("Event:FixedString64Bytes|LastTurn:int|Count:int");
            Fields<NightPlanState>("Event:FixedString64Bytes|Turn:int|BaseThreat:int|PreparedTurn:int|BossDefinition:int|CombatElapsed:float|FirstActionAt:float|ClockStarted:byte|Committed:byte|AnySpawned:byte|BossKilled:byte|BossEscaped:byte");
            Fields<NightPreparation>("Definition:int|Health:float|Damage:float|Speed:float|Range:float|Interval:float|ProjectileSpeed:float|Combat:CombatProfile");
            Fields<NightRules>("EntryLeadSeconds:float|WarningSeconds:float|ProtectionSeconds:float|SpawnSafety:float|BorderBuffer:float|HeroWeight:float|FacilityWeight:float|TargetRadius:float|ThreatFloor:float|ThreatPerStrengthCap:float");
            Fields<NightWave>("At:float|PowerScale:float|WarnedAt:float|Definition:int|Count:int|Direction:int|Region:int|Position:float3|Target:ulong|Spawned:byte|Warned:byte|SpatiallyBlocked:byte");
            Fields<OpportunityProfile>("Kind:VisitorKind|Weight:int|MaximumPerNight:int|StartFraction:float|EndFraction:float|Speed:float|MinimumResponse:float|CaptureRadius:float|ResponseRadius:float|RouteLength:float|Soldiers:bool|Heroes:bool");
            Fields<PeacefulRules>("MaximumPerNight:int|MaximumConcurrent:int|TheftValueBudget:int|FirstOpportunity:float|Interval:float");
            Fields<PendingItem>("Item:int|Amount:int|LossRemainder:float");
            Fields<PersonRequestEntry>("Kind:PersonRequestKind|Status:PersonRequestStatus|CreatedTurn:int|ResolvedTurn:int|Journey:ulong");
            Fields<PolicyChoice>("Definition:int");
            Fields<PortraitDNA>("Parts:FixedList128Bytes<int>|SkinDetails:FixedList64Bytes<int>|Skin:Color32|Hair:Color32|Eyes:Color32|Seed:uint|Customized:byte|InvitationAnnounced:byte");
            Fields<PortraitSettings>("YouthAge:int|GreyAge:int|ElderAge:int|SoldierRecruitMinAge:int|SoldierRecruitMaxAge:int|SoldierLifeMin:int|SoldierLifeMax:int|ColorMutation:float");
            Fields<Quest>("Status:QuestStatus|StartTurn:int|Deadline:int|Source:ulong|Container:ulong|Slot:int|ContainerSlot:int|Mainline:byte");
            Fields<QuestGenerationSettings>("StrengthStep:int|MarketValuePerStrength:int|Low:int4|Medium:int4|High:int4|Maximum:int4");
            Fields<QuestOfferSlot>("Type:int|Index:int|NextTurn:int");
            Fields<QuestProgress>("RuleIndex:int|Amount:int|Key:FixedString64Bytes");
            Fields<QuestTracking>("Target:ulong|Mode:byte");
            Fields<RepairMaterial>("Item:int|Amount:int");
            Fields<ResearchEntry>("Definition:int|Progress:int|Completions:int|QueueOrder:int");
            Fields<Royal>("Role:byte|Alive:byte|Retired:byte|FateUsed:byte|Evidence:byte|TaskClaimed:byte|EverMonarch:byte|Gender:PersonGender|MarriageRequestTurn:int|MarriageCooldownUntil:int|RequestedSpouse:ulong|MarriageRequestMonarch:ulong|Age:int|Generation:int|ReignSince:int|FateUntil:int|LastGiftTurn:int|Affection:int|VisitUntil:int|Parent:ulong|SecondParent:ulong|Spouse:ulong|Influence:float|Growth:float|Ambition:float|Grievance:float|InfluenceSource:FixedString128Bytes");
            Fields<Rule>("Kind:RuleKind|Target:int|Secondary:int|Level:int|Amount:int|B:int|C:int|Value:float|Extra:float|Key:FixedString64Bytes");
            Fields<Session>("Turn:int|Stage:int|BasePopulation:int|PublicOpinion:int|ExpeditionPenaltyStacks:int|ExpeditionPenaltyUntil:int|Phase:Phase|NightKind:NightKind|RandomState:uint|NightSeed:uint|NextId:ulong|RetryCount:int|Threat:int|StartCombatStrength:int|LastSettledTurn:int|ResearchPoints:int|BossReturnTurn:int|IntelAtNight:int|Time:float|PhaseTime:float|NightDuration:float|DeploymentTime:float|Paused:byte|Initialized:byte|BossEscaped:byte|CheckpointPending:byte|NightSpeed:byte|IntelligenceMode:byte|SelectedHero:Entity|ActiveBell:ulong|DynastyName:FixedString128Bytes");
            Fields<Soldier>("Garrison:ulong|Slot:int|PopulationCost:int|PendingSince:int|Experience:int|LastExperienceTurn:int|RecallState:byte");
            Fields<SoldierGrowth>("MaxLevel:int|FirstLevelExperience:int|ExperienceStep:int|BattleExperience:int|HealthPerLevel:float|DamagePerLevel:float");
            Fields<SoldierPerson>("Age:int|Lifespan:int|LastAgeTurn:int|Incarnation:int|Gender:PersonGender|SpecialAttention:byte|DeathNotified:byte");
            Fields<SpawnRegion>("Direction:int|Center:float3|Size:float3");
            Fields<Talent>("Slot:int|Experience:int|Level:int|AssignedTurns:int|WageTurn:int|LastBenefitTurn:int|Recruited:byte|Paid:byte");
            Fields<TheftProfile>("Protection:ItemProtection|Weight:int|Maximum:int|UnitValue:int");
            Fields<TraitEntry>("Definition:int|Revealed:byte|Active:byte");
            Fields<UnresolvedBoss>("Event:FixedString64Bytes|Definition:int|DueTurn:int");
            Fields<float3>("x:float|y:float|z:float");
            Fields<float4>("x:float|y:float|z:float|w:float");
            Fields<int2>("x:int|y:int");
            Fields<int4>("x:int|y:int|z:int|w:int");
            Fields<quaternion>("value:float4");
        }
        static void Fields<T>(string expected)
        {
            string Alias(Type t) => t == typeof(int) ? "int" : t == typeof(uint) ? "uint" : t == typeof(byte) ? "byte" : t == typeof(ulong) ? "ulong" : t == typeof(long) ? "long" : t == typeof(float) ? "float" : t == typeof(bool) ? "bool" : t.IsGenericType ? t.Name.Split('`')[0] + "<" + Alias(t.GetGenericArguments()[0]) + ">" : t.Name;
            var actual = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name + ":" + Alias(f.FieldType)).OrderBy(x => x).ToArray();
            if (!actual.SequenceEqual(expected.Split('|').OrderBy(x => x))) throw new InvalidOperationException("Review persistent field coverage: " + typeof(T).Name);
        }
    }
}
