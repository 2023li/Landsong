#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class InvitationExpeditionVerification
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

        public static void FixturePermissions(EntityManager em, Entity root)
        {
            foreach (var name in new[]
            {
                "Building",
                "Inventory",
                "Expedition"
            }

            )
            {
                var d = FeatureDefinitions.Find(em, root, new FixedString128Bytes("feature." + name));
                if (d.IsValid)
                    FeatureUnlocks.Unlock(em, root, d);
            }
        }

        [MenuItem("Landsong/ECS/Verification/InvitationExpedition")]
        public static string Run()
        {
            checks = 0;
            log = new StringBuilder();
            try
            {
                Configuration();
                // Only the workflow acceptance map supplies the authored onboarding fixtures.
                Map(VerificationMap.EntityScene);
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception e)
            {
                log.AppendLine(e.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/invitations-expeditions-verification.txt", log.ToString());
            }
        }

        static T Formal<T>(string domain)
            where T : ScriptableObject => AssetDatabase.LoadAssetAtPath<T>("Assets/Landsong/ECSContent/Catalogs/Source/" + domain + "Catalog.asset");
        static BlobAssetReference<ExpeditionCatalogBlob> Compile(ExpeditionCatalogAsset catalog) => ExpeditionCatalogCompiler.Build(catalog, new BuffCatalogIndex(Formal<BuffCatalogAsset>("Buff")), new BuildingCatalogIndex(Formal<BuildingCatalogAsset>("Building")), new FeatureCatalogIndex(Formal<FeatureCatalogAsset>("Feature")), new ItemCatalogIndex(Formal<ItemCatalogAsset>("Item")), new QuestCatalogIndex(Formal<QuestCatalogAsset>("Quest")), new TechnologyCatalogIndex(Formal<TechnologyCatalogAsset>("Technology")));
        public static StartExpeditionRequest Departure(ulong site, ulong captain, ExpeditionId destination, int crew, ExpeditionQuote quote)
        {
            var request = new StartExpeditionRequest
            {
                Site = site,
                Captain = captain,
                Destination = destination,
                Crew = crew,
                ExpectedQuote = new FixedString128Bytes(quote.Stamp)
            };
            foreach (var supply in quote.Supplies)
                request.SupplyQuantities.Add(supply.Amount);
            return request;
        }

        static void Configuration()
        {
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/ECSContent/World/GameWorldTemplate.prefab");
            var settings = template.GetComponent<QuestGenerationSettingsAuthoring>().Settings;
            var expeditionSettings = template.GetComponent<ExpeditionSettingsAuthoring>().Settings;
            Check(settings.MarketValuePerStrength == 100 && settings.StrengthStep == 10, "Source conversion preserved");
            Check(expeditionSettings.PenaltyTurns == 5 && expeditionSettings.AttractionPerStack == 5, "Five-turn and five-attraction pension penalty preserved");
            var quests = Formal<QuestCatalogAsset>("Quest");
            using var questBlob = QuestCatalogCompiler.Build(quests, new BuffCatalogIndex(Formal<BuffCatalogAsset>("Buff")), new BuildingCatalogIndex(Formal<BuildingCatalogAsset>("Building")), new ExpeditionCatalogIndex(Formal<ExpeditionCatalogAsset>("Expedition")), new FeatureCatalogIndex(Formal<FeatureCatalogAsset>("Feature")), new ItemCatalogIndex(Formal<ItemCatalogAsset>("Item")), new TechnologyCatalogIndex(Formal<TechnologyCatalogAsset>("Technology")));
            foreach (var entry in new[]
            {
                ("random_supply_stone", 1, 2f, 10, 16, 8),
                ("random_supply_soil", 2, 3f, 30, 45, 15),
                ("random_supply_wood", 0, 1f, 10, 15, 3)
            }

            )
            {
                int index = Array.FindIndex(quests.Definitions, asset => asset.Metadata.Id == entry.Item1);
                ref var definition = ref questBlob.Value.Definitions[index];
                Check(definition.Intensity == entry.Item2 && quests.Definitions[index].ItemQuantityScale == entry.Item3 && definition.OfferWeight == 100, "Authored intensity/weight/scale " + entry.Item1);
                Check(definition.Objectives.SubmittedItemObjectives[0].Quantity == entry.Item4 && definition.Rewards.Items[0].Quantity == entry.Item5 && definition.FailurePenalties[0].Quantity == entry.Item6, "Requirements/rewards/penalties scaled once " + entry.Item1);
            }

            ref var low = ref questBlob.Value.Definitions[Array.FindIndex(quests.Definitions, asset => asset.Metadata.Id == "random_supply_wood")];
            ref var high = ref questBlob.Value.Definitions[Array.FindIndex(quests.Definitions, asset => asset.Metadata.Id == "random_supply_soil")];
            Check(QuestOfferOps.Weight(settings, ref low, 0) > QuestOfferOps.Weight(settings, ref high, 0), "Weak source favours low intensity");
            Check(QuestOfferOps.Weight(settings, ref low, 30) < QuestOfferOps.Weight(settings, ref high, 30), "Strong source favours higher intensity without modifying amounts");
            var expeditions = Formal<ExpeditionCatalogAsset>("Expedition");
            foreach (var entry in new[]
            {
                ("expedition.nearby_recon", 1, 3, .4f, .03f, .2f, 5, 1, 2),
                ("expedition.abandoned_fort", 2, 5, .25f, .035f, .35f, 10, 2, 3)
            }

            )
            {
                var definition = expeditions.Definitions.Single(asset => asset.Metadata.Id == entry.Item1);
                Check(definition.MinimumSiteLevel == entry.Item2 && definition.TravelTurns == entry.Item3 && definition.MinimumCrew == 10 && definition.MaximumCrew == 15 && definition.Repeatable, "Destination level/duration/crew/repeat " + definition.Metadata.Id);
                Check(definition.BaseSuccessChance == entry.Item4 && definition.SuccessChancePerCrew == entry.Item5 && definition.MaximumSuccessChance == .9f && definition.FailureCasualtyRatio == entry.Item6 && definition.BaseCompensation == entry.Item7 && definition.CompensationPerCrew == entry.Item8, "Destination probability/casualties/pension " + definition.Metadata.Id);
                Check(definition.Rewards.Items.Length == entry.Item9 && definition.Supplies.Length == 0, "Original rewards and intentionally empty supplies " + definition.Metadata.Id);
            }

            using var scope = new CatalogFixture.Scope();
            var copyQuests = scope.Clone(quests);
            var copyExpeditions = scope.Clone(expeditions);
            var task = copyQuests.Definitions.Single(asset => asset.Metadata.Id == "random_supply_soil");
            void InvalidQuest(Action change, string label)
            {
                change();
                bool rejected = false;
                try
                {
                    QuestCatalogValidation.Validate(copyQuests);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, label);
            }

            InvalidQuest(() => task.ItemQuantityScale = float.NaN, "NaN task scaling rejected");
            task.ItemQuantityScale = 3;
            InvalidQuest(() => task.OfferWeight = -1, "Negative weight rejected");
            task.OfferWeight = 100;
            InvalidQuest(() => task.Intensity = 4, "Unknown intensity rejected");
            task.Intensity = 2;
            var destination = copyExpeditions.Definitions.Single(asset => asset.Metadata.Id == "expedition.nearby_recon");
            destination.Supplies = new[]
            {
                new ExpeditionSupplySource
                {
                    Item = Formal<ItemCatalogAsset>("Item").Definitions.Single(asset => asset.Metadata.Id == "原木"),
                    MinimumQuantity = 10,
                    SuccessPerExtra = .01f,
                    RewardPerExtra = .02f
                }
            };
            using (var valid = Compile(copyExpeditions))
                Check(valid.IsCreated, "Optional authored supplies accepted");
            destination.Supplies[0].ExtraLimit = 6;
            bool invalid = false;
            try
            {
                using var blob = Compile(copyExpeditions);
            }
            catch (InvalidOperationException)
            {
                invalid = true;
            }

            Check(invalid, "Extra supply cannot exceed 50 percent minimum");
        }

        static void Map(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Wave eight map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                void Turn(int turn)
                {
                    var s = em.GetComponentData<Session>(root);
                    GameClock sClock = em.GetComponentData<GameClock>(root);
                    PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                    sClock.Turn = turn;
                    s.Phase = Phase.Day;
                    sPersistence.CheckpointPending = 0;
                    {
                        em.SetComponentData(root, s);
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sPersistence);
                    }
                }

                void Stock(ItemId item, int amount)
                {
                    InventoryOps.Remove(em, root, item, InventoryOps.Count(em, root, item));
                    Check(InventoryOps.Add(em, root, item, amount) == amount, "Fixture stock " + amount);
                }

                Entity NewBuilding(string name, int level = 1)
                {
                    var def = BuildingDefinitions.Find(em, root, name);
                    var grid = em.GetComponentData<GridData>(root);
                    for (var i = 0; i < grid.Value.Value.Cells.Length; i++)
                    {
                        var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                        if (GridOps.CanPlace(em, root, def, cell, 0))
                            return BuildingCreation.Create(em, root, def, cell, 0, level, true);
                    }

                    throw new InvalidOperationException("No fixture location");
                }

                Entity Quest(string name)
                {
                    using var all = WorldQueries.OrderedEntities<Quest>(em);
                    foreach (var e in all)
                        if (em.GetComponentData<QuestDefinitionRef>(e).Definition == QuestDefinitions.Find(em, root, name))
                            return e;
                    return Entity.Null;
                }

                var original = SnapshotCodec.Capture(em, root);
                foreach (var feature in new[]
                {
                    "Building",
                    "Inventory",
                    "Expedition"
                }

                )
                    Check(!FeatureOps.Unlocked(em, root, feature), "Formal new dynasty does not grant " + feature);
                var locked = new (string Name, Func<ResultCode> Execute)[]
                {
                    ("Build", () => GameRequestExecution.Execute(em, root, new BuildRequest())),
                    ("BuildRoad", () => GameRequestExecution.Execute(em, root, new BuildRoadRequest())),
                    ("MoveInventory", () => GameRequestExecution.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.MoveInventory })),
                    ("SortInventory", () => GameRequestExecution.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.SortInventory })),
                    ("StorePending", () => GameRequestExecution.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.StorePending })),
                    ("Discard", () => GameRequestExecution.Execute(em, root, new InventoryLayoutRequest { Action = InventoryLayoutAction.DiscardStored })),
                    ("StartExpedition", () => GameRequestExecution.Execute(em, root, new StartExpeditionRequest())),
                    ("ClaimExpedition", () => GameRequestExecution.Execute(em, root, new ClaimExpeditionRequest())),
                    ("AbandonExpedition", () => GameRequestExecution.Execute(em, root, new AbandonExpeditionRequest()))
                };
                foreach (var lockedRequest in locked)
                    Check(lockedRequest.Execute() == ResultCode.Unavailable && original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Locked request is mutation free " + lockedRequest.Name);
                GameRequestExecution.Execute(em, root, new CameraMovedRequest());
                GameRequestExecution.Execute(em, root, new CameraZoomedRequest());
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(Quest("main_camera_survey")) }) == ResultCode.Success && FeatureOps.Unlocked(em, root, "Inventory") && !FeatureOps.Unlocked(em, root, "Building"), "First actual mainline unlocks only inventory");
                foreach (var source in new[]
                {
                    "b树木1",
                    "b小土堆",
                    "b小石堆"
                }

                )
                {
                    using var buildings = WorldQueries.OrderedEntities<Building>(em);
                    var pile = Entity.Null;
                    foreach (var candidate in buildings)
                        if (em.GetComponentData<BuildingDefinitionRef>(candidate).Definition == BuildingDefinitions.Find(em, root, source))
                        {
                            pile = candidate;
                            break;
                        }

                    Check(pile != Entity.Null, "Authored starting map supplies tutorial harvest source " + source);
                    Check(GameRequestExecution.Execute(em, root, new HarvestBuildingRequest { Building = Id(pile) }) == ResultCode.Success && !FeatureOps.Unlocked(em, root, "Building"), "Initial harvest works before building permission " + source);
                }

                foreach (var item in new[]
                {
                    "原木",
                    "泥土",
                    "石头"
                }

                )
                    Check(InventoryOps.Count(em, root, ItemDefinitions.Find(em, root, item)) >= 10, "Tutorial materials earned by harvesting, not fixture stock " + item);
                QuestLifecycle.EvaluateQuests(em, root);
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(Quest("main_collect_building_materials")) }) == ResultCode.Success && FeatureOps.Unlocked(em, root, "Building") && !FeatureOps.Unlocked(em, root, "Expedition"), "Second actual mainline unlocks construction without early expedition grant");
                FixturePermissions(em, root);
                var clean = SnapshotCodec.Capture(em, root);
                var gold = ItemDefinitions.Find(em, root, "金币");
                var market = NewBuilding("b市场");
                var marketId = Id(market);
                BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                bWorkforce.Workers = em.GetComponentData<BuildingWorkforceStats>(market).Capacity;
                bWorkforce.StableWorkers = bWorkforce.Workers;
                {
                    em.SetComponentData(market, bWorkforce);
                }

                var before = SnapshotCodec.Capture(em, root);
                var quote = QuestOfferOps.Quote(em, root, market, 0);
                Check(quote.Code == ResultCode.Success && quote.Candidates.Count == 3 && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Source preview filters candidates and never consumes RNG");
                Stock(gold, 0);
                before = SnapshotCodec.Capture(em, root);
                Check(QuestOfferOps.Generate(em, root, market, 0, true) == ResultCode.InsufficientResources && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Paid recruit failure preserves inventory RNG cooldown and IDs");
                Stock(gold, 30);
                Check(QuestOfferOps.Generate(em, root, market, 0, true) == ResultCode.Success && InventoryOps.Count(em, root, gold) == 20, "Paid recruit consumes exact fee once");
                var offer = QuestOfferOps.Offered(em, marketId, 0);
                Check(offer != Entity.Null && em.GetComponentData<Quest>(offer).Status == QuestStatus.Offered, "Invitation is not automatically accepted");
                before = SnapshotCodec.Capture(em, root);
                Check(QuestOfferOps.Generate(em, root, market, 0, true) == ResultCode.Busy && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Full slot cannot recruit or pay again");
                {
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                }

                bWorkforce.Workers = 0;
                {
                    em.SetComponentData(market, bWorkforce);
                }

                QuestLifecycle.EvaluateQuests(em, root);
                Check(em.Exists(offer) && !QuestOfferOps.Available(em, root, market, out _), "Workforce stop retains existing offer");
                var next = em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn;
                QuestOfferOps.Settle(em, root);
                Check(em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn == next + 1, "Unavailable source pauses countdown");
                Check(GameRequestExecution.Execute(em, root, new AcceptQuestRequest { Quest = Id(offer) }) == ResultCode.Success, "Retained offer can be accepted while source paused");
                var acceptedId = Id(offer);
                BuildingLifecycle.Demolish(em, root, market);
                Check(em.Exists(offer) && em.GetComponentData<Quest>(offer).Status == QuestStatus.Active, "Demolition preserves accepted task");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                market = NewBuilding("b市场");
                {
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                }

                bWorkforce.Workers = em.GetComponentData<BuildingWorkforceStats>(market).Capacity;
                {
                    em.SetComponentData(market, bWorkforce);
                }

                ResourceNetworkOps.Record(em, root, market, ItemDefinitions.Find(em, root, "原木"), 100);
                var lifetime = em.GetComponentData<BuildingMarketState>(market).LifetimeValue;
                ResourceNetworkOps.SettleMarkets(em, root);
                Check(lifetime > 0 && em.GetComponentData<BuildingMarketState>(market).LifetimeValue == lifetime && em.GetComponentData<BuildingMarketState>(market).TurnValue == 0, "Lifetime source strength survives per-turn market clearing");
                QuestOfferOps.Settle(em, root);
                next = em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn;
                Check(next >= 31 && next <= 51, "Initial automatic cooldown uses authored random range");
                Turn(next - 1);
                QuestOfferOps.Settle(em, root);
                Check(QuestOfferOps.Offered(em, Id(market), 0) != Entity.Null, "Automatic cooldown fills one invitation");
                before = SnapshotCodec.Capture(em, root);
                var random = em.GetComponentData<SimulationRandomState>(root).State;
                QuestOfferOps.Settle(em, root);
                Check(em.GetComponentData<SimulationRandomState>(root).State == random, "Full source does not roll meaningless cooldowns");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, before));
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Current schema roundtrip preserves source lifetime and cooldown");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                var post = NewBuilding("b陆上远征所", 2);
                var postId = Id(post);
                {
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(post);
                }

                bWorkforce.Workers = bWorkforce.StableWorkers = 15;
                {
                    em.SetComponentData(post, bWorkforce);
                }

                PopulationState sessionPopulation = em.GetComponentData<PopulationState>(root);
                sessionPopulation.BasePopulation = 100;
                {
                    em.SetComponentData(root, sessionPopulation);
                }

                var destination = ExpeditionDefinitions.Find(em, root, "expedition.nearby_recon");
                var departure = ExpeditionOps.Quote(em, root, post, destination, 15);
                Check(departure.Code == ResultCode.Success && departure.Minimum == 10 && departure.Maximum == 15 && math.abs(departure.SuccessChance - .85f) < .00001f && departure.RewardBonus == .25f, "Crew bounds, success and full-crew bonus preview");
                Check(ExpeditionOps.Quote(em, root, post, destination, 9).Code == ResultCode.InsufficientPopulation, "Understaffed departure refused");
                var command = Departure(postId, 0, destination, departure.Crew, departure);
                InventoryOps.Add(em, root, gold, 1);
                before = SnapshotCodec.Capture(em, root);
                Check(GameRequestExecution.Execute(em, root, command) == ResultCode.Unavailable && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Stale departure confirmation cannot spend");
                departure = ExpeditionOps.Quote(em, root, post, destination, 15);
                command = Departure(postId, 0, destination, departure.Crew, departure);
                Check(GameRequestExecution.Execute(em, root, command) == ResultCode.Success && WorkforceSettlement.Locked(em, postId), "Departure locks current building workforce");
                Check(!QuestOfferOps.Available(em, root, post, out _), "Travelling site cannot produce invitations");
                before = SnapshotCodec.Capture(em, root);
                Check(GameRequestExecution.Execute(em, root, command) == ResultCode.Busy && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Only one travelling party per site");
                Entity Journey()
                {
                    using var all = WorldQueries.OrderedEntities<Expedition>(em);
                    return all[all.Length - 1];
                }

                var journey = Journey();
                var journeyId = Id(journey);
                var j = em.GetComponentData<Expedition>(journey);
                var grants = em.GetBuffer<UnlockedFeature>(root);
                for (var i = grants.Length - 1; i >= 0; i--)
                    if (grants[i].Feature == FeatureDefinitions.Find(em, root, "feature.Expedition"))
                        grants.RemoveAt(i);
                var oldArrival = j.Arrival;
                var frozenRandom = em.GetComponentData<SimulationRandomState>(root).State;
                ExpeditionOps.Settle(em, root);
                Check(em.GetComponentData<Expedition>(journey).Arrival == oldArrival + 1 && em.GetComponentData<SimulationRandomState>(root).State == frozenRandom, "Locked expedition pauses travel duration without rolling outcome");
                Check(GameRequestExecution.Execute(em, root, new AbandonExpeditionRequest { Expedition = journeyId }) == ResultCode.Unavailable && WorkforceSettlement.Locked(em, postId), "Locked action cannot abandon frozen party");
                FeatureUnlocks.Unlock(em, root, FeatureDefinitions.Find(em, root, "feature.Expedition"));
                j = em.GetComponentData<Expedition>(journey);
                j.SuccessChance = 1;
                em.SetComponentData(journey, j);
                Turn(j.Arrival - 1);
                before = SnapshotCodec.Capture(em, root);
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Arrival forecast preserves departure, supplies and RNG");
                ExpeditionOps.Settle(em, root);
                Check(em.GetComponentData<Expedition>(journey).Status == ExpeditionStatus.Success && !WorkforceSettlement.Locked(em, postId), "Successful arrival frees workforce and awaits manual claim");
                var exp = em.GetComponentData<BuildingExperienceState>(post).Experience;
                ExpeditionOps.Settle(em, root);
                Check(em.GetComponentData<BuildingExperienceState>(post).Experience == exp, "Arrival experience cannot settle twice");
                var resultBytes = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, resultBytes));
                Check(resultBytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Current schema departure/result snapshot roundtrip");
                post = WorldQueries.Find(em, postId);
                journey = WorldQueries.Find(em, journeyId);
                BuildingLifecycle.Demolish(em, root, post);
                Check(em.Exists(journey), "Completed result survives source demolition");
                Check(GameRequestExecution.Execute(em, root, new ClaimExpeditionRequest { Expedition = journeyId }) == ResultCode.Success && !em.Exists(journey), "Result claim works after source demolition");
                Check(GameRequestExecution.Execute(em, root, new ClaimExpeditionRequest { Expedition = journeyId }) == ResultCode.InvalidTarget, "Result cannot grant twice");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                post = NewBuilding("b陆上远征所", 2);
                postId = Id(post);
                {
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(post);
                }

                bWorkforce.Workers = bWorkforce.StableWorkers = 15;
                {
                    em.SetComponentData(post, bWorkforce);
                }

                {
                    sessionPopulation = em.GetComponentData<PopulationState>(root);
                }

                sessionPopulation.BasePopulation = 100;
                {
                    em.SetComponentData(root, sessionPopulation);
                }

                Stock(gold, 3);
                departure = ExpeditionOps.Quote(em, root, post, destination, 10);
                Check(GameRequestExecution.Execute(em, root, Departure(postId, 0, destination, 10, departure)) == ResultCode.Success, "Failure fixture departure");
                journey = Journey();
                j = em.GetComponentData<Expedition>(journey);
                j.SuccessChance = 0;
                em.SetComponentData(journey, j);
                Turn(j.Arrival - 1);
                var population = PopulationOps.Population(em, root);
                ExpeditionOps.Settle(em, root);
                j = em.GetComponentData<Expedition>(journey);
                Check(j.Status == ExpeditionStatus.Failure && j.Casualties == 2 && PopulationOps.Population(em, root) == population - 2 && em.GetComponentData<BuildingWorkforceState>(post).Workers == 13, "Failure removes actual crew casualties and population");
                Check(j.SubsidyRequired == 15 && j.SubsidyPaid == 3 && InventoryOps.Count(em, root, gold) == 0 && j.PenaltyStacks == 2 && ExpeditionOps.Penalty(em, root) == 10, "Partial pension pays available gold and applies attraction stacks");
                before = SnapshotCodec.Capture(em, root);
                ExpeditionOps.Settle(em, root);
                Check(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Failure casualties/pension/penalty are one-shot");
                var invalid = SnapshotCodec.Decode(em, root, before);
                invalid.Records.OfType<ExpeditionSnapshot>().Single().Expedition.SuccessChance = float.NaN;
                var rejected = false;
                try
                {
                    SnapshotCodec.Restore(em, root, invalid);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }

                Check(rejected && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Invalid expedition snapshot rejected atomically");
                Turn(em.GetComponentData<ExpeditionPenaltyState>(root).UntilTurn + 1);
                Check(ExpeditionOps.Penalty(em, root) == 0, "Pension penalty expires by configured turn");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                post = NewBuilding("b陆上远征所", 2);
                postId = Id(post);
                {
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(post);
                }

                bWorkforce.Workers = bWorkforce.StableWorkers = 15;
                {
                    em.SetComponentData(post, bWorkforce);
                }

                departure = ExpeditionOps.Quote(em, root, post, destination, 10);
                Check(GameRequestExecution.Execute(em, root, Departure(postId, 0, destination, 10, departure)) == ResultCode.Success, "Ruin fixture departs");
                population = PopulationOps.Population(em, root);
                BuildingLifecycle.Ruin(em, root, post);
                using (var all = WorldQueries.OrderedEntities<Expedition>(em))
                    Check(all.Length == 0 && !WorkforceSettlement.Locked(em, postId) && PopulationOps.Population(em, root) == population, "Ruined source withdraws travel immediately without extra casualties");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, clean));
                SupplyAndCapacity(em, root, NewBuilding, Turn, Stock);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void SupplyAndCapacity(EntityManager em, Entity root, Func<string, int, Entity> building, Action<int> turn, Action<ItemId, int> stock)
        {
            var originalCatalog = em.GetComponentData<ExpeditionCatalog>(root);
            using var scope = new CatalogFixture.Scope();
            var expeditions = scope.Clone(Formal<ExpeditionCatalogAsset>("Expedition"));
            using var buildings = new BuildingCatalogTestFixture(em, root);
            try
            {
                var destination = expeditions.Definitions.Single(asset => asset.Metadata.Id == "expedition.nearby_recon");
                destination.Repeatable = false;
                destination.Supplies = new[]
                {
                    new ExpeditionSupplySource
                    {
                        Item = Formal<ItemCatalogAsset>("Item").Definitions.Single(asset => asset.Metadata.Id == "原木"),
                        MinimumQuantity = 10,
                        SuccessPerExtra = .01f,
                        RewardPerExtra = .02f
                    }
                };
                var marketData = buildings.Buildings.Definitions.Single(asset => asset.Metadata.Id == "b市场");
                marketData.Capabilities.Quests.Invitations.Single().Slots = 2;
                marketData.MaximumLevel = 2;
                marketData.Capabilities.Quests.Invitations = marketData.Capabilities.Quests.Invitations.Append(new BuildingQuestInvitationSource { Level = 2, Slots = 1, Type = BuildingInvitationKind.Trade, MinimumRefreshTurns = 30, MaximumRefreshTurns = 50 }).ToArray();
                marketData.Capabilities.Quests.Capacity = marketData.Capabilities.Quests.Capacity.Append(new BuildingQuestCapacitySource { Level = 0, Slots = 4 }).ToArray();
                marketData.Capabilities.Maintenance.Enabled = true;
                marketData.Capabilities.Maintenance.Costs = marketData.Capabilities.Maintenance.Costs.Append(new BuildingMaintenanceCostSource { Level = 0, Item = buildings.Items.Definitions.Single(asset => asset.Metadata.Id == "金币"), Quantity = 1000000 }).ToArray();
                marketData.Capabilities.Quests.Invitations[0].Level = 1;
                using var blob = Compile(expeditions);
                em.SetComponentData(root, new ExpeditionCatalog { Value = blob });
                buildings.Apply();
                // Quest progress is keyed; changing another domain never requires rebinding it.
                var post = building("b陆上远征所", 2);
                var b = em.GetComponentData<Building>(post);
                BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(post);
                BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(post);
                bWorkforce.Workers = bWorkforce.StableWorkers = 15;
                {
                    em.SetComponentData(post, b);
                    em.SetComponentData(post, bWorkforce);
                    em.SetComponentData(post, bMaintenance);
                }

                stock(ItemDefinitions.Find(em, root, "原木"), 15);
                var q = ExpeditionOps.Quote(em, root, post, ExpeditionDefinitions.Find(em, root, "expedition.nearby_recon"), 15, new[] { 15 });
                Check(q.Code == ResultCode.Success && math.abs(q.SuccessChance - .9f) < .0001 && math.abs(q.RewardBonus - .35f) < .0001 && q.Rewards.Single(r => r.Item == ItemDefinitions.Find(em, root, "原木")).Amount == 16, "Extra supply succeeds with capped chance and frozen reward bonus");
                Check(ExpeditionOps.Quote(em, root, post, ExpeditionDefinitions.Find(em, root, "expedition.nearby_recon"), 15, new[] { 16 }).Code == ResultCode.InvalidContent, "Extra supply limit enforced in domain");
                var command = Departure(em.GetComponentData<Identity>(post).Id, 0, ExpeditionDefinitions.Find(em, root, "expedition.nearby_recon"), 15, q);
                Check(GameRequestExecution.Execute(em, root, command) == ResultCode.Success && InventoryOps.Count(em, root, ItemDefinitions.Find(em, root, "原木")) == 0, "Supply deducted atomically at departure");
                using var journeys = WorldQueries.OrderedEntities<Expedition>(em);
                var journey = journeys[0];
                Check(em.GetBuffer<ExpeditionSupply>(journey)[0].Amount == 15, "Assigned supplies stored on persistent expedition");
                var bytes = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Supply departure persists through rebuild");
                using (var all = WorldQueries.OrderedEntities<Expedition>(em))
                    foreach (var e in all)
                        Check(GameRequestExecution.Execute(em, root, new AbandonExpeditionRequest { Expedition = em.GetComponentData<Identity>(e).Id }) == ResultCode.Success, "Explicit abandon clears travelling party");
                Check(InventoryOps.Count(em, root, ItemDefinitions.Find(em, root, "原木")) == 0, "Abandon does not refund supplies");
                stock(ItemDefinitions.Find(em, root, "原木"), 15);
                post = WorldQueries.Find(em, command.Site);
                q = ExpeditionOps.Quote(em, root, post, command.Destination, 15, new[] { 15 });
                command = Departure(command.Site, command.Captain, command.Destination, 15, q);
                Check(GameRequestExecution.Execute(em, root, command) == ResultCode.Success, "Abandoned nonrepeatable destination can still be retried");
                using (var all = WorldQueries.OrderedEntities<Expedition>(em))
                    foreach (var e in all)
                    {
                        var state = em.GetComponentData<Expedition>(e);
                        state.SuccessChance = 1;
                        em.SetComponentData(e, state);
                        turn(state.Arrival - 1);
                    }

                ExpeditionOps.Settle(em, root);
                Check(ExpeditionOps.CompletedAt(em, post, command.Destination) && ExpeditionOps.Quote(em, root, post, command.Destination, 15).Code == ResultCode.Unavailable, "Successful nonrepeatable destination locks this source before reward claim");
                bytes = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
                post = WorldQueries.Find(em, command.Site);
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)) && ExpeditionOps.CompletedAt(em, post, command.Destination), "Per-site nonrepeatable history persists");
                using (var all = WorldQueries.OrderedEntities<Expedition>(em))
                    foreach (var e in all)
                        Check(GameRequestExecution.Execute(em, root, new ClaimExpeditionRequest { Expedition = em.GetComponentData<Identity>(e).Id }) == ResultCode.Success, "Claim removes result without removing nonrepeatable history");
                Check(ExpeditionOps.Quote(em, root, post, command.Destination, 15).Code == ResultCode.Unavailable, "Claim does not reopen completed nonrepeatable source");
                var otherPost = building("b陆上远征所", 1);
                BuildingWorkforceState otherStateWorkforce = em.GetComponentData<BuildingWorkforceState>(otherPost);
                otherStateWorkforce.Workers = otherStateWorkforce.StableWorkers = 15;
                {
                    em.SetComponentData(otherPost, otherStateWorkforce);
                }

                stock(ItemDefinitions.Find(em, root, "原木"), 15);
                Check(ExpeditionOps.Quote(em, root, otherPost, command.Destination, 10).Code == ResultCode.Success, "Other source can complete the same nonrepeatable destination as in legacy rules");
                var market = building("b市场", 1);
                {
                    b = em.GetComponentData<Building>(market);
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                    bMaintenance = em.GetComponentData<BuildingMaintenanceState>(market);
                }

                bWorkforce.Workers = em.GetComponentData<BuildingWorkforceStats>(market).Capacity;
                {
                    em.SetComponentData(market, b);
                    em.SetComponentData(market, bWorkforce);
                    em.SetComponentData(market, bMaintenance);
                }

                QuestOfferOps.Settle(em, root);
                var next = em.GetBuffer<QuestOfferSlot>(market)[0].NextTurn;
                turn(next - 1);
                QuestOfferOps.Settle(em, root);
                var id = em.GetComponentData<Identity>(market).Id;
                Check(QuestOfferOps.Offered(em, id, 0) != Entity.Null && QuestOfferOps.Offered(em, id, 1) == Entity.Null, "Multi-slot source fills only one vacancy per cooldown");
                var random = em.GetComponentData<SimulationRandomState>(root).State;
                QuestOfferOps.Settle(em, root);
                Check(em.GetComponentData<SimulationRandomState>(root).State == random, "Other vacancies wait for the restarted source cooldown");
                stock(ItemDefinitions.Find(em, root, "金币"), 30);
                Check(QuestOfferOps.Generate(em, root, market, 1, true) == ResultCode.Success, "Second stable slot can be filled by paid recruit");
                var retained = QuestOfferOps.Offered(em, id, 0);
                var removed = QuestOfferOps.Offered(em, id, 1);
                Check(GameRequestExecution.Execute(em, root, new AcceptQuestRequest { Quest = em.GetComponentData<Identity>(retained).Id }) == ResultCode.Success, "Accept first slot before source capacity shrink");
                {
                    b = em.GetComponentData<Building>(market);
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                    bMaintenance = em.GetComponentData<BuildingMaintenanceState>(market);
                }

                b.Level = 2;
                {
                    em.SetComponentData(market, b);
                    em.SetComponentData(market, bWorkforce);
                    em.SetComponentData(market, bMaintenance);
                }

                BuildingLevelConfiguration.Apply(em, root, market, false);
                Check(em.Exists(retained) && !em.Exists(removed) && em.GetBuffer<QuestOfferSlot>(market).Length == 2, "Capacity shrink removes only excess offered tasks and retains stable slot identities");
                {
                    b = em.GetComponentData<Building>(market);
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                    bMaintenance = em.GetComponentData<BuildingMaintenanceState>(market);
                }

                b.Level = 1;
                bMaintenance.Maintained = 1;
                {
                    em.SetComponentData(market, b);
                    em.SetComponentData(market, bWorkforce);
                    em.SetComponentData(market, bMaintenance);
                }

                BuildingLevelConfiguration.Apply(em, root, market, false);
                using (var all = WorldQueries.OrderedEntities<Quest>(em))
                    foreach (var e in all)
                        if (em.GetComponentData<Quest>(e).Mainline == 0)
                            em.DestroyEntity(e);
                QuestOps.RefreshTracking(em, root);
                {
                    b = em.GetComponentData<Building>(market);
                    bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                    bMaintenance = em.GetComponentData<BuildingMaintenanceState>(market);
                }

                bWorkforce.Workers = bWorkforce.StableWorkers = em.GetComponentData<BuildingWorkforceStats>(market).Capacity;
                {
                    em.SetComponentData(market, b);
                    em.SetComponentData(market, bWorkforce);
                    em.SetComponentData(market, bMaintenance);
                }

                for (var i = 0; i < 4; i++)
                {
                    var task = QuestLifecycle.CreateQuest(em, root, QuestDefinitions.Find(em, root, "random_supply_wood"), id, 100 + i);
                    Check(GameRequestExecution.Execute(em, root, new AcceptQuestRequest { Quest = em.GetComponentData<Identity>(task).Id }) == ResultCode.Success, "Capacity-loss fixture accepted task " + i);
                }

                Check(QuestLifecycle.QuestCount(em) == 5 && QuestLifecycle.QuestCapacity(em) == 8, "Pre-settlement capacity is sufficient");
                var held = new System.Collections.Generic.List<ulong>();
                using (var all = WorldQueries.OrderedEntities<Quest>(em))
                    foreach (var e in all)
                        if (em.GetComponentData<Quest>(e).Container == id)
                            held.Add(em.GetComponentData<Identity>(e).Id);
                Check(held.Count == 4, "Accepted tasks bind actual distinct market slots before using other providers");
                var completed = WorldQueries.Find(em, held[0]);
                var cq = em.GetComponentData<Quest>(completed);
                cq.Status = QuestStatus.Completed;
                em.SetComponentData(completed, cq);
                var failure = QuestLifecycle.FailureCosts(em, root, QuestDefinitions.Find(em, root, "random_supply_wood"))[0];
                stock(failure.Item, failure.Amount * 4);
                var available = InventoryOps.Count(em, root, failure.Item);
                var baseline = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, baseline));
                Check(baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Concrete task containers and completed occupancy round trip");
                var invalid = SnapshotCodec.Decode(em, root, baseline);
                var first = invalid.Records.OfType<QuestSnapshot>().Single(r => r.Identity.Id == held[0]);
                var second = invalid.Records.OfType<QuestSnapshot>().Single(r => r.Identity.Id == held[1]);
                second.Quest.ContainerSlot = first.Quest.ContainerSlot;
                var rejected = false;
                try
                {
                    SnapshotCodec.Restore(em, root, invalid);
                }
                catch (System.IO.InvalidDataException)
                {
                    rejected = true;
                }

                Check(rejected && baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Duplicate task occupancy rejected without mutating live world");
                foreach (var loss in new[]
                {
                    "maintenance",
                    "workers",
                    "stage",
                    "shrink",
                    "demolish"
                }

                )
                {
                    SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, baseline));
                    market = WorldQueries.Find(em, id);
                    {
                        b = em.GetComponentData<Building>(market);
                        bWorkforce = em.GetComponentData<BuildingWorkforceState>(market);
                        bMaintenance = em.GetComponentData<BuildingMaintenanceState>(market);
                    }

                    if (loss == "maintenance")
                    {
                        bMaintenance.Maintained = 0;
                        {
                            em.SetComponentData(market, b);
                            em.SetComponentData(market, bWorkforce);
                            em.SetComponentData(market, bMaintenance);
                        }
                    }
                    else if (loss == "workers")
                    {
                        bWorkforce.Workers = 0;
                        {
                            em.SetComponentData(market, b);
                            em.SetComponentData(market, bWorkforce);
                            em.SetComponentData(market, bMaintenance);
                        }
                    }
                    else if (loss == "stage")
                    {
                        b.Stage = LifeStage.Repairing;
                        {
                            em.SetComponentData(market, b);
                            em.SetComponentData(market, bWorkforce);
                            em.SetComponentData(market, bMaintenance);
                        }
                    }
                    else if (loss == "shrink")
                    {
                        BuildingQuestStats statsQuests = em.GetComponentData<BuildingQuestStats>(market);
                        statsQuests.Capacity = 0;
                        {
                            em.SetComponentData(market, statsQuests);
                        }
                    }
                    else
                        BuildingLifecycle.Demolish(em, root, market);
                    QuestLifecycle.ReconcileQuestContainers(em, root);
                    foreach (var taskId in held)
                        Check(WorldQueries.Find(em, taskId) == Entity.Null, loss + " removes the bound task including completed rewards");
                    Check(InventoryOps.Count(em, root, failure.Item) == math.max(0, available - failure.Amount * 4), loss + " charges normal abandonment penalties once");
                    var count = InventoryOps.Count(em, root, failure.Item);
                    QuestLifecycle.ReconcileQuestContainers(em, root);
                    Check(count == InventoryOps.Count(em, root, failure.Item), loss + " reconciliation cannot charge twice");
                    Check(QuestLifecycle.QuestCount(em) == 1, "Only the unaffected palace mainline remains after " + loss);
                }

                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, baseline));
                var injected = false;
                try
                {
                    NightEntryOps.Begin(em, root, false, 0, stage =>
                    {
                        if (stage == "day-settled")
                        {
                            injected = true;
                            foreach (var taskId in held)
                                Check(WorldQueries.Find(em, taskId) == Entity.Null, "Day settlement loses tasks whose containers fail maintenance");
                            throw new InvalidOperationException("quest-container-rollback-probe");
                        }
                    });
                }
                catch (InvalidOperationException error)when (error.Message == "quest-container-rollback-probe")
                {
                }

                Check(injected && baseline.SequenceEqual(SnapshotCodec.Capture(em, root)), "Failed night transaction restores removed quests, containers and penalties");
            }
            finally
            {
                em.SetComponentData(root, originalCatalog);
            }
        }
    }
}
#endif
