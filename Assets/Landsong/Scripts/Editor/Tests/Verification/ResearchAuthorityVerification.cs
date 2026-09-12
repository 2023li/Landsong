#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
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
            if (!value) throw new InvalidOperationException("FAIL " + label);
            assertions++; log.AppendLine("PASS " + label);
        }
        static void Reject(Action action, string label)
        {
            bool rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Check(rejected, label);
        }
        static T[] Rows<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData
        {
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        [MenuItem("Landsong/ECS/Verification/Research authority")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            log = new StringBuilder(); assertions = 0;
            try
            {
                Domain();
                Archive("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");
                Archive("Assets/Landsong/Scenes/EntityMaps/Map_Test01_Entities.unity");
                log.AppendLine("Assertions: " + assertions); return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/research-authority-verification.txt", log.ToString());
            }
        }

        // An isolated authoring catalog exercises the real compiler and reward operations.
        // Indices are local to this fixture; no formal asset or player archive is edited.
        static GameCatalogAsset Catalog()
        {
            var c = ScriptableObject.CreateInstance<GameCatalogAsset>();
            c.NightEvents = new[] { NightEventSource.Defaults()[0] };
            c.Definitions = Enumerable.Range(0, 8).Select(_ => ScriptableObject.CreateInstance<GameDefinitionAsset>()).ToArray();
            c.Definitions[0].Data = new ContentSource { Id = "coin", Name = "金币", Kind = ContentKind.Item, Capacity = 10 };
            c.Definitions[1].Data = new ContentSource { Id = "first", Name = "基础", Kind = ContentKind.Technology, Cost = 5 };
            c.Definitions[2].Data = new ContentSource { Id = "repeat", Name = "重复", Kind = ContentKind.Technology, Cost = 3, Flags = 1 };
            c.Definitions[3].Data = new ContentSource { Id = ResearchOps.FeatureId, Name = "科技", Kind = ContentKind.Feature };
            c.Definitions[4].Data = new ContentSource { Id = "buff", Name = "增益", Kind = ContentKind.Buff };
            c.Definitions[5].Data = new ContentSource { Id = "other", Name = "新功能", Kind = ContentKind.Feature };
            c.Definitions[6].Data = new ContentSource { Id = "dependent", Name = "后继", Kind = ContentKind.Technology, Cost = 2 };
            c.Definitions[7].Data = new ContentSource { Id = "building", Name = "蓝图", Kind = ContentKind.Building, Level = 2 };
            c.Definitions[1].Data.Configuration.Rewards = new RewardsContentModule
            {
                Enabled = true,
                Items = new[] { new ItemsReward { Order = 1, Item = c.Definitions[0], Quantity = 2 } },
                Blueprints = new[] { new BlueprintsReward { Order = 2, Building = c.Definitions[7], GrantedLevel = 2 } },
                Buffs = new[] { new BuffsReward { Order = 3, Buff = c.Definitions[4], GrantedLevel = 1 } },
                Features = new[] { new FeaturesReward { Order = 4, Feature = c.Definitions[5], GrantedLevel = 1 } }
            };
            c.Definitions[2].Data.Configuration.Rewards = new RewardsContentModule
            { Enabled = true, Items = new[] { new ItemsReward { Item = c.Definitions[0], Quantity = 1 } } };
            c.Definitions[6].Data.Configuration.Conditions = new ConditionsContentModule
            { Enabled = true, Completions = new[] { new CompletionsConfiguration { Content = c.Definitions[1], Count = 1 } } };
            return c;
        }
        static void Domain()
        {
            var catalog = Catalog();
            using var world = new World("Research authority isolated domain");
            var em = world.EntityManager; var root = em.CreateEntity();
            try
            {
                using var blob = GameWorldAuthoring.BuildCatalog(catalog);
                em.AddComponentData(root, new ContentCatalog { Value = blob });
                em.AddComponentData(root, new Session { Turn = 1, Phase = Phase.Day, RandomState = 1234, ResearchPoints = 5 });
                em.AddBuffer<ResearchEntry>(root); em.AddBuffer<Entitlement>(root);
                em.AddBuffer<InventorySlot>(root); em.AddBuffer<PendingItem>(root); em.AddBuffer<GameEvent>(root);
                em.AddBuffer<BattleReportEntry>(root); em.AddBuffer<EconomyEntry>(root); em.AddBuffer<HistoryEntry>(root);
                em.AddComponentData(root, new EconomyJournalState { Turn = 1, Recording = 1 });
                QueryPurity(em, root);
                Gates(em, root);
                Rewards(em, root);
                ImportRules(em, root);
            }
            finally
            {
                foreach (var definition in catalog.Definitions) UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }
        static void Reset(EntityManager em, Entity root, int technology = 1, int progress = 0, int points = 5)
        {
            em.SetComponentData(root, new Session { Turn = 1, Phase = Phase.Day, RandomState = 1234, ResearchPoints = points });
            em.SetComponentData(root, new EconomyJournalState { Turn = 1, Recording = 1 });
            em.GetBuffer<ResearchEntry>(root).Clear();
            if (technology >= 0) em.GetBuffer<ResearchEntry>(root).Add(new ResearchEntry { Definition = technology, Progress = progress, QueueOrder = 1 });
            em.GetBuffer<Entitlement>(root).Clear(); FeatureOps.Unlock(em, root, 3);
            em.GetBuffer<InventorySlot>(root).Clear();
            em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Index = 0, Item = -1, SlotType = -1 });
            em.GetBuffer<PendingItem>(root).Clear(); em.GetBuffer<GameEvent>(root).Clear();
            em.GetBuffer<EconomyEntry>(root).Clear(); em.GetBuffer<HistoryEntry>(root).Clear(); em.GetBuffer<BattleReportEntry>(root).Clear();
        }
        static void QueryPurity(EntityManager em, Entity root)
        {
            var session = em.GetComponentData<Session>(root);
            using var types = em.GetComponentTypes(root, Allocator.Temp);
            string stamp = ResearchOps.Fingerprint(em, root);
            Check(!ResearchOps.Unlocked(em, root), "Feature starts locked in an explicitly configured catalog");
            for (int i = 0; i < 3; i++)
            {
                Check(ResearchOps.Entry(em, root, 1).Completions == 0 && ResearchOps.Completed(em, root, 1) == 0,
                    "Missing row is a read-only incomplete value " + i);
                Check(ResearchOps.Queue(em, root).Count == 0 && ResearchOps.Quote(em, root, 1).CanQueue == ResultCode.Unavailable,
                    "Viewing an empty plan does not unlock research " + i);
                Check(ResearchOps.Path(em, root, 6).Definitions.SequenceEqual(new[] { 1, 6 }),
                    "Path preview can describe a locked feature without authoring state " + i);
            }
            using var afterTypes = em.GetComponentTypes(root, Allocator.Temp);
            Check(types.ToArray().SequenceEqual(afterTypes.ToArray()) && session.Equals(em.GetComponentData<Session>(root))
                && stamp == ResearchOps.Fingerprint(em, root) && Rows<ResearchEntry>(em, root).Length == 0
                && Rows<Entitlement>(em, root).Length == 0 && Rows<GameEvent>(em, root).Length == 0
                && Rows<HistoryEntry>(em, root).Length == 0, "Query family creates no rows, components, permits or events");
            FeatureOps.Unlock(em, root, 3); stamp = ResearchOps.Fingerprint(em, root);
            em.GetBuffer<Entitlement>(root).Add(new Entitlement { Definition = 1, Level = 1 });
            Check(ResearchOps.Completed(em, root, 1) == 0 && !ConditionOps.Satisfied(em, root, 1)
                && !ResearchOps.Quote(em, root, 6).PrerequisitesMet,
                "Live legacy projection alone cannot become a second completion authority");
            Check(stamp == ResearchOps.Fingerprint(em, root), "Derived technology projection does not influence research-plan consent");
        }
        static void Gates(EntityManager em, Entity root)
        {
            foreach (string gate in new[] { "paused", "intelligence", "checkpoint", "night", "deployment", "ended", "feature" })
            {
                Reset(em, root, progress: 2, points: 3);
                var state = em.GetComponentData<Session>(root); var expected = ResultCode.Busy;
                switch (gate)
                {
                    case "paused": state.Paused = 1; break;
                    case "intelligence": state.IntelligenceMode = 1; break;
                    case "checkpoint": state.CheckpointPending = 1; break;
                    case "night": state.Phase = Phase.Night; expected = ResultCode.WrongPhase; break;
                    case "deployment": state.Phase = Phase.Deployment; expected = ResultCode.WrongPhase; break;
                    case "ended": state.Phase = Phase.Ended; expected = ResultCode.WrongPhase; break;
                    case "feature": em.GetBuffer<Entitlement>(root).Clear(); expected = ResultCode.Unavailable; break;
                }
                em.SetComponentData(root, state);
                var entries = Rows<ResearchEntry>(em, root); var grants = Rows<Entitlement>(em, root);
                Check(!ResearchOps.Quote(em, root, 2).Editable && ResearchOps.Editable(em, root) == expected,
                    "Domain quote states external gate: " + gate);
                Check(ResearchOps.Command(em, root, 2, false) == expected && ResearchOps.Command(em, root, 1, true) == expected
                    && ResearchOps.Plan(em, root, 6) == expected, "Queue, cancellation and path all enforce gate: " + gate);
                ResearchOps.Settle(em, root);
                Check(state.Equals(em.GetComponentData<Session>(root)) && entries.SequenceEqual(Rows<ResearchEntry>(em, root))
                    && grants.SequenceEqual(Rows<Entitlement>(em, root)) && Rows<GameEvent>(em, root).Length == 0
                    && Rows<HistoryEntry>(em, root).Length == 0 && InventoryOps.Count(em, root, 0) == 0,
                    "Rejected operations and settlement are side-effect free: " + gate);
            }
            foreach (var phase in new[] { Phase.Day, Phase.Settlement })
            {
                Reset(em, root); var state = em.GetComponentData<Session>(root); state.Phase = phase; em.SetComponentData(root, state);
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, 1) == 1 && em.GetComponentData<Session>(root).ResearchPoints == 0,
                    "Internal research settlement remains legal in " + phase);
            }
        }
        static void Rewards(EntityManager em, Entity root)
        {
            foreach (int failure in new[] { 1, 4, 5 })
            {
                Reset(em, root);
                var grants = Rows<Entitlement>(em, root); var inventory = Rows<InventorySlot>(em, root);
                bool threw = false;
                try { ResearchOps.Settle(em, root, at => { if (at == failure) throw new InvalidOperationException("Research reward probe"); }); }
                catch (InvalidOperationException error) { threw = error.Message == "Research reward probe"; }
                var held = ResearchOps.Entry(em, root, 1);
                Check(threw && held.Progress == 5 && held.Completions == 0 && held.QueueOrder == 1
                    && em.GetComponentData<Session>(root).ResearchPoints == 0,
                    "Reward failure keeps already-paid full progress without publishing completion: " + failure);
                Check(grants.SequenceEqual(Rows<Entitlement>(em, root)) && inventory.SequenceEqual(Rows<InventorySlot>(em, root))
                    && Rows<PendingItem>(em, root).Length == 0 && Rows<GameEvent>(em, root).Length == 0
                    && Rows<HistoryEntry>(em, root).Length == 0 && Rows<EconomyEntry>(em, root).Length == 0,
                    "First item, final award and post-completion faults restore every reward effect: " + failure);
                ResearchOps.Settle(em, root); ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, 1) == 1 && Rows<Entitlement>(em, root).Single(g => g.Definition == 1).Level == 1
                    && InventoryOps.Count(em, root, 0) == 2 && BlueprintOps.Has(em, root, 7, 2)
                    && PermanentBuffOps.Has(em, root, 4) && FeatureOps.IsUnlocked(em, root, 5)
                    && em.GetComponentData<Session>(root).ResearchPoints == 0,
                    "Retry publishes complete reward batch and projection exactly once: " + failure);
            }
            Reset(em, root);
            em.GetBuffer<InventorySlot>(root).CopyFrom(new[] { new InventorySlot { Provider = 1, Index = 0, Item = 0, SlotType = -1, Count = 9 } });
            ResearchOps.Settle(em, root);
            Check(ResearchOps.Entry(em, root, 1).Progress == 5 && ResearchOps.Completed(em, root, 1) == 0
                && InventoryOps.Count(em, root, 0) == 9 && !BlueprintOps.Has(em, root, 7)
                && ResearchOps.Quote(em, root, 1).Status == ResearchStatus.AwaitingRewards,
                "Partial inventory capacity keeps paid progress while rejecting all first-completion rewards");

            Reset(em, root, technology: 2, points: 3);
            ResearchOps.Settle(em, root);
            Check(ResearchOps.Completed(em, root, 2) == 1 && InventoryOps.Count(em, root, 0) == 1, "Repeatable technology issues its first reward once");
            var state = em.GetComponentData<Session>(root); state.ResearchPoints = 3; em.SetComponentData(root, state);
            Check(ResearchOps.Command(em, root, 2, false) == ResultCode.Success, "Repeatable research explicitly queues its next completion");
            // Force the non-append HistoryOps.Message branch; rollback must restore the prior Count.
            em.SetComponentData(root, new EconomyJournalState { Turn = 1 });
            em.GetBuffer<HistoryEntry>(root).Clear();
            em.GetBuffer<HistoryEntry>(root).Add(new HistoryEntry { Turn = 1, Item = -1, Count = 7, Text = "研究完成", Category = HistoryCategory.General });
            var history = Rows<HistoryEntry>(em, root); var events = Rows<GameEvent>(em, root);
            bool repeatFailed = false;
            try { ResearchOps.Settle(em, root, _ => throw new InvalidOperationException("Repeat completion probe")); }
            catch (InvalidOperationException error) { repeatFailed = error.Message == "Repeat completion probe"; }
            Check(repeatFailed && ResearchOps.Completed(em, root, 2) == 1 && ResearchOps.Entry(em, root, 2).Progress == 3
                && Rows<Entitlement>(em, root).Single(g => g.Definition == 2).Level == 1
                && history.SequenceEqual(Rows<HistoryEntry>(em, root)) && events.SequenceEqual(Rows<GameEvent>(em, root)),
                "Repeat completion failure restores projection, events and an existing merged history row");
            ResearchOps.Settle(em, root);
            Check(ResearchOps.Completed(em, root, 2) == 2 && InventoryOps.Count(em, root, 0) == 1
                && Rows<Entitlement>(em, root).Single(g => g.Definition == 2).Level == 2, "Repeated completion increments authority and projection without reissuing rewards");

            Reset(em, root, technology: -1);
            em.GetBuffer<ResearchEntry>(root).Add(new ResearchEntry { Definition = 2, Completions = ResearchOps.MaximumCompletions });
            em.GetBuffer<Entitlement>(root).Add(new Entitlement { Definition = 2, Level = ResearchOps.MaximumCompletions });
            Check(ResearchOps.Command(em, root, 2, false) == ResultCode.Unavailable && ResearchOps.Plan(em, root, 2) == ResultCode.Unavailable,
                "Maximum supported completion cannot queue an overflowing research cycle");
        }
        static void ImportRules(EntityManager em, Entity root)
        {
            var empty = Array.Empty<ResearchEntry>(); var grant = new[] { new Entitlement { Definition = 1, Level = 1 } };
            var normalized = ResearchOps.NormalizeImportedState(em, root, 0, empty, grant);
            Check(empty.Length == 0 && grant[0].Level == 1 && normalized.Length == 1 && normalized[0].Definition == 1
                && normalized[0].Completions == 1 && normalized[0].Progress == 0 && normalized[0].QueueOrder == 0,
                "Legacy grant-only state becomes a completed, unqueued row without modifying input or replaying rewards");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, new[] { new ResearchEntry { Definition = 1 } }, grant),
                "An explicit incomplete row conflicts with a positive legacy projection");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, new[] { new ResearchEntry { Definition = 2, Completions = 1 } },
                new[] { new Entitlement { Definition = 2, Level = 2 } }), "Different positive completion counts are a conflict, not missing data");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, normalized, Array.Empty<Entitlement>()),
                "Existing completed row without its required legacy projection remains invalid");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, empty, new[] { grant[0], grant[0] }), "Duplicate legacy technology projection is rejected");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, new[] { normalized[0], normalized[0] }, grant), "Duplicate research authority is rejected");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, empty, new[] { new Entitlement { Definition = 2, Level = int.MaxValue } }),
                "Legacy completion counter with no representable next value is rejected");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, new[] { new ResearchEntry { Definition = 2, Completions = int.MaxValue } },
                new[] { new Entitlement { Definition = 2, Level = int.MaxValue } }), "Extreme explicit completion counter is rejected");
            Reject(() => ResearchOps.NormalizeImportedState(em, root, 0, new[] { new ResearchEntry { Definition = 2, Completions = ResearchOps.MaximumCompletions, QueueOrder = 1 } },
                new[] { new Entitlement { Definition = 2, Level = ResearchOps.MaximumCompletions } }), "Already queued completion beyond supported limit is rejected");
            var maximum = ResearchOps.NormalizeImportedState(em, root, 0, empty,
                new[] { new Entitlement { Definition = 2, Level = ResearchOps.MaximumCompletions } });
            Check(maximum.Single().Completions == ResearchOps.MaximumCompletions, "Highest supported lawful legacy repeat count remains readable");
        }
        static void Archive(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128); using var world = new World("Research authority legacy archive", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                int tech = Sim.FindDefinition(em, root, "TN_3_1_启蒙");
                Check(tech >= 0 && ResearchOps.Completed(em, root, tech) == 0, "Formal archive fixture starts with the first technology incomplete");
                var initial = SnapshotCodec.Capture(em, root);
                var entries = em.GetBuffer<ResearchEntry>(root);
                for (int i = entries.Length - 1; i >= 0; i--) if (entries[i].Definition == tech) entries.RemoveAt(i);
                var grants = em.GetBuffer<Entitlement>(root);
                for (int i = grants.Length - 1; i >= 0; i--) if (grants[i].Definition == tech) grants.RemoveAt(i);
                grants.Add(new Entitlement { Definition = tech, Level = 1 });
                var legacy23 = SnapshotCodec.CaptureLegacyV23ForVerification(em, root);
                var current24 = SnapshotCodec.Capture(em, root);
                // These are synthetic boundary cases from the frozen legacy writer, not historical
                // gold samples. Keep all historical archives untouched in the persistence suite.
                var layout = SnapshotCodec.Decode(em, root, legacy23);
                Check(layout.Records.All(r => (r.Mask & 512) == 0 || r.SoldierPerson.SpecialAttention == 0),
                    "Synthetic v22 branch fixture has no changed soldier-mark semantics");
                var legacyWriter = typeof(SnapshotCodec).GetMethod("CaptureVersion",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(EntityManager), typeof(Entity), typeof(int) }, null);
                Check(legacyWriter != null, "Explicit frozen legacy writer is available for the synthetic v22 fixture");
                var legacy22 = (byte[])legacyWriter.Invoke(null, new object[] { em, root, 22 });
                foreach (var format in new[] { (Version: 22, Bytes: legacy22), (Version: 23, Bytes: legacy23), (Version: 24, Bytes: current24) })
                {
                    var before = (byte[])format.Bytes.Clone();
                    var decoded = SnapshotCodec.Decode(em, root, format.Bytes);
                    Check(decoded.Research.Single(r => r.Definition == tech).Completions == 1 && format.Bytes.SequenceEqual(before)
                        && ResearchOps.Completed(em, root, tech) == 0, "Decode v" + format.Version + " normalizes legacy-only completion without changing live state or bytes");
                    SnapshotCodec.Restore(em, root, decoded);
                    var restored = SnapshotCodec.Capture(em, root);
                    ResearchOps.Settle(em, root);
                    Check(ResearchOps.Completed(em, root, tech) == 1 && ResearchOps.Entry(em, root, tech).QueueOrder == 0
                        && restored.SequenceEqual(SnapshotCodec.Capture(em, root)), "Restored v" + format.Version + " completion cannot replay first rewards");
                    SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, initial));
                }

                var direct = SnapshotCodec.Decode(em, root, current24);
                direct.Research = direct.Research.Where(r => r.Definition != tech).ToArray();
                var sourceRows = direct.Research; var sourceGrants = direct.Grants; var sourceGrantValues = (Entitlement[])sourceGrants.Clone();
                uint originalRandom = direct.Session.RandomState; direct.Session.RandomState = 0;
                Reject(() => SnapshotCodec.Restore(em, root, direct), "Public Restore validates the normalized copy before live mutation");
                Check(ReferenceEquals(direct.Research, sourceRows) && sourceRows.All(r => r.Definition != tech)
                    && ReferenceEquals(direct.Grants, sourceGrants) && sourceGrants.SequenceEqual(sourceGrantValues)
                    && initial.SequenceEqual(SnapshotCodec.Capture(em, root)), "Failed validation cannot repair caller-owned arrays in place");
                direct.Session.RandomState = originalRandom;
                bool threw = false; bool prepared = false;
                try
                {
                    SnapshotCodec.Restore(em, root, direct, prepare: (manager, candidate) =>
                    {
                        prepared = ResearchOps.Completed(manager, candidate, tech) == 1;
                    }, probe: step => { if (step == "root-published") throw new InvalidOperationException("Normalized publication probe"); });
                }
                catch (InvalidOperationException error) { threw = error.Message == "Normalized publication probe"; }
                Check(threw && prepared && initial.SequenceEqual(SnapshotCodec.Capture(em, root))
                    && ReferenceEquals(direct.Research, sourceRows) && sourceRows.All(r => r.Definition != tech),
                    "Candidate preparation sees normalized authority and publication rollback preserves live state and caller DTO");
                SnapshotCodec.Restore(em, root, direct, prepare: (manager, candidate) =>
                    Check(ResearchOps.Completed(manager, candidate, tech) == 1, "Public Restore derives candidate state from normalized research before preparation"));
                Check(ResearchOps.Completed(em, root, tech) == 1 && ReferenceEquals(direct.Research, sourceRows)
                    && sourceRows.All(r => r.Definition != tech) && sourceGrants.SequenceEqual(sourceGrantValues),
                    "Successful public Restore accepts legal grant-only DTO without mutating it");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
