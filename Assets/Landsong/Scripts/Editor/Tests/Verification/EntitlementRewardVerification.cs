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
    public static class EntitlementRewardVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException("FAIL " + description); checks++; log.AppendLine("PASS " + description); }
        static void Reject(Action action, string description)
        { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Check(rejected, description); }
        static T[] Rows<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData
        { using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); return rows.ToArray(); }

        [MenuItem("Landsong/ECS/Verification/Entitlements and rewards")]
        public static string Run()
        {
            checks = 0; log = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            try
            {
                Rewards(false); Rewards(true); StorageOrder(); Import();
                log.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O")); log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error) { log.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/entitlement-rewards-verification.txt", log.ToString()); }
        }

        static void Rewards(bool invalidLast)
        {
            using var world = new World("Entitlement reward transaction fixture"); var em = world.EntityManager;
            using var builder = new BlobBuilder(Allocator.Temp); ref var content = ref builder.ConstructRoot<ContentBlob>();
            var definitions = builder.Allocate(ref content.Definitions, 6); var rules = builder.Allocate(ref content.Rules, 4);
            definitions[0] = new ContentDefinition { Id = "coin", Kind = ContentKind.Item, Capacity = 10, Group = -1 };
            definitions[1] = new ContentDefinition { Id = "building", Kind = ContentKind.Building, Level = 3 };
            definitions[2] = new ContentDefinition { Id = "buff", Kind = ContentKind.Buff };
            definitions[3] = new ContentDefinition { Id = "feature.Test", Kind = ContentKind.Feature };
            definitions[4] = new ContentDefinition { Id = "quest", Kind = ContentKind.Quest, RuleCount = 4 };
            definitions[5] = new ContentDefinition { Id = "limit.Test", Kind = ContentKind.Feature };
            rules[0] = new Rule { Kind = RuleKind.RewardBlueprint, Target = 1, Amount = 2 };
            rules[1] = new Rule { Kind = RuleKind.RewardItem, Target = 0, Amount = 3 };
            rules[2] = new Rule { Kind = RuleKind.RewardBuff, Target = 2, Amount = 1 };
            rules[3] = new Rule { Kind = RuleKind.RewardFeature, Target = invalidLast ? 5 : 3, Amount = 1 };
            using var blob = builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
            var root = em.CreateEntity(); em.AddComponentData(root, new ContentCatalog { Value = blob });
            em.AddComponentData(root, new Session { Turn = 1, Phase = Phase.Day, ResearchPoints = 7, RandomState = 123 });
            em.AddBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Index = 0, Item = -1, SlotType = -1 });
            em.AddBuffer<PendingItem>(root); em.AddBuffer<Entitlement>(root); em.AddBuffer<ResearchEntry>(root);
            em.AddBuffer<GameEvent>(root); em.AddBuffer<BattleReportEntry>(root);
            HistoryOps.Ensure(em, root); EconomyJournalOps.Begin(em, root, false);
            var initialInventory = Rows<InventorySlot>(em, root); var initialSession = em.GetComponentData<Session>(root);
            void Unchanged(string label)
            {
                Check(Rows<InventorySlot>(em, root).SequenceEqual(initialInventory)
                    && em.GetBuffer<Entitlement>(root).Length == 0 && em.GetBuffer<PendingItem>(root).Length == 0
                    && em.GetBuffer<ResearchEntry>(root).Length == 0 && em.GetBuffer<GameEvent>(root).Length == 0
                    && em.GetBuffer<BattleReportEntry>(root).Length == 0 && em.GetBuffer<EconomyEntry>(root).Length == 0
                    && em.GetBuffer<HistoryEntry>(root).Length == 0 && em.GetComponentData<Session>(root).Equals(initialSession), label);
            }
            if (invalidLast)
            {
                Reject(() => RewardOps.ApplyDefinition(em, root, 4), "The final invalid feature rejects the entire batch before any grant");
                Unchanged("Invalid batch leaves inventory, permissions, completion, session and journals unchanged");
                return;
            }
            for (int step = 1; step <= 5; step++)
            {
                var failAt = step;
                Reject(() => RewardOps.ApplyDefinition(em, root, 4, probe: n =>
                {
                    if (n != failAt) return;
                    // A failed completion callback must not leak root facts or published events either.
                    em.GetBuffer<ResearchEntry>(root).Add(new ResearchEntry { Definition = 4, Completions = 1 });
                    em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = EventKind.Reward });
                    var changed = em.GetComponentData<Session>(root); changed.ResearchPoints = 99; em.SetComponentData(root, changed);
                    throw new InvalidOperationException("Injected reward failure");
                }), "Injected failure after batch step " + step);
                Unchanged("Complete reward rollback at step " + step);
            }
            var slot = initialInventory[0]; slot.Item = 0; slot.Count = 9; var inventory = em.GetBuffer<InventorySlot>(root); inventory[0] = slot;
            Check(!RewardOps.ApplyDefinition(em, root, 4), "Partially fitting item rejects a mixed batch");
            Check(InventoryOps.Count(em, root, 0) == 9 && !BlueprintOps.Has(em, root, 1) && !PermanentBuffOps.Has(em, root, 2), "Capacity failure rolls back partial stock and issues no licenses");
            inventory = em.GetBuffer<InventorySlot>(root); inventory[0] = initialInventory[0];
            Check(RewardOps.ApplyDefinition(em, root, 4), "Valid mixed reward batch commits");
            Check(InventoryOps.Count(em, root, 0) == 3 && BlueprintOps.Has(em, root, 1, 2) && PermanentBuffOps.Has(em, root, 2) && FeatureOps.IsUnlocked(em, root, 3), "All reward kinds become visible together");
            Check(RewardOps.ApplyDefinition(em, root, 4, 1.6666666f) && InventoryOps.Count(em, root, 0) == 8,
                "Reward scaling preserves float multiplication before flooring (3 times 1.6666666 yields 5)");
            BlueprintOps.Grant(em, root, 1, 3); BlueprintOps.Grant(em, root, 1);
            Check(BlueprintOps.Has(em, root, 1, 1) && BlueprintOps.Has(em, root, 1, 2) && BlueprintOps.Has(em, root, 1, 3) && !BlueprintOps.Has(em, root, 1, 4), "Highest blueprint retains every lower authorized level");
            Reject(() => BlueprintOps.Grant(em, root, 1, 0), "Zero blueprint is rejected");
            Reject(() => BlueprintOps.Grant(em, root, 1, 4), "Above-maximum blueprint is rejected");
            Reject(() => BlueprintOps.Grant(em, root, 2), "Blueprint issuer rejects Buff targets");
            Reject(() => FeatureOps.Unlock(em, root, 5), "Building limit key is not a feature entitlement");
            PermanentBuffOps.Grant(em, root, 2, 3); PermanentBuffOps.Grant(em, root, 2);
            Check(PermanentBuffOps.Level(em, root, 2) == 3 && Rows<Entitlement>(em, root).Count(g => g.Definition == 2) == 1, "Repeated permanent Buff grants retain one highest-level ownership row");
            slot.Count = 10; inventory = em.GetBuffer<InventorySlot>(root); inventory[0] = slot;
            NightResultOps.Reset(em, root); NightResultOps.Record(em, root, 45, 0, RuleKind.RewardItem, 0, 3);
            NightResultOps.Commit(em, root); NightResultOps.Commit(em, root);
            Check(InventoryOps.PendingCount(em, root, 0) == 3 && em.GetComponentData<NightResultState>(root).Committed == 1, "Night reward overflow commits only once to pending storage");
        }

        static void StorageOrder()
        {
            using var world = new World("Reward storage-order fixture"); var em = world.EntityManager;
            using var builder = new BlobBuilder(Allocator.Temp); ref var content = ref builder.ConstructRoot<ContentBlob>();
            var definitions = builder.Allocate(ref content.Definitions, 5); var rules = builder.Allocate(ref content.Rules, 3);
            definitions[0] = new ContentDefinition { Id = "food", Kind = ContentKind.Item, Capacity = 10, Group = -1, Loss = .1f };
            definitions[1] = new ContentDefinition { Id = "normal", Kind = ContentKind.SlotType, Loss = 1 };
            definitions[2] = new ContentDefinition { Id = "preserved", Kind = ContentKind.SlotType, Loss = .1f };
            definitions[3] = new ContentDefinition { Id = "no.loss", Kind = ContentKind.Buff, RuleCount = 1 };
            definitions[4] = new ContentDefinition { Id = "reward", Kind = ContentKind.Quest, RuleStart = 1, RuleCount = 2 };
            rules[0] = new Rule { Kind = RuleKind.LossModifier, Target = -1, Value = 1 };
            rules[1] = new Rule { Kind = RuleKind.RewardBuff, Target = 3, Amount = 1 };
            rules[2] = new Rule { Kind = RuleKind.RewardItem, Target = 0, Amount = 1 };
            using var blob = builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
            var root = em.CreateEntity(); em.AddComponentData(root, new ContentCatalog { Value = blob });
            em.AddComponentData(root, new Session { Turn = 1, Phase = Phase.Day });
            var slots = em.AddBuffer<InventorySlot>(root);
            slots.Add(new InventorySlot { Provider = 1, Index = 0, Item = -1, SlotType = 1 });
            slots.Add(new InventorySlot { Provider = 2, Index = 0, Item = -1, SlotType = 2 });
            em.AddBuffer<PendingItem>(root); em.AddBuffer<Entitlement>(root); em.AddBuffer<ResearchEntry>(root); em.AddBuffer<GameEvent>(root);
            Check(RewardOps.ApplyDefinition(em, root, 4), "Definition reward with a loss Buff commits");
            slots = em.GetBuffer<InventorySlot>(root);
            Check(slots[0].Count == 0 && slots[1].Count == 1 && PermanentBuffOps.Has(em, root, 3),
                "Items choose the pre-reward low-loss slot before the newly awarded Buff activates");
        }

        static void Import()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");
            using var blobs = new BlobAssetStore(128); using var world = new World("Entitlement import fixture", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager; var root = Sim.Root(em); GameLoopSystem.Initialize(em, root);
                var before = SnapshotCodec.Capture(em, root);
                var buff = Sim.FirstDefinition(em, root, ContentKind.Buff);
                var building = Sim.FirstDefinition(em, root, ContentKind.Building);
                var item = Sim.FirstDefinition(em, root, ContentKind.Item);
                void Invalid(Entitlement[] additions, string label)
                {
                    var data = SnapshotCodec.Decode(em, root, before);
                    data.Grants = data.Grants.Where(g => !additions.Any(a => a.Definition == g.Definition)).Concat(additions).ToArray();
                    bool rejected = false;
                    try { SnapshotCodec.Restore(em, root, data); } catch (InvalidDataException) { rejected = true; }
                    Check(rejected, label);
                    Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), label + " preserves the active session");
                }
                Invalid(new[] { new Entitlement { Definition = buff, Level = 1 }, new Entitlement { Definition = buff, Level = 1 } }, "Duplicate Buff entitlement rejected before effect aggregation");
                Invalid(new[] { new Entitlement { Definition = -1, Level = 1 } }, "Negative definition rejected");
                Invalid(new[] { new Entitlement { Definition = item, Level = 1 } }, "Item cannot be imported as an entitlement");
                Invalid(new[] { new Entitlement { Definition = building, Level = 0 } }, "Zero saved license rejected");
                Invalid(new[] { new Entitlement { Definition = building, Level = Sim.Definition(em, root, building).Level + 1 } }, "Out-of-range saved blueprint rejected");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, before));
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Valid entitlement snapshot still round-trips");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
