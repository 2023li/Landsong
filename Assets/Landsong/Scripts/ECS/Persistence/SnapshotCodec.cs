using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Landsong.ECS.Persistence
{
    // DTOs exist only at the IO boundary. They never simulate or feed UI as a second live state.
    public static class SnapshotCodec
    {
        public const int CurrentVersion = 24;
        const int Version = CurrentVersion;
        static int Header(BinaryReader reader)
        {
            if (reader.ReadString() != "LANDSONG-ECS") throw new InvalidDataException("不是 ECS 存档。");
            int version = reader.ReadInt32();
            if (version != 22 && version != 23 && version != Version) throw new InvalidDataException("不是当前支持的 ECS 存档版本。");
            if (reader is SnapshotReader snapshot) snapshot.FormatVersion = version;
            return version;
        }
        sealed class SnapshotReader : BinaryReader
        {
            public int FormatVersion;
            public SnapshotReader(Stream stream) : base(stream) { }
        }
        sealed class SnapshotWriter : BinaryWriter
        {
            public readonly int FormatVersion;
            public SnapshotWriter(Stream stream, int version) : base(stream) { FormatVersion = version; }
        }
        const int MaximumRecords = 1000000;
        public static string ReadMapId(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes, false));
            int version=Header(reader);
            return reader.ReadString();
        }
        public static (string Map, Session Session) ReadSummary(byte[] bytes)
        {
            using var reader=new SnapshotReader(new MemoryStream(bytes,false));
            Header(reader);
            var map=reader.ReadString();reader.ReadString();var count=Count(reader);for(int i=0;i<count;i++)reader.ReadString();var session=Read<Session>(reader);
            if(session.Turn<1||(byte)session.Phase>(byte)Phase.Ended)throw new InvalidDataException("节点摘要无效。");return(map,session);
        }
        public sealed class Snapshot
        {
            public Session Session;
            public QuestTracking Tracking;
            public CourtState Court;
            public CourtLogEntry[] CourtLog;
            public NightPlanState NightPlan;
            public NightEventHistory[] NightHistory;
            public UnresolvedBoss[] Bosses;
            public NightPreparation[] Preparation;
            public InventorySlot[] Inventory;
            public PendingItem[] Pending;
            public Entitlement[] Grants;
            public ResearchEntry[] Research;
            public PolicyChoice[] Policies;
            public NightWave[] Waves;
            public BattleReportEntry[] Report;
            public BattleHistoryEntry[] BattleHistory = Array.Empty<BattleHistoryEntry>();
            public HistoryEntry[] History = Array.Empty<HistoryEntry>();
            public int LedgerTurn;
            public EconomyEntry[] Economy;
            public Record[] Records;
            internal Snapshot WithResearchState(ResearchEntry[] research, Entitlement[] grants)
            {
                var copy = (Snapshot)MemberwiseClone();
                copy.Research = research;
                copy.Grants = grants;
                return copy;
            }
        }
        public sealed class Record
        {
            public Identity Identity;
            public LocalTransform Transform;
            public uint Mask;
            public Health Health;
            public Building Building;
            public Soldier Soldier;
            public Hero Hero;
            public Quest Quest;
            public Expedition Expedition;
            public ExpeditionSupply[] Supplies;
            public ExpeditionDestinationHistory[] ExpeditionHistory;
            public Talent Talent;
            public Royal Royal;
            public PortraitDNA Portrait; public SoldierPerson SoldierPerson;
            public FoodSelection[] Food;
            public QuestOfferSlot[] Offers;
            public QuestProgress[] Progress;
            public TraitEntry[] Traits;
            public BuildingInvestment[] Investment;
            public RepairMaterial[] RepairMaterials;
            public PersonRequestEntry[] PersonRequests;
        }
        public static byte[] Capture(EntityManager em, Entity root)
            => CaptureVersion(em, root, Version);
#if UNITY_EDITOR
        // Verification only: exact legacy ABI and legacy signature; never a production write mode.
        public static byte[] CaptureLegacyV23ForVerification(EntityManager em, Entity root)
            => CaptureVersion(em, root, 23);
#endif
        static byte[] CaptureVersion(EntityManager em, Entity root, int version)
        {
            em.CompleteAllTrackedJobs();
            using var stream = new MemoryStream(); using var writer = new SnapshotWriter(stream, version);
            writer.Write("LANDSONG-ECS"); writer.Write(version);
            writer.Write(em.GetComponentData<MapIdentity>(root).Id.ToString());
            writer.Write(ContentSignature(em, root, version));
            var blob = em.GetComponentData<ContentCatalog>(root).Value; writer.Write(blob.Value.Definitions.Length);
            for (var i = 0; i < blob.Value.Definitions.Length; i++) writer.Write(blob.Value.Definitions[i].Id.ToString());
            var session = em.GetComponentData<Session>(root); session.SelectedHero = Entity.Null; session.ActiveBell = 0; session.Paused = 0; session.CheckpointPending = 0; session.IntelligenceMode = 0; Write(writer, session);
            Write(writer, QuestOps.Tracking(em, root));
            Write(writer, CourtOps.State(em, root)); WriteBuffer<CourtLogEntry>(writer, em, root);
            Write(writer, NightPlanOps.State(em, root)); WriteBuffer<NightEventHistory>(writer, em, root); WriteBuffer<UnresolvedBoss>(writer, em, root); WriteBuffer<NightPreparation>(writer, em, root);
            WriteBuffer<InventorySlot>(writer, em, root); WriteBuffer<PendingItem>(writer, em, root); WriteBuffer<Entitlement>(writer, em, root);
            WriteBuffer<ResearchEntry>(writer, em, root); WriteBuffer<PolicyChoice>(writer, em, root); WriteBuffer<NightWave>(writer, em, root); WriteBuffer<BattleReportEntry>(writer, em, root);
            WriteBuffer<BattleHistoryEntry>(writer, em, root);
            WriteBuffer<HistoryEntry>(writer, em, root);
            writer.Write(em.HasComponent<EconomyJournalState>(root) ? em.GetComponentData<EconomyJournalState>(root).Turn : 0);
            WriteBuffer<EconomyEntry>(writer, em, root);
            using var all = Sim.OrderedEntities<Persistent>(em); writer.Write(all.Length);
            foreach (var e in all)
            {
                Write(writer, em.GetComponentData<Identity>(e)); Write(writer, em.HasComponent<LocalTransform>(e) ? em.GetComponentData<LocalTransform>(e) : LocalTransform.Identity);
                uint mask = 0;
                if (em.HasComponent<Health>(e)) mask |= 1;
                if (em.HasComponent<Building>(e)) mask |= 2;
                if (em.HasComponent<Soldier>(e)) mask |= 4;
                if (em.HasComponent<Hero>(e)) mask |= 8;
                if (em.HasComponent<Quest>(e)) mask |= 16;
                if (em.HasComponent<Expedition>(e)) mask |= 32;
                if (em.HasComponent<Talent>(e)) mask |= 64;
                if (em.HasComponent<Royal>(e)) mask |= 128;
                if(em.HasComponent<PortraitDNA>(e))mask|=256; if(em.HasComponent<SoldierPerson>(e))mask|=512;
                writer.Write(mask);
                if ((mask & 1) != 0) Write(writer, em.GetComponentData<Health>(e));
                if ((mask & 2) != 0) Write(writer, em.GetComponentData<Building>(e));
                if ((mask & 4) != 0) Write(writer, em.GetComponentData<Soldier>(e));
                if ((mask & 8) != 0) Write(writer, em.GetComponentData<Hero>(e));
                if ((mask & 16) != 0) Write(writer, em.GetComponentData<Quest>(e));
                if ((mask & 32) != 0) Write(writer, em.GetComponentData<Expedition>(e));
                if ((mask & 64) != 0) Write(writer, em.GetComponentData<Talent>(e));
                if ((mask & 128) != 0) Write(writer, em.GetComponentData<Royal>(e));
                if((mask&256)!=0)Write(writer,em.GetComponentData<PortraitDNA>(e));if((mask&512)!=0)Write(writer,em.GetComponentData<SoldierPerson>(e));
                WriteBuffer<FoodSelection>(writer, em, e); WriteBuffer<QuestOfferSlot>(writer, em, e); WriteBuffer<QuestProgress>(writer, em, e); WriteBuffer<TraitEntry>(writer, em, e);
                WriteBuffer<ExpeditionSupply>(writer, em, e);
                WriteBuffer<ExpeditionDestinationHistory>(writer, em, e);
                WriteBuffer<BuildingInvestment>(writer, em, e); WriteBuffer<RepairMaterial>(writer, em, e);
                WriteBuffer<PersonRequestEntry>(writer,em,e);
            }
            return stream.ToArray();
        }
        public static Snapshot Decode(EntityManager em, Entity root, byte[] bytes)
            => DecodeCore(em, root, bytes, false);
        static Snapshot DecodeCore(EntityManager em, Entity root, byte[] bytes, bool deferQuestContainerReconciliation)
        {
            using var stream = new MemoryStream(bytes, false); using var reader = new SnapshotReader(stream);
            int version=Header(reader);
            if (reader.ReadString() != em.GetComponentData<MapIdentity>(root).Id.ToString()) throw new InvalidDataException("存档属于另一张地图，请返回主菜单选择对应地图。");
            // Old files cannot authenticate settings they never stored. They retain the exact
            // historical signature contract; only v24 writes/read checks use the complete contract.
            if (reader.ReadString() != ContentSignature(em, root, version)) throw new InvalidDataException("内容规则已改变，请开始新王朝；原存档未修改。");
            var count = Count(reader); var remap = new int[count];
            for (var i = 0; i < count; i++)
            {
                var id = reader.ReadString(); remap[i] = Sim.FindDefinition(em, root, new FixedString128Bytes(id));
                if (remap[i] < 0) throw new InvalidDataException("存档引用的内容已被移除：" + id);
            }
            int Map(int value) { if (value < 0) return -1; if (value >= remap.Length) throw new InvalidDataException("Invalid definition index"); return remap[value]; }
            var snapshot = new Snapshot { Session = Read<Session>(reader), Tracking = Read<QuestTracking>(reader), Court = Read<CourtState>(reader), CourtLog = ReadArray<CourtLogEntry>(reader), NightPlan = Read<NightPlanState>(reader), NightHistory = ReadArray<NightEventHistory>(reader), Bosses = ReadArray<UnresolvedBoss>(reader), Preparation = ReadArray<NightPreparation>(reader), Inventory = ReadArray<InventorySlot>(reader), Pending = ReadArray<PendingItem>(reader), Grants = ReadArray<Entitlement>(reader), Research = ReadArray<ResearchEntry>(reader), Policies = ReadArray<PolicyChoice>(reader), Waves = ReadArray<NightWave>(reader), Report = ReadArray<BattleReportEntry>(reader) };
            snapshot.NightPlan.BossDefinition = Map(snapshot.NightPlan.BossDefinition);
            snapshot.BattleHistory = ReadArray<BattleHistoryEntry>(reader);
            snapshot.History=ReadArray<HistoryEntry>(reader);for(int i=0;i<snapshot.History.Length;i++){var h=snapshot.History[i];h.Item=Map(h.Item);snapshot.History[i]=h;}
            for (int i = 0; i < snapshot.BattleHistory.Length; i++) { var h = snapshot.BattleHistory[i]; h.Entry.Definition = Map(h.Entry.Definition); snapshot.BattleHistory[i] = h; }
            for (int i = 0; i < snapshot.Bosses.Length; i++) { var b = snapshot.Bosses[i]; b.Definition = Map(b.Definition); snapshot.Bosses[i] = b; }
            for (int i = 0; i < snapshot.Preparation.Length; i++) { var p = snapshot.Preparation[i]; p.Definition = Map(p.Definition); snapshot.Preparation[i] = p; }
            if (snapshot.Session.Phase != Phase.Day && snapshot.Session.Phase != Phase.Deployment && !(snapshot.Session.Phase == Phase.GameOver && snapshot.Court.Extinction != 0)) throw new InvalidDataException("只允许白天、黄昏节点或绝嗣终局快照。");
            snapshot.LedgerTurn = reader.ReadInt32(); snapshot.Economy = ReadArray<EconomyEntry>(reader);
            for (var i = 0; i < snapshot.Economy.Length; i++) { var e = snapshot.Economy[i]; e.Item = Map(e.Item); snapshot.Economy[i] = e; }
            for (var i = 0; i < snapshot.Inventory.Length; i++) { var s = snapshot.Inventory[i]; s.Item = Map(s.Item); s.SlotType = Map(s.SlotType); if (s.Count < 0) throw new InvalidDataException("Negative stock"); snapshot.Inventory[i] = s; }
            for (var i = 0; i < snapshot.Pending.Length; i++) { var p = snapshot.Pending[i]; p.Item = Map(p.Item); snapshot.Pending[i] = p; }
            for (var i = 0; i < snapshot.Grants.Length; i++) { var g = snapshot.Grants[i]; g.Definition = Map(g.Definition); snapshot.Grants[i] = g; }
            for (var i = 0; i < snapshot.Research.Length; i++) { var r = snapshot.Research[i]; r.Definition = Map(r.Definition); snapshot.Research[i] = r; }
            for (var i = 0; i < snapshot.Policies.Length; i++) { var p = snapshot.Policies[i]; p.Definition = Map(p.Definition); snapshot.Policies[i] = p; }
            for (var i = 0; i < snapshot.Waves.Length; i++) { var w = snapshot.Waves[i]; w.Definition = Map(w.Definition); snapshot.Waves[i] = w; }
            for (var i = 0; i < snapshot.Report.Length; i++) { var r = snapshot.Report[i]; r.Definition = Map(r.Definition); snapshot.Report[i] = r; }
            snapshot.Records = new Record[Count(reader)]; var ids = new HashSet<ulong>(); var buildings = new HashSet<ulong>();
            for (var i = 0; i < snapshot.Records.Length; i++)
            {
                var r = new Record { Identity = Read<Identity>(reader), Transform = Read<LocalTransform>(reader), Mask = reader.ReadUInt32() };
                if (r.Identity.Id == 0 || !ids.Add(r.Identity.Id)) throw new InvalidDataException("Duplicate entity stable ID");
                r.Identity.Definition = Map(r.Identity.Definition);
                if ((r.Mask & 1) != 0) r.Health = Read<Health>(reader);
                if ((r.Mask & 2) != 0) { r.Building = Read<Building>(reader); r.Building.Crop = Map(r.Building.Crop); buildings.Add(r.Identity.Id); }
                if ((r.Mask & 4) != 0) r.Soldier = Read<Soldier>(reader);
                if ((r.Mask & 8) != 0) r.Hero = Read<Hero>(reader);
                if ((r.Mask & 16) != 0) r.Quest = Read<Quest>(reader);
                if ((r.Mask & 32) != 0) r.Expedition = Read<Expedition>(reader);
                if ((r.Mask & 64) != 0) { r.Talent = Read<Talent>(reader); r.Talent.Slot = Map(r.Talent.Slot); }
                if ((r.Mask & 128) != 0) r.Royal = Read<Royal>(reader);
                if((r.Mask&256)!=0)r.Portrait=Read<PortraitDNA>(reader);if((r.Mask&512)!=0){r.SoldierPerson=Read<SoldierPerson>(reader);if(version==22){if(r.SoldierPerson.SpecialAttention>1)throw new InvalidDataException("Invalid legacy soldier name mark");r.SoldierPerson.SpecialAttention=0;}}
                r.Food = ReadArray<FoodSelection>(reader); r.Offers = ReadArray<QuestOfferSlot>(reader); r.Progress = ReadArray<QuestProgress>(reader); r.Traits = ReadArray<TraitEntry>(reader);
                r.Supplies = ReadArray<ExpeditionSupply>(reader); for (var n = 0; n < r.Supplies.Length; n++) { var supply = r.Supplies[n]; supply.Item = Map(supply.Item); r.Supplies[n] = supply; }
                r.ExpeditionHistory = ReadArray<ExpeditionDestinationHistory>(reader); for (var n = 0; n < r.ExpeditionHistory.Length; n++) { var history = r.ExpeditionHistory[n]; history.Definition = Map(history.Definition); r.ExpeditionHistory[n] = history; }
                r.Investment = ReadArray<BuildingInvestment>(reader); r.RepairMaterials = ReadArray<RepairMaterial>(reader);
                r.PersonRequests=ReadArray<PersonRequestEntry>(reader);
                for (var n = 0; n < r.Investment.Length; n++) { var c = r.Investment[n]; c.Item = Map(c.Item); if (c.Amount < 0) throw new InvalidDataException("Negative building investment"); r.Investment[n] = c; }
                for (var n = 0; n < r.RepairMaterials.Length; n++) { var c = r.RepairMaterials[n]; c.Item = Map(c.Item); if (c.Amount < 0) throw new InvalidDataException("Negative repair material"); r.RepairMaterials[n] = c; }
                for (var n = 0; n < r.Food.Length; n++) { var f = r.Food[n]; f.Group = Map(f.Group); f.Item = Map(f.Item); r.Food[n] = f; }
                for (var n = 0; n < r.Traits.Length; n++) { var t = r.Traits[n]; t.Definition = Map(t.Definition); r.Traits[n] = t; }
                // Rebind stable requirement keys; neither array order nor old blob addresses identify progress.
                if ((r.Mask & 16) != 0)
                {
                    var d = Sim.Definition(em, root, r.Identity.Definition);
                    for (var n = 0; n < r.Progress.Length; n++)
                    {
                        var p = r.Progress[n]; p.RuleIndex = -1;
                        for (var k = 0; k < d.RuleCount; k++) { var rule = Sim.GetRule(em, root, d.RuleStart + k); if (QuestOps.Requirement(rule.Kind) && QuestOps.Key(rule, k) == p.Key) { p.RuleIndex = d.RuleStart + k; break; } }
                        r.Progress[n] = p;
                    }
                }
                snapshot.Records[i] = r;
            }
            foreach (var slot in snapshot.Inventory) if (!buildings.Contains(slot.Provider)) throw new InvalidDataException("Inventory provider is missing");
            if (stream.Position != stream.Length) throw new InvalidDataException("Snapshot trailing data");
            snapshot = NormalizeResearch(em, root, snapshot);
            ValidateRestore(em, root, snapshot, deferQuestContainerReconciliation);
            return snapshot;
        }
        static Snapshot NormalizeResearch(EntityManager em, Entity root, Snapshot snapshot)
        {
            if (snapshot == null) throw new InvalidDataException("Missing snapshot");
            var research = ResearchOps.NormalizeImportedState(em, root, snapshot.Session.ResearchPoints, snapshot.Research, snapshot.Grants);
            return snapshot.WithResearchState(research, (Entitlement[])snapshot.Grants.Clone());
        }
        public static void Restore(EntityManager em, Entity root, Snapshot snapshot, Action<EntityManager, Entity> prepare = null, Action<string> probe = null)
        {
            snapshot = NormalizeResearch(em, root, snapshot);
            ValidateRestore(em, root, snapshot);
            using var transaction = new RestoreTransaction(em, root);
            Rebuild(em, transaction.Root, snapshot, probe);
            prepare?.Invoke(em, transaction.Root);
            probe?.Invoke("prepared");
            transaction.Commit(probe);
        }
        // Only called within a RestoreTransaction; all previous runtime entities are isolated.
        internal static void Rebuild(EntityManager em, Entity root, Snapshot snapshot, Action<string> probe = null)
            => RebuildCore(em, root, snapshot, false, probe);
        // Only the live-day night-entry transaction may defer a lost quest binding. This is not
        // an archive import option: all other constraints remain enforced during decode/rebuild.
        internal static void RebuildNightEntryCandidate(EntityManager em, Entity root, byte[] liveDay, Action<string> probe = null)
            => RebuildCore(em, root, DecodeCore(em, root, liveDay, true), true, probe);
        internal static void ValidateReconciledNightEntry(EntityManager em, Entity root)
            => Decode(em, root, Capture(em, root));
        static void RebuildCore(EntityManager em, Entity root, Snapshot snapshot, bool deferQuestContainerReconciliation, Action<string> probe)
        {
            // Normalize before derived entity statistics read research completion. The DTO belongs
            // to the caller, so never append legacy completion rows into its arrays in place.
            snapshot = NormalizeResearch(em, root, snapshot);
            ValidateRestore(em, root, snapshot, deferQuestContainerReconciliation);
            var occupied = em.GetBuffer<Occupancy>(root); for (var i = 0; i < occupied.Length; i++) occupied[i] = default;
            em.GetBuffer<InventorySlot>(root).Clear();
            em.GetBuffer<Command>(root).Clear(); em.GetBuffer<DamageRequest>(root).Clear();
            em.GetBuffer<GameEvent>(root).Clear();
            Sim.Buffer<IntelGeometry>(em, root); em.GetBuffer<IntelGeometry>(root).Clear();
            Sim.Set(em, root, default(IntelProjection));
            NightResultOps.Reset(em, root);
            em.SetComponentData(root, snapshot.Session);
            Sim.Set(em, root, snapshot.Tracking);
            Sim.Set(em, root, snapshot.Court); RestoreBuffer(em, root, snapshot.CourtLog);
            RestoreBuffer(em, root, snapshot.BattleHistory);
            RestoreBuffer(em,root,snapshot.History);Sim.Set(em,root,new ManualHistoryContext());
            Sim.Set(em, root, snapshot.NightPlan); RestoreBuffer(em, root, snapshot.NightHistory); RestoreBuffer(em, root, snapshot.Bosses); RestoreBuffer(em, root, snapshot.Preparation);
            NightEntryOps.Clear(em, root);
            Sim.Set(em, root, new EconomyJournalState { Turn = snapshot.LedgerTurn }); RestoreBuffer(em, root, snapshot.Economy);
            EconomyForecastOps.Clear(em, root);
            RestoreBuffer(em, root, snapshot.Grants); RestoreBuffer(em, root, snapshot.Research); RestoreBuffer(em, root, snapshot.Policies);
            probe?.Invoke("root-reset");
            foreach (var r in snapshot.Records)
            {
                Entity e;
                if ((r.Mask & 2) != 0) e = BuildingOps.Create(em, root, r.Identity.Definition, r.Building.Cell, r.Building.Rotation, r.Building.Level, false);
                else if (r.Identity.Definition >= 0) e = Sim.Spawn(em, root, r.Identity.Definition, r.Transform.Position, true);
                else { e = em.CreateEntity(); em.AddComponentData(e, new Persistent()); em.AddComponentData(e, new SimulationOwner { Root = root }); }
                Sim.Set(em, e, r.Identity); Sim.Set(em, e, r.Transform);
                if ((r.Mask & 2) != 0) { Sim.Set(em, e, r.Building); BuildingOps.ApplyLevel(em, root, e, false); }
                if ((r.Mask & 4) != 0) Sim.Set(em, e, r.Soldier);
                if ((r.Mask & 8) != 0) Sim.Set(em, e, r.Hero);
                if ((r.Mask & 1) != 0) Sim.Set(em, e, r.Health);
                if ((r.Mask & 16) != 0) { Sim.Set(em, e, r.Quest); Sim.Buffer<QuestProgress>(em, e); }
                if ((r.Mask & 32) != 0) Sim.Set(em, e, r.Expedition);
                if ((r.Mask & 64) != 0) { Sim.Set(em, e, r.Talent); Sim.Buffer<TraitEntry>(em, e); }
                if ((r.Mask & 128) != 0) { Sim.Set(em, e, r.Royal); Sim.Buffer<TraitEntry>(em, e); }
                RestoreBuffer(em, e, r.Food); RestoreBuffer(em, e, r.Offers); RestoreBuffer(em, e, r.Progress); RestoreBuffer(em, e, r.Traits);
                RestoreBuffer(em, e, r.Supplies); RestoreBuffer(em, e, r.Investment); RestoreBuffer(em, e, r.RepairMaterials);
                RestoreBuffer(em,e,r.PersonRequests);
                if((r.Mask&256)!=0)Sim.Set(em,e,r.Portrait);if((r.Mask&512)!=0)Sim.Set(em,e,r.SoldierPerson);
                RestoreBuffer(em, e, r.ExpeditionHistory);
                probe?.Invoke("record-created");
            }
            // Create assigned temporary IDs while instantiating; only rebuild occupancy after stable IDs are restored.
            occupied = em.GetBuffer<Occupancy>(root); for (var i = 0; i < occupied.Length; i++) occupied[i] = default;
            using (var buildings = Sim.Entities<Building>(em)) foreach (var e in buildings) GridOps.Occupy(em, root, e);
            RestoreBuffer(em, root, snapshot.Inventory); RestoreBuffer(em, root, snapshot.Pending); RestoreBuffer(em, root, snapshot.Grants); RestoreBuffer(em, root, snapshot.Research); RestoreBuffer(em, root, snapshot.Policies); RestoreBuffer(em, root, snapshot.Waves); RestoreBuffer(em, root, snapshot.Report);
            em.SetComponentData(root, snapshot.Session);
            NightResultOps.Reset(em, root);
            // Talents and grants must all exist before deriving unit stats, irrespective of record order.
            foreach (var r in snapshot.Records) if ((r.Mask & (4u | 8u)) != 0)
            {
                var e = Sim.Find(em, r.Identity.Id);
                MilitaryOps.ConfigureCombatant(em, root, e, 0, (r.Mask & 8) != 0, false, (r.Mask & 8) != 0 ? r.Hero.Sanctum : r.Soldier.Garrison, r.Transform.Position);
                if ((r.Mask & 1) != 0) Sim.Set(em, e, r.Health);
            }
            if (snapshot.Session.Phase == Phase.Deployment) MilitaryOps.PrepareNight(em, root);
            probe?.Invoke("garrisons-prepared");
        }
        public static string ContentFingerprint(EntityManager em, Entity root) => ContentSignature(em, root, Version);
        static string ContentSignature(EntityManager em, Entity root, int version)
        {
            var catalog = em.GetComponentData<ContentCatalog>(root).Value;
            using var stream = new MemoryStream(); using var writer = new SnapshotWriter(stream, version);
            writer.Write(catalog.Value.Definitions.Length); writer.Write(catalog.Value.Rules.Length);
            var grid = em.GetComponentData<GridData>(root); writer.Write(grid.Value.Value.Cells.Length);
            for (int i = 0; i < grid.Value.Value.Cells.Length; i++) Write(writer, grid.Value.Value.Cells[i]);
            Write(writer, catalog.Value.Quests); Write(writer, catalog.Value.Expeditions); Write(writer, catalog.Value.Court); Write(writer, em.GetComponentData<DynastySettings>(root));
            writer.Write(em.GetBuffer<InitialRoyal>(root).Length);foreach(var royal in em.GetBuffer<InitialRoyal>(root)) writer.Write((byte)royal.Gender);
            if(PortraitOps.Ready(em,root))Write(writer,em.GetComponentData<PortraitLibrary>(root).Value.Value.Settings);
            Write(writer, catalog.Value.Night);
            Write(writer, catalog.Value.Peaceful);
            var intelSettings = em.GetComponentData<GameSettings>(root); writer.Write(intelSettings.LowIntel); writer.Write(intelSettings.MediumIntel); writer.Write(intelSettings.HighIntel); writer.Write(intelSettings.MediumIntelLead); writer.Write(intelSettings.HighIntelLead);
            writer.Write(catalog.Value.NightEvents.Length); for (int i = 0; i < catalog.Value.NightEvents.Length; i++) Write(writer, catalog.Value.NightEvents[i]);
            writer.Write(catalog.Value.NightEnemies.Length); for (int i = 0; i < catalog.Value.NightEnemies.Length; i++) Write(writer, catalog.Value.NightEnemies[i]);
            writer.Write(catalog.Value.NightConditions.Length); for (int i = 0; i < catalog.Value.NightConditions.Length; i++) Write(writer, catalog.Value.NightConditions[i]);
            for (var i = 0; i < catalog.Value.Definitions.Length; i++)
            {
                var definition = catalog.Value.Definitions[i];
                if (version >= 24) { definition.Name = default; definition.DefaultSkin = default; definition.BuildingPolicy.MenuOrder = 0; }
                Write(writer, definition);
            }
            for (var i = 0; i < catalog.Value.Definitions.Length; i++)
            {
                var d = catalog.Value.Definitions[i]; var requirements = new List<Rule>();
                for (var n = 0; n < d.RuleCount; n++) { var rule = catalog.Value.Rules[d.RuleStart + n]; if (d.Kind == ContentKind.Quest && QuestOps.Requirement(rule.Kind)) requirements.Add(rule); else Write(writer, rule); }
                requirements.Sort((a, b) => string.CompareOrdinal(a.Key.ToString(), b.Key.ToString())); foreach (var rule in requirements) Write(writer, rule);
            }
            if (version >= 24)
            {
                Write(writer, em.GetComponentData<GameSettings>(root));
                Write(writer, grid.Origin); writer.Write(grid.CellSize);
                Write(writer, grid.Value.Value.Min); Write(writer, grid.Value.Value.Size);
                WriteBuffer<SpawnRegion>(writer, em, root);
                var royalCount = em.HasBuffer<InitialRoyal>(root) ? em.GetBuffer<InitialRoyal>(root).Length : 0;
                writer.Write(royalCount);
                for (int i = 0; i < royalCount; i++) { var royal = em.GetBuffer<InitialRoyal>(root)[i]; royal.Name = default; Write(writer, royal); }
                var buildingCount = em.HasBuffer<InitialBuilding>(root) ? em.GetBuffer<InitialBuilding>(root).Length : 0;
                writer.Write(buildingCount);
                for (int i = 0; i < buildingCount; i++) { var building = em.GetBuffer<InitialBuilding>(root)[i]; building.Name = default; Write(writer, building); }
                var rewardCount = em.HasBuffer<StartingGrant>(root) ? em.GetBuffer<StartingGrant>(root).Length : 0;
                writer.Write(rewardCount);
                for (int i = 0; i < rewardCount; i++) Write(writer, em.GetBuffer<StartingGrant>(root)[i].Rule);
            }
            using var hash = System.Security.Cryptography.SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }
        // All predictable content/format failures are rejected before destroying any live entities.
        static void ValidateRestore(EntityManager em, Entity root, Snapshot snapshot, bool deferQuestContainerReconciliation = false)
        {
            if (snapshot?.Economy == null || snapshot.LedgerTurn < 0 || snapshot.LedgerTurn > snapshot.Session.Turn) throw new InvalidDataException("Invalid economy journal");
            foreach (var entry in snapshot.Economy)
                if (entry.Turn != snapshot.LedgerTurn || entry.Source > snapshot.Session.NextId || entry.Pending > 1 || (byte)entry.Reason > (byte)EconomyReason.NightDiscard || entry.Delta != 0 && !Sim.ValidDefinition(em, root, entry.Item)) throw new InvalidDataException("Invalid economy entry");
            if (snapshot == null || snapshot.Records == null || snapshot.Inventory == null || snapshot.Pending == null || snapshot.Grants == null || snapshot.Waves == null || snapshot.Research == null || snapshot.Policies == null || snapshot.Report == null) throw new InvalidDataException("Incomplete snapshot");
            var s = snapshot.Session;
            if (snapshot.BattleHistory == null) throw new InvalidDataException("Missing battle history");
            if(snapshot.History==null)throw new InvalidDataException("Missing interface history");
            foreach(var h in snapshot.History)if(h.Turn<1||h.Turn>s.Turn||h.Count<1||h.Count>100000||h.Pending>1||h.HasPosition>1||h.Transfer>1||(byte)h.Category>(byte)HistoryCategory.Important||!Unity.Mathematics.math.all(Unity.Mathematics.math.isfinite(h.Position))||h.Item>=0&&!Sim.ValidDefinition(em,root,h.Item))throw new InvalidDataException("Invalid interface history");
            foreach (var entry in snapshot.Report) ValidateReport(entry);
            int historyTurn = 0;
            foreach (var h in snapshot.BattleHistory) { if (h.Turn < 1 || h.Turn < historyTurn || h.Turn > s.Turn) throw new InvalidDataException("Invalid battle history turn"); historyTurn = h.Turn; ValidateReport(h.Entry); }
            void ValidateReport(BattleReportEntry entry)
            { if (entry.Amount < 0 || !Unity.Mathematics.math.isfinite(entry.Value) || entry.Value < 0 || (byte)entry.Kind > (byte)EventKind.HeroOfferingExperience || entry.Definition >= 0 && !Sim.ValidDefinition(em, root, entry.Definition)) throw new InvalidDataException("Invalid battle report"); }
            EntitlementStore.ValidateImport(em, root, snapshot.Grants);
            ResearchOps.ValidateState(em, root, s.ResearchPoints, snapshot.Research, snapshot.Grants);
            if ((s.Phase != Phase.Day && s.Phase != Phase.Deployment && !(s.Phase == Phase.GameOver && snapshot.Court.Extinction == 1)) || s.Turn < 1 || s.RandomState == 0 || s.NextId == 0) throw new InvalidDataException("Invalid session");
            var ids = new HashSet<ulong>(); var buildings = new HashSet<ulong>();
            foreach (var r in snapshot.Records)
            {
                if (r == null || r.Identity.Id == 0 || r.Identity.Id > s.NextId || !ids.Add(r.Identity.Id) || (r.Mask & ~1023u) != 0 || !Unity.Mathematics.math.all(Unity.Mathematics.math.isfinite(r.Transform.Position))) throw new InvalidDataException("Invalid entity identity or transform");
                var definition = r.Identity.Definition;
                if (definition >= 0 && !Sim.ValidDefinition(em, root, definition)) throw new InvalidDataException("Invalid entity definition");
                if (r.Food == null || r.Offers == null || r.Progress == null || r.Traits == null || r.Investment == null || r.RepairMaterials == null) throw new InvalidDataException("Missing entity buffers");
                if((r.Mask&256)!=0){if((r.Mask&(4|8|128))==0||!PortraitOps.Ready(em,root))throw new InvalidDataException("Invalid portrait owner");var lib=em.GetComponentData<PortraitLibrary>(root).Value;var gender=(r.Mask&128)!=0?r.Royal.Gender:(r.Mask&512)!=0?r.SoldierPerson.Gender:PersonGender.Male;if(!PortraitOps.Valid(ref lib.Value,r.Portrait,gender))throw new InvalidDataException("Invalid portrait DNA");}
                if((r.Mask&512)!=0){var p=r.SoldierPerson;if((r.Mask&4)==0||(r.Mask&256)==0||p.Age<0||p.Lifespan<=p.Age||p.LastAgeTurn<0||p.LastAgeTurn>s.Turn||p.Incarnation<0||p.SpecialAttention>1||p.DeathNotified>1||(p.Gender!=PersonGender.Male&&p.Gender!=PersonGender.Female))throw new InvalidDataException("Invalid soldier person");}
                if(r.PersonRequests==null||r.PersonRequests.Length>16||(r.Mask&128)==0&&r.PersonRequests.Length!=0)throw new InvalidDataException("Invalid person request buffer");
                if ((r.Mask & 16) != 0) QuestOps.ValidateProgress(em, root, definition, r.Quest, r.Progress, s.Turn);
                if ((r.Mask & 1) != 0 && (!Unity.Mathematics.math.isfinite(r.Health.Current) || !Unity.Mathematics.math.isfinite(r.Health.Maximum) || r.Health.Current < 0 || r.Health.Maximum <= 0 || r.Health.Current > r.Health.Maximum)) throw new InvalidDataException("Invalid health");
                if (definition >= 0)
                {
                    var kind = Sim.Definition(em, root, definition).Kind;
                    if (((r.Mask & 2) != 0 && kind != ContentKind.Building) || ((r.Mask & 4) != 0 && kind != ContentKind.Soldier) || ((r.Mask & 8) != 0 && kind != ContentKind.Hero)) throw new InvalidDataException("Entity role mismatch");
                    if (kind == ContentKind.Building || kind == ContentKind.Soldier || kind == ContentKind.Hero)
                    {
                        var found = false;
                        foreach (var p in em.GetBuffer<ContentPrefab>(root)) if (p.Definition == definition && p.Prefab != Entity.Null && em.Exists(p.Prefab)) { found = true; break; }
                        if (!found) throw new InvalidDataException("Missing entity prefab");
                    }
                }
                else if ((r.Mask & (2u | 4u | 8u | 16u | 32u | 64u)) != 0) throw new InvalidDataException("Missing required definition");
                if ((r.Mask & 2) != 0) { if (r.Building.Level < 1 || r.Building.Workers < 0 || r.Building.Population < 0 || r.Building.SubsidyBudget < 0 || r.Building.SubsidyBudget > Math.Max(0,Sim.Rule(em,root,r.Identity.Definition,RuleKind.Workforce,r.Building.Level).Amount) || r.Building.PaidSubsidy < 0 || r.Building.PaidSubsidyTurn < 0 || r.Building.PaidSubsidyTurn > snapshot.Session.Turn) throw new InvalidDataException("Invalid building state"); buildings.Add(r.Identity.Id); }
            }
            InvitationStateValidation.Validate(em, root, snapshot, deferQuestContainerReconciliation);
            CourtStateValidation.Validate(em, root, snapshot);
            NightStateValidation.Validate(em, root, snapshot);
            ValidateSoldiers(em, root, snapshot);
            var heroDefinitions = new HashSet<int>();
            foreach (var r in snapshot.Records) if ((r.Mask & 8) != 0)
            {
                var h = r.Hero;
                if (!heroDefinitions.Add(r.Identity.Definition) || h.Experience < 0 || h.CooldownUntil < 0 || h.LastCombatTurn < 0 || h.LastCombatTurn > snapshot.Session.Turn || h.Recruited > 1 || h.DeathPending != 0 || h.Recruited == 0 && h.Experience != 0) throw new InvalidDataException("Invalid hero checkpoint state");
                if (h.Recruited != 0 && !System.Array.Exists(snapshot.Records, site => (site.Mask & 2) != 0 && site.Identity.Id == h.Sanctum && site.Building.Stage != LifeStage.Ruined && site.Building.Stage != LifeStage.Repairing)) throw new InvalidDataException("Living hero requires an operational sanctum");
            }
            var slots = new HashSet<(ulong, int)>();
            if (snapshot.Tracking.Mode > 2 || snapshot.Tracking.Mode == 2 && snapshot.Tracking.Target != 0) throw new InvalidDataException("Invalid quest tracking mode");
            if (snapshot.Tracking.Target != 0)
            {
                var found = false; foreach (var r in snapshot.Records) if (r.Identity.Id == snapshot.Tracking.Target && (r.Mask & 16) != 0 && (r.Quest.Status == QuestStatus.Active || r.Quest.Status == QuestStatus.Completed)) found = true;
                if (!found) throw new InvalidDataException("Tracked quest is missing or not trackable");
            }
            foreach (var slot in snapshot.Inventory)
            {
                if (!buildings.Contains(slot.Provider) || !slots.Add((slot.Provider, slot.Index)) || slot.Index < 0 || slot.Count < 0 || slot.Unavailable > 1 || !Unity.Mathematics.math.isfinite(slot.LossRemainder) || slot.LossRemainder < 0 || slot.LossRemainder > slot.Count || (slot.Count == 0 && slot.LossRemainder != 0)) throw new InvalidDataException("Invalid inventory slot");
                if (slot.SlotType >= 0 && (!Sim.ValidDefinition(em, root, slot.SlotType) || Sim.Definition(em, root, slot.SlotType).Kind != ContentKind.SlotType)) throw new InvalidDataException("Invalid slot type");
                if (slot.Count > 0 && (!InventoryOps.Accepts(em, root, slot.SlotType, slot.Item) || slot.Count > Sim.Definition(em, root, slot.Item).Capacity)) throw new InvalidDataException("Incompatible or overfull inventory slot");
            }
            var pendingItems = new HashSet<int>();
            foreach (var item in snapshot.Pending) if (!pendingItems.Add(item.Item) || item.Amount < 0 || !Sim.ValidDefinition(em, root, item.Item) || Sim.Definition(em, root, item.Item).Kind != ContentKind.Item || !Unity.Mathematics.math.isfinite(item.LossRemainder) || item.LossRemainder < 0 || item.LossRemainder > item.Amount) throw new InvalidDataException("Invalid pending item");
        }
        static void ValidateSoldiers(EntityManager em, Entity root, Snapshot snapshot)
        {
            var sites = new Dictionary<ulong, Record>(); var slots = new HashSet<(ulong, int)>();
            foreach (var r in snapshot.Records) if ((r.Mask & 2) != 0)
            {
                if (r.Building.SoldierRecruitTurn < 0 || r.Building.SoldierRecruitTurn > snapshot.Session.Turn || r.Building.SoldiersRecruited < 0 || r.Building.SoldierRecruitTurn == 0 && r.Building.SoldiersRecruited != 0) throw new InvalidDataException("Invalid garrison recruitment ledger");
                sites.Add(r.Identity.Id, r);
            }
            foreach (var r in snapshot.Records) if ((r.Mask & 4) != 0)
            {
                var s = r.Soldier;
                if (s.PopulationCost < 0 || s.Experience < 0 || s.PendingSince < 0 || s.PendingSince > snapshot.Session.Turn || s.LastExperienceTurn < 0 || s.LastExperienceTurn > snapshot.Session.Turn || s.RecallState != 0 || s.Slot < 0 || s.Experience > MilitaryOps.LevelThreshold(Sim.Definition(em, root, r.Identity.Definition).SoldierGrowth, Sim.Definition(em, root, r.Identity.Definition).SoldierGrowth.MaxLevel)) throw new InvalidDataException("Invalid soldier state");
                if (s.Garrison == 0) { if (s.Slot != 0) throw new InvalidDataException("Unassigned soldier owns a slot"); continue; }
                if (!sites.TryGetValue(s.Garrison, out var home) || s.Slot < 1 || !slots.Add((s.Garrison, s.Slot))) throw new InvalidDataException("Missing or duplicate garrison slot");
                var rule = Sim.Rule(em, root, home.Identity.Definition, RuleKind.Garrison, home.Building.Level);
                // Day may contain slots invalidated by settlement; entry must preview their dissolution.
                if (snapshot.Session.Phase == Phase.Deployment && (home.Building.Stage != LifeStage.Operational || rule.Level < 0 || s.Slot > rule.Amount)) throw new InvalidDataException("Invalid dusk garrison capacity or stage");
            }
        }
        static int Count(BinaryReader reader) { var n = reader.ReadInt32(); if (n < 0 || n > MaximumRecords) throw new InvalidDataException("Snapshot count exceeds limits"); return n; }
        static void WriteBuffer<T>(BinaryWriter writer, EntityManager em, Entity e) where T : unmanaged, IBufferElementData
        { if (!em.HasBuffer<T>(e)) { writer.Write(0); return; } var buffer = em.GetBuffer<T>(e); writer.Write(buffer.Length); foreach (var value in buffer) Write(writer, value); }
        static void RestoreBuffer<T>(EntityManager em, Entity e, T[] data) where T : unmanaged, IBufferElementData
        { if (data.Length == 0 && !em.HasBuffer<T>(e)) return; Sim.Buffer<T>(em, e); var buffer = em.GetBuffer<T>(e); buffer.Clear(); foreach (var value in data) buffer.Add(value); }
        static T[] ReadArray<T>(BinaryReader reader) where T : unmanaged
        { var array = new T[Count(reader)]; for (var i = 0; i < array.Length; i++) array[i] = Read<T>(reader); return array; }
        static void Write<T>(BinaryWriter writer, T value) where T : unmanaged
        { if (((SnapshotWriter)writer).FormatVersion >= 24) SnapshotBinaryV24.Write(writer, value); else LegacySnapshotBinaryV23.Write(writer, value); }
        static T Read<T>(BinaryReader reader) where T : unmanaged
            => ((SnapshotReader)reader).FormatVersion >= 24 ? SnapshotBinaryV24.Read<T>(reader) : LegacySnapshotBinaryV23.Read<T>(reader);
    }
}
