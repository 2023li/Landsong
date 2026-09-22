#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class EntitlementRewardVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool value, string description)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + description);
            checks++;
            log.AppendLine("PASS " + description);
        }

        static void Reject(Action action, string description)
        {
            bool rejected = false;
            try
            {
                action();
            }
            catch (Exception error)when (error is InvalidOperationException || error is ArgumentException)
            {
                rejected = true;
            }

            Check(rejected, description);
        }

        static T[] Rows<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        [MenuItem("Landsong/ECS/Verification/Entitlements and rewards")]
        public static string Run()
        {
            checks = 0;
            log = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            try
            {
                Rewards(false);
                Rewards(true);
                StorageOrder();
                Import();
                log.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O"));
                log.AppendLine("Assertions: " + checks);
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
                File.WriteAllText("Library/LandsongEcs/entitlement-rewards-verification.txt", log.ToString());
            }
        }

        static void Rewards(bool invalidLast)
        {
            using var world = new World("Entitlement reward transaction fixture");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            using var fixture = new RewardTestFixture(em, root, invalidLast: invalidLast);
            {
                em.AddComponentData(root, new Session { Phase = Phase.Day });
                em.AddComponentData(root, new GameClock() { Turn = 1 });
                em.AddComponentData(root, new SimulationControl() { });
                em.AddComponentData(root, new PopulationState() { });
                em.AddComponentData(root, new PublicOpinionState() { });
                em.AddComponentData(root, new ResearchState() { Points = 7 });
                em.AddComponentData(root, new ExpeditionPenaltyState() { });
                em.AddComponentData(root, new NightRuntimeState() { });
                em.AddComponentData(root, new DaySettlementState() { });
                em.AddComponentData(root, new RetryState() { });
                em.AddComponentData(root, new HeroSelection() { });
                em.AddComponentData(root, new BellState() { });
                em.AddComponentData(root, new IntelligenceModeState() { });
                em.AddComponentData(root, new PersistenceGate() { });
                em.AddComponentData(root, new SimulationRandomState() { State = 123 });
                em.AddComponentData(root, new IdentitySequence() { });
                em.AddComponentData(root, new DynastyIdentity() { });
            }

            em.AddBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Index = 0, Item = default, SlotType = default });
            em.AddBuffer<PendingItem>(root);
            ResearchTestFixture.AddFacts(em, root);
            em.AddBuffer<TechnologyProgress>(root);
            em.AddBuffer<GameEvent>(root);
            em.AddBuffer<BattleReportEntry>(root);
            em.AddComponentData(root, PeacefulRules.Default);
            HistoryOps.Ensure(em, root);
            EconomyJournalOps.Begin(em, root, false);
            var initialInventory = Rows<InventorySlot>(em, root);
            var initialSession = em.GetComponentData<Session>(root);
            var initialResearch = em.GetComponentData<ResearchState>(root);
            void Unchanged(string label)
            {
                Check(em.GetComponentData<ResearchState>(root).Equals(initialResearch), label + " restores research points");
                Check(Rows<InventorySlot>(em, root).SequenceEqual(initialInventory) && em.GetBuffer<BlueprintUnlock>(root).Length == 0 && em.GetBuffer<OwnedBuff>(root).Length == 0 && em.GetBuffer<UnlockedFeature>(root).Length == 0 && em.GetBuffer<PendingItem>(root).Length == 0 && em.GetBuffer<TechnologyProgress>(root).Length == 0 && em.GetBuffer<GameEvent>(root).Length == 0 && em.GetBuffer<BattleReportEntry>(root).Length == 0 && em.GetBuffer<EconomyEntry>(root).Length == 0 && em.GetBuffer<HistoryEntry>(root).Length == 0 && em.GetComponentData<Session>(root).Equals(initialSession), label);
            }

            if (invalidLast)
            {
                Reject(() => fixture.Apply(em, root), "The final invalid feature rejects the entire batch before any grant");
                Unchanged("Invalid batch leaves inventory, permissions, completion, session and journals unchanged");
                return;
            }

            for (int step = 1; step <= 5; step++)
            {
                var failAt = step;
                Reject(() => fixture.Apply(em, root, probe: n =>
                {
                    if (n != failAt)
                        return;
                    // A failed completion callback must not leak root facts or published events either.
                    em.GetBuffer<TechnologyProgress>(root).Add(new TechnologyProgress { Technology = TechnologyId.FromIndex(0), Completions = 1 });
                    em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = EventKind.Reward });
                    ResearchState changedResearchState = em.GetComponentData<ResearchState>(root);
                    changedResearchState.Points = 99;
                    {
                        em.SetComponentData(root, changedResearchState);
                    }

                    throw new InvalidOperationException("Injected reward failure");
                }), "Injected failure after batch step " + step);
                Unchanged("Complete reward rollback at step " + step);
            }

            var slot = initialInventory[0];
            slot.Item = RewardTestFixture.Item;
            slot.Count = 9;
            var inventory = em.GetBuffer<InventorySlot>(root);
            inventory[0] = slot;
            Check(!fixture.Apply(em, root), "Partially fitting item rejects a mixed batch");
            Check(InventoryOps.Count(em, root, RewardTestFixture.Item) == 9 && !BuildingBlueprints.Has(em, root, RewardTestFixture.Building) && !(PermanentBuffs.Level(em, root, RewardTestFixture.Buff) > 0), "Capacity failure rolls back partial stock and issues no licenses");
            inventory = em.GetBuffer<InventorySlot>(root);
            inventory[0] = initialInventory[0];
            Check(fixture.Apply(em, root), "Valid mixed reward batch commits");
            Check(InventoryOps.Count(em, root, RewardTestFixture.Item) == 3 && BuildingBlueprints.Has(em, root, RewardTestFixture.Building, 2) && (PermanentBuffs.Level(em, root, RewardTestFixture.Buff) > 0) && FeatureUnlocks.Has(em, root, RewardTestFixture.Feature), "All reward kinds become visible together");
            Check(fixture.Apply(em, root, 1.6666666f) && InventoryOps.Count(em, root, RewardTestFixture.Item) == 8, "Reward scaling preserves float multiplication before flooring (3 times 1.6666666 yields 5)");
            BuildingBlueprints.Grant(em, root, RewardTestFixture.Building, 3);
            BuildingBlueprints.Grant(em, root, RewardTestFixture.Building, 1);
            Check(BuildingBlueprints.Has(em, root, RewardTestFixture.Building, 1) && BuildingBlueprints.Has(em, root, RewardTestFixture.Building, 2) && BuildingBlueprints.Has(em, root, RewardTestFixture.Building, 3) && !BuildingBlueprints.Has(em, root, RewardTestFixture.Building, 4), "Highest blueprint retains every lower authorized level");
            Reject(() => BuildingBlueprints.Grant(em, root, RewardTestFixture.Building, 0), "Zero blueprint is rejected");
            Reject(() => BuildingBlueprints.Grant(em, root, RewardTestFixture.Building, 4), "Above-maximum blueprint is rejected");
            Check(typeof(BlueprintUnlock).GetField("Building").FieldType == typeof(BuildingId), "Blueprint facts cannot contain buff identifiers");
            Check(typeof(UnlockedFeature).GetField("Feature").FieldType == typeof(FeatureId), "Building limit groups cannot be feature facts");
            PermanentBuffs.Grant(em, root, RewardTestFixture.Buff, 3);
            PermanentBuffs.Grant(em, root, RewardTestFixture.Buff, 1);
            Check(PermanentBuffs.Level(em, root, RewardTestFixture.Buff) == 3 && Rows<OwnedBuff>(em, root).Count(g => g.Buff == RewardTestFixture.Buff) == 1, "Repeated permanent Buff grants retain one highest-level ownership row");
            slot.Count = 10;
            inventory = em.GetBuffer<InventorySlot>(root);
            inventory[0] = slot;
            NightResultOps.Reset(em, root);
            NightResultOps.RecordItem(em, root, 45, 0, RewardTestFixture.Item, 3);
            NightResultOps.Commit(em, root);
            NightResultOps.Commit(em, root);
            Check(InventoryOps.PendingCount(em, root, RewardTestFixture.Item) == 3 && em.GetComponentData<NightResultState>(root).Committed == 1, "Night reward overflow commits only once to pending storage");
        }

        static void StorageOrder()
        {
            using var world = new World("Reward storage-order fixture");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            using var fixture = new RewardTestFixture(em, root, storageOrder: true);
            {
                em.AddComponentData(root, new Session { Phase = Phase.Day });
                em.AddComponentData(root, new GameClock() { Turn = 1 });
                em.AddComponentData(root, new SimulationControl() { });
                em.AddComponentData(root, new PopulationState() { });
                em.AddComponentData(root, new PublicOpinionState() { });
                em.AddComponentData(root, new ResearchState() { });
                em.AddComponentData(root, new ExpeditionPenaltyState() { });
                em.AddComponentData(root, new NightRuntimeState() { });
                em.AddComponentData(root, new DaySettlementState() { });
                em.AddComponentData(root, new RetryState() { });
                em.AddComponentData(root, new HeroSelection() { });
                em.AddComponentData(root, new BellState() { });
                em.AddComponentData(root, new IntelligenceModeState() { });
                em.AddComponentData(root, new PersistenceGate() { });
                em.AddComponentData(root, new SimulationRandomState() { });
                em.AddComponentData(root, new IdentitySequence() { });
                em.AddComponentData(root, new DynastyIdentity() { });
            }

            var slots = em.AddBuffer<InventorySlot>(root);
            slots.Add(new InventorySlot { Provider = 1, Index = 0, Item = default, SlotType = StorageSlotId.FromIndex(0) });
            slots.Add(new InventorySlot { Provider = 2, Index = 0, Item = default, SlotType = StorageSlotId.FromIndex(1) });
            em.AddBuffer<PendingItem>(root);
            ResearchTestFixture.AddFacts(em, root);
            em.AddBuffer<TechnologyProgress>(root);
            em.AddBuffer<GameEvent>(root);
            Check(fixture.Apply(em, root), "Definition reward with a loss Buff commits");
            slots = em.GetBuffer<InventorySlot>(root);
            Check(slots[0].Count == 0 && slots[1].Count == 1 && (PermanentBuffs.Level(em, root, RewardTestFixture.Buff) > 0), "Items choose the pre-reward low-loss slot before the newly awarded Buff activates");
        }

        static void Import()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Entitlement import fixture", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var before = SnapshotCodec.Capture(em, root);
                var buff = BuffId.FromIndex(0);
                var building = BuildingId.FromIndex(0);
                void Invalid(Action<SnapshotCodec.Snapshot> mutate, string label)
                {
                    var data = SnapshotCodec.Decode(em, root, before);
                    mutate(data);
                    bool rejected = false;
                    try
                    {
                        SnapshotCodec.Restore(em, root, data);
                    }
                    catch (InvalidDataException)
                    {
                        rejected = true;
                    }

                    Check(rejected, label);
                    Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), label + " preserves the active session");
                }

                Invalid(data => data.Buffs = new[] { new OwnedBuff { Buff = buff, Level = 1 }, new OwnedBuff { Buff = buff, Level = 1 } }, "Duplicate buff facts rejected before effect aggregation");
                Invalid(data => data.Buffs = new[] { new OwnedBuff { Buff = default, Level = 1 } }, "Absent buff identifier rejected");
                Invalid(data => data.Buffs = new[] { new OwnedBuff { Buff = BuffId.FromIndex(BuffDefinitions.Count(em, root)), Level = 1 } }, "Unregistered buff rejected");
                Invalid(data => data.Blueprints = new[] { new BlueprintUnlock { Building = building, MaximumLevel = 0 } }, "Zero saved blueprint rejected");
                Invalid(data => data.Blueprints = new[] { new BlueprintUnlock { Building = building, MaximumLevel = BuildingDefinitions.Get(em, root, building).MaximumLevel + 1 } }, "Out-of-range blueprint rejected");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, before));
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Valid entitlement snapshot still round-trips");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
