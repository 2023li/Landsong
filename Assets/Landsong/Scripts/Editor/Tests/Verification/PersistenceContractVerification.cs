#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using System.IO;
using System.Linq;
using System.Reflection;
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
        static StringBuilder report;
        static int assertions;
        struct UndeclaredRootState : IComponentData
        {
            public int Value;
        }

        static void Check(bool value, string description)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + description);
            assertions++;
            report.AppendLine("PASS " + description);
        }

        static void Reject(Action action, string description)
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

            Check(rejected, description);
        }

        static T[] Rows<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        [MenuItem("Landsong/ECS/Verification/Persistence contract")]
        public static string Run()
        {
            report = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O"));
            assertions = 0;
            try
            {
                SnapshotBinary.ValidateFieldCoverage();
                Check(true, "Current explicit schema covers every registered public field");
                HarvestRollback();
                HistoryCategories();
                foreach (var path in Landsong.EditorTools.GameMapPaths.BakedScenes())
                    Map(path);
                report.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O"));
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
                File.WriteAllText("Library/LandsongEcs/persistence-contract-verification.txt", report.ToString());
            }
        }

        static void HarvestRollback()
        {
            using var world = new World("Partial harvest rollback fixture");
            var em = world.EntityManager;
            using var itemsBuilder = new BlobBuilder(Allocator.Temp);
            ref var items = ref itemsBuilder.ConstructRoot<ItemCatalogBlob>();
            var itemDefinitions = itemsBuilder.Allocate(ref items.Definitions, 1);
            itemDefinitions[0] = new ItemDefinition
            {
                Metadata = new DefinitionMetadata
                {
                    Id = "item",
                    Name = "物品"
                },
                MaximumStack = 100
            };
            using var itemsBlob = itemsBuilder.CreateBlobAssetReference<ItemCatalogBlob>(Allocator.Persistent);
            using var buildingBuilder = new BlobBuilder(Allocator.Temp);
            ref var buildings = ref buildingBuilder.ConstructRoot<BuildingCatalogBlob>();
            var buildingDefinitions = buildingBuilder.Allocate(ref buildings.Definitions, 1);
            buildingDefinitions[0].Metadata = new DefinitionMetadata
            {
                Id = "harvest",
                Name = "采集堆"
            };
            var harvest = buildingBuilder.Allocate(ref buildingDefinitions[0].Capabilities.Gathering.Levels, 1);
            harvest[0] = new BuildingGatheringLevel
            {
                Level = 1,
                Item = ItemId.FromIndex(0),
                Uses = 3,
                AmountPerUse = 10
            };
            using var buildingsBlob = buildingBuilder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
            var root = em.CreateEntity();
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

            em.AddComponentData(root, new ItemCatalog { Value = itemsBlob });
            em.AddComponentData(root, new BuildingCatalog { Value = buildingsBlob });
            em.AddBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Item = ItemId.FromIndex(0), Count = 95, SlotType = StorageSlotId.None });
            em.AddBuffer<PendingItem>(root);
            em.AddBuffer<OwnedBuff>(root);
            em.AddBuffer<PolicyChoice>(root);
            em.AddBuffer<GameEvent>(root);
            HistoryOps.Ensure(em, root);
            EconomyJournalOps.Begin(em, root, false);
            em.GetBuffer<HistoryEntry>(root).Add(new HistoryEntry { Turn = 1, Item = ItemId.None, Count = 1, Text = "已有历史" });
            var pile = em.CreateEntity();
            em.AddComponentData(pile, new BuildingDefinitionRef { Definition = BuildingId.FromIndex(0) });
            em.AddComponentData(pile, new Identity { Id = 1, Name = "采集堆" });
            {
                em.AddComponentData(pile, new Building { Stage = LifeStage.Operational, Level = 1 });
                em.AddComponentData(pile, new BuildingPlacementState() { });
                em.AddComponentData(pile, new BuildingAppearanceState() { });
                em.AddComponentData(pile, new BuildingConstructionState() { });
                em.AddComponentData(pile, new BuildingWorkforceState() { });
                em.AddComponentData(pile, new BuildingHousingState() { });
                em.AddComponentData(pile, new BuildingProductionState() { });
                em.AddComponentData(pile, new BuildingFarmingState() { Crop = CropId.None });
                em.AddComponentData(pile, new BuildingSanctumState() { });
                em.AddComponentData(pile, new BuildingGatheringState() { RemainingUses = 3 });
                em.AddComponentData(pile, new BuildingRecruitmentState() { });
                em.AddComponentData(pile, new BuildingMarketState() { });
                em.AddComponentData(pile, new BuildingExperienceState() { });
                em.AddComponentData(pile, new BuildingMaintenanceState() { });
            }

            em.AddComponentData(pile, LocalTransform.Identity);
            var stock = Rows<InventorySlot>(em, root);
            var history = Rows<HistoryEntry>(em, root);
            var ledger = Rows<EconomyEntry>(em, root);
            for (int i = 0; i < 2; i++)
            {
                Check(GameRequestExecution.Execute(em, root, new HarvestBuildingRequest { Building = 1 }) == ResultCode.NoCapacity, "Partial harvest rejected through real command context " + i);
                Check(stock.SequenceEqual(Rows<InventorySlot>(em, root)) && history.SequenceEqual(Rows<HistoryEntry>(em, root)) && ledger.SequenceEqual(Rows<EconomyEntry>(em, root)), "Rejected partial output leaves no phantom stock, history or journal " + i);
                Check(em.GetComponentData<BuildingGatheringState>(pile).RemainingUses == 3, "Rejected harvest retains all uses " + i);
            }
        }

        static void HistoryCategories()
        {
            using var world = new World("Typed history fixture");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            {
                em.AddComponentData(root, new Session { });
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

            em.AddBuffer<GameEvent>(root);
            HistoryOps.Ensure(em, root);
            SimulationEvents.Emit(em, root, EventKind.Message, "failure 金币 死亡", category: HistoryCategory.General);
            SimulationEvents.Emit(em, root, EventKind.Message, "同一段文案", category: HistoryCategory.Important);
            SimulationEvents.Emit(em, root, EventKind.Message, "同一段文案", category: HistoryCategory.Economy);
            var rows = Rows<HistoryEntry>(em, root);
            var events = Rows<GameEvent>(em, root);
            Check(rows.Length == 3 && rows[0].Category == HistoryCategory.General && rows[1].Category == HistoryCategory.Important && rows[2].Category == HistoryCategory.Economy, "History category comes from event data, independent of words and deduplication");
            Check(events.Select(e => e.Category).SequenceEqual(rows.Select(e => e.Category)), "Realtime and durable history share producer category");
        }

        static void Map(string path)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Persistence contract isolated map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var map = em.GetComponentData<MapIdentity>(root).Id.ToString();
                var baseline = SnapshotCodec.Capture(em, root);
                Check(Format(baseline) == SnapshotCodec.CurrentVersion, "New snapshots use the current explicit format: " + map);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, baseline));
                Check(baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Explicit protocol round trips state exactly: " + map);
                SignatureCoverage(em, root, baseline);
                RootCoverage(em, root);
                UnsupportedVersions(em, root, baseline);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void SignatureCoverage(EntityManager em, Entity root, byte[] saved)
        {
            SettingsSignature<NightSettings>(em, root, saved);
            SettingsSignature<IntelligenceSettings>(em, root, saved);
            SettingsSignature<CurrencySettings>(em, root, saved);
            SettingsSignature<RoyalFamilySettings>(em, root, saved);
            SettingsSignature<TalentSettings>(em, root, saved);
            SettingsSignature<QuestGenerationSettings>(em, root, saved);
            SettingsSignature<NightRules>(em, root, saved);
            SettingsSignature<PeacefulRules>(em, root, saved);
            SettingsSignature<ExpeditionSettings>(em, root, saved);
            SettingsSignature<CourtSettings>(em, root, saved);
            var grid = em.GetComponentData<GridData>(root);
            try
            {
                var changed = grid;
                changed.CellSize += .125f;
                em.SetComponentData(root, changed);
                Reject(() => SnapshotCodec.Decode(em, root, saved), "Grid scale participates in signature");
                changed = grid;
                changed.Origin.x += 1;
                em.SetComponentData(root, changed);
                Reject(() => SnapshotCodec.Decode(em, root, saved), "Grid origin participates in signature");
            }
            finally
            {
                em.SetComponentData(root, grid);
            }

            Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected configuration changes leave the live state intact");
        }

        static void SettingsSignature<T>(EntityManager em, Entity root, byte[] saved)
            where T : unmanaged, IComponentData
        {
            var baseline = SnapshotCodec.ContentFingerprint(em, root);
            var settings = em.GetComponentData<T>(root);
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object changed = settings;
                if (field.FieldType == typeof(int))
                    field.SetValue(changed, (int)field.GetValue(changed) + 1);
                else if (field.FieldType == typeof(float))
                    field.SetValue(changed, (float)field.GetValue(changed) + .125f);
                else if (field.FieldType == typeof(int4))
                {
                    var value = (int4)field.GetValue(changed);
                    value.x++;
                    field.SetValue(changed, value);
                }
                else if (field.FieldType == typeof(ItemId))
                    field.SetValue(changed, ItemId.FromIndex(((ItemId)field.GetValue(changed)).Index + 1));
                else
                    throw new InvalidOperationException("Add a mutation for new gameplay field " + typeof(T).Name + "." + field.Name);
                try
                {
                    em.SetComponentData(root, (T)changed);
                    Check(SnapshotCodec.ContentFingerprint(em, root) != baseline, "Gameplay setting participates in signature: " + typeof(T).Name + "." + field.Name);
                    Reject(() => SnapshotCodec.Decode(em, root, saved), "Changed gameplay setting rejects snapshot before restore: " + typeof(T).Name + "." + field.Name);
                }
                finally
                {
                    em.SetComponentData(root, settings);
                }
            }
        }

        static void RootCoverage(EntityManager em, Entity root)
        {
            var before = SnapshotCodec.Capture(em, root);
            var count = em.UniversalQuery.CalculateEntityCount();
            em.AddComponentData(root, new UndeclaredRootState { Value = 42 });
            bool rejected = false;
            try
            {
                using var transaction = new RestoreTransaction(em, root);
            }
            catch (InvalidOperationException error)
            {
                rejected = error.Message.Contains(nameof(UndeclaredRootState));
            }
            finally
            {
                em.RemoveComponent<UndeclaredRootState>(root);
            }

            Check(rejected && !em.HasComponent<Disabled>(root) && count == em.UniversalQuery.CalculateEntityCount() && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Unregistered mutable root field fails before staging or hiding any entity");
        }

        static void UnsupportedVersions(EntityManager em, Entity root, byte[] baseline)
        {
            // Header mutations exercise early rejection, not emulated old encoders.
            foreach (int version in new[]
            {
                22,
                23,
                24,
                25,
                26,
                SnapshotCodec.CurrentVersion + 1
            }

            )
            {
                var bytes = (byte[])baseline.Clone();
                using (var stream = new MemoryStream(bytes, true))
                {
                    using var reader = new BinaryReader(stream, Encoding.UTF8, true);
                    reader.ReadString();
                    using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
                    writer.Write(version);
                }

                Reject(() => SnapshotCodec.ReadMapId(bytes), "Map header rejects unsupported version " + version);
                Reject(() => SnapshotCodec.ReadSummary(bytes), "Summary rejects unsupported version " + version);
                Reject(() => SnapshotCodec.Decode(em, root, bytes), "Decode rejects unsupported version " + version);
                Check(baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Rejected version preserves live state " + version);
            }
        }

        static int Format(byte[] bytes)
        {
            using var reader = new BinaryReader(new MemoryStream(bytes, false));
            reader.ReadString();
            return reader.ReadInt32();
        }
    }
}
#endif
