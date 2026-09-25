using System;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class CheckpointSystem : SystemBase
    {
        Entity observedRoot;
        RunArchive archive;
        bool dirty;
        bool duskCheckpointDeferred;
        double nextWrite;
        public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "ECS");
        public static RunArchiveStore DefaultStore => new RunArchiveStore(SaveDirectory);
        public RunArchiveStore Store { get; set; } // Isolated test directory, never a global override.

        protected override void OnCreate() => RequireForUpdate<SimulationReady>();
        void Observe(Entity root)
        {
            if (root == observedRoot && archive != null)
                return;
            observedRoot = root;
            dirty = false;
            duskCheckpointDeferred = false;
            nextWrite = 0;
            EntityState.Set(EntityManager, root, new RunPersistence { RunId = Guid.NewGuid().ToString("N") });
            if (!EntityManager.HasComponent<RecoveryState>(root) || EntityManager.GetComponentData<RecoveryState>(root).Turn < 1)
                EntityState.Set(EntityManager, root, new RecoveryState { Turn = EntityManager.GetComponentData<GameClock>(root).Turn });
            var day = SnapshotCodec.Capture(EntityManager, root);
            archive = new RunArchive
            {
                RunId = EntityManager.GetComponentData<RunPersistence>(root).RunId.ToString(),
                Day = day,
                Current = day,
                Recovery = EntityManager.GetComponentData<RecoveryState>(root)
            };
        }

        public void OpenNewRun(Entity root)
        {
            Observe(root);
            archive.Day = archive.Current = SnapshotCodec.Capture(EntityManager, root);
            var io = EntityManager.GetComponentData<RunPersistence>(root);
            io.Enabled = 1;
            EntityManager.SetComponentData(root, io);
            Flush(root); // Persist before handing control out of the loading scene.
        }

        public RunArchive Export(Entity root)
        {
            Observe(root);
            var copy = archive.Copy();
            copy.Recovery = EntityManager.GetComponentData<RecoveryState>(root);
            return copy;
        }

        public void Import(Entity root, RunArchive data, bool persistent, Action<string> probe = null)
        {
            ValidateArchive(EntityManager, root, data);
            var snapshot = SnapshotCodec.Decode(EntityManager, root, data.Current);
            var preparedArchive = data.Copy();
            SnapshotCodec.Restore(EntityManager, root, snapshot, (manager, candidate) =>
            {
                EntityState.Set(manager, candidate, new RunPersistence { RunId = data.RunId, Enabled = (byte)(persistent ? 1 : 0) });
                EntityState.Set(manager, candidate, data.Recovery);
                ApplyRecovery(manager, candidate);
                if (data.Recovery.AwaitingDecision != 0)
                {
                    var state = manager.GetComponentData<Session>(candidate);
                    SimulationControl stateControl = manager.GetComponentData<SimulationControl>(candidate);
                    state.Phase = Phase.GameOver;
                    stateControl.Paused = 0;
                    {
                        manager.SetComponentData(candidate, state);
                        manager.SetComponentData(candidate, stateControl);
                    }
                }
            }, probe);
            observedRoot = root;
            archive = preparedArchive;
            dirty = false;
            duskCheckpointDeferred = false;
        }

        public static void ValidateArchive(EntityManager em, Entity root, RunArchive data)
        {
            RunArchiveCodec.Encode(data);
            var current = SnapshotCodec.Decode(em, root, data.Current);
            var day = SnapshotCodec.Decode(em, root, data.Day);
            if ((current.Court.Extinction != 0) != (data.Recovery.Extinction != 0))
                throw new InvalidDataException("绝嗣终局与恢复元数据不一致。");
            if (day.Session.Phase != Phase.Day || day.Clock.Turn != current.Clock.Turn || data.Recovery.Turn != current.Clock.Turn)
                throw new InvalidDataException("原始白天节点与当前回合不一致。");
            if (data.Dusk != null)
            {
                var dusk = SnapshotCodec.Decode(em, root, data.Dusk);
                if (dusk.Session.Phase != Phase.Deployment || dusk.Clock.Turn != day.Clock.Turn)
                    throw new InvalidDataException("黄昏节点无效。");
            }

            if (data.Manual != null)
            {
                var manual = SnapshotCodec.Decode(em, root, data.Manual);
                var manualDay = SnapshotCodec.Decode(em, root, data.ManualDay);
                if (manual.Session.Phase != Phase.Day || manualDay.Session.Phase != Phase.Day || manual.Clock.Turn != manualDay.Clock.Turn)
                    throw new InvalidDataException("手动保存与其原始白天节点不一致。");
            }
        }

        protected override void OnUpdate()
        {
            var root = WorldQueries.Root(EntityManager);
            if (root == Entity.Null)
                return;
            Observe(root);
            var recovery = EntityManager.GetComponentData<RecoveryState>(root);
            var priorIntel = recovery.KnownIntel;
            recovery.KnownIntel = math.clamp(math.max(recovery.KnownIntel, NightOps.Intelligence(EntityManager, root)), 0, 100);
            if (priorIntel != recovery.KnownIntel || !recovery.Equals(archive.Recovery))
                dirty = true;
            EntityManager.SetComponentData(root, recovery);
            if (EntityManager.GetComponentData<Session>(root).Phase == Phase.GameOver && recovery.AwaitingDecision == 0)
                PrepareRecovery(root);
            using var events = EntityManager.GetBuffer<GameEvent>(root).ToNativeArray(Allocator.Temp);
            bool hasDuskCheckpoint = false;
            foreach (var e in events)
                if (e.Kind == EventKind.DuskCheckpoint)
                {
                    hasDuskCheckpoint = true;
                    break;
                }
            // Entry preparation already ran in GameLoopSystem this frame. Keep the gate
            // closed and capture the dusk node on the next simulation update.
            bool postponeDusk = hasDuskCheckpoint && !duskCheckpointDeferred;
            duskCheckpointDeferred = postponeDusk;
            var pending = EntityManager.GetBuffer<GameEvent>(root);
            for (var i = pending.Length - 1; i >= 0; i--)
                if (IsRequest(pending[i].Kind) && !(postponeDusk && pending[i].Kind == EventKind.DuskCheckpoint))
                    pending.RemoveAt(i);
            foreach (var e in events)
            {
                if (!IsRequest(e.Kind) || postponeDusk && e.Kind == EventKind.DuskCheckpoint)
                    continue;
                try
                {
                    switch (e.Kind)
                    {
                        case EventKind.DayCheckpoint:
                            var state = EntityManager.GetComponentData<Session>(root);
                            var stateClock = EntityManager.GetComponentData<GameClock>(root);
                            if (state.Phase != Phase.Day)
                                break;
                            archive.Day = archive.Current = SnapshotCodec.Capture(EntityManager, root);
                            archive.Dusk = null;
                            var previous = EntityManager.GetComponentData<RecoveryState>(root);
                            if (previous.Turn != stateClock.Turn)
                                EntityManager.SetComponentData(root, new RecoveryState { Turn = stateClock.Turn });
                            dirty = true;
                            break;
                        case EventKind.DuskCheckpoint:
                            if (EntityManager.GetComponentData<Session>(root).Phase != Phase.Deployment)
                                break;
                            archive.Dusk = archive.Current = SnapshotCodec.Capture(EntityManager, root);
                            dirty = true;
                            break;
                        case EventKind.Save:
                            SaveManual(root, e.Amount, e.Message.ToString(), e.Target);
                            break;
                        case EventKind.Load:
                            LoadManual(root, e.Message.ToString(), e.Amount == 1);
                            break;
                        case EventKind.Retry:
                            Retry(root, e.Amount == 1);
                            break;
                        case EventKind.EndDynasty:
                            End(root);
                            break;
                    }
                }
                catch (Exception error)
                {
                    if (!dirty)
                    {
                        PersistenceGate statePersistence = EntityManager.GetComponentData<PersistenceGate>(root);
                        statePersistence.CheckpointPending = 0;
                        {
                            EntityManager.SetComponentData(root, statePersistence);
                        }
                    }

                    SimulationEvents.Emit(EntityManager, root, EventKind.Message, "节点操作未完成，请检查磁盘空间或 Console。", category: HistoryCategory.Important);
                    Debug.LogException(error);
                }
            }

            if (dirty && !postponeDusk && UnityEngine.Time.realtimeSinceStartupAsDouble >= nextWrite)
            {
                try
                {
                    Flush(root);
                }
                catch (Exception error)
                {
                    nextWrite = UnityEngine.Time.realtimeSinceStartupAsDouble + 10;
                    SimulationEvents.Emit(EntityManager, root, EventKind.Message, "记录写入失败，阶段已锁定；请检查磁盘空间。", category: HistoryCategory.Important);
                    Debug.LogException(error);
                }
            }
        }

        void PrepareRecovery(Entity root)
        {
            if (CourtOps.State(EntityManager, root).Extinction != 0)
            {
                var terminal = EntityManager.GetComponentData<RecoveryState>(root);
                terminal.Extinction = 1;
                terminal.AwaitingDecision = 1;
                EntityManager.SetComponentData(root, terminal);
                archive.Current = SnapshotCodec.Capture(EntityManager, root);
                dirty = true;
                return;
            }

            if (archive.Dusk == null)
                return; // Invalid/test-only core loss before a night has no dusk.
            var r = EntityManager.GetComponentData<RecoveryState>(root);
            r.LossCount = checked(r.LossCount + 1);
            r.AwaitingDecision = 1;
            // IO metadata: once per core loss, independent of rewindable gameplay randomness.
            var previousSeed = r.Seed;
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                var bytes = new byte[4];
                do
                {
                    rng.GetBytes(bytes);
                    r.Seed = BitConverter.ToUInt32(bytes, 0);
                }
                while (r.Seed == 0 || r.Seed == previousSeed);
            }

            EntityManager.SetComponentData(root, r);
            archive.Current = archive.Dusk;
            dirty = true;
            NightRuntimeState plannedSessionNight;
            RetryState plannedSessionRetry;
            SimulationRandomState plannedSessionRandom;
            NightPlanState plannedState;
            NightWave[] waves;
            SpawnRegion[] regions;
            using (var transaction = new RestoreTransaction(EntityManager, root))
            {
                SnapshotCodec.Rebuild(EntityManager, transaction.Root, SnapshotCodec.Decode(EntityManager, transaction.Root, archive.Dusk));
                ApplyRecovery(EntityManager, transaction.Root);
                {
                    plannedSessionNight = EntityManager.GetComponentData<NightRuntimeState>(transaction.Root);
                    plannedSessionRetry = EntityManager.GetComponentData<RetryState>(transaction.Root);
                    plannedSessionRandom = EntityManager.GetComponentData<SimulationRandomState>(transaction.Root);
                }

                plannedState = NightPlanOps.State(EntityManager, transaction.Root);
                using var waveArray = EntityManager.GetBuffer<NightWave>(transaction.Root).ToNativeArray(Allocator.Temp);
                using var regionArray = EntityManager.GetBuffer<SpawnRegion>(transaction.Root).ToNativeArray(Allocator.Temp);
                waves = waveArray.ToArray();
                regions = regionArray.ToArray();
            }

            NightRuntimeState liveNight = EntityManager.GetComponentData<NightRuntimeState>(root);
            RetryState liveRetry = EntityManager.GetComponentData<RetryState>(root);
            SimulationRandomState liveRandom = EntityManager.GetComponentData<SimulationRandomState>(root);
            liveRetry.Count = plannedSessionRetry.Count;
            liveRandom.State = plannedSessionRandom.State;
            liveNight.Seed = plannedSessionNight.Seed;
            {
                EntityManager.SetComponentData(root, liveNight);
                EntityManager.SetComponentData(root, liveRetry);
                EntityManager.SetComponentData(root, liveRandom);
            }

            EntityState.Set(EntityManager, root, plannedState);
            EntityManager.GetBuffer<NightWave>(root).CopyFrom(waves);
            EntityManager.GetBuffer<SpawnRegion>(root).CopyFrom(regions);
            EntityManager.GetBuffer<IntelGeometry>(root).Clear();
            IntelOps.Refresh(EntityManager, root);
        }

        static void ApplyRecovery(EntityManager manager, Entity root)
        {
            var r = manager.GetComponentData<RecoveryState>(root);
            if (r.LossCount == 0 || r.Extinction != 0)
                return;
            RetryState stateRetry = manager.GetComponentData<RetryState>(root);
            SimulationRandomState stateRandom = manager.GetComponentData<SimulationRandomState>(root); // A node captured after this recovery already owns its locked plan. Day edits and
            // continuing that node must not choose new targets/spawn positions behind the intel UI.
            if (stateRetry.Count == r.LossCount)
                return;
            stateRetry.Count = r.LossCount;
            stateRandom.State = r.Seed;
            {
                manager.SetComponentData(root, stateRetry);
                manager.SetComponentData(root, stateRandom);
            }

            NightOps.Plan(manager, root, true);
            PeacefulOps.Reset(manager, root);
            IntelOps.Refresh(manager, root);
        }

        public void Retry(Entity root, bool dusk)
        {
            Observe(root);
            if (EntityManager.GetComponentData<Session>(root).Phase != Phase.GameOver)
                return;
            if (CourtOps.State(EntityManager, root).Extinction != 0 || EntityManager.GetComponentData<RecoveryState>(root).Extinction != 0)
                throw new InvalidDataException("绝嗣王朝不能重试。");
            if (EntityManager.GetComponentData<RecoveryState>(root).AwaitingDecision == 0)
                PrepareRecovery(root);
            Flush(root); // Persist the ticket before restoring either node.
            var bytes = dusk ? archive.Dusk : archive.Day;
            if (bytes == null)
                throw new InvalidDataException("当前节点不可用。");
            byte[] current = null;
            SnapshotCodec.Restore(EntityManager, root, SnapshotCodec.Decode(EntityManager, root, bytes), (manager, candidate) =>
            {
                ApplyRecovery(manager, candidate);
                var r = manager.GetComponentData<RecoveryState>(candidate);
                r.AwaitingDecision = 0;
                manager.SetComponentData(candidate, r);
                current = SnapshotCodec.Capture(manager, candidate);
            });
            archive.Current = current;
            dirty = true;
            Flush(root);
        }

        public Action<RunArchiveStore, string, string> SlotSaved;
        public void ManageSlot(Entity root, string slot, string name, ulong stamp, bool delete)
        {
            var state = EntityManager.GetComponentData<Session>(root);
            PersistenceGate statePersistence = EntityManager.GetComponentData<PersistenceGate>(root);
            if (state.Phase != Phase.Day || statePersistence.CheckpointPending != 0)
                throw new InvalidDataException("只能在白天管理存档槽。");
            var run = EntityManager.GetComponentData<RunPersistence>(root).RunId.ToString();
            var store = Store ?? DefaultStore;
            if (delete)
                store.DeleteSlot(run, slot, stamp);
            else
                store.RenameSlot(run, slot, name, stamp);
        }

        void SaveManual(Entity root, int mode, string labelOrSlot, ulong expected)
        {
            if (EntityManager.GetComponentData<Session>(root).Phase != Phase.Day)
                throw new InvalidDataException("夜晚不能手动保存。");
            var saved = archive.Copy();
            saved.Manual = saved.Current = SnapshotCodec.Capture(EntityManager, root);
            saved.ManualDay = saved.Day;
            saved.Recovery = EntityManager.GetComponentData<RecoveryState>(root);
            var store = Store ?? DefaultStore;
            string slot;
            if (mode == 2)
            {
                store.OverwriteSlot(saved, labelOrSlot, expected);
                slot = labelOrSlot;
            }
            else
            {
                slot = store.SaveSlot(saved, mode == 1);
                if (mode == 1 && !string.IsNullOrWhiteSpace(labelOrSlot))
                    store.RenameSlot(saved.RunId, slot, labelOrSlot, store.SlotStamp(saved.RunId, slot));
            }

            archive = saved;
            var io = EntityManager.GetComponentData<RunPersistence>(root);
            io.Enabled = 1;
            EntityManager.SetComponentData(root, io);
            dirty = true;
            Flush(root);
            SimulationEvents.Emit(EntityManager, root, EventKind.Message, mode == 1 ? "已创建独立存档，并设为当前存档；回退节点独立保留" : "当前存档已快速保存；回退节点独立保留");
            try
            {
                SlotSaved?.Invoke(store, saved.RunId, slot);
            }
            catch (Exception error)
            {
                Debug.LogWarning("存档已保存，但缩略图未生成：" + error.Message);
            }
        }

        void LoadManual(Entity root, string slot, bool backupOnly = false)
        {
            if (EntityManager.GetComponentData<Session>(root).Phase != Phase.Day)
                throw new InvalidDataException("只能在白天载入手动保存点。");
            var io = EntityManager.GetComponentData<RunPersistence>(root);
            var store = Store ?? DefaultStore;
            var data = !string.IsNullOrEmpty(slot) ? backupOnly ? store.ReadBackup(io.RunId.ToString(), slot) : store.ReadSlot(io.RunId.ToString(), slot, out _) : io.Enabled != 0 ? store.Read(io.RunId.ToString(), out _) : archive.Copy();
            if (data.Manual == null)
                throw new InvalidDataException("本王朝尚无手动保存点。");
            data.Current = data.Manual;
            data.Day = data.ManualDay;
            data.Dusk = null;
            var manual = SnapshotCodec.Decode(EntityManager, root, data.Current);
            var turn = manual.Clock.Turn;
            if (data.Recovery.Turn != turn)
                data.Recovery = new RecoveryState
                {
                    Turn = turn
                };
            var knowledge = EntityManager.GetComponentData<RecoveryState>(root);
            if (data.RunId == io.RunId.ToString() && knowledge.Turn == turn && NightPlanOps.State(EntityManager, root).Event == manual.NightPlan.Event)
            {
                int highest = math.max(data.Recovery.KnownIntel, knowledge.KnownIntel);
                if (knowledge.LossCount > data.Recovery.LossCount)
                    data.Recovery = knowledge;
                data.Recovery.KnownIntel = highest;
            }

            Import(root, data, io.Enabled != 0);
            IntelOps.Refresh(EntityManager, root);
            archive.Current = SnapshotCodec.Capture(EntityManager, root);
            dirty = true;
            Flush(root);
            if (!string.IsNullOrEmpty(slot))
                store.SelectSlot(io.RunId.ToString(), slot);
            SimulationEvents.Emit(EntityManager, root, EventKind.Message, "白天进度已载入");
        }

        void Flush(Entity root)
        {
            archive.Recovery = EntityManager.GetComponentData<RecoveryState>(root);
            PersistenceGate statePersistence = EntityManager.GetComponentData<PersistenceGate>(root);
            statePersistence.CheckpointPending = 1;
            {
                EntityManager.SetComponentData(root, statePersistence);
            }

            dirty = true;
            if (EntityManager.GetComponentData<RunPersistence>(root).Enabled != 0)
                (Store ?? DefaultStore).Write(archive);
            dirty = false;
            statePersistence.CheckpointPending = 0;
            {
                EntityManager.SetComponentData(root, statePersistence);
            }
        }

        void End(Entity root)
        {
            var state = EntityManager.GetComponentData<Session>(root);
            GameClock stateClock = EntityManager.GetComponentData<GameClock>(root);
            PersistenceGate statePersistence = EntityManager.GetComponentData<PersistenceGate>(root);
            DynastyIdentity stateDynasty = EntityManager.GetComponentData<DynastyIdentity>(root);
            if (state.Phase != Phase.GameOver)
                return;
            var io = EntityManager.GetComponentData<RunPersistence>(root);
            if (io.Enabled != 0 || Store != null)
                (Store ?? DefaultStore).End(io.RunId.ToString(), stateDynasty.Name.ToString(), stateClock.Turn, CourtOps.State(EntityManager, root).Extinction != 0 ? "王朝绝嗣" : "聚落核心失守");
            dirty = false;
            state.Phase = Phase.Ended;
            statePersistence.CheckpointPending = 0;
            {
                EntityManager.SetComponentData(root, state);
                EntityManager.SetComponentData(root, stateClock);
                EntityManager.SetComponentData(root, statePersistence);
                EntityManager.SetComponentData(root, stateDynasty);
            }

            SimulationEvents.Emit(EntityManager, root, EventKind.Message, "本王朝存档已永久删除，覆灭记录已保留");
        }

        static bool IsRequest(EventKind kind) => kind == EventKind.Save || kind == EventKind.Load || kind == EventKind.Retry || kind == EventKind.EndDynasty || kind == EventKind.DayCheckpoint || kind == EventKind.DuskCheckpoint;
        public static void AtomicWrite(string path, byte[] bytes) => new RunArchiveStore(Path.GetDirectoryName(path)).AtomicWrite(path, bytes);
    }
}
