#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class ResearchAuthorityVerification
    {
        static StringBuilder log;
        static int assertions;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            assertions++;
            log.AppendLine("PASS " + label);
        }

        static void Reject(Action action, string label)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (InvalidDataException)
            {
                rejected = true;
            }

            Check(rejected, label);
        }

        static T[] Rows<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        [MenuItem("Landsong/ECS/Verification/Research authority")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode first.");
            log = new StringBuilder();
            assertions = 0;
            try
            {
                Domain();
                foreach (var path in Landsong.EditorTools.GameMapPaths.BakedScenes())
                    Archive(path);
                log.AppendLine("Assertions: " + assertions);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/research-authority-verification.txt", log.ToString());
            }
        }

        static TechnologyId First => TechnologyId.FromIndex(0);
        static TechnologyId Repeat => TechnologyId.FromIndex(1);
        static TechnologyId Dependent => TechnologyId.FromIndex(2);

        static string Facts(EntityManager em, Entity root) => string.Join(";", Rows<BlueprintUnlock>(em, root).Select(row => "building:" + row.Building.Index + ":" + row.MaximumLevel).Concat(Rows<OwnedBuff>(em, root).Select(row => "buff:" + row.Buff.Index + ":" + row.Level)).Concat(Rows<UnlockedFeature>(em, root).Select(row => "feature:" + row.Feature.Index)));
        static void Domain()
        {
            using var fixture = new ResearchTestFixture(authority: true);
            using var world = new World("Research authority isolated domain");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            try
            {
                fixture.Install(em, root);
                {
                    em.AddComponentData(root, new Session { Phase = Phase.Day });
                    em.AddComponentData(root, new GameClock() { Turn = 1 });
                    em.AddComponentData(root, new SimulationControl() { });
                    em.AddComponentData(root, new PopulationState() { });
                    em.AddComponentData(root, new PublicOpinionState() { });
                    em.AddComponentData(root, new ResearchState() { Points = 5 });
                    em.AddComponentData(root, new ExpeditionPenaltyState() { });
                    em.AddComponentData(root, new NightRuntimeState() { });
                    em.AddComponentData(root, new DaySettlementState() { });
                    em.AddComponentData(root, new RetryState() { });
                    em.AddComponentData(root, new HeroSelection() { });
                    em.AddComponentData(root, new BellState() { });
                    em.AddComponentData(root, new IntelligenceModeState() { });
                    em.AddComponentData(root, new PersistenceGate() { });
                    em.AddComponentData(root, new SimulationRandomState() { State = 1234 });
                    em.AddComponentData(root, new IdentitySequence() { });
                    em.AddComponentData(root, new DynastyIdentity() { });
                }

                em.AddBuffer<TechnologyProgress>(root);
                ResearchTestFixture.AddFacts(em, root);
                em.AddBuffer<InventorySlot>(root);
                em.AddBuffer<PendingItem>(root);
                em.AddBuffer<GameEvent>(root);
                em.AddBuffer<BattleReportEntry>(root);
                em.AddBuffer<EconomyEntry>(root);
                em.AddBuffer<HistoryEntry>(root);
                em.AddComponentData(root, new EconomyJournalState { Turn = 1, Recording = 1 });
                QueryPurity(em, root);
                Gates(em, root);
                Rewards(em, root);
                ImportRules(em, root);
            }
            finally
            {
            }
        }

        static void Reset(EntityManager em, Entity root, TechnologyId? technology = null, int progress = 0, int points = 5)
        {
            {
                em.SetComponentData(root, new Session { Phase = Phase.Day });
                EntityState.Set(em, root, new GameClock() { Turn = 1 });
                EntityState.Set(em, root, new SimulationControl() { });
                EntityState.Set(em, root, new PopulationState() { });
                EntityState.Set(em, root, new PublicOpinionState() { });
                EntityState.Set(em, root, new ResearchState() { Points = points });
                EntityState.Set(em, root, new ExpeditionPenaltyState() { });
                EntityState.Set(em, root, new NightRuntimeState() { });
                EntityState.Set(em, root, new DaySettlementState() { });
                EntityState.Set(em, root, new RetryState() { });
                EntityState.Set(em, root, new HeroSelection() { });
                EntityState.Set(em, root, new BellState() { });
                EntityState.Set(em, root, new IntelligenceModeState() { });
                EntityState.Set(em, root, new PersistenceGate() { });
                EntityState.Set(em, root, new SimulationRandomState() { State = 1234 });
                EntityState.Set(em, root, new IdentitySequence() { });
                EntityState.Set(em, root, new DynastyIdentity() { });
            }

            em.SetComponentData(root, new EconomyJournalState { Turn = 1, Recording = 1 });
            em.GetBuffer<TechnologyProgress>(root).Clear();
            if ((technology ?? First).IsValid)
                em.GetBuffer<TechnologyProgress>(root).Add(new TechnologyProgress { Technology = technology ?? First, ResearchPoints = progress, QueueOrder = 1 });
            em.GetBuffer<BlueprintUnlock>(root).Clear();
            em.GetBuffer<OwnedBuff>(root).Clear();
            em.GetBuffer<UnlockedFeature>(root).Clear();
            FeatureUnlocks.Unlock(em, root, ResearchTestFixture.AccessId);
            em.GetBuffer<InventorySlot>(root).Clear();
            em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Index = 0, Item = default, SlotType = default });
            em.GetBuffer<PendingItem>(root).Clear();
            em.GetBuffer<GameEvent>(root).Clear();
            em.GetBuffer<ResearchCompletedEvent>(root).Clear();
            em.GetBuffer<EconomyEntry>(root).Clear();
            em.GetBuffer<HistoryEntry>(root).Clear();
            em.GetBuffer<BattleReportEntry>(root).Clear();
        }

        static void QueryPurity(EntityManager em, Entity root)
        {
            var session = em.GetComponentData<Session>(root);
            using var types = em.GetComponentTypes(root, Allocator.Temp);
            string stamp = ResearchOps.Fingerprint(em, root);
            Check(!ResearchOps.Unlocked(em, root), "Feature starts locked in an explicitly configured catalog");
            for (int i = 0; i < 3; i++)
            {
                Check(ResearchOps.Entry(em, root, First).Completions == 0 && ResearchOps.Completed(em, root, First) == 0, "Missing row is a read-only incomplete value " + i);
                Check(ResearchOps.Queue(em, root).Count == 0 && ResearchOps.Quote(em, root, First).CanQueue == ResultCode.Unavailable, "Viewing an empty plan does not unlock research " + i);
                Check(ResearchOps.Path(em, root, Dependent).Definitions.SequenceEqual(new[] { First, Dependent }), "Path preview can describe a locked feature without authoring state " + i);
            }

            using var afterTypes = em.GetComponentTypes(root, Allocator.Temp);
            Check(types.ToArray().SequenceEqual(afterTypes.ToArray()) && session.Equals(em.GetComponentData<Session>(root)) && stamp == ResearchOps.Fingerprint(em, root) && Rows<TechnologyProgress>(em, root).Length == 0 && Facts(em, root).Length == 0 && Rows<GameEvent>(em, root).Length == 0 && Rows<HistoryEntry>(em, root).Length == 0, "Query family creates no rows, components, permits or events");
            FeatureUnlocks.Unlock(em, root, ResearchTestFixture.AccessId);
            stamp = ResearchOps.Fingerprint(em, root);
            em.GetBuffer<BlueprintUnlock>(root).Add(new BlueprintUnlock { Building = ResearchTestFixture.BuildingId, MaximumLevel = 1 });
            Check(ResearchOps.Completed(em, root, First) == 0 && !ResearchOps.Quote(em, root, Dependent).PrerequisitesMet, "An unrelated blueprint with the same domain-local index cannot become research completion");
            var blueprintStamp = ResearchOps.Fingerprint(em, root);
            Check(stamp != blueprintStamp, "A changed blueprint reward preview invalidates previous research-plan consent");
            em.GetBuffer<BlueprintUnlock>(root).Clear();
            em.GetBuffer<OwnedBuff>(root).Add(new OwnedBuff { Buff = ResearchTestFixture.BuffId, Level = 1 });
            Check(blueprintStamp != ResearchOps.Fingerprint(em, root), "Equal local indices in different reward domains cannot alias research-plan consent");
        }

        static void Gates(EntityManager em, Entity root)
        {
            foreach (string gate in new[]
            {
                "paused",
                "intelligence",
                "checkpoint",
                "night",
                "deployment",
                "ended",
                "feature"
            }

            )
            {
                Reset(em, root, progress: 2, points: 3);
                var state = em.GetComponentData<Session>(root);
                SimulationControl stateControl = em.GetComponentData<SimulationControl>(root);
                IntelligenceModeState stateIntelligenceMode = em.GetComponentData<IntelligenceModeState>(root);
                PersistenceGate statePersistence = em.GetComponentData<PersistenceGate>(root);
                var expected = ResultCode.Busy;
                switch (gate)
                {
                    case "paused":
                        stateControl.Paused = 1;
                        break;
                    case "intelligence":
                        stateIntelligenceMode.Enabled = 1;
                        break;
                    case "checkpoint":
                        statePersistence.CheckpointPending = 1;
                        break;
                    case "night":
                        state.Phase = Phase.Night;
                        expected = ResultCode.WrongPhase;
                        break;
                    case "deployment":
                        state.Phase = Phase.Deployment;
                        expected = ResultCode.WrongPhase;
                        break;
                    case "ended":
                        state.Phase = Phase.Ended;
                        expected = ResultCode.WrongPhase;
                        break;
                    case "feature":
                        em.GetBuffer<BlueprintUnlock>(root).Clear();
                        em.GetBuffer<OwnedBuff>(root).Clear();
                        em.GetBuffer<UnlockedFeature>(root).Clear();
                        expected = ResultCode.Unavailable;
                        break;
                }

                {
                    em.SetComponentData(root, state);
                    em.SetComponentData(root, stateControl);
                    em.SetComponentData(root, stateIntelligenceMode);
                    em.SetComponentData(root, statePersistence);
                }

                var entries = Rows<TechnologyProgress>(em, root);
                var grants = Facts(em, root);
                Check(!ResearchOps.Quote(em, root, Repeat).Editable && ResearchOps.Editable(em, root) == expected, "Domain quote states external gate: " + gate);
                Check(ResearchOps.Command(em, root, Repeat, false) == expected && ResearchOps.Command(em, root, First, true) == expected && ResearchOps.Plan(em, root, Dependent) == expected, "Queue, cancellation and path all enforce gate: " + gate);
                ResearchOps.Settle(em, root);
                Check(state.Equals(em.GetComponentData<Session>(root)) && entries.SequenceEqual(Rows<TechnologyProgress>(em, root)) && grants == Facts(em, root) && Rows<GameEvent>(em, root).Length == 0 && Rows<HistoryEntry>(em, root).Length == 0 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 0, "Rejected operations and settlement are side-effect free: " + gate);
            }

            foreach (var phase in new[]
            {
                Phase.Day,
                Phase.Settlement
            }

            )
            {
                Reset(em, root);
                var state = em.GetComponentData<Session>(root);
                state.Phase = phase;
                em.SetComponentData(root, state);
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, First) == 1 && em.GetComponentData<ResearchState>(root).Points == 0, "Internal research settlement remains legal in " + phase);
            }
        }

        static void Rewards(EntityManager em, Entity root)
        {
            foreach (int failure in new[]
            {
                1,
                4,
                5
            }

            )
            {
                Reset(em, root);
                var grants = Facts(em, root);
                var inventory = Rows<InventorySlot>(em, root);
                bool threw = false;
                try
                {
                    ResearchOps.Settle(em, root, at =>
                    {
                        if (at == failure)
                            throw new InvalidOperationException("Research reward probe");
                    });
                }
                catch (InvalidOperationException error)
                {
                    threw = error.Message == "Research reward probe";
                }

                var held = ResearchOps.Entry(em, root, First);
                Check(threw && held.ResearchPoints == 5 && held.Completions == 0 && held.QueueOrder == 1 && em.GetComponentData<ResearchState>(root).Points == 0, "Reward failure keeps already-paid full progress without publishing completion: " + failure);
                Check(grants == Facts(em, root) && inventory.SequenceEqual(Rows<InventorySlot>(em, root)) && Rows<PendingItem>(em, root).Length == 0 && Rows<GameEvent>(em, root).Length == 0 && Rows<HistoryEntry>(em, root).Length == 0 && Rows<EconomyEntry>(em, root).Length == 0, "First item, final award and post-completion faults restore every reward effect: " + failure);
                ResearchOps.Settle(em, root);
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, First) == 1 && Rows<TechnologyProgress>(em, root).Count(row => row.Technology == First) == 1 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 2 && BuildingBlueprints.Has(em, root, ResearchTestFixture.BuildingId, 2) && (PermanentBuffs.Level(em, root, ResearchTestFixture.BuffId) > 0) && FeatureUnlocks.Has(em, root, ResearchTestFixture.RewardFeatureId) && em.GetComponentData<ResearchState>(root).Points == 0, "Retry publishes complete reward batch and research record exactly once: " + failure);
            }

            Reset(em, root);
            em.GetBuffer<InventorySlot>(root).CopyFrom(new[] { new InventorySlot { Provider = 1, Index = 0, Item = ResearchTestFixture.CoinId, SlotType = default, Count = 9 } });
            ResearchOps.Settle(em, root);
            Check(ResearchOps.Entry(em, root, First).ResearchPoints == 5 && ResearchOps.Completed(em, root, First) == 0 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 9 && !BuildingBlueprints.Has(em, root, ResearchTestFixture.BuildingId) && ResearchOps.Quote(em, root, First).Status == ResearchStatus.AwaitingRewards, "Partial inventory capacity keeps paid progress while rejecting all first-completion rewards");
            Reset(em, root, technology: Repeat, points: 3);
            ResearchOps.Settle(em, root);
            Check(ResearchOps.Completed(em, root, Repeat) == 1 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 1, "Repeatable technology issues its first reward once");
            ResearchState stateResearchState = em.GetComponentData<ResearchState>(root);
            stateResearchState.Points = 3;
            {
                em.SetComponentData(root, stateResearchState);
            }

            Check(ResearchOps.Command(em, root, Repeat, false) == ResultCode.Success, "Repeatable research explicitly queues its next completion");
            // Force the non-append HistoryOps.Message branch; rollback must restore the prior Count.
            em.SetComponentData(root, new EconomyJournalState { Turn = 1 });
            em.GetBuffer<HistoryEntry>(root).Clear();
            em.GetBuffer<HistoryEntry>(root).Add(new HistoryEntry { Turn = 1, Item = default, Count = 7, Text = "研究完成", Category = HistoryCategory.General });
            var history = Rows<HistoryEntry>(em, root);
            var events = Rows<GameEvent>(em, root);
            var researchEvents = Rows<ResearchCompletedEvent>(em, root);
            bool repeatFailed = false;
            try
            {
                ResearchOps.Settle(em, root, _ => throw new InvalidOperationException("Repeat completion probe"));
            }
            catch (InvalidOperationException error)
            {
                repeatFailed = error.Message == "Repeat completion probe";
            }

            Check(repeatFailed && ResearchOps.Completed(em, root, Repeat) == 1 && ResearchOps.Entry(em, root, Repeat).ResearchPoints == 3 && Rows<TechnologyProgress>(em, root).Count(row => row.Technology == Repeat) == 1 && history.SequenceEqual(Rows<HistoryEntry>(em, root)) && events.SequenceEqual(Rows<GameEvent>(em, root)) && researchEvents.SequenceEqual(Rows<ResearchCompletedEvent>(em, root)), "Repeat completion failure restores research, both event streams and an existing merged history row");
            ResearchOps.Settle(em, root);
            Check(ResearchOps.Completed(em, root, Repeat) == 2 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 1 && Rows<TechnologyProgress>(em, root).Count(row => row.Technology == Repeat) == 1, "Repeated completion increments only research authority without reissuing rewards");
            Reset(em, root, technology: default(TechnologyId));
            em.GetBuffer<TechnologyProgress>(root).Add(new TechnologyProgress { Technology = Repeat, Completions = ResearchOps.MaximumCompletions });
            Check(ResearchOps.Command(em, root, Repeat, false) == ResultCode.Unavailable && ResearchOps.Plan(em, root, Repeat) == ResultCode.Unavailable, "Maximum supported completion cannot queue an overflowing research cycle");
        }

        static void ImportRules(EntityManager em, Entity root)
        {
            var completed = new[]
            {
                new TechnologyProgress
                {
                    Technology = First,
                    Completions = 1
                }
            };
            ResearchOps.ValidateState(em, root, 0, completed);
            Check(completed[0].Completions == 1, "Research completion validates from its own fact buffer");
            Check(typeof(BlueprintUnlock).GetField("Building").FieldType == typeof(BuildingId) && typeof(OwnedBuff).GetField("Buff").FieldType == typeof(BuffId), "Nonresearch facts cannot contain technology identifiers");
            Reject(() => ResearchOps.ValidateState(em, root, 0, new[] { completed[0], completed[0] }), "Duplicate research authority rejected");
            Reject(() => ResearchOps.ValidateState(em, root, 0, new[] { new TechnologyProgress { Technology = default, Completions = 1 } }), "Missing technology rejected");
            Reject(() => ResearchOps.ValidateState(em, root, 0, new[] { new TechnologyProgress { Technology = Repeat, Completions = int.MaxValue } }), "Extreme completion count rejected");
            Reject(() => ResearchOps.ValidateState(em, root, 0, new[] { new TechnologyProgress { Technology = Repeat, Completions = ResearchOps.MaximumCompletions, QueueOrder = 1 } }), "Queued completion beyond supported limit rejected");
            ResearchOps.ValidateState(em, root, 0, new[] { new TechnologyProgress { Technology = Repeat, Completions = ResearchOps.MaximumCompletions } });
            Check(true, "Highest supported repeat count validates from research alone");
        }

        static void Archive(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Research authority current archive", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var tech = TechnologyDefinitions.Find(em, root, "TN_3_1_启蒙");
                Check(tech.IsValid && ResearchOps.Completed(em, root, tech) == 0, "Archive fixture starts with incomplete technology");
                var initial = SnapshotCodec.Capture(em, root);
                var direct = SnapshotCodec.Decode(em, root, initial);
                direct.Research = direct.Research.Where(r => r.Technology != tech).Concat(new[] { new TechnologyProgress { Technology = tech, Completions = 1 } }).ToArray();
                var sourceRows = direct.Research;
                var sourceGrants = direct.Blueprints;
                var rowValues = (TechnologyProgress[])sourceRows.Clone();
                var grantValues = (BlueprintUnlock[])sourceGrants.Clone();
                uint originalRandom = direct.Random.State;
                direct.Random.State = 0;
                Reject(() => SnapshotCodec.Restore(em, root, direct), "Public Restore rejects invalid current state before mutation");
                Check(ReferenceEquals(direct.Research, sourceRows) && sourceRows.SequenceEqual(rowValues) && ReferenceEquals(direct.Blueprints, sourceGrants) && sourceGrants.SequenceEqual(grantValues) && initial.SequenceEqual(SnapshotCodec.Capture(em, root)), "Failed validation leaves caller arrays and live state intact");
                direct.Random.State = originalRandom;
                bool threw = false;
                bool prepared = false;
                try
                {
                    SnapshotCodec.Restore(em, root, direct, prepare: (manager, candidate) => prepared = ResearchOps.Completed(manager, candidate, tech) == 1, probe: step =>
                    {
                        if (step == "root-published")
                            throw new InvalidOperationException("Research publication probe");
                    });
                }
                catch (InvalidOperationException error)
                {
                    threw = error.Message == "Research publication probe";
                }

                Check(threw && prepared && initial.SequenceEqual(SnapshotCodec.Capture(em, root)) && sourceRows.SequenceEqual(rowValues) && sourceGrants.SequenceEqual(grantValues), "Publication rollback preserves live state and caller-owned research");
                SnapshotCodec.Restore(em, root, direct);
                var saved = SnapshotCodec.Capture(em, root);
                ResearchOps.Settle(em, root);
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Restored completion does not replay first rewards");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved));
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)) && ResearchOps.Completed(em, root, tech) == 1 && Rows<TechnologyProgress>(em, root).Count(row => row.Technology == tech) == 1, "Current archive round trips research without technology grants");
                var invalid = SnapshotCodec.Decode(em, root, initial);
                invalid.Research = invalid.Research.Concat(new[] { new TechnologyProgress { Technology = default, Completions = 1 } }).ToArray();
                Reject(() => SnapshotCodec.Restore(em, root, invalid), "Invalid research identifier rejected without migration");
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected snapshot preserves current session");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
