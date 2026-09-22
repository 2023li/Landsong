#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class TechnologyVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            checks++;
            log.AppendLine("PASS " + label);
        }

        [MenuItem("Landsong/ECS/Verification/Technology")]
        public static string Run()
        {
            checks = 0;
            log = new StringBuilder();
            try
            {
                Configuration();
                Research();
                foreach (var path in Landsong.EditorTools.GameMapPaths.BakedScenes())
                    Map(path);
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
                File.WriteAllText("Library/LandsongEcs/technology-verification.txt", log.ToString());
            }
        }

        static void Configuration()
        {
            using var fixture = new ResearchTestFixture();
            using (var valid = fixture.Compile())
                Check(valid.IsCreated, "Synthetic graph supports dependencies outside catalog order");
            var original = JsonUtility.ToJson(fixture.First);
            void Reject(Action<TechnologyDefinitionAsset> configure, string label)
            {
                var invalid = ScriptableObject.CreateInstance<TechnologyDefinitionAsset>();
                try
                {
                    invalid.Metadata = new DefinitionMetadataSource { Id = "root", Name = "root" };
                    configure(invalid);
                    // Keep the catalog member's identity so prerequisite references still form the same graph.
                    EditorUtility.CopySerialized(invalid, fixture.First);
                    bool failed = false;
                    try
                    {
                        using var blob = fixture.Compile();
                    }
                    catch (InvalidOperationException)
                    {
                        failed = true;
                    }

                    Check(failed, label);
                }
                finally
                {
                    try
                    {
                        JsonUtility.FromJsonOverwrite(original, fixture.First);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(invalid);
                    }
                }
            }

            void Parent(TechnologyDefinitionAsset value, TechnologyDefinitionAsset parent, int count = 1)
            {
                value.Prerequisites.TechnologyRequirements = new[]
                {
                    new TechnologyRequirementSource
                    {
                        Technology = parent,
                        Required = count
                    }
                };
            }

            Reject(invalid => invalid.ResearchPointCost = -1, "Reject negative research cost");
            Reject(invalid => invalid.TreePosition = new Vector2(float.NaN, 0), "Reject nonfinite tree coordinates");
            Reject(invalid => Parent(invalid, fixture.Target), "Reject dependency cycles");
            Reject(invalid => Parent(invalid, null), "Reject missing prerequisites");
            Reject(invalid => Parent(invalid, fixture.Repeat, 2), "Reject unsupported prerequisite completion count");
            Reject(invalid => invalid.Prerequisites.BuildingRequirements = new[]
            {
                new BuildingRequirementSource
                {
                    Building = fixture.Building,
                    Required = 1
                }
            }, "Research prerequisites accept only research completion records");
            Reject(invalid => invalid.Rewards.Items = new[]
            {
                new ItemAmountSource
                {
                    Item = fixture.Coin,
                    Quantity = 0
                }
            }, "Reject nonpositive item reward");
            Reject(invalid => invalid.Rewards.Blueprints = new[]
            {
                new BlueprintRewardSource
                {
                    Building = fixture.Building,
                    GrantedLevel = 3
                }
            }, "Blueprint cannot exceed authored maximum level");
            Reject(invalid =>
            {
                Parent(invalid, fixture.Repeat);
                invalid.Prerequisites.TechnologyRequirements = new[]
                {
                    invalid.Prerequisites.TechnologyRequirements[0],
                    invalid.Prerequisites.TechnologyRequirements[0]
                };
            }, "Duplicate prerequisites cannot silently merge");
            Check(typeof(TechnologyDefinitionAsset).GetField("Repeatable").FieldType == typeof(bool), "Repeatability cannot contain undefined bit flags");
            Check(typeof(BuffRewardSource).GetField("Buff").FieldType == typeof(BuffDefinitionAsset), "Item definitions cannot be assigned as buff rewards");
            Check(typeof(FeatureRewardSource).GetField("Feature").FieldType == typeof(FeatureDefinitionAsset), "Building limit groups cannot be feature rewards");
            Check(typeof(TechnologyDefinitionAsset).GetField("UnitCosts") == null, "Research definitions expose no recruitment cost fields");
            var catalog = AssetDatabase.LoadAssetAtPath<TechnologyCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/TechnologyCatalog.asset");
            TechnologyCatalogValidation.Validate(catalog);
            var nodes = catalog.Definitions;
            Check(nodes.Length == 56, "All 56 formal technology nodes retained");
            var costs = new[]
            {
                5,
                8,
                12,
                18,
                26,
                36,
                48,
                62,
                78,
                96,
                116,
                140,
                168,
                200,
                240,
                285,
                335,
                500
            };
            foreach (var node in nodes)
            {
                var parts = node.Metadata.Id.Split('_');
                int row = int.Parse(parts[1]), column = int.Parse(parts[2]);
                Check(node.ResearchPointCost == costs[column - 1] && node.HasTreePosition && Mathf.Abs(node.TreePosition.x - (column - 1) * 220) < 2 && Mathf.Abs(node.TreePosition.y - (row - 1) * 150) < 2, "Retained cost and tree layout: " + node.Metadata.Id);
            }

            Check(nodes.All(node => !node.Repeatable), "All 56 formal technologies remain nonrepeatable");
            Check(nodes.Sum(node => node.Prerequisites.TechnologyRequirements.Length) == 104 && nodes.Sum(node => node.Rewards.Items.Length + node.Rewards.Blueprints.Length + node.Rewards.Buffs.Length + node.Rewards.Features.Length) == 24, "104 prerequisite edges and 24 reward mappings retained");
            var features = AssetDatabase.LoadAssetAtPath<FeatureCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/FeatureCatalog.asset");
            Check(features.Definitions.Length == 4, "Four real feature licenses have their own catalog");
            var quests = AssetDatabase.LoadAssetAtPath<QuestCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/QuestCatalog.asset");
            Check(quests.Definitions.Any(asset => asset.Rewards.Features.Any(reward => reward.Feature.Metadata.Id == ResearchOps.FeatureId)), "Mainline can grant research access");
        }

        static void Research()
        {
            using var fixture = new ResearchTestFixture();
            using var world = new World("Wave six isolated research");
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

                em.AddBuffer<TechnologyProgress>(root);
                ResearchTestFixture.AddFacts(em, root);
                em.AddBuffer<InventorySlot>(root);
                em.AddBuffer<PendingItem>(root);
                em.AddBuffer<GameEvent>(root);
                void Points(int n)
                {
                    ResearchState sResearchState = em.GetComponentData<ResearchState>(root);
                    sResearchState.Points = n;
                    {
                        em.SetComponentData(root, sResearchState);
                    }
                }

                int PointsNow() => em.GetComponentData<ResearchState>(root).Points;
                Check(!ResearchOps.Unlocked(em, root) && ResearchOps.Command(em, root, fixture.FirstId, false) == ResultCode.Unavailable && ResearchOps.Plan(em, root, fixture.TargetId) == ResultCode.Unavailable, "Locked feature blocks single and path commands");
                FeatureUnlocks.Unlock(em, root, ResearchTestFixture.AccessId);
                Check(ResearchOps.Unlocked(em, root), "Feature entitlement opens research");
                Check(ResearchOps.Command(em, root, fixture.TargetId, false) == ResultCode.MissingResearch && ResearchOps.Command(em, root, default, false) == ResultCode.InvalidContent, "Single enqueue rejects missing prerequisite and nontechnology");
                var path = ResearchOps.Path(em, root, fixture.TargetId);
                Check(path.Definitions.SequenceEqual(new[] { fixture.FirstId, fixture.MiddleId, fixture.TargetId }) && path.Remaining == 16, "Path topological order not catalog index order");
                Points(2);
                Check(ResearchOps.Plan(em, root, fixture.TargetId) == ResultCode.Success && PointsNow() == 2, "Planning does not charge points");
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Entry(em, root, fixture.FirstId).ResearchPoints == 2 && PointsNow() == 0, "Partial funding spends only available points");
                Check(ResearchOps.Command(em, root, fixture.FirstId, true) == ResultCode.Success && ResearchOps.Entry(em, root, fixture.FirstId).ResearchPoints == 2 && ResearchOps.Queue(em, root)[0].QueueOrder == 1, "Cancel keeps progress and compacts queue");
                Points(30);
                ResearchOps.Settle(em, root);
                Check(PointsNow() == 30 && ResearchOps.Completed(em, root, fixture.MiddleId) == 0, "Cancelled prerequisite pauses dependent queue without spending");
                var stale = ResearchOps.Fingerprint(em, root);
                ResearchOps.Command(em, root, fixture.MiddleId, true);
                var before = ResearchOps.Fingerprint(em, root);
                Check(ResearchOps.Plan(em, root, fixture.TargetId, stale) == ResultCode.Unavailable && before == ResearchOps.Fingerprint(em, root), "Stale path consent leaves state untouched");
                Check(ResearchOps.Plan(em, root, fixture.TargetId) == ResultCode.Success && ResearchOps.Path(em, root, fixture.TargetId).Remaining == 14, "Rebuild path reuses retained investment");
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, fixture.FirstId) == 1 && ResearchOps.Completed(em, root, fixture.MiddleId) == 1 && ResearchOps.Entry(em, root, fixture.TargetId).ResearchPoints == 8 && PointsNow() == 16, "One settlement completes funded prerequisites then holds full reward-blocked target");
                Check(ResearchOps.Completed(em, root, fixture.TargetId) == 0 && !BuildingBlueprints.Has(em, root, ResearchTestFixture.BuildingId) && !(PermanentBuffs.Level(em, root, ResearchTestFixture.BuffId) > 0) && !FeatureUnlocks.Has(em, root, ResearchTestFixture.RewardFeatureId), "No completion or nonitem grants before all rewards fit");
                Check(ResearchOps.Quote(em, root, fixture.TargetId).Status == ResearchStatus.AwaitingRewards, "Awaiting-reward status explicit");
                em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = 1, Index = 0, Item = default, SlotType = default });
                var testSlots = em.GetBuffer<InventorySlot>(root);
                var limited = testSlots[0];
                limited.Item = ResearchTestFixture.CoinId;
                limited.Count = 9;
                testSlots[0] = limited;
                ResearchOps.Settle(em, root);
                Check(InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 9 && ResearchOps.Completed(em, root, fixture.TargetId) == 0 && !BuildingBlueprints.Has(em, root, ResearchTestFixture.BuildingId), "Partially fitting reward rolls back items and grants together");
                limited.Item = default;
                limited.Count = 0;
                testSlots = em.GetBuffer<InventorySlot>(root);
                testSlots[0] = limited;
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, fixture.TargetId) == 1 && PointsNow() == 16 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 2 && BuildingBlueprints.Has(em, root, ResearchTestFixture.BuildingId, 2) && (PermanentBuffs.Level(em, root, ResearchTestFixture.BuffId) > 0) && FeatureUnlocks.Has(em, root, ResearchTestFixture.RewardFeatureId), "Deferred award commits all reward kinds once with no extra research charge");
                Check(ResearchOps.Command(em, root, fixture.TargetId, false) == ResultCode.Unavailable && ResearchOps.Queue(em, root).Count == 0, "Nonrepeatable completed node cannot enqueue again");
                ResearchOps.Command(em, root, fixture.RepeatId, false);
                ResearchOps.Settle(em, root);
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, fixture.RepeatId) == 1 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 3, "Zero-cost repeated technology completes only once per explicit enqueue");
                ResearchOps.Command(em, root, fixture.RepeatId, false);
                Check(ResearchOps.Command(em, root, fixture.RepeatId, false) == ResultCode.Unavailable, "Duplicate queue rejected");
                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, fixture.RepeatId) == 2 && InventoryOps.Count(em, root, ResearchTestFixture.CoinId) == 3 && PointsNow() == 16, "Repeat count increments without repeating first reward");
                var s = em.GetComponentData<Session>(root);
                PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                s.Phase = Phase.Night;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sPersistence);
                }

                Check(ResearchOps.Command(em, root, fixture.RepeatId, false) == ResultCode.WrongPhase && ResearchOps.Plan(em, root, fixture.RepeatId) == ResultCode.WrongPhase && ResearchOps.Command(em, root, fixture.RepeatId, true) == ResultCode.WrongPhase, "Night permits viewing but rejects all research writes");
                s.Phase = Phase.Day;
                sPersistence.CheckpointPending = 1;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sPersistence);
                }

                Check(ResearchOps.Plan(em, root, fixture.RepeatId) == ResultCode.Busy, "Checkpoint preparation locks planning");
                using var entries = em.GetBuffer<TechnologyProgress>(root).ToNativeArray(Allocator.Temp);
                ResearchOps.ValidateState(em, root, 16, entries.ToArray());
                Check(true, "Completed and repeated research passes restore validation");
                var failed = false;
                try
                {
                    ResearchOps.ValidateState(em, root, -1, entries.ToArray());
                }
                catch (InvalidDataException)
                {
                    failed = true;
                }

                Check(failed, "Reject negative saved points");
            }
            finally
            {
            }
        }

        static void Map(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Technology baked map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var target = TechnologyDefinitions.Find(em, root, "TN_4_2_木工术");
                var first = TechnologyDefinitions.Find(em, root, "TN_3_1_启蒙");
                Check(!ResearchOps.Unlocked(em, root) && GameRequestExecution.Execute(em, root, new QueueResearchRequest { Technology = first }) == ResultCode.Unavailable, "Real map starts with locked technology command");
                FeatureUnlocks.Unlock(em, root, FeatureDefinitions.Find(em, root, ResearchOps.FeatureId));
                Check(GameRequestExecution.Execute(em, root, new PlanResearchRequest { Technology = target }) == ResultCode.Success, "Real processor dispatches research path");
                ResearchState sResearchState = em.GetComponentData<ResearchState>(root);
                sResearchState.Points = 2;
                {
                    em.SetComponentData(root, sResearchState);
                }

                ResearchOps.Settle(em, root);
                var original = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                Check(original.SequenceEqual(SnapshotCodec.Capture(em, root)) && ResearchOps.Entry(em, root, first).ResearchPoints == 2 && ResearchOps.Queue(em, root).Count == 2, "Current schema roundtrip retains partial progress and path order");
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Forecast clones research without point, queue, grant or RNG mutation");
                void Reject(Action<SnapshotCodec.Snapshot> mutate, string label)
                {
                    var invalid = SnapshotCodec.Decode(em, root, original);
                    mutate(invalid);
                    var failed = false;
                    try
                    {
                        SnapshotCodec.Restore(em, root, invalid);
                    }
                    catch (InvalidDataException)
                    {
                        failed = true;
                    }

                    Check(failed && original.SequenceEqual(SnapshotCodec.Capture(em, root)), label);
                }

                Reject(x => x.Research[1].QueueOrder = x.Research[0].QueueOrder, "Duplicate saved queue order rejected atomically");
                Reject(x => x.Research[1].Technology = x.Research[0].Technology, "Duplicate saved research definition rejected atomically");
                Reject(x => x.Research[0].ResearchPoints = 99999, "Excess progress rejected atomically");
                Reject(x => x.Research[0].Completions = 1, "Completed nonrepeatable node cannot still be queued");
                Reject(x => x.ResearchState.Points = -1, "Negative saved research pool rejected atomically");
                {
                    sResearchState = em.GetComponentData<ResearchState>(root);
                }

                sResearchState.Points = 100;
                {
                    em.SetComponentData(root, sResearchState);
                }

                ResearchOps.Settle(em, root);
                Check(ResearchOps.Completed(em, root, target) == 1 && ResearchOps.Queue(em, root).Count == 0, "Real woodwork path completes in one funded settlement");
                ref var definition = ref TechnologyDefinitions.Get(em, root, target);
                for (int i = 0; i < definition.Rewards.Blueprints.Length; i++)
                {
                    var reward = definition.Rewards.Blueprints[i];
                    Check(BuildingBlueprints.Has(em, root, reward.Building, reward.GrantedLevel), "Woodwork blueprint retained: " + reward.Building.Index);
                }

                var completed = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, completed));
                ResearchOps.Settle(em, root);
                Check(completed.SequenceEqual(SnapshotCodec.Capture(em, root)), "Load cannot reaward completed technology");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
