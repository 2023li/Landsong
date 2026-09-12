#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class PersistenceContractVerification
    {
        const string Fixtures = "Assets/Landsong/Scripts/Editor/Tests/Verification/Fixtures/Persistence";
        static StringBuilder report;
        static int assertions;
        struct UndeclaredRootState : IComponentData { public int Value; }

        static void Check(bool value, string description)
        { if (!value) throw new InvalidOperationException("FAIL " + description); assertions++; report.AppendLine("PASS " + description); }
        static void Reject(Action action, string description)
        { bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; } Check(rejected, description); }
        static T[] Rows<T>(EntityManager em, Entity root) where T : unmanaged, IBufferElementData
        { using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp); return rows.ToArray(); }

        [MenuItem("Landsong/ECS/Verification/Persistence contract")]
        public static string Run()
        {
            report = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O")); assertions = 0;
            try
            {
                SnapshotBinaryV24.ValidateFieldCoverage(); Check(true, "Explicit v24 schema covers every registered public field");
                HarvestRollback(); HistoryCategories();
                foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes/EntityMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity"))) Map(path);
                report.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O")); report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error) { report.AppendLine(error.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/persistence-contract-verification.txt", report.ToString()); }
        }

        static void HarvestRollback()
        {
            using var world = new World("Partial harvest rollback fixture"); var em = world.EntityManager;
            using var builder = new BlobBuilder(Allocator.Temp); ref var content = ref builder.ConstructRoot<ContentBlob>();
            var definitions = builder.Allocate(ref content.Definitions, 2); var rules = builder.Allocate(ref content.Rules, 1);
            definitions[0] = new ContentDefinition { Id = "item", Name = "物品", Kind = ContentKind.Item, Capacity = 100, Group = -1 };
            definitions[1] = new ContentDefinition { Id = "harvest", Name = "采集堆", Kind = ContentKind.Building, RuleCount = 1 };
            rules[0] = new Rule { Kind = RuleKind.Harvest, Target = 0, Level = 1, Amount = 3, B = 10 };
            using var blob = builder.CreateBlobAssetReference<ContentBlob>(Allocator.Persistent);
            var root = em.CreateEntity(); em.AddComponentData(root, new Session { Turn = 1, Phase = Phase.Day });
            em.AddComponentData(root, new ContentCatalog { Value = blob });
            em.AddBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Item = 0, Count = 95, SlotType = -1 });
            em.AddBuffer<PendingItem>(root); em.AddBuffer<GameEvent>(root); HistoryOps.Ensure(em, root); EconomyJournalOps.Begin(em, root, false);
            em.GetBuffer<HistoryEntry>(root).Add(new HistoryEntry { Turn = 1, Item = -1, Count = 1, Text = "已有历史" });
            var pile = em.CreateEntity(); em.AddComponentData(pile, new Identity { Id = 1, Definition = 1, Name = "采集堆" });
            em.AddComponentData(pile, new Building { Stage = LifeStage.Operational, Crop = -1, Level = 1, HarvestRemaining = 3 });
            em.AddComponentData(pile, LocalTransform.Identity);
            var stock = Rows<InventorySlot>(em, root); var history = Rows<HistoryEntry>(em, root); var ledger = Rows<EconomyEntry>(em, root);
            for (int i = 0; i < 2; i++)
            {
                Check(GameLoopSystem.Execute(em, root, CommandRequests.Harvest(1)) == ResultCode.NoCapacity, "Partial harvest rejected through real command context " + i);
                Check(stock.SequenceEqual(Rows<InventorySlot>(em, root)) && history.SequenceEqual(Rows<HistoryEntry>(em, root)) && ledger.SequenceEqual(Rows<EconomyEntry>(em, root)), "Rejected partial output leaves no phantom stock, history or journal " + i);
                Check(em.GetComponentData<Building>(pile).HarvestRemaining == 3, "Rejected harvest retains all uses " + i);
            }
        }

        static void HistoryCategories()
        {
            using var world = new World("Typed history fixture"); var em = world.EntityManager; var root = em.CreateEntity();
            em.AddComponentData(root, new Session { Turn = 1 }); em.AddBuffer<GameEvent>(root); HistoryOps.Ensure(em, root);
            Sim.Emit(em, root, EventKind.Message, "failure 金币 死亡", category: HistoryCategory.General);
            Sim.Emit(em, root, EventKind.Message, "同一段文案", category: HistoryCategory.Important);
            Sim.Emit(em, root, EventKind.Message, "同一段文案", category: HistoryCategory.Economy);
            var rows = Rows<HistoryEntry>(em, root); var events = Rows<GameEvent>(em, root);
            Check(rows.Length == 3 && rows[0].Category == HistoryCategory.General && rows[1].Category == HistoryCategory.Important && rows[2].Category == HistoryCategory.Economy, "History category comes from event data, independent of words and deduplication");
            Check(events.Select(e => e.Category).SequenceEqual(rows.Select(e => e.Category)), "Realtime and durable history share producer category");
        }

        static void Map(string path)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var blobs = new BlobAssetStore(128); using var world = new World("Persistence contract isolated map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs); var em = world.EntityManager; var root = Sim.Root(em);
                GameLoopSystem.Initialize(em, root); var map = em.GetComponentData<MapIdentity>(root).Id.ToString();
                var baseline = SnapshotCodec.Capture(em, root);
                Check(Format(baseline) == 24, "New snapshots use explicit v24: " + map);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, baseline));
                Check(baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Explicit protocol round trips state exactly: " + map);
                SignatureCoverage(em, root, baseline);
                RootCoverage(em, root);
                Golden(world, root, map);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void SignatureCoverage(EntityManager em, Entity root, byte[] saved)
        {
            var baseline = SnapshotCodec.ContentFingerprint(em, root); var settings = em.GetComponentData<GameSettings>(root);
            foreach (var field in typeof(GameSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object changed = settings;
                if (field.FieldType == typeof(int)) field.SetValue(changed, (int)field.GetValue(changed) + 1);
                else if (field.FieldType == typeof(float)) field.SetValue(changed, (float)field.GetValue(changed) + .125f);
                else throw new InvalidOperationException("Add a mutation for new gameplay field " + field.Name);
                try
                {
                    em.SetComponentData(root, (GameSettings)changed);
                    Check(SnapshotCodec.ContentFingerprint(em, root) != baseline, "Gameplay setting participates in signature: " + field.Name);
                    Reject(() => SnapshotCodec.Decode(em, root, saved), "Changed gameplay setting rejects v24 before restore: " + field.Name);
                }
                finally { em.SetComponentData(root, settings); }
            }
            var grid = em.GetComponentData<GridData>(root);
            try
            {
                var changed = grid; changed.CellSize += .125f; em.SetComponentData(root, changed);
                Reject(() => SnapshotCodec.Decode(em, root, saved), "Grid scale participates in signature");
                changed = grid; changed.Origin.x += 1; em.SetComponentData(root, changed);
                Reject(() => SnapshotCodec.Decode(em, root, saved), "Grid origin participates in signature");
            }
            finally { em.SetComponentData(root, grid); }
            Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected configuration changes leave the live state intact");
        }

        static void RootCoverage(EntityManager em, Entity root)
        {
            var before = SnapshotCodec.Capture(em, root); var count = em.UniversalQuery.CalculateEntityCount();
            em.AddComponentData(root, new UndeclaredRootState { Value = 42 });
            bool rejected = false;
            try { using var transaction = new RestoreTransaction(em, root); }
            catch (InvalidOperationException error) { rejected = error.Message.Contains(nameof(UndeclaredRootState)); }
            finally { em.RemoveComponent<UndeclaredRootState>(root); }
            Check(rejected && !em.HasComponent<Disabled>(root) && count == em.UniversalQuery.CalculateEntityCount() && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Unregistered mutable root field fails before staging or hiding any entity");
        }

        static void Golden(World world, Entity root, string map)
        {
            string name = map == "Map_Test01" ? "map-test01-day-v23.lsrun.bytes" : map == "Map_Test2" ? "map-test2-day-v23.lsrun.bytes" : null;
            if (name == null) return;
            string expected = map == "Map_Test01" ? "7c44c941bfdbaceda41b46c0f8c4cb63a458b678e73e0b74522038ea08181094" : "6626ccaaa357b15caee5902c63fff888d673a7b659941821fc9a39daa9cd2d14";
            var bytes = File.ReadAllBytes(Path.Combine(Fixtures, name));
            using (var hash = SHA256.Create()) Check(string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2"))) == expected, "Frozen archive bytes retain original pre-refactor checksum: " + map);
            var archive = RunArchiveCodec.Decode(bytes); var em = world.EntityManager;
            Check(Format(archive.Current) == 23 && bytes.SequenceEqual(RunArchiveCodec.Encode(archive)), "Original v23/v3 golden envelope reads without rewriting: " + map);
            CheckpointSystem.ValidateArchive(em, root, archive);
            var legacy = SnapshotCodec.Decode(em, root, archive.Current);
            var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>(); checkpoint.Import(root, archive, false);
            Check(em.GetComponentData<Session>(root).Turn == legacy.Session.Turn && em.GetComponentData<RunPersistence>(root).RunId.ToString() == archive.RunId, "Original golden restores session and ownership: " + map);
            Check(archive.Current.SequenceEqual(SnapshotCodec.CaptureLegacyV23ForVerification(em, root)), "Frozen legacy field mapper preserves original v23 bytes: " + map);
            var current = SnapshotCodec.Capture(em, root);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, current));
            Check(current.SequenceEqual(SnapshotCodec.Capture(em, root)), "Old golden upgrades to v24 and restores without state loss: " + map);
            var legacy22 = (byte[])archive.Current.Clone();
            using (var stream = new MemoryStream(legacy22, true))
            { using var reader = new BinaryReader(stream, Encoding.UTF8, true); reader.ReadString(); using var writer = new BinaryWriter(stream, Encoding.UTF8, true); writer.Write(22); }
            Check(SnapshotCodec.Decode(em, root, legacy22).Records.Length == legacy.Records.Length, "Explicit v22 branch remains supported: " + map);
        }
        static int Format(byte[] bytes)
        { using var reader = new BinaryReader(new MemoryStream(bytes, false)); reader.ReadString(); return reader.ReadInt32(); }
    }
}
#endif
