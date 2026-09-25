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
    internal static class SnapshotRootStorage
    {
        internal static void Capture(BinaryWriter writer, EntityManager em, Entity root)
        {
            SnapshotBinary.Write(writer, em.GetComponentData<Session>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<GameClock>(root));
            WriteWeather(writer, em.GetComponentData<SeasonWeatherState>(root));
            SnapshotBinary.Write(writer, default(SimulationControl));
            SnapshotBinary.Write(writer, em.GetComponentData<PopulationState>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<PublicOpinionState>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<ResearchState>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<ExpeditionPenaltyState>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<NightRuntimeState>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<DaySettlementState>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<RetryState>(root));
            SnapshotBinary.Write(writer, default(HeroSelection));
            SnapshotBinary.Write(writer, default(BellState));
            SnapshotBinary.Write(writer, default(IntelligenceModeState));
            SnapshotBinary.Write(writer, default(PersistenceGate));
            SnapshotBinary.Write(writer, em.GetComponentData<SimulationRandomState>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<IdentitySequence>(root));
            SnapshotBinary.Write(writer, em.GetComponentData<DynastyIdentity>(root));
            SnapshotBinary.Write(writer, QuestOps.Tracking(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<TrackedQuest>(em, root));
            SnapshotBinary.Write(writer, CourtOps.State(em, root));
            SnapshotBinary.Write(writer, NightPlanOps.State(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<CourtLogEntry>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<SpawnRegion>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<NightEventHistory>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<UnresolvedBoss>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<PreparedSoldier>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<PreparedHero>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<InventorySlot>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<PendingItem>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<BlueprintUnlock>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<OwnedBuff>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<UnlockedFeature>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<ClaimedQuest>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<QuestRefreshCooldown>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<CompletedExpedition>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<TechnologyProgress>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<PolicyChoice>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<NightWave>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<BattleReportEntry>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<BattleHistoryEntry>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<HistoryEntry>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<EconomyEntry>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<EconomyBillEntry>(em, root));
            SnapshotBuffers.Write(writer, SnapshotBuffers.Capture<PreparedBuildingDefense>(em, root));
            writer.Write(em.HasComponent<EconomyJournalState>(root) ? em.GetComponentData<EconomyJournalState>(root).Turn : 0);
        }

        internal static SnapshotCodec.Snapshot Read(BinaryReader reader, int version)
        {
            var data = new SnapshotCodec.Snapshot
            {
                Session = SnapshotBinary.Read<Session>(reader),
                Clock = SnapshotBinary.Read<GameClock>(reader),
                Weather = version >= 35 ? ReadWeather(reader) : default,
                Control = SnapshotBinary.Read<SimulationControl>(reader),
                Population = SnapshotBinary.Read<PopulationState>(reader),
                Opinion = SnapshotBinary.Read<PublicOpinionState>(reader),
                ResearchState = SnapshotBinary.Read<ResearchState>(reader),
                ExpeditionPenalty = SnapshotBinary.Read<ExpeditionPenaltyState>(reader),
                Night = SnapshotBinary.Read<NightRuntimeState>(reader),
                Settlement = SnapshotBinary.Read<DaySettlementState>(reader),
                Retry = SnapshotBinary.Read<RetryState>(reader),
                HeroSelection = SnapshotBinary.Read<HeroSelection>(reader),
                Bell = SnapshotBinary.Read<BellState>(reader),
                IntelligenceMode = SnapshotBinary.Read<IntelligenceModeState>(reader),
                Persistence = SnapshotBinary.Read<PersistenceGate>(reader),
                Random = SnapshotBinary.Read<SimulationRandomState>(reader),
                Ids = SnapshotBinary.Read<IdentitySequence>(reader),
                Dynasty = SnapshotBinary.Read<DynastyIdentity>(reader),
                Tracking = SnapshotBinary.Read<QuestTracking>(reader),
                TrackedQuests = version >= 34 ? SnapshotBuffers.Read<TrackedQuest>(reader) : Array.Empty<TrackedQuest>(),
                Court = SnapshotBinary.Read<CourtState>(reader),
                NightPlan = SnapshotBinary.Read<NightPlanState>(reader),
                CourtLog = SnapshotBuffers.Read<CourtLogEntry>(reader),
                SpawnRegions = SnapshotBuffers.Read<SpawnRegion>(reader),
                NightHistory = SnapshotBuffers.Read<NightEventHistory>(reader),
                Bosses = SnapshotBuffers.Read<UnresolvedBoss>(reader),
                PreparedSoldiers = SnapshotBuffers.Read<PreparedSoldier>(reader),
                PreparedHeroes = SnapshotBuffers.Read<PreparedHero>(reader),
                Inventory = SnapshotBuffers.Read<InventorySlot>(reader),
                Pending = SnapshotBuffers.Read<PendingItem>(reader),
                Blueprints = SnapshotBuffers.Read<BlueprintUnlock>(reader),
                Buffs = SnapshotBuffers.Read<OwnedBuff>(reader),
                Features = SnapshotBuffers.Read<UnlockedFeature>(reader),
                ClaimedQuests = SnapshotBuffers.Read<ClaimedQuest>(reader),
                QuestRefreshCooldowns = version >= 38 ? SnapshotBuffers.Read<QuestRefreshCooldown>(reader) : Array.Empty<QuestRefreshCooldown>(),
                CompletedExpeditions = SnapshotBuffers.Read<CompletedExpedition>(reader),
                Research = SnapshotBuffers.Read<TechnologyProgress>(reader),
                Policies = SnapshotBuffers.Read<PolicyChoice>(reader),
                Waves = SnapshotBuffers.Read<NightWave>(reader),
                Report = SnapshotBuffers.Read<BattleReportEntry>(reader),
                BattleHistory = SnapshotBuffers.Read<BattleHistoryEntry>(reader),
                History = SnapshotBuffers.Read<HistoryEntry>(reader),
                Economy = SnapshotBuffers.Read<EconomyEntry>(reader),
                Bills = SnapshotBuffers.Read<EconomyBillEntry>(reader),
                PreparedBuildings = SnapshotBuffers.Read<PreparedBuildingDefense>(reader),
                LedgerTurn = reader.ReadInt32(),
            };
            if (version == 33 && data.Tracking.Mode == 1 && data.Tracking.Target != 0)
                data.TrackedQuests = new[] { new TrackedQuest { Quest = data.Tracking.Target } };
            return data;
        }

        internal static void Restore(EntityManager em, Entity root, SnapshotCodec.Snapshot data)
        {
            EntityState.Set(em, root, data.Session);
            EntityState.Set(em, root, data.Clock);
            EntityState.Set(em, root, data.Weather);
            EntityState.Set(em, root, data.Control);
            EntityState.Set(em, root, data.Population);
            EntityState.Set(em, root, data.Opinion);
            EntityState.Set(em, root, data.ResearchState);
            EntityState.Set(em, root, data.ExpeditionPenalty);
            EntityState.Set(em, root, data.Night);
            EntityState.Set(em, root, data.Settlement);
            EntityState.Set(em, root, data.Retry);
            EntityState.Set(em, root, data.HeroSelection);
            EntityState.Set(em, root, data.Bell);
            EntityState.Set(em, root, data.IntelligenceMode);
            EntityState.Set(em, root, data.Persistence);
            EntityState.Set(em, root, data.Random);
            EntityState.Set(em, root, data.Ids);
            EntityState.Set(em, root, data.Dynasty);
            EntityState.Set(em, root, data.Tracking);
            SnapshotBuffers.Restore(em, root, data.TrackedQuests);
            EntityState.Set(em, root, data.Court);
            EntityState.Set(em, root, data.NightPlan);
            SnapshotBuffers.Restore(em, root, data.CourtLog);
            SnapshotBuffers.Restore(em, root, data.SpawnRegions);
            SnapshotBuffers.Restore(em, root, data.NightHistory);
            SnapshotBuffers.Restore(em, root, data.Bosses);
            SnapshotBuffers.Restore(em, root, data.PreparedSoldiers);
            SnapshotBuffers.Restore(em, root, data.PreparedHeroes);
            SnapshotBuffers.Restore(em, root, data.Inventory);
            SnapshotBuffers.Restore(em, root, data.Pending);
            SnapshotBuffers.Restore(em, root, data.Blueprints);
            SnapshotBuffers.Restore(em, root, data.Buffs);
            SnapshotBuffers.Restore(em, root, data.Features);
            SnapshotBuffers.Restore(em, root, data.ClaimedQuests);
            SnapshotBuffers.Restore(em, root, data.QuestRefreshCooldowns);
            SnapshotBuffers.Restore(em, root, data.CompletedExpeditions);
            SnapshotBuffers.Restore(em, root, data.Research);
            SnapshotBuffers.Restore(em, root, data.Policies);
            SnapshotBuffers.Restore(em, root, data.Waves);
            SnapshotBuffers.Restore(em, root, data.Report);
            SnapshotBuffers.Restore(em, root, data.BattleHistory);
            SnapshotBuffers.Restore(em, root, data.History);
            SnapshotBuffers.Restore(em, root, data.Economy);
            SnapshotBuffers.Restore(em, root, data.Bills);
            SnapshotBuffers.Restore(em, root, data.PreparedBuildings);
            EntityState.Set(em, root, new EconomyJournalState { Turn = data.LedgerTurn });
        }

        static void WriteWeather(BinaryWriter writer, SeasonWeatherState value)
        {
            writer.Write(value.DayTurn);
            writer.Write(value.Temperature);
            writer.Write((byte)value.Season);
            writer.Write((byte)value.Weather);
            writer.Write((byte)value.Wind);
            writer.Write(value.WindDegrees);
            writer.Write(value.RandomState);
            writer.Write(value.Initialized);
            writer.Write(value.LightningLimit);
            writer.Write(value.LightningCount);
            writer.Write(value.DayElapsed);
            writer.Write(value.NextThunderAt);
        }

        static SeasonWeatherState ReadWeather(BinaryReader reader) => new SeasonWeatherState
        {
            DayTurn = reader.ReadInt32(),
            Temperature = reader.ReadInt32(),
            Season = (SeasonKind)reader.ReadByte(),
            Weather = (WeatherKind)reader.ReadByte(),
            Wind = (WindKind)reader.ReadByte(),
            WindDegrees = reader.ReadSingle(),
            RandomState = reader.ReadUInt32(),
            Initialized = reader.ReadByte(),
            LightningLimit = reader.ReadByte(),
            LightningCount = reader.ReadByte(),
            DayElapsed = reader.ReadSingle(),
            NextThunderAt = reader.ReadSingle(),
        };
    }
}
