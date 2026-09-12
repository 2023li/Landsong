#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class SessionBoundaryVerification
    {
        static StringBuilder log;
        static int assertions;

        static void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException("FAIL " + label);
            assertions++;
            log.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verification/Session boundaries")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            log = new StringBuilder(); assertions = 0;
            try
            {
                Lifetime();
                foreach (var path in new[]
                {
                    "Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity",
                    "Assets/Landsong/Scenes/EntityMaps/Map_Test01_Entities.unity"
                }) QuestEntry(path);
                log.AppendLine("Assertions: " + assertions);
                return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/session-boundaries-verification.txt", log.ToString());
            }
        }

        static void Lifetime()
        {
            using var world = new World("Session boundary ownership");
            var em = world.EntityManager;
            Check(SimulationLifetimeSystem.IsReleased(em), "Empty isolated world has released its session");
            var prefab = em.CreateEntity(typeof(Prefab), typeof(Session), typeof(SimulationOwner));
            Check(SimulationLifetimeSystem.IsReleased(em), "Baked templates do not count as live session ownership");
            var root = em.CreateEntity(typeof(Session));
            Entity Owned(Entity owner, bool disabled)
            {
                var e = em.CreateEntity(typeof(SimulationOwner), typeof(Persistent));
                em.SetComponentData(e, new SimulationOwner { Root = owner });
                if (disabled) em.AddComponent<Disabled>(e);
                return e;
            }

            var active = Owned(root, false);
            var disabled = Owned(root, true);
            var nullOwner = Owned(Entity.Null, true);
            var retiredRoot = em.CreateEntity(typeof(Session));
            var retiredOwner = Owned(retiredRoot, true);
            em.DestroyEntity(retiredRoot);
            var other = em.CreateEntity();
            var invalidOwner = Owned(other, false);
            SimulationLifetimeSystem.Cleanup(em);
            Check(!em.Exists(nullOwner) && !em.Exists(retiredOwner) && !em.Exists(invalidOwner),
                "Cleanup removes null, destroyed and non-session owners including disabled instances");
            Check(em.Exists(active) && em.Exists(disabled) && em.Exists(prefab),
                "Cleanup retains valid active and disabled instances and baked templates");

            em.AddComponent<Disabled>(root);
            SimulationLifetimeSystem.Cleanup(em);
            Check(!SimulationLifetimeSystem.IsReleased(em) && em.Exists(active) && em.Exists(disabled),
                "A disabled Session remains a valid owner and blocks release");
            em.RemoveComponent<Disabled>(root);
            using (var transaction = new RestoreTransaction(em, root))
            {
                Check(em.HasComponent<Disabled>(root) && Sim.Root(em) == transaction.Root,
                    "Restore stages a candidate while hiding the original root");
                SimulationLifetimeSystem.Cleanup(em);
                Check(em.Exists(root) && em.Exists(active) && em.Exists(disabled) && !SimulationLifetimeSystem.IsReleased(em),
                    "Ownership cleanup does not delete the originals isolated by synchronous restore");
            }
            Check(Sim.Root(em) == root && em.Exists(active) && em.Exists(disabled) && em.HasComponent<Disabled>(disabled),
                "Aborted staging restores original visibility and preserves pre-existing disabled state");
            em.DestroyEntity(root);
            Check(!SimulationLifetimeSystem.IsReleased(em), "Owned orphans block release after the root is destroyed");
            SimulationLifetimeSystem.Cleanup(em);
            Check(!em.Exists(active) && !em.Exists(disabled) && SimulationLifetimeSystem.IsReleased(em),
                "All active and disabled orphans are released after map ownership ends");

            var disabledRoot = em.CreateEntity(typeof(Session), typeof(Disabled));
            Check(!SimulationLifetimeSystem.IsReleased(em), "A standalone disabled root cannot evade the release audit");
            em.DestroyEntity(disabledRoot);
            var disabledOrphan = Owned(Entity.Null, true);
            Check(!SimulationLifetimeSystem.IsReleased(em), "A standalone disabled orphan cannot evade the release audit");
            SimulationLifetimeSystem.Cleanup(em);
            Check(!em.Exists(disabledOrphan) && SimulationLifetimeSystem.IsReleased(em),
                "Cleanup makes the disabled-only orphan world releasable");
        }

        static T[] Buffer<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(root)) return Array.Empty<T>();
            using var values = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return values.ToArray();
        }

        static Entity[] Entities(EntityManager em)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            { Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab });
            using var values = query.ToEntityArray(Allocator.Temp);
            return values.ToArray().OrderBy(e => e.Index).ThenBy(e => e.Version).ToArray();
        }

        static void QuestEntry(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Session boundary quest preview", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager; var root = Sim.Root(em);
                GameLoopSystem.Initialize(em, root);
                var core = Entity.Null;
                using (var buildings = Sim.OrderedEntities<Building>(em))
                    foreach (var e in buildings)
                        if (em.GetComponentData<BuildingStats>(e).IsCore != 0) { core = e; break; }
                Check(core != Entity.Null, "Fixture has an authored core");
                ulong coreId = em.GetComponentData<Identity>(core).Id;
                int definition = Sim.FindDefinition(em, root, "random_supply_wood");
                Check(definition >= 0, "Fixture uses a registered ordinary quest with a real failure penalty");
                var costs = ProgressionOps.FailureCosts(em, root, definition);
                Check(costs.Count == 1 && costs[0].Amount > 0, "Fixture penalty is a single positive resource charge");
                var penalty = costs[0];
                int missing = Math.Max(0, penalty.Amount + 100 - InventoryOps.Count(em, root, penalty.Item));
                Check(InventoryOps.Add(em, root, penalty.Item, missing) == missing, "Fixture funds the complete penalty in ordinary storage");
                var strictImport = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                var importedQuest = Array.Find(strictImport.Records, r => (r.Mask & 16) != 0
                    && (r.Quest.Status == QuestStatus.Active || r.Quest.Status == QuestStatus.Completed));
                Check(importedQuest != null, "Strict import fixture contains an existing bound quest");
                var invalidImportQuest = importedQuest.Quest; invalidImportQuest.ContainerSlot = int.MaxValue;
                importedQuest.Quest = invalidImportQuest;

                // Model a previously valid assignment whose capacity has disappeared, without
                // executing another command that would already reconcile the fixture's task.
                var quest = ProgressionOps.CreateQuest(em, root, definition, coreId);
                var questState = em.GetComponentData<Quest>(quest);
                questState.Status = QuestStatus.Active; questState.Container = coreId;
                questState.ContainerSlot = em.GetComponentData<BuildingStats>(core).QuestCapacity + 100;
                questState.StartTurn = em.GetComponentData<Session>(root).Turn;
                questState.Deadline = questState.StartTurn + Sim.Definition(em, root, definition).Duration;
                em.SetComponentData(quest, questState);
                ulong questId = em.GetComponentData<Identity>(quest).Id;
                var state = em.GetComponentData<Session>(root); state.BasePopulation += 10; em.SetComponentData(root, state);
                var soldier = Sim.Spawn(em, root, Sim.FirstDefinition(em, root, ContentKind.Soldier), Sim.Position(em, core), true);
                MilitaryOps.ConfigureCombatant(em, root, soldier, 0, false, false, 0, Sim.Position(em, core));
                Sim.Set(em, soldier, new Soldier { PopulationCost = 1 });
                MilitaryOps.InitializePerson(em, root, soldier);
                ulong soldierId = em.GetComponentData<Identity>(soldier).Id;

                var before = SnapshotCodec.Capture(em, root);
                var originalEntities = Entities(em);
                var originalEvents = Buffer<GameEvent>(em, root);
                var originalHistory = Buffer<HistoryEntry>(em, root);
                void Unchanged(byte[] snapshot, string label)
                {
                    Check(snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)) && em.Exists(quest)
                        && questState.Equals(em.GetComponentData<Quest>(quest)), label + " preserves live task, penalty resources, RNG and day");
                    Check(originalEntities.SequenceEqual(Entities(em)) && originalEvents.SequenceEqual(Buffer<GameEvent>(em, root))
                        && originalHistory.SequenceEqual(Buffer<HistoryEntry>(em, root)), label + " preserves entity handles, events and history");
                }
                void Reject(Action action, string label)
                {
                    bool rejected = false;
                    try { action(); } catch (InvalidDataException) { rejected = true; }
                    Check(rejected, label);
                }
                Reject(() => SnapshotCodec.Decode(em, root, before), "External Decode still rejects an out-of-capacity bound quest");
                Reject(() => SnapshotCodec.Restore(em, root, strictImport), "External Restore still rejects an invalid container in a caller DTO");
                Unchanged(before, "Strict archive rejection");

                var corruptProgress = questState; corruptProgress.StartTurn = em.GetComponentData<Session>(root).Turn + 1;
                em.SetComponentData(quest, corruptProgress);
                var corruptBytes = SnapshotCodec.Capture(em, root);
                try
                {
                    Reject(() => NightEntryOps.Begin(em, root, false, 0), "Internal staging does not defer invalid quest chronology");
                    Check(corruptBytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected unrelated quest data remains untouched");
                }
                finally { em.SetComponentData(quest, questState); }
                Unchanged(before, "Internal chronology rejection");

                var otherQuest = Sim.Find(em, importedQuest.Identity.Id);
                var otherState = em.GetComponentData<Quest>(otherQuest);
                var duplicate = otherState; duplicate.Container = questState.Container; duplicate.ContainerSlot = questState.ContainerSlot;
                em.SetComponentData(otherQuest, duplicate);
                try
                {
                    Reject(() => NightEntryOps.Begin(em, root, false, 0), "Internal staging still rejects duplicate ownership of a deferred invalid slot");
                }
                finally { em.SetComponentData(otherQuest, otherState); }
                Unchanged(before, "Internal duplicate-slot rejection");
                foreach (var failure in new[] { "quest-containers-reconciled", "day-settled" })
                {
                    bool reached = false;
                    int stock = InventoryOps.Count(em, root, penalty.Item);
                    try
                    {
                        NightEntryOps.Begin(em, root, false, 0, stage =>
                        {
                            if (stage == "quest-containers-reconciled")
                            {
                                var candidate = Sim.Root(em);
                                Check(candidate != root && Sim.Find(em, questId) == Entity.Null
                                    && InventoryOps.Count(em, candidate, penalty.Item) == stock - penalty.Amount,
                                    "Invalid task and its penalty are reconciled only on the staged candidate");
                            }
                            if (stage == failure) { reached = true; throw new IOException("Injected " + failure); }
                        });
                    }
                    catch (IOException) { }
                    Check(reached, "Fault injection reaches " + failure);
                    Unchanged(before, failure);
                }

                Check(NightEntryOps.Begin(em, root, false, 0) == ResultCode.ConfirmationRequired,
                    "A pending soldier forces review after candidate task reconciliation");
                Check(Buffer<NightEntryLoss>(em, root).Any(loss => loss.Soldier == soldierId),
                    "Review names the actual pending soldier");
                Unchanged(before, "Unconfirmed entry/cancel");
                var review = em.GetComponentData<NightEntryReview>(root);
                Check(review.Fingerprint.ToString() == EconomyForecastOps.Fingerprint(em, root),
                    "Review fingerprint describes the unchanged live day before task reconciliation");

                Check(InventoryOps.Add(em, root, penalty.Item, 1) == 1, "Fixture changes the reviewed resource state");
                var changed = SnapshotCodec.Capture(em, root);
                Check(NightEntryOps.Begin(em, root, true, review.Token) == ResultCode.ConfirmationRequired,
                    "A stale confirmation cannot commit task removal or its penalty");
                Unchanged(changed, "Stale confirmation");
                var refreshed = em.GetComponentData<NightEntryReview>(root);
                Check(refreshed.Token != review.Token && refreshed.Fingerprint.ToString() == EconomyForecastOps.Fingerprint(em, root),
                    "Changed state receives a fresh consent token and matching live fingerprint");

                Check(NightEntryOps.Begin(em, root, true, refreshed.Token) == ResultCode.Success,
                    "Exact consent commits the candidate day");
                Check(Sim.Find(em, questId) == Entity.Null && Sim.Find(em, soldierId) == Entity.Null
                    && em.GetComponentData<Session>(root).Phase == Phase.Deployment,
                    "Successful commit removes the invalid task and approved pending soldier");
                int PenaltyEvents() => Buffer<GameEvent>(em, root).Count(e => e.Kind == EventKind.Message
                    && e.Target == questId && e.Definition == penalty.Item && e.Amount == penalty.Amount);
                Check(PenaltyEvents() == 1, "Only the committed candidate publishes the task penalty once");
                var committed = SnapshotCodec.Capture(em, root);
                Check(NightEntryOps.Begin(em, root, true, refreshed.Token) == ResultCode.WrongPhase
                    && committed.SequenceEqual(SnapshotCodec.Capture(em, root)) && PenaltyEvents() == 1,
                    "Repeated confirmation cannot charge the committed task again");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
