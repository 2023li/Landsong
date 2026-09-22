#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    public struct FailingHistoryNameRequest : IGameRequest, IHistoryNamedRequest
    {
        public CommandKind Kind => CommandKind.Pause;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => throw new InvalidOperationException("injected name failure");
    }

    public static class GameplayRequestVerification
    {
        static StringBuilder report;
        static int assertions;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            assertions++;
            report.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verification/Typed gameplay requests")]
        public static string Run()
        {
            report = new StringBuilder();
            assertions = 0;
            try
            {
                OrderingAndOwnership();
                Failures();
                RestoreOwnership();
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/gameplay-requests-verification.txt", report.ToString());
            }
        }

        static Entity Root(EntityManager em)
        {
            var root = em.CreateEntity();
            em.AddComponentData(root, new Session { Initialized = 1, Phase = Phase.Ended });
            em.AddComponentData(root, new GameClock());
            em.AddComponentData(root, new SimulationControl());
            em.AddComponentData(root, new NightRuntimeState());
            em.AddComponentData(root, new IntelligenceModeState());
            em.AddComponentData(root, new PersistenceGate());
            em.AddBuffer<QueuedGameplayRequest>(root);
            em.AddBuffer<GameEvent>(root);
            HistoryOps.Ensure(em, root);
            return root;
        }

        static QueuedGameplayRequest[] Queue(EntityManager em, Entity root)
        {
            using var values = em.GetBuffer<QueuedGameplayRequest>(root).ToNativeArray(Allocator.Temp);
            return values.ToArray();
        }

        static int Payloads(EntityManager em)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<SimulationOwner>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
            return query.CalculateEntityCount();
        }

        static void AddPair(EntityManager em, Entity root)
        {
            GameplayRequests.Enqueue(em, root, new RenameSoldierRequest { Soldier = 71, Name = "request soldier" }, 23);
            GameplayRequests.Enqueue(em, root, new WakeHeroRequest { Sanctum = 92 }, 11);
        }

        static void OrderingAndOwnership()
        {
            using var world = new World("Typed request ordering");
            var em = world.EntityManager;
            var root = Root(em);
            AddPair(em, root);
            var queue = Queue(em, root);
            Check(queue.Length == 2 && queue[0].RequestId == 23 && queue[1].RequestId == 11, "Different payload types retain enqueue order independently of request identifier");
            Check(queue[0].Target == 71 && queue[1].Target == 92 && queue[0].Kind == CommandKind.RenameSoldier && queue[1].Kind == CommandKind.WakeHero, "Queue headers carry each typed request's target and operation");
            Check(em.HasComponent<RenameSoldierRequest>(queue[0].Payload) && !em.HasComponent<WakeHeroRequest>(queue[0].Payload) && em.HasComponent<WakeHeroRequest>(queue[1].Payload) && !em.HasComponent<RenameSoldierRequest>(queue[1].Payload), "Payload entities contain only their concrete operation component");
            Check(em.GetComponentData<RenameSoldierRequest>(queue[0].Payload).Name.ToString() == "request soldier" && queue.All(item => em.GetComponentData<SimulationOwner>(item.Payload).Root == root), "Typed data and session ownership survive enqueue unchanged");
            GameplayRequestProcessing.Drain(em, root);
            var events = em.GetBuffer<GameEvent>(root);
            Check(events.Length == 2 && events[0].RequestId == 23 && events[1].RequestId == 11 && events[0].Target == 71 && events[1].Target == 92, "Actual processing publishes results in deterministic enqueue order");
            Check(events[0].Result == ResultCode.WrongPhase && events[1].Result == ResultCode.WrongPhase, "Phase rejection is reported for both concrete requests");
            Check(Queue(em, root).Length == 0 && queue.All(item => !em.Exists(item.Payload)) && Payloads(em) == 0, "Rejected requests release every payload and empty the queue");
            AddPair(em, root);
            queue = Queue(em, root);
            GameplayRequests.Clear(em, root);
            Check(Queue(em, root).Length == 0 && queue.All(item => !em.Exists(item.Payload)), "Explicit queue clear destroys both typed payloads");
            GameplayRequests.Clear(em, root);
            Check(Payloads(em) == 0, "Repeated clear is idempotent");
        }

        static void Failures()
        {
            using var world = new World("Typed request failures");
            var em = world.EntityManager;
            var root = Root(em);
            bool failed = false;
            try
            {
                GameplayRequests.Enqueue(em, root, new FailingHistoryNameRequest());
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed && Queue(em, root).Length == 0 && Payloads(em) == 0, "A failure after payload allocation during enqueue leaves neither payload nor queue entry");
            failed = false;
            try
            {
                GameRequestExecution.Execute(em, root, new FailingHistoryNameRequest());
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed && Payloads(em) == 0, "Immediate typed execution releases its payload when history naming throws");
            Check(GameRequestExecution.Execute(em, root, new RenameSoldierRequest { Soldier = 71 }) == ResultCode.WrongPhase && Payloads(em) == 0, "Immediate typed execution releases rejected payloads");
            var state = em.GetComponentData<Session>(root);
            state.Phase = Phase.Day;
            em.SetComponentData(root, state);
            AddPair(em, root);
            var queued = Queue(em, root);
            em.RemoveComponent<PersistenceGate>(root);
            failed = false;
            try
            {
                GameplayRequestProcessing.Drain(em, root);
            }
            catch (ArgumentException)
            {
                failed = true;
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            Check(failed, "A missing required session component propagates the execution failure");
            Check(queued.All(item => !em.Exists(item.Payload)) && Queue(em, root).Length == 0 && Payloads(em) == 0, "Processing failure releases the failing payload and all unprocessed payloads in finally");
            Check(em.GetBuffer<GameEvent>(root).Length == 0, "Failed processing does not publish fabricated command results");
        }

        static void RestoreOwnership()
        {
            using var world = new World("Typed request restore ownership");
            var em = world.EntityManager;
            var root = Root(em);
            AddPair(em, root);
            var original = Queue(em, root);
            using (var transaction = new RestoreTransaction(em, root))
            {
                Check(Queue(em, transaction.Root).Length == 0 && Queue(em, root).SequenceEqual(original), "Restore candidate starts with an empty queue while the live queue retains its entries");
                Check(original.All(item => em.Exists(item.Payload) && em.HasComponent<Disabled>(item.Payload)), "Restore staging hides original payloads without destroying them");
                GameplayRequests.Clear(em, transaction.Root);
                Check(original.All(item => em.Exists(item.Payload)), "Clearing the candidate queue cannot destroy original payloads");
            }

            Check(Queue(em, root).SequenceEqual(original) && original.All(item => em.Exists(item.Payload) && !em.HasComponent<Disabled>(item.Payload)), "Cancelled restore restores exact queue entries and original payload handles");
            bool failed = false;
            try
            {
                using var transaction = new RestoreTransaction(em, root);
                transaction.Commit(stage =>
                {
                    if (stage == "root-published")
                        throw new IOException("injected publication failure");
                });
            }
            catch (IOException)
            {
                failed = true;
            }

            Check(failed && Queue(em, root).SequenceEqual(original) && original.All(item => em.Exists(item.Payload) && !em.HasComponent<Disabled>(item.Payload)), "Publication rollback retains the live queue and original typed payloads");
            using (var transaction = new RestoreTransaction(em, root))
                transaction.Commit();
            Check(Queue(em, root).Length == 0 && original.All(item => !em.Exists(item.Payload)) && Payloads(em) == 0, "Successful restore retires original payloads only at commit");
        }
    }
}
#endif
