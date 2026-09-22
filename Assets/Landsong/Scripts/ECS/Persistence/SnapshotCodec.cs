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
    // Coordinates the archive boundary; domain storage owns each record's schema.
    public static class SnapshotCodec
    {
        public const int CurrentVersion = 33;
        static void Header(BinaryReader reader)
        {
            if (reader.ReadString() != "LANDSONG-ECS")
                throw new InvalidDataException("不是 ECS 存档。");
            if (reader.ReadInt32() != CurrentVersion)
                throw new InvalidDataException("存档版本已过期或不受支持，请开始新王朝。");
        }

        public static string ReadMapId(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes, false));
            Header(reader);
            return reader.ReadString();
        }

        public static (string Map, Phase Phase, int Turn, FixedString128Bytes DynastyName) ReadSummary(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes, false));
            Header(reader);
            var map = reader.ReadString();
            reader.ReadString();
            var data = SnapshotRootStorage.Read(reader);
            if (data.Clock.Turn < 1 || (byte)data.Session.Phase > (byte)Phase.Ended)
                throw new InvalidDataException("节点摘要无效。");
            return (map, data.Session.Phase, data.Clock.Turn, data.Dynasty.Name);
        }

        public sealed class Snapshot
        {
            public Session Session;
            public GameClock Clock;
            public SimulationControl Control;
            public PopulationState Population;
            public PublicOpinionState Opinion;
            public ResearchState ResearchState;
            public ExpeditionPenaltyState ExpeditionPenalty;
            public NightRuntimeState Night;
            public DaySettlementState Settlement;
            public RetryState Retry;
            public HeroSelection HeroSelection;
            public BellState Bell;
            public IntelligenceModeState IntelligenceMode;
            public PersistenceGate Persistence;
            public SimulationRandomState Random;
            public IdentitySequence Ids;
            public DynastyIdentity Dynasty;
            public QuestTracking Tracking;
            public CourtState Court;
            public NightPlanState NightPlan;
            public CourtLogEntry[] CourtLog = Array.Empty<CourtLogEntry>();
            public SpawnRegion[] SpawnRegions = Array.Empty<SpawnRegion>();
            public NightEventHistory[] NightHistory = Array.Empty<NightEventHistory>();
            public UnresolvedBoss[] Bosses = Array.Empty<UnresolvedBoss>();
            public PreparedSoldier[] PreparedSoldiers = Array.Empty<PreparedSoldier>();
            public PreparedHero[] PreparedHeroes = Array.Empty<PreparedHero>();
            public InventorySlot[] Inventory = Array.Empty<InventorySlot>();
            public PendingItem[] Pending = Array.Empty<PendingItem>();
            public BlueprintUnlock[] Blueprints = Array.Empty<BlueprintUnlock>();
            public OwnedBuff[] Buffs = Array.Empty<OwnedBuff>();
            public UnlockedFeature[] Features = Array.Empty<UnlockedFeature>();
            public ClaimedQuest[] ClaimedQuests = Array.Empty<ClaimedQuest>();
            public CompletedExpedition[] CompletedExpeditions = Array.Empty<CompletedExpedition>();
            public TechnologyProgress[] Research = Array.Empty<TechnologyProgress>();
            public PolicyChoice[] Policies = Array.Empty<PolicyChoice>();
            public NightWave[] Waves = Array.Empty<NightWave>();
            public BattleReportEntry[] Report = Array.Empty<BattleReportEntry>();
            public BattleHistoryEntry[] BattleHistory = Array.Empty<BattleHistoryEntry>();
            public HistoryEntry[] History = Array.Empty<HistoryEntry>();
            public EconomyEntry[] Economy = Array.Empty<EconomyEntry>();
            public EconomyBillEntry[] Bills = Array.Empty<EconomyBillEntry>();
            public PreparedBuildingDefense[] PreparedBuildings = Array.Empty<PreparedBuildingDefense>();
            public int LedgerTurn;
            public EntitySnapshot[] Records = Array.Empty<EntitySnapshot>();
        }

        public static byte[] Capture(EntityManager em, Entity root, bool includeTransportWorkers = true)
        {
            em.CompleteAllTrackedJobs();
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write("LANDSONG-ECS");
            writer.Write(CurrentVersion);
            writer.Write(em.GetComponentData<MapIdentity>(root).Id.ToString());
            writer.Write(ContentFingerprint(em, root));
            SnapshotRootStorage.Capture(writer, em, root);
            using var entities = WorldQueries.OrderedEntities<Persistent>(em);
            int count = 0;
            foreach (var entity in entities)
                if (includeTransportWorkers || !em.HasComponent<TransportWorker>(entity)) count++;
            writer.Write(count);
            foreach (var entity in entities)
                if (includeTransportWorkers || !em.HasComponent<TransportWorker>(entity)) EntitySnapshotStorage.Capture(writer, em, entity);
            return stream.ToArray();
        }

        public static Snapshot Decode(EntityManager em, Entity root, byte[] bytes) => DecodeCore(em, root, bytes, false);
        static Snapshot DecodeCore(EntityManager em, Entity root, byte[] bytes, bool deferQuestContainerReconciliation)
        {
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            Header(reader);
            if (reader.ReadString() != em.GetComponentData<MapIdentity>(root).Id.ToString())
                throw new InvalidDataException("存档属于另一张地图，请返回主菜单选择对应地图。");
            if (reader.ReadString() != ContentFingerprint(em, root))
                throw new InvalidDataException("内容规则已改变，请开始新王朝；原存档未修改。");
            var snapshot = SnapshotRootStorage.Read(reader);
            snapshot.Records = new EntitySnapshot[SnapshotBuffers.Count(reader)];
            for (int i = 0; i < snapshot.Records.Length; i++)
                snapshot.Records[i] = EntitySnapshotStorage.Read(reader);
            if (stream.Position != stream.Length)
                throw new InvalidDataException("Unexpected snapshot trailing data");
            SnapshotValidation.Validate(em, root, snapshot, deferQuestContainerReconciliation);
            return snapshot;
        }

        public static void Restore(EntityManager em, Entity root, Snapshot snapshot, Action<EntityManager, Entity> prepare = null, Action<string> probe = null)
        {
            SnapshotValidation.Validate(em, root, snapshot);
            using var transaction = new RestoreTransaction(em, root);
            Rebuild(em, transaction.Root, snapshot, probe);
            prepare?.Invoke(em, transaction.Root);
            probe?.Invoke("prepared");
            transaction.Commit(probe);
        }

        internal static void Rebuild(EntityManager em, Entity root, Snapshot snapshot, Action<string> probe = null) => RebuildCore(em, root, snapshot, false, probe);
        internal static void RebuildNightEntryCandidate(EntityManager em, Entity root, byte[] liveDay, Action<string> probe = null) => RebuildCore(em, root, DecodeCore(em, root, liveDay, true), true, probe);
        internal static void ValidateReconciledNightEntry(EntityManager em, Entity root) => Decode(em, root, Capture(em, root));
        static void RebuildCore(EntityManager em, Entity root, Snapshot snapshot, bool deferQuestContainerReconciliation, Action<string> probe)
        {
            SnapshotValidation.Validate(em, root, snapshot, deferQuestContainerReconciliation);
            var occupied = em.GetBuffer<Occupancy>(root);
            for (int i = 0; i < occupied.Length; i++)
                occupied[i] = default;
            em.GetBuffer<InventorySlot>(root).Clear();
            em.GetBuffer<DamageRequest>(root).Clear();
            em.GetBuffer<GameEvent>(root).Clear();
            EntityState.Buffer<IntelGeometry>(em, root);
            em.GetBuffer<IntelGeometry>(root).Clear();
            EntityState.Set(em, root, default(IntelProjection));
            SnapshotRootStorage.Restore(em, root, snapshot);
            EntityState.Set(em, root, default(ManualHistoryContext));
            NightEntryOps.Clear(em, root);
            EconomyForecastOps.Clear(em, root);
            NightResultOps.Reset(em, root);
            probe?.Invoke("root-reset");
            foreach (var record in snapshot.Records)
            {
                EntitySnapshotStorage.Restore(em, root, record);
                probe?.Invoke("record-created");
            }

            // Factories temporarily allocate IDs; occupancy must use the restored stable identities.
            occupied = em.GetBuffer<Occupancy>(root);
            for (int i = 0; i < occupied.Length; i++)
                occupied[i] = default;
            using (var buildings = WorldQueries.Entities<Building>(em))
                foreach (var entity in buildings)
                    GridOps.Occupy(em, root, entity);
            SurfaceNavigationGraph.Invalidate(em, root);
            SnapshotRootStorage.Restore(em, root, snapshot);
            NightResultOps.Reset(em, root);
            // Derived unit stats depend on all restored people, research, and permanent effects.
            foreach (var record in snapshot.Records)
            {
                if (record is SoldierSnapshot soldier)
                {
                    var entity = WorldQueries.Find(em, soldier.Identity.Id);
                    SoldierCombatants.Configure(em, root, entity, false, soldier.Soldier.Garrison, soldier.Transform.Position);
                    EntityState.Set(em, entity, soldier.Health);
                }
                else if (record is HeroSnapshot hero)
                {
                    var entity = WorldQueries.Find(em, hero.Identity.Id);
                    HeroCombatants.Configure(em, root, entity, false, hero.Hero.Sanctum, hero.Transform.Position);
                    EntityState.Set(em, entity, hero.Health);
                }
            }

            if (snapshot.Session.Phase == Phase.Deployment)
                BattleLifecycle.PrepareNight(em, root);
            probe?.Invoke("garrisons-prepared");
        }

        public static string ContentFingerprint(EntityManager em, Entity root) => SnapshotContentFingerprint.Compute(em, root);
    }
}
