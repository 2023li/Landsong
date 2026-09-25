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
    // Explicit versioned disk schema; runtime layout and padding never enter the archive.
    public static partial class SnapshotBinary
    {
        public static void Write<T>(BinaryWriter writer, T value)
            where T : unmanaged
        {
            if (typeof(T) == typeof(BattleHistoryEntry))
            {
                WriteBattleHistoryEntry(writer, (BattleHistoryEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(BattleReportEntry))
            {
                WriteBattleReportEntry(writer, (BattleReportEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(BellState))
            {
                WriteBellState(writer, (BellState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BlueprintUnlock))
            {
                WriteBlueprintUnlock(writer, (BlueprintUnlock)(object)value);
                return;
            }

            if (typeof(T) == typeof(Building))
            {
                WriteBuilding(writer, (Building)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingAppearanceState))
            {
                WriteBuildingAppearanceState(writer, (BuildingAppearanceState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingCategory))
            {
                writer.Write((int)(BuildingCategory)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingConstructionState))
            {
                WriteBuildingConstructionState(writer, (BuildingConstructionState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingExperienceState))
            {
                WriteBuildingExperienceState(writer, (BuildingExperienceState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingFarmingState))
            {
                WriteBuildingFarmingState(writer, (BuildingFarmingState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingGatheringState))
            {
                WriteBuildingGatheringState(writer, (BuildingGatheringState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingHousingState))
            {
                WriteBuildingHousingState(writer, (BuildingHousingState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingInvestment))
            {
                WriteBuildingInvestment(writer, (BuildingInvestment)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingMaintenanceState))
            {
                WriteBuildingMaintenanceState(writer, (BuildingMaintenanceState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingMarketState))
            {
                WriteBuildingMarketState(writer, (BuildingMarketState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingPlacementState))
            {
                WriteBuildingPlacementState(writer, (BuildingPlacementState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingProductionState))
            {
                WriteBuildingProductionState(writer, (BuildingProductionState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingRecruitmentState))
            {
                WriteBuildingRecruitmentState(writer, (BuildingRecruitmentState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingSanctumState))
            {
                WriteBuildingSanctumState(writer, (BuildingSanctumState)(object)value);
                return;
            }

            if (typeof(T) == typeof(BuildingWorkforceState))
            {
                WriteBuildingWorkforceState(writer, (BuildingWorkforceState)(object)value);
                return;
            }

            if (typeof(T) == typeof(ClaimedQuest))
            {
                WriteClaimedQuest(writer, (ClaimedQuest)(object)value);
                return;
            }

            if (typeof(T) == typeof(CombatProfile))
            {
                WriteCombatProfile(writer, (CombatProfile)(object)value);
                return;
            }

            if (typeof(T) == typeof(CombatStatsSnapshot))
            {
                WriteCombatStatsSnapshot(writer, (CombatStatsSnapshot)(object)value);
                return;
            }

            if (typeof(T) == typeof(CompletedExpedition))
            {
                WriteCompletedExpedition(writer, (CompletedExpedition)(object)value);
                return;
            }

            if (typeof(T) == typeof(CourtLogEntry))
            {
                WriteCourtLogEntry(writer, (CourtLogEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(CourtSettings))
            {
                WriteCourtSettings(writer, (CourtSettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(CourtState))
            {
                WriteCourtState(writer, (CourtState)(object)value);
                return;
            }

            if (typeof(T) == typeof(CurrencySettings))
            {
                WriteCurrencySettings(writer, (CurrencySettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(DaySettlementState))
            {
                WriteDaySettlementState(writer, (DaySettlementState)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuffId))
            {
                writer.Write(((Landsong.ECS.Definitions.BuffId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingEffectStacking))
            {
                writer.Write((int)(Landsong.ECS.Definitions.BuildingEffectStacking)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingEnvironmentKind))
            {
                writer.Write((int)(Landsong.ECS.Definitions.BuildingEnvironmentKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingId))
            {
                writer.Write(((Landsong.ECS.Definitions.BuildingId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingInvitationKind))
            {
                writer.Write((int)(Landsong.ECS.Definitions.BuildingInvitationKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingLimitGroupId))
            {
                writer.Write(((Landsong.ECS.Definitions.BuildingLimitGroupId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.CropId))
            {
                writer.Write(((Landsong.ECS.Definitions.CropId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.EnemyBehaviorFlags))
            {
                writer.Write((byte)(Landsong.ECS.Definitions.EnemyBehaviorFlags)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.EnemyId))
            {
                writer.Write(((Landsong.ECS.Definitions.EnemyId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.ExpeditionId))
            {
                writer.Write(((Landsong.ECS.Definitions.ExpeditionId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.FeatureId))
            {
                writer.Write(((Landsong.ECS.Definitions.FeatureId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.HeroId))
            {
                writer.Write(((Landsong.ECS.Definitions.HeroId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.ItemGroupId))
            {
                writer.Write(((Landsong.ECS.Definitions.ItemGroupId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.ItemId))
            {
                writer.Write(((Landsong.ECS.Definitions.ItemId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.KingdomEffectKind))
            {
                writer.Write((byte)(Landsong.ECS.Definitions.KingdomEffectKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.NumericEffectKind))
            {
                writer.Write((byte)(Landsong.ECS.Definitions.NumericEffectKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.PolicyGroupId))
            {
                writer.Write(((Landsong.ECS.Definitions.PolicyGroupId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.PolicyId))
            {
                writer.Write(((Landsong.ECS.Definitions.PolicyId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.QuestBehaviorFlags))
            {
                writer.Write((byte)(Landsong.ECS.Definitions.QuestBehaviorFlags)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.QuestId))
            {
                writer.Write(((Landsong.ECS.Definitions.QuestId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.QuestOfferType))
            {
                writer.Write((byte)(Landsong.ECS.Definitions.QuestOfferType)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.RoyalTraitId))
            {
                writer.Write(((Landsong.ECS.Definitions.RoyalTraitId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.SoldierId))
            {
                writer.Write(((Landsong.ECS.Definitions.SoldierId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.SpecialDropRarity))
            {
                writer.Write((byte)(Landsong.ECS.Definitions.SpecialDropRarity)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.StorageSlotId))
            {
                writer.Write(((Landsong.ECS.Definitions.StorageSlotId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.TalentId))
            {
                writer.Write(((Landsong.ECS.Definitions.TalentId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.TalentScalingKind))
            {
                writer.Write((byte)(Landsong.ECS.Definitions.TalentScalingKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.TalentSlotId))
            {
                writer.Write(((Landsong.ECS.Definitions.TalentSlotId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(Landsong.ECS.Definitions.TechnologyId))
            {
                writer.Write(((Landsong.ECS.Definitions.TechnologyId)(object)value).Index);
                return;
            }

            if (typeof(T) == typeof(DynastyIdentity))
            {
                WriteDynastyIdentity(writer, (DynastyIdentity)(object)value);
                return;
            }

            if (typeof(T) == typeof(EconomyBillEntry))
            {
                WriteEconomyBillEntry(writer, (EconomyBillEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(EconomyEntry))
            {
                WriteEconomyEntry(writer, (EconomyEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(EconomyReason))
            {
                writer.Write((byte)(EconomyReason)(object)value);
                return;
            }

            if (typeof(T) == typeof(EventKind))
            {
                writer.Write((byte)(EventKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(Expedition))
            {
                WriteExpedition(writer, (Expedition)(object)value);
                return;
            }

            if (typeof(T) == typeof(ExpeditionDestinationHistory))
            {
                WriteExpeditionDestinationHistory(writer, (ExpeditionDestinationHistory)(object)value);
                return;
            }

            if (typeof(T) == typeof(ExpeditionPenaltyState))
            {
                WriteExpeditionPenaltyState(writer, (ExpeditionPenaltyState)(object)value);
                return;
            }

            if (typeof(T) == typeof(ExpeditionSettings))
            {
                WriteExpeditionSettings(writer, (ExpeditionSettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(ExpeditionStatus))
            {
                writer.Write((byte)(ExpeditionStatus)(object)value);
                return;
            }

            if (typeof(T) == typeof(ExpeditionSupply))
            {
                WriteExpeditionSupply(writer, (ExpeditionSupply)(object)value);
                return;
            }

            if (typeof(T) == typeof(FoodSelection))
            {
                WriteFoodSelection(writer, (FoodSelection)(object)value);
                return;
            }

            if (typeof(T) == typeof(GameClock))
            {
                WriteGameClock(writer, (GameClock)(object)value);
                return;
            }

            if (typeof(T) == typeof(Health))
            {
                WriteHealth(writer, (Health)(object)value);
                return;
            }

            if (typeof(T) == typeof(Hero))
            {
                WriteHero(writer, (Hero)(object)value);
                return;
            }

            if (typeof(T) == typeof(HeroSelection))
            {
                WriteHeroSelection(writer, (HeroSelection)(object)value);
                return;
            }

            if (typeof(T) == typeof(HistoryCategory))
            {
                writer.Write((byte)(HistoryCategory)(object)value);
                return;
            }

            if (typeof(T) == typeof(HistoryEntry))
            {
                WriteHistoryEntry(writer, (HistoryEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(Identity))
            {
                WriteIdentity(writer, (Identity)(object)value);
                return;
            }

            if (typeof(T) == typeof(IdentitySequence))
            {
                WriteIdentitySequence(writer, (IdentitySequence)(object)value);
                return;
            }

            if (typeof(T) == typeof(InitialBuilding))
            {
                WriteInitialBuilding(writer, (InitialBuilding)(object)value);
                return;
            }

            if (typeof(T) == typeof(InitialRoyal))
            {
                WriteInitialRoyal(writer, (InitialRoyal)(object)value);
                return;
            }

            if (typeof(T) == typeof(IntelligenceModeState))
            {
                WriteIntelligenceModeState(writer, (IntelligenceModeState)(object)value);
                return;
            }

            if (typeof(T) == typeof(IntelligenceSettings))
            {
                WriteIntelligenceSettings(writer, (IntelligenceSettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(InventorySlot))
            {
                WriteInventorySlot(writer, (InventorySlot)(object)value);
                return;
            }

            if (typeof(T) == typeof(ItemProtection))
            {
                writer.Write((byte)(ItemProtection)(object)value);
                return;
            }

            if (typeof(T) == typeof(LifeStage))
            {
                writer.Write((byte)(LifeStage)(object)value);
                return;
            }

            if (typeof(T) == typeof(NightEventHistory))
            {
                WriteNightEventHistory(writer, (NightEventHistory)(object)value);
                return;
            }

            if (typeof(T) == typeof(NightKind))
            {
                writer.Write((byte)(NightKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(NightPlanState))
            {
                WriteNightPlanState(writer, (NightPlanState)(object)value);
                return;
            }

            if (typeof(T) == typeof(NightRules))
            {
                WriteNightRules(writer, (NightRules)(object)value);
                return;
            }

            if (typeof(T) == typeof(NightRuntimeState))
            {
                WriteNightRuntimeState(writer, (NightRuntimeState)(object)value);
                return;
            }

            if (typeof(T) == typeof(NightSettings))
            {
                WriteNightSettings(writer, (NightSettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(NightWave))
            {
                WriteNightWave(writer, (NightWave)(object)value);
                return;
            }

            if (typeof(T) == typeof(OwnedBuff))
            {
                WriteOwnedBuff(writer, (OwnedBuff)(object)value);
                return;
            }

            if (typeof(T) == typeof(PeacefulRules))
            {
                WritePeacefulRules(writer, (PeacefulRules)(object)value);
                return;
            }

            if (typeof(T) == typeof(PendingItem))
            {
                WritePendingItem(writer, (PendingItem)(object)value);
                return;
            }

            if (typeof(T) == typeof(PersistenceGate))
            {
                WritePersistenceGate(writer, (PersistenceGate)(object)value);
                return;
            }

            if (typeof(T) == typeof(PersonGender))
            {
                writer.Write((byte)(PersonGender)(object)value);
                return;
            }

            if (typeof(T) == typeof(PersonRequestEntry))
            {
                WritePersonRequestEntry(writer, (PersonRequestEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(PersonRequestKind))
            {
                writer.Write((byte)(PersonRequestKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(PersonRequestStatus))
            {
                writer.Write((byte)(PersonRequestStatus)(object)value);
                return;
            }

            if (typeof(T) == typeof(Phase))
            {
                writer.Write((byte)(Phase)(object)value);
                return;
            }

            if (typeof(T) == typeof(PolicyChoice))
            {
                WritePolicyChoice(writer, (PolicyChoice)(object)value);
                return;
            }

            if (typeof(T) == typeof(PopulationState))
            {
                WritePopulationState(writer, (PopulationState)(object)value);
                return;
            }

            if (typeof(T) == typeof(PortraitDNA))
            {
                WritePortraitDNA(writer, (PortraitDNA)(object)value);
                return;
            }

            if (typeof(T) == typeof(PortraitSettings))
            {
                WritePortraitSettings(writer, (PortraitSettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(PreparedBuildingDefense))
            {
                WritePreparedBuildingDefense(writer, (PreparedBuildingDefense)(object)value);
                return;
            }

            if (typeof(T) == typeof(PreparedHero))
            {
                WritePreparedHero(writer, (PreparedHero)(object)value);
                return;
            }

            if (typeof(T) == typeof(PreparedSoldier))
            {
                WritePreparedSoldier(writer, (PreparedSoldier)(object)value);
                return;
            }

            if (typeof(T) == typeof(ProjectileMode))
            {
                writer.Write((byte)(ProjectileMode)(object)value);
                return;
            }

            if (typeof(T) == typeof(PublicOpinionState))
            {
                WritePublicOpinionState(writer, (PublicOpinionState)(object)value);
                return;
            }

            if (typeof(T) == typeof(Quest))
            {
                WriteQuest(writer, (Quest)(object)value);
                return;
            }

            if (typeof(T) == typeof(QuestGenerationSettings))
            {
                WriteQuestGenerationSettings(writer, (QuestGenerationSettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(QuestOfferSlot))
            {
                WriteQuestOfferSlot(writer, (QuestOfferSlot)(object)value);
                return;
            }

            if (typeof(T) == typeof(QuestProgress))
            {
                WriteQuestProgress(writer, (QuestProgress)(object)value);
                return;
            }

            if (typeof(T) == typeof(QuestStatus))
            {
                writer.Write((byte)(QuestStatus)(object)value);
                return;
            }

            if (typeof(T) == typeof(QuestTracking))
            {
                WriteQuestTracking(writer, (QuestTracking)(object)value);
                return;
            }

            if (typeof(T) == typeof(RepairMaterial))
            {
                WriteRepairMaterial(writer, (RepairMaterial)(object)value);
                return;
            }

            if (typeof(T) == typeof(ResearchState))
            {
                WriteResearchState(writer, (ResearchState)(object)value);
                return;
            }

            if (typeof(T) == typeof(RetryState))
            {
                WriteRetryState(writer, (RetryState)(object)value);
                return;
            }

            if (typeof(T) == typeof(Royal))
            {
                WriteRoyal(writer, (Royal)(object)value);
                return;
            }

            if (typeof(T) == typeof(RoyalFamilySettings))
            {
                WriteRoyalFamilySettings(writer, (RoyalFamilySettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(Session))
            {
                WriteSession(writer, (Session)(object)value);
                return;
            }

            if (typeof(T) == typeof(SimulationControl))
            {
                WriteSimulationControl(writer, (SimulationControl)(object)value);
                return;
            }

            if (typeof(T) == typeof(SimulationRandomState))
            {
                WriteSimulationRandomState(writer, (SimulationRandomState)(object)value);
                return;
            }

            if (typeof(T) == typeof(Soldier))
            {
                WriteSoldier(writer, (Soldier)(object)value);
                return;
            }

            if (typeof(T) == typeof(SoldierPerson))
            {
                WriteSoldierPerson(writer, (SoldierPerson)(object)value);
                return;
            }

            if (typeof(T) == typeof(SpawnRegion))
            {
                WriteSpawnRegion(writer, (SpawnRegion)(object)value);
                return;
            }

            if (typeof(T) == typeof(TacticalTraits))
            {
                writer.Write((byte)(TacticalTraits)(object)value);
                return;
            }

            if (typeof(T) == typeof(Talent))
            {
                WriteTalent(writer, (Talent)(object)value);
                return;
            }

            if (typeof(T) == typeof(TalentSettings))
            {
                WriteTalentSettings(writer, (TalentSettings)(object)value);
                return;
            }

            if (typeof(T) == typeof(TechnologyProgress))
            {
                WriteTechnologyProgress(writer, (TechnologyProgress)(object)value);
                return;
            }

            if (typeof(T) == typeof(TerrainType))
            {
                writer.Write((int)(TerrainType)(object)value);
                return;
            }

            if (typeof(T) == typeof(TraitEntry))
            {
                WriteTraitEntry(writer, (TraitEntry)(object)value);
                return;
            }

            if (typeof(T) == typeof(UnlockedFeature))
            {
                WriteUnlockedFeature(writer, (UnlockedFeature)(object)value);
                return;
            }

            if (typeof(T) == typeof(UnresolvedBoss))
            {
                WriteUnresolvedBoss(writer, (UnresolvedBoss)(object)value);
                return;
            }

            if (typeof(T) == typeof(VisitorKind))
            {
                writer.Write((byte)(VisitorKind)(object)value);
                return;
            }

            if (typeof(T) == typeof(FixedList128Bytes<Landsong.ECS.Definitions.RoyalTraitId>))
            {
                WriteFixedList128Bytes_Landsong_ECS_Definitions_RoyalTraitId_(writer, (FixedList128Bytes<Landsong.ECS.Definitions.RoyalTraitId>)(object)value);
                return;
            }

            if (typeof(T) == typeof(FixedList128Bytes<int>))
            {
                WriteFixedList128Bytes_int_(writer, (FixedList128Bytes<int>)(object)value);
                return;
            }

            if (typeof(T) == typeof(FixedList512Bytes<float>))
            {
                WriteFixedList512Bytes_float_(writer, (FixedList512Bytes<float>)(object)value);
                return;
            }

            if (typeof(T) == typeof(FixedList64Bytes<int>))
            {
                WriteFixedList64Bytes_int_(writer, (FixedList64Bytes<int>)(object)value);
                return;
            }

            if (typeof(T) == typeof(FixedString128Bytes))
            {
                writer.Write(((FixedString128Bytes)(object)value).ToString());
                return;
            }

            if (typeof(T) == typeof(FixedString64Bytes))
            {
                writer.Write(((FixedString64Bytes)(object)value).ToString());
                return;
            }

            if (typeof(T) == typeof(Entity))
            {
                WriteEntity(writer, (Entity)(object)value);
                return;
            }

            if (typeof(T) == typeof(float3))
            {
                Writefloat3(writer, (float3)(object)value);
                return;
            }

            if (typeof(T) == typeof(float4))
            {
                Writefloat4(writer, (float4)(object)value);
                return;
            }

            if (typeof(T) == typeof(int2))
            {
                Writeint2(writer, (int2)(object)value);
                return;
            }

            if (typeof(T) == typeof(int4))
            {
                Writeint4(writer, (int4)(object)value);
                return;
            }

            if (typeof(T) == typeof(quaternion))
            {
                Writequaternion(writer, (quaternion)(object)value);
                return;
            }

            if (typeof(T) == typeof(LocalTransform))
            {
                WriteLocalTransform(writer, (LocalTransform)(object)value);
                return;
            }

            if (typeof(T) == typeof(Color32))
            {
                WriteColor32(writer, (Color32)(object)value);
                return;
            }

            if (typeof(T) == typeof(bool))
            {
                writer.Write((bool)(object)value);
                return;
            }

            if (typeof(T) == typeof(byte))
            {
                writer.Write((byte)(object)value);
                return;
            }

            if (typeof(T) == typeof(float))
            {
                writer.Write((float)(object)value);
                return;
            }

            if (typeof(T) == typeof(int))
            {
                writer.Write((int)(object)value);
                return;
            }

            if (typeof(T) == typeof(long))
            {
                writer.Write((long)(object)value);
                return;
            }

            if (typeof(T) == typeof(uint))
            {
                writer.Write((uint)(object)value);
                return;
            }

            if (typeof(T) == typeof(ulong))
            {
                writer.Write((ulong)(object)value);
                return;
            }

            throw new InvalidDataException("No snapshot writer for " + typeof(T));
        }

        public static T Read<T>(BinaryReader reader)
            where T : unmanaged
        {
            if (typeof(T) == typeof(BattleHistoryEntry))
                return (T)(object)(ReadBattleHistoryEntry(reader));
            if (typeof(T) == typeof(BattleReportEntry))
                return (T)(object)(ReadBattleReportEntry(reader));
            if (typeof(T) == typeof(BellState))
                return (T)(object)(ReadBellState(reader));
            if (typeof(T) == typeof(BlueprintUnlock))
                return (T)(object)(ReadBlueprintUnlock(reader));
            if (typeof(T) == typeof(Building))
                return (T)(object)(ReadBuilding(reader));
            if (typeof(T) == typeof(BuildingAppearanceState))
                return (T)(object)(ReadBuildingAppearanceState(reader));
            if (typeof(T) == typeof(BuildingCategory))
                return (T)(object)((BuildingCategory)reader.ReadInt32());
            if (typeof(T) == typeof(BuildingConstructionState))
                return (T)(object)(ReadBuildingConstructionState(reader));
            if (typeof(T) == typeof(BuildingExperienceState))
                return (T)(object)(ReadBuildingExperienceState(reader));
            if (typeof(T) == typeof(BuildingFarmingState))
                return (T)(object)(ReadBuildingFarmingState(reader));
            if (typeof(T) == typeof(BuildingGatheringState))
                return (T)(object)(ReadBuildingGatheringState(reader));
            if (typeof(T) == typeof(BuildingHousingState))
                return (T)(object)(ReadBuildingHousingState(reader));
            if (typeof(T) == typeof(BuildingInvestment))
                return (T)(object)(ReadBuildingInvestment(reader));
            if (typeof(T) == typeof(BuildingMaintenanceState))
                return (T)(object)(ReadBuildingMaintenanceState(reader));
            if (typeof(T) == typeof(BuildingMarketState))
                return (T)(object)(ReadBuildingMarketState(reader));
            if (typeof(T) == typeof(BuildingPlacementState))
                return (T)(object)(ReadBuildingPlacementState(reader));
            if (typeof(T) == typeof(BuildingProductionState))
                return (T)(object)(ReadBuildingProductionState(reader));
            if (typeof(T) == typeof(BuildingRecruitmentState))
                return (T)(object)(ReadBuildingRecruitmentState(reader));
            if (typeof(T) == typeof(BuildingSanctumState))
                return (T)(object)(ReadBuildingSanctumState(reader));
            if (typeof(T) == typeof(BuildingWorkforceState))
                return (T)(object)(ReadBuildingWorkforceState(reader));
            if (typeof(T) == typeof(ClaimedQuest))
                return (T)(object)(ReadClaimedQuest(reader));
            if (typeof(T) == typeof(CombatProfile))
                return (T)(object)(ReadCombatProfile(reader));
            if (typeof(T) == typeof(CombatStatsSnapshot))
                return (T)(object)(ReadCombatStatsSnapshot(reader));
            if (typeof(T) == typeof(CompletedExpedition))
                return (T)(object)(ReadCompletedExpedition(reader));
            if (typeof(T) == typeof(CourtLogEntry))
                return (T)(object)(ReadCourtLogEntry(reader));
            if (typeof(T) == typeof(CourtSettings))
                return (T)(object)(ReadCourtSettings(reader));
            if (typeof(T) == typeof(CourtState))
                return (T)(object)(ReadCourtState(reader));
            if (typeof(T) == typeof(CurrencySettings))
                return (T)(object)(ReadCurrencySettings(reader));
            if (typeof(T) == typeof(DaySettlementState))
                return (T)(object)(ReadDaySettlementState(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuffId))
                return (T)(object)(ReadLandsong_ECS_Definitions_BuffId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingEffectStacking))
                return (T)(object)((Landsong.ECS.Definitions.BuildingEffectStacking)reader.ReadInt32());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingEnvironmentKind))
                return (T)(object)((Landsong.ECS.Definitions.BuildingEnvironmentKind)reader.ReadInt32());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingId))
                return (T)(object)(ReadLandsong_ECS_Definitions_BuildingId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingInvitationKind))
                return (T)(object)((Landsong.ECS.Definitions.BuildingInvitationKind)reader.ReadInt32());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.BuildingLimitGroupId))
                return (T)(object)(ReadLandsong_ECS_Definitions_BuildingLimitGroupId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.CropId))
                return (T)(object)(ReadLandsong_ECS_Definitions_CropId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.EnemyBehaviorFlags))
                return (T)(object)((Landsong.ECS.Definitions.EnemyBehaviorFlags)reader.ReadByte());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.EnemyId))
                return (T)(object)(ReadLandsong_ECS_Definitions_EnemyId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.ExpeditionId))
                return (T)(object)(ReadLandsong_ECS_Definitions_ExpeditionId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.FeatureId))
                return (T)(object)(ReadLandsong_ECS_Definitions_FeatureId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.HeroId))
                return (T)(object)(ReadLandsong_ECS_Definitions_HeroId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.ItemGroupId))
                return (T)(object)(ReadLandsong_ECS_Definitions_ItemGroupId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.ItemId))
                return (T)(object)(ReadLandsong_ECS_Definitions_ItemId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.KingdomEffectKind))
                return (T)(object)((Landsong.ECS.Definitions.KingdomEffectKind)reader.ReadByte());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.NumericEffectKind))
                return (T)(object)((Landsong.ECS.Definitions.NumericEffectKind)reader.ReadByte());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.PolicyGroupId))
                return (T)(object)(ReadLandsong_ECS_Definitions_PolicyGroupId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.PolicyId))
                return (T)(object)(ReadLandsong_ECS_Definitions_PolicyId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.QuestBehaviorFlags))
                return (T)(object)((Landsong.ECS.Definitions.QuestBehaviorFlags)reader.ReadByte());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.QuestId))
                return (T)(object)(ReadLandsong_ECS_Definitions_QuestId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.QuestOfferType))
                return (T)(object)((Landsong.ECS.Definitions.QuestOfferType)reader.ReadByte());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.RoyalTraitId))
                return (T)(object)(ReadLandsong_ECS_Definitions_RoyalTraitId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.SoldierId))
                return (T)(object)(ReadLandsong_ECS_Definitions_SoldierId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.SpecialDropRarity))
                return (T)(object)((Landsong.ECS.Definitions.SpecialDropRarity)reader.ReadByte());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.StorageSlotId))
                return (T)(object)(ReadLandsong_ECS_Definitions_StorageSlotId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.TalentId))
                return (T)(object)(ReadLandsong_ECS_Definitions_TalentId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.TalentScalingKind))
                return (T)(object)((Landsong.ECS.Definitions.TalentScalingKind)reader.ReadByte());
            if (typeof(T) == typeof(Landsong.ECS.Definitions.TalentSlotId))
                return (T)(object)(ReadLandsong_ECS_Definitions_TalentSlotId(reader));
            if (typeof(T) == typeof(Landsong.ECS.Definitions.TechnologyId))
                return (T)(object)(ReadLandsong_ECS_Definitions_TechnologyId(reader));
            if (typeof(T) == typeof(DynastyIdentity))
                return (T)(object)(ReadDynastyIdentity(reader));
            if (typeof(T) == typeof(EconomyBillEntry))
                return (T)(object)(ReadEconomyBillEntry(reader));
            if (typeof(T) == typeof(EconomyEntry))
                return (T)(object)(ReadEconomyEntry(reader));
            if (typeof(T) == typeof(EconomyReason))
                return (T)(object)((EconomyReason)reader.ReadByte());
            if (typeof(T) == typeof(EventKind))
                return (T)(object)((EventKind)reader.ReadByte());
            if (typeof(T) == typeof(Expedition))
                return (T)(object)(ReadExpedition(reader));
            if (typeof(T) == typeof(ExpeditionDestinationHistory))
                return (T)(object)(ReadExpeditionDestinationHistory(reader));
            if (typeof(T) == typeof(ExpeditionPenaltyState))
                return (T)(object)(ReadExpeditionPenaltyState(reader));
            if (typeof(T) == typeof(ExpeditionSettings))
                return (T)(object)(ReadExpeditionSettings(reader));
            if (typeof(T) == typeof(ExpeditionStatus))
                return (T)(object)((ExpeditionStatus)reader.ReadByte());
            if (typeof(T) == typeof(ExpeditionSupply))
                return (T)(object)(ReadExpeditionSupply(reader));
            if (typeof(T) == typeof(FoodSelection))
                return (T)(object)(ReadFoodSelection(reader));
            if (typeof(T) == typeof(GameClock))
                return (T)(object)(ReadGameClock(reader));
            if (typeof(T) == typeof(Health))
                return (T)(object)(ReadHealth(reader));
            if (typeof(T) == typeof(Hero))
                return (T)(object)(ReadHero(reader));
            if (typeof(T) == typeof(HeroSelection))
                return (T)(object)(ReadHeroSelection(reader));
            if (typeof(T) == typeof(HistoryCategory))
                return (T)(object)((HistoryCategory)reader.ReadByte());
            if (typeof(T) == typeof(HistoryEntry))
                return (T)(object)(ReadHistoryEntry(reader));
            if (typeof(T) == typeof(Identity))
                return (T)(object)(ReadIdentity(reader));
            if (typeof(T) == typeof(IdentitySequence))
                return (T)(object)(ReadIdentitySequence(reader));
            if (typeof(T) == typeof(InitialBuilding))
                return (T)(object)(ReadInitialBuilding(reader));
            if (typeof(T) == typeof(InitialRoyal))
                return (T)(object)(ReadInitialRoyal(reader));
            if (typeof(T) == typeof(IntelligenceModeState))
                return (T)(object)(ReadIntelligenceModeState(reader));
            if (typeof(T) == typeof(IntelligenceSettings))
                return (T)(object)(ReadIntelligenceSettings(reader));
            if (typeof(T) == typeof(InventorySlot))
                return (T)(object)(ReadInventorySlot(reader));
            if (typeof(T) == typeof(ItemProtection))
                return (T)(object)((ItemProtection)reader.ReadByte());
            if (typeof(T) == typeof(LifeStage))
                return (T)(object)((LifeStage)reader.ReadByte());
            if (typeof(T) == typeof(NightEventHistory))
                return (T)(object)(ReadNightEventHistory(reader));
            if (typeof(T) == typeof(NightKind))
                return (T)(object)((NightKind)reader.ReadByte());
            if (typeof(T) == typeof(NightPlanState))
                return (T)(object)(ReadNightPlanState(reader));
            if (typeof(T) == typeof(NightRules))
                return (T)(object)(ReadNightRules(reader));
            if (typeof(T) == typeof(NightRuntimeState))
                return (T)(object)(ReadNightRuntimeState(reader));
            if (typeof(T) == typeof(NightSettings))
                return (T)(object)(ReadNightSettings(reader));
            if (typeof(T) == typeof(NightWave))
                return (T)(object)(ReadNightWave(reader));
            if (typeof(T) == typeof(OwnedBuff))
                return (T)(object)(ReadOwnedBuff(reader));
            if (typeof(T) == typeof(PeacefulRules))
                return (T)(object)(ReadPeacefulRules(reader));
            if (typeof(T) == typeof(PendingItem))
                return (T)(object)(ReadPendingItem(reader));
            if (typeof(T) == typeof(PersistenceGate))
                return (T)(object)(ReadPersistenceGate(reader));
            if (typeof(T) == typeof(PersonGender))
                return (T)(object)((PersonGender)reader.ReadByte());
            if (typeof(T) == typeof(PersonRequestEntry))
                return (T)(object)(ReadPersonRequestEntry(reader));
            if (typeof(T) == typeof(PersonRequestKind))
                return (T)(object)((PersonRequestKind)reader.ReadByte());
            if (typeof(T) == typeof(PersonRequestStatus))
                return (T)(object)((PersonRequestStatus)reader.ReadByte());
            if (typeof(T) == typeof(Phase))
                return (T)(object)((Phase)reader.ReadByte());
            if (typeof(T) == typeof(PolicyChoice))
                return (T)(object)(ReadPolicyChoice(reader));
            if (typeof(T) == typeof(PopulationState))
                return (T)(object)(ReadPopulationState(reader));
            if (typeof(T) == typeof(PortraitDNA))
                return (T)(object)(ReadPortraitDNA(reader));
            if (typeof(T) == typeof(PortraitSettings))
                return (T)(object)(ReadPortraitSettings(reader));
            if (typeof(T) == typeof(PreparedBuildingDefense))
                return (T)(object)(ReadPreparedBuildingDefense(reader));
            if (typeof(T) == typeof(PreparedHero))
                return (T)(object)(ReadPreparedHero(reader));
            if (typeof(T) == typeof(PreparedSoldier))
                return (T)(object)(ReadPreparedSoldier(reader));
            if (typeof(T) == typeof(ProjectileMode))
                return (T)(object)((ProjectileMode)reader.ReadByte());
            if (typeof(T) == typeof(PublicOpinionState))
                return (T)(object)(ReadPublicOpinionState(reader));
            if (typeof(T) == typeof(Quest))
                return (T)(object)(ReadQuest(reader));
            if (typeof(T) == typeof(QuestGenerationSettings))
                return (T)(object)(ReadQuestGenerationSettings(reader));
            if (typeof(T) == typeof(QuestOfferSlot))
                return (T)(object)(ReadQuestOfferSlot(reader));
            if (typeof(T) == typeof(QuestProgress))
                return (T)(object)(ReadQuestProgress(reader));
            if (typeof(T) == typeof(QuestStatus))
                return (T)(object)((QuestStatus)reader.ReadByte());
            if (typeof(T) == typeof(QuestTracking))
                return (T)(object)(ReadQuestTracking(reader));
            if (typeof(T) == typeof(RepairMaterial))
                return (T)(object)(ReadRepairMaterial(reader));
            if (typeof(T) == typeof(ResearchState))
                return (T)(object)(ReadResearchState(reader));
            if (typeof(T) == typeof(RetryState))
                return (T)(object)(ReadRetryState(reader));
            if (typeof(T) == typeof(Royal))
                return (T)(object)(ReadRoyal(reader));
            if (typeof(T) == typeof(RoyalFamilySettings))
                return (T)(object)(ReadRoyalFamilySettings(reader));
            if (typeof(T) == typeof(Session))
                return (T)(object)(ReadSession(reader));
            if (typeof(T) == typeof(SimulationControl))
                return (T)(object)(ReadSimulationControl(reader));
            if (typeof(T) == typeof(SimulationRandomState))
                return (T)(object)(ReadSimulationRandomState(reader));
            if (typeof(T) == typeof(Soldier))
                return (T)(object)(ReadSoldier(reader));
            if (typeof(T) == typeof(SoldierPerson))
                return (T)(object)(ReadSoldierPerson(reader));
            if (typeof(T) == typeof(SpawnRegion))
                return (T)(object)(ReadSpawnRegion(reader));
            if (typeof(T) == typeof(TacticalTraits))
                return (T)(object)((TacticalTraits)reader.ReadByte());
            if (typeof(T) == typeof(Talent))
                return (T)(object)(ReadTalent(reader));
            if (typeof(T) == typeof(TalentSettings))
                return (T)(object)(ReadTalentSettings(reader));
            if (typeof(T) == typeof(TechnologyProgress))
                return (T)(object)(ReadTechnologyProgress(reader));
            if (typeof(T) == typeof(TerrainType))
                return (T)(object)((TerrainType)reader.ReadInt32());
            if (typeof(T) == typeof(TraitEntry))
                return (T)(object)(ReadTraitEntry(reader));
            if (typeof(T) == typeof(UnlockedFeature))
                return (T)(object)(ReadUnlockedFeature(reader));
            if (typeof(T) == typeof(UnresolvedBoss))
                return (T)(object)(ReadUnresolvedBoss(reader));
            if (typeof(T) == typeof(VisitorKind))
                return (T)(object)((VisitorKind)reader.ReadByte());
            if (typeof(T) == typeof(FixedList128Bytes<Landsong.ECS.Definitions.RoyalTraitId>))
                return (T)(object)(ReadFixedList128Bytes_Landsong_ECS_Definitions_RoyalTraitId_(reader));
            if (typeof(T) == typeof(FixedList128Bytes<int>))
                return (T)(object)(ReadFixedList128Bytes_int_(reader));
            if (typeof(T) == typeof(FixedList512Bytes<float>))
                return (T)(object)(ReadFixedList512Bytes_float_(reader));
            if (typeof(T) == typeof(FixedList64Bytes<int>))
                return (T)(object)(ReadFixedList64Bytes_int_(reader));
            if (typeof(T) == typeof(FixedString128Bytes))
                return (T)(object)(new FixedString128Bytes(reader.ReadString()));
            if (typeof(T) == typeof(FixedString64Bytes))
                return (T)(object)(new FixedString64Bytes(reader.ReadString()));
            if (typeof(T) == typeof(Entity))
                return (T)(object)(ReadEntity(reader));
            if (typeof(T) == typeof(float3))
                return (T)(object)(Readfloat3(reader));
            if (typeof(T) == typeof(float4))
                return (T)(object)(Readfloat4(reader));
            if (typeof(T) == typeof(int2))
                return (T)(object)(Readint2(reader));
            if (typeof(T) == typeof(int4))
                return (T)(object)(Readint4(reader));
            if (typeof(T) == typeof(quaternion))
                return (T)(object)(Readquaternion(reader));
            if (typeof(T) == typeof(LocalTransform))
                return (T)(object)(ReadLocalTransform(reader));
            if (typeof(T) == typeof(Color32))
                return (T)(object)(ReadColor32(reader));
            if (typeof(T) == typeof(bool))
                return (T)(object)(reader.ReadBoolean());
            if (typeof(T) == typeof(byte))
                return (T)(object)(reader.ReadByte());
            if (typeof(T) == typeof(float))
                return (T)(object)(reader.ReadSingle());
            if (typeof(T) == typeof(int))
                return (T)(object)(reader.ReadInt32());
            if (typeof(T) == typeof(long))
                return (T)(object)(reader.ReadInt64());
            if (typeof(T) == typeof(uint))
                return (T)(object)(reader.ReadUInt32());
            if (typeof(T) == typeof(ulong))
                return (T)(object)(reader.ReadUInt64());
            throw new InvalidDataException("No snapshot reader for " + typeof(T));
        }

        public static void ValidateFieldCoverage()
        {
            Fields<BattleHistoryEntry>(new string[] { "Turn", "Entry" });
            Fields<BattleReportEntry>(new string[] { "Kind", "Id", "Building", "Item", "Amount", "Value", "SourceName" });
            Fields<BellState>(new string[] { "ActiveBell" });
            Fields<BlueprintUnlock>(new string[] { "Building", "MaximumLevel" });
            Fields<Building>(new string[] { "Stage", "Level", "RuinPending" });
            Fields<BuildingAppearanceState>(new string[] { "Skin" });
            Fields<BuildingConstructionState>(new string[] { "Progress", "RepairDuration", "RepairCompletedTurn" });
            Fields<BuildingExperienceState>(new string[] { "Experience" });
            Fields<BuildingFarmingState>(new string[] { "Crop", "Progress", "Seed", "FullCycle", "AutoHarvest" });
            Fields<BuildingGatheringState>(new string[] { "RemainingUses" });
            Fields<BuildingHousingState>(new string[] { "Population", "Growth", "FoodFailures", "TaxProgress", "DeferredResidents" });
            Fields<BuildingInvestment>(new string[] { "Item", "Amount" });
            Fields<BuildingMaintenanceState>(new string[] { "Maintained" });
            Fields<BuildingMarketState>(new string[] { "TurnValue", "LifetimeValue" });
            Fields<BuildingPlacementState>(new string[] { "Cell", "Size", "Rotation", "Elevation", "Surface" });
            Fields<BuildingProductionState>(new string[] { "Progress" });
            Fields<BuildingRecruitmentState>(new string[] { "Turn", "Count" });
            Fields<BuildingSanctumState>(new string[] { "Offering", "PaidOfferingTurn", "WokenTurn" });
            Fields<BuildingWorkforceState>(new string[] { "Workers", "StableWorkers", "WorkerTarget", "ProtectionUntil", "PaidSubsidy", "PaidSubsidyTurn", "SubsidyBudget", "Subsidy" });
            Fields<ClaimedQuest>(new string[] { "Quest" });
            Fields<CombatProfile>(new string[] { "DetectionRadius", "ChaseRadius", "ChaseSeconds", "BodyRadius", "Armor", "Reduction", "Penetration", "BlastRadius", "WarningSeconds", "ProjectileLifetime", "ProjectileMode", "Traits", "BlocksProjectile" });
            Fields<CombatStatsSnapshot>(new string[] { "Health", "Damage", "Speed", "Range", "Interval", "ProjectileSpeed", "Combat" });
            Fields<CompletedExpedition>(new string[] { "Expedition" });
            Fields<CourtLogEntry>(new string[] { "Turn", "Person", "Message" });
            Fields<CourtSettings>(new string[] { "MarriageAge", "CaptainAge", "GiftCost", "GiftAffection", "RecruitAffection", "MarriageAffection", "StableDesignationTurns", "MinimumReign", "TemporaryTurns", "ElectionTurns", "DisorderTurns", "VisitInterval", "VisitDuration", "VisitCost", "MarriageRequestCooldown", "ExpeditionRequestCooldown", "ExpeditionRequestChance", "MarriageRequestChance", "MarriageRefusalGrievanceChance", "MarriageRefusalGrievance", "InitialOpinion", "OpinionRecovery", "DisorderOpinionCost", "PrinceGrowth", "StrongInfluence", "UsurpGap", "UsurpChance", "RegicideGap", "RegicideChance", "PrinceRisk", "StableProduction", "StableAttack", "WeakProduction", "WeakAttack", "ElectionProduction", "ElectionAttack", "UsurpProduction", "UsurpAttack", "RegicideProduction", "RegicideAttack", "DisorderPerStack", "DisorderCap", "DeathYoung", "DeathAdult", "DeathMature", "DeathOld", "DeathAncient" });
            Fields<CourtState>(new string[] { "Crown", "LegacyFounder", "CrownSince", "LastSettledTurn", "TemporaryUntil", "DisorderUntil", "LegacyGeneration", "VisitOfferTurn", "TemporaryProduction", "TemporaryAttack", "LegacyProduction", "LegacyAttack", "Disorder", "Extinction", "LegacySeverity", "VisitResolved" });
            Fields<CurrencySettings>(new string[] { "Gold" });
            Fields<DaySettlementState>(new string[] { "LastSettledTurn" });
            Fields<DynastyIdentity>(new string[] { "Name" });
            Fields<EconomyBillEntry>(new string[] { "Turn", "Item", "Source", "Income", "Expense", "Stored", "Pending" });
            Fields<EconomyEntry>(new string[] { "Turn", "Delta", "Item", "Source", "Reason", "Pending", "SourceName", "Note" });
            Fields<Expedition>(new string[] { "Site", "Captain", "SourceName", "Crew", "Departure", "Arrival", "SourceLevel", "Casualties", "SubsidyRequired", "SubsidyPaid", "PenaltyStacks", "RewardBonus", "SuccessChance", "Status" });
            Fields<ExpeditionDestinationHistory>(new string[] { "Definition" });
            Fields<ExpeditionPenaltyState>(new string[] { "Stacks", "UntilTurn" });
            Fields<ExpeditionSettings>(new string[] { "PenaltyTurns", "AttractionPerStack" });
            Fields<ExpeditionSupply>(new string[] { "Item", "Amount" });
            Fields<FoodSelection>(new string[] { "Group", "Item", "Amount" });
            Fields<GameClock>(new string[] { "Turn", "Time", "PhaseTime", "DawnRemaining", "DawnSourceNightTime" });
            Fields<Health>(new string[] { "Current", "Maximum" });
            Fields<Hero>(new string[] { "Sanctum", "CooldownUntil", "Experience", "LastCombatTurn", "Recruited", "DeathPending" });
            Fields<HeroSelection>(new string[] { "SelectedHero" });
            Fields<HistoryEntry>(new string[] { "Turn", "Delta", "Count", "Item", "Source", "Pending", "HasPosition", "Transfer", "Category", "Position", "SourceName", "Text" });
            Fields<Identity>(new string[] { "Id", "Name" });
            Fields<IdentitySequence>(new string[] { "NextId" });
            Fields<InitialBuilding>(new string[] { "Definition", "Level", "Rotation", "Cell", "Name" });
            Fields<InitialRoyal>(new string[] { "Name", "Age", "Role", "Gender", "Traits" });
            Fields<IntelligenceModeState>(new string[] { "Enabled" });
            Fields<IntelligenceSettings>(new string[] { "LowIntel", "MediumIntel", "HighIntel", "MediumIntelLead", "HighIntelLead" });
            Fields<InventorySlot>(new string[] { "Provider", "Index", "Count", "SlotType", "Item", "LossRemainder", "Unavailable" });
            Fields<NightEventHistory>(new string[] { "Event", "LastTurn", "Count" });
            Fields<NightPlanState>(new string[] { "Event", "Turn", "BaseThreat", "PreparedTurn", "BossDefinition", "CombatElapsed", "FirstActionAt", "ClockStarted", "Committed", "AnySpawned", "BossKilled", "BossEscaped" });
            Fields<NightRules>(new string[] { "WarningSeconds", "ProtectionSeconds", "MinSpawnRegions", "MaxSpawnRegions", "SpawnRegionSize", "SpawnRegionGap", "HeroWeight", "FacilityWeight", "TargetRadius", "ThreatFloor", "ThreatPerStrengthCap" });
            Fields<NightRuntimeState>(new string[] { "Kind", "Seed", "Duration", "Speed", "Threat", "StartCombatStrength", "DeploymentTime", "BossEscaped", "BossReturnTurn", "Intelligence" });
            Fields<NightSettings>(new string[] { "NightSeconds", "DeployInterval", "NightPreparationSeconds", "NightClosureSeconds", "RetreatDelaySeconds", "VictoryCaptionDelaySeconds", "CelebrationDelaySeconds", "VictoryAdvanceDelaySeconds", "WaveIntervalSeconds", "DawnSeconds", "InvasionChance", "StrengthRatio", "RetryStep", "RetryCap", "FirstInvasion", "FirstBoss", "BossInterval", "ThreatPerTurn" });
            Fields<NightWave>(new string[] { "At", "PowerScale", "WarnedAt", "Definition", "Count", "Direction", "Region", "Position", "Target", "Spawned", "Warned", "SpatiallyBlocked" });
            Fields<OwnedBuff>(new string[] { "Buff", "Level" });
            Fields<PeacefulRules>(new string[] { "MaximumPerNight", "MaximumConcurrent", "TheftValueBudget", "FirstOpportunity", "Interval" });
            Fields<PendingItem>(new string[] { "Item", "Amount", "LossRemainder" });
            Fields<PersistenceGate>(new string[] { "CheckpointPending" });
            Fields<PersonRequestEntry>(new string[] { "Kind", "Status", "CreatedTurn", "ResolvedTurn", "Journey" });
            Fields<PolicyChoice>(new string[] { "Definition" });
            Fields<PopulationState>(new string[] { "BasePopulation" });
            Fields<PortraitDNA>(new string[] { "Parts", "SkinDetails", "Skin", "Hair", "Eyes", "Seed", "Customized", "InvitationAnnounced" });
            Fields<PortraitSettings>(new string[] { "YouthAge", "GreyAge", "ElderAge", "SoldierRecruitMinAge", "SoldierRecruitMaxAge", "SoldierLifeMin", "SoldierLifeMax", "ColorMutation" });
            Fields<PreparedBuildingDefense>(new string[] { "Definition", "Profile" });
            Fields<PreparedHero>(new string[] { "Definition", "Stats" });
            Fields<PreparedSoldier>(new string[] { "Definition", "Stats" });
            Fields<PublicOpinionState>(new string[] { "Value" });
            Fields<Quest>(new string[] { "Status", "StartTurn", "Deadline", "Source", "Container", "Slot", "ContainerSlot", "Mainline" });
            Fields<QuestGenerationSettings>(new string[] { "StrengthStep", "MarketValuePerStrength", "Low", "Medium", "High", "Maximum" });
            Fields<QuestOfferSlot>(new string[] { "Type", "Index", "NextTurn" });
            Fields<QuestProgress>(new string[] { "Amount", "Key" });
            Fields<QuestTracking>(new string[] { "Target", "Mode" });
            Fields<RepairMaterial>(new string[] { "Item", "Amount" });
            Fields<ResearchState>(new string[] { "Points" });
            Fields<RetryState>(new string[] { "Count" });
            Fields<Royal>(new string[] { "Role", "Alive", "Retired", "FateUsed", "Evidence", "TaskClaimed", "EverMonarch", "Gender", "MarriageRequestTurn", "MarriageCooldownUntil", "RequestedSpouse", "MarriageRequestMonarch", "Age", "Generation", "ReignSince", "FateUntil", "LastGiftTurn", "Affection", "VisitUntil", "Parent", "SecondParent", "Spouse", "Influence", "Growth", "Ambition", "Grievance", "InfluenceSource" });
            Fields<RoyalFamilySettings>(new string[] { "MaxChildren", "BirthChance", "MutationChance" });
            Fields<Session>(new string[] { "Phase", "Initialized" });
            Fields<SimulationControl>(new string[] { "Paused" });
            Fields<SimulationRandomState>(new string[] { "State" });
            Fields<Soldier>(new string[] { "Garrison", "Slot", "PopulationCost", "PendingSince", "Experience", "LastExperienceTurn", "RecallState", "Weapon" });
            Fields<SoldierPerson>(new string[] { "Age", "Lifespan", "LastAgeTurn", "Incarnation", "Gender", "SpecialAttention", "DeathNotified" });
            Fields<SpawnRegion>(new string[] { "Direction", "Center", "Size", "EdgeOnly" });
            Fields<Talent>(new string[] { "Slot", "Experience", "Level", "AssignedTurns", "WageTurn", "LastBenefitTurn", "Recruited", "Paid" });
            Fields<TalentSettings>(new string[] { "TalentCapacity", "TalentRecruitCost", "TalentExperience" });
            Fields<TechnologyProgress>(new string[] { "Technology", "ResearchPoints", "Completions", "QueueOrder" });
            Fields<TraitEntry>(new string[] { "Definition", "Revealed", "Active" });
            Fields<UnlockedFeature>(new string[] { "Feature" });
            Fields<UnresolvedBoss>(new string[] { "Event", "Definition", "DueTurn" });
        }

        static void Fields<T>(string[] expected)
        {
            var actual = typeof(T).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public).Select(field => field.Name).OrderBy(name => name).ToArray();
            if (!actual.SequenceEqual(expected.OrderBy(name => name)))
                throw new InvalidOperationException("Snapshot schema needs a versioned update: " + typeof(T));
        }
    }
}
