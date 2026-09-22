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
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class QuestVerification
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

        [MenuItem("Landsong/ECS/Verification/Quest")]
        public static string Run()
        {
            checks = 0;
            log = new StringBuilder();
            try
            {
                Configuration();
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
                File.WriteAllText("Library/LandsongEcs/quests-verification.txt", log.ToString());
            }
        }

        static void Configuration()
        {
            var c = AssetDatabase.LoadAssetAtPath<QuestCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/QuestCatalog.asset");
            QuestCatalogValidation.Validate(c);
            var quests = c.Definitions.Select(asset => asset).ToArray();
            Check(quests.Length == 11 && quests.Count(x => x.Behavior == QuestBehaviorFlags.Mainline) == 6 && quests.Count(x => x.Behavior == QuestBehaviorFlags.None) == 4 && quests.Single(x => x.Metadata.Id == "QM007").Behavior == (QuestBehaviorFlags.Mainline | QuestBehaviorFlags.Draft), "11 definitions: six mainline, four random, one disabled original placeholder");
            foreach (var d in quests.Where(x => (x.Behavior & QuestBehaviorFlags.Draft) == 0))
                Check(Keys(d.Objectives).All(key => key.Length >= 32), "Authored stable requirement IDs: " + d.Metadata.Id);
            void Expected(string id, int duration, int type, string rules)
            {
                var d = quests.Single(x => x.Metadata.Id == id);
                Check(d.DeadlineTurns == duration && (int)d.OfferType == type && Describe(d) == rules, "Retained requirements, rewards, penalties and parent: " + id);
            }

            Expected("main_build_farms_3", 0, 0, "Quest:main_collect_building_materials:1:0:0;Item:小麦:20:0:0;Item:卷心菜:10:0:0;Building:b农田:3:0:0;Turn::1:1:0");
            Expected("main_build_residential_houses_3", 0, 0, "Quest:main_plant_farms_3:1:0:0;Feature:feature.Technology:1:0:0;Item:金币:100:0:0;Building:b居民房:3:0:1");
            Expected("main_camera_survey", 0, 0, "Item:金币:500:0:0;Feature:feature.Inventory:1:0:0;CameraMove::1:0:0;CameraZoom::1:0:0");
            Expected("main_collect_building_materials", 0, 0, "Quest:main_camera_survey:1:0:0;Item:泥土:100:0:0;Item:原木:100:0:0;Item:石头:100:0:0;Blueprint:b农田:1:0:0;Feature:feature.Building:1:0:0;Owned:泥土:10:0:0;Owned:原木:10:0:0;Owned:石头:10:0:0");
            Expected("main_plant_farms_3", 0, 0, "Quest:main_build_farms_3:1:0:0;Item:小麦:200:0:0;Item:卷心菜:100:0:0;Blueprint:b居民房:1:0:0;Planted:b农田:3:0:0");
            Expected("main_select_technology", 0, 0, "Quest:main_build_residential_houses_3:1:0:0;Technology::1:0:0");
            Expected("QM007", 0, 0, "");
            Expected("random_exploration_supplies", 6, 3, "Item:金币:18:0:0;Submitted:原木:5:0:0;Submitted:石头:3:0:0;Penalty:金币:5:0:0");
            Expected("random_supply_soil", 5, 0, "Item:金币:15:0:0;Submitted:泥土:10:0:0;Penalty:金币:5:0:0"); // v8 authoring restores base amount; baking applies x3 to all item rules.
            Expected("random_supply_stone", 5, 0, "Item:金币:8:0:0;Submitted:石头:5:0:0;Penalty:金币:4:0:0"); // v8 authoring x2, runtime requirement remains 10.
            Expected("random_supply_wood", 5, 0, "Item:金币:15:0:0;Submitted:原木:10:0:0;Penalty:金币:3:0:0");
            var clone = CatalogFixture.Clone(c);
            try
            {
                var d = clone.Definitions.Single(asset => asset.Metadata.Id == "main_camera_survey");
                var move = d.Objectives.CameraMoveObjectives[0];
                var zoom = d.Objectives.CameraZoomObjectives[0];
                var key = zoom.Key;
                void Invalid(Action action, string label)
                {
                    action();
                    var failed = false;
                    try
                    {
                        QuestCatalogValidation.Validate(clone);
                    }
                    catch (InvalidOperationException)
                    {
                        failed = true;
                    }

                    Check(failed, label);
                }

                Invalid(() => zoom.Key = move.Key, "Duplicate requirement ID rejected");
                zoom.Key = key;
                Invalid(() => zoom.Key = "", "Missing requirement ID rejected");
                zoom.Key = key;
                Invalid(() => d.Objectives.CameraZoomObjectives[0].Count = 0, "Zero requirement rejected");
                d.Objectives.CameraZoomObjectives[0].Count = 1;
                var old = d.Prerequisites;
                d.Prerequisites = new DefinitionPrerequisitesSource
                {
                    QuestRequirements = new[]
                    {
                        new QuestRequirementSource
                        {
                            Quest = clone.Definitions.Single(asset => asset.Metadata.Id == "main_collect_building_materials"),
                            Required = 1
                        }
                    }
                };
                Invalid(() =>
                {
                }, "Cyclic mainline dependencies rejected");
                d.Prerequisites = old;
                QuestCatalogValidation.Validate(clone);
                Check(true, "Valid configuration still accepted after rejected edits");
            }
            finally
            {
                foreach (var asset in clone.Definitions)
                    UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        static T Formal<T>(string domain)
            where T : ScriptableObject => AssetDatabase.LoadAssetAtPath<T>("Assets/Landsong/ECSContent/Catalogs/Source/" + domain + "Catalog.asset");
        static BlobAssetReference<QuestCatalogBlob> Compile(QuestCatalogAsset catalog) => QuestCatalogCompiler.Build(catalog, new BuffCatalogIndex(Formal<BuffCatalogAsset>("Buff")), new BuildingCatalogIndex(Formal<BuildingCatalogAsset>("Building")), new ExpeditionCatalogIndex(Formal<ExpeditionCatalogAsset>("Expedition")), new FeatureCatalogIndex(Formal<FeatureCatalogAsset>("Feature")), new ItemCatalogIndex(Formal<ItemCatalogAsset>("Item")), new TechnologyCatalogIndex(Formal<TechnologyCatalogAsset>("Technology")));
        static System.Collections.Generic.IEnumerable<string> Keys(QuestObjectivesSource objectives) => objectives.BuildingObjectives.Select(row => row.Key).Concat(objectives.PlantedBuildingObjectives.Select(row => row.Key)).Concat(objectives.OwnedItemObjectives.Select(row => row.Key)).Concat(objectives.SubmittedItemObjectives.Select(row => row.Key)).Concat(objectives.TechnologyObjectives.Select(row => row.Key)).Concat(objectives.CameraMoveObjectives.Select(row => row.Key)).Concat(objectives.CameraZoomObjectives.Select(row => row.Key)).Concat(objectives.TurnObjectives.Select(row => row.Key));
        static string Describe(QuestDefinitionAsset source)
        {
            var terms = new System.Collections.Generic.List<(int Order, string Text)>();
            foreach (var row in source.Prerequisites.QuestRequirements)
                terms.Add((row.Order, $"Quest:{row.Quest.Metadata.Id}:{row.Required}:0:0"));
            foreach (var row in source.Rewards.Items)
                terms.Add((row.Order, $"Item:{row.Item.Metadata.Id}:{row.Quantity}:0:0"));
            foreach (var row in source.Rewards.Blueprints)
                terms.Add((row.Order, $"Blueprint:{row.Building.Metadata.Id}:{row.GrantedLevel}:0:0"));
            foreach (var row in source.Rewards.Buffs)
                terms.Add((row.Order, $"Buff:{row.Buff.Metadata.Id}:{row.GrantedLevel}:0:0"));
            foreach (var row in source.Rewards.Features)
                terms.Add((row.Order, $"Feature:{row.Feature.Metadata.Id}:{row.GrantedLevel}:0:0"));
            foreach (var row in source.Objectives.BuildingObjectives)
                terms.Add((row.Order, $"Building:{row.Building.Metadata.Id}:{row.Count}:{row.MinimumLevel}:{(row.CompletedOnly ? 1 : 0)}"));
            foreach (var row in source.Objectives.PlantedBuildingObjectives)
                terms.Add((row.Order, $"Planted:{row.Building.Metadata.Id}:{row.Count}:0:0"));
            foreach (var row in source.Objectives.OwnedItemObjectives)
                terms.Add((row.Order, $"Owned:{row.Item.Metadata.Id}:{row.Quantity}:0:0"));
            foreach (var row in source.Objectives.SubmittedItemObjectives)
                terms.Add((row.Order, $"Submitted:{row.Item.Metadata.Id}:{row.Quantity}:0:0"));
            foreach (var row in source.Objectives.CameraMoveObjectives)
                terms.Add((row.Order, $"CameraMove::{row.Count}:0:0"));
            foreach (var row in source.Objectives.CameraZoomObjectives)
                terms.Add((row.Order, $"CameraZoom::{row.Count}:0:0"));
            foreach (var row in source.Objectives.TechnologyObjectives)
                terms.Add((row.Order, $"Technology:{(row.Technology == null ? "" : row.Technology.Metadata.Id)}:{row.Count}:0:0"));
            foreach (var row in source.Objectives.TurnObjectives)
                terms.Add((row.Order, $"Turn::{row.Turns}:{(row.SinceAccepted ? 1 : 0)}:0"));
            foreach (var row in source.FailurePenalties)
                terms.Add((row.Order, $"Penalty:{row.Item.Metadata.Id}:{row.Quantity}:0:0"));
            return string.Join(";", terms.OrderBy(term => term.Order).Select(term => term.Text));
        }

        static void Map(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Quest baked map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var catalog = AssetDatabase.LoadAssetAtPath<QuestCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/QuestCatalog.asset");
                Entity Quest(string name)
                {
                    using var all = WorldQueries.OrderedEntities<Quest>(em);
                    foreach (var e in all)
                        if (em.GetComponentData<QuestDefinitionRef>(e).Definition == QuestDefinitions.Find(em, root, name))
                            return e;
                    return Entity.Null;
                }

                ulong Id(Entity e) => em.GetComponentData<Identity>(e).Id;
                void Stock(ItemId item, int count)
                {
                    InventoryOps.Remove(em, root, item, InventoryOps.Count(em, root, item));
                    Check(InventoryOps.Add(em, root, item, count) == count, "Fixture normal stock " + item);
                }

                var camera = Quest("main_camera_survey");
                Check(camera != Entity.Null && Quest("QM007") == Entity.Null, "Only actual first mainline discovered");
                Check(QuestOps.Tracking(em, root).Target == Id(camera), "Automatic mainline tracking initialized");
                Check(Quest("main_collect_building_materials") == Entity.Null, "Second mainline requires reward claim");
                GameRequestExecution.Execute(em, root, new CameraMovedRequest { });
                Check(em.GetComponentData<Quest>(camera).Status == QuestStatus.Active, "Camera move alone does not finish two requirements");
                GameRequestExecution.Execute(em, root, new CameraZoomedRequest { });
                Check(em.GetComponentData<Quest>(camera).Status == QuestStatus.Completed, "Actual camera commands complete immediately");
                Check(Quest("main_collect_building_materials") == Entity.Null, "Completion without claim keeps next task locked");
                var chainSlot = em.GetComponentData<Quest>(camera);
                chainSlot.ContainerSlot = QuestLifecycle.QuestContainerCapacity(em, WorldQueries.Find(em, chainSlot.Container)) - 1;
                em.SetComponentData(camera, chainSlot); // Leave earlier slots free to detect accidental reallocation.
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(camera) }) == ResultCode.Success, "First mainline reward claim succeeds");
                var materials = Quest("main_collect_building_materials");
                Check(materials != Entity.Null && QuestOps.Tracking(em, root).Target == Id(materials), "Claim discovers and auto-tracks next mainline");
                var nextSlot = em.GetComponentData<Quest>(materials);
                Check(nextSlot.Container == chainSlot.Container && nextSlot.ContainerSlot == chainSlot.ContainerSlot, "Successor inherits exact slot even when earlier slots are free");
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(camera) }) == ResultCode.Unavailable, "Already claimed mainline cannot reaward");
                var beforeHold = SnapshotCodec.Capture(em, root);
                ref var materialDefinition = ref QuestDefinitions.Get(em, root, em.GetComponentData<QuestDefinitionRef>(materials).Definition);
                var rules = materialDefinition.Objectives.OwnedItemObjectives.ToArray();
                foreach (var requirement in rules)
                    Stock(requirement.Item, requirement.Quantity);
                QuestLifecycle.EvaluateQuests(em, root);
                Check(em.GetComponentData<Quest>(materials).Status == QuestStatus.Completed, "Hold requirements complete without submission");
                foreach (var requirement in rules)
                {
                    Check(InventoryOps.Count(em, root, requirement.Item) >= requirement.Quantity, "Hold requirement does not consume item");
                    InventoryOps.Remove(em, root, requirement.Item, requirement.Quantity);
                }

                QuestLifecycle.EvaluateQuests(em, root);
                Check(em.GetComponentData<Quest>(materials).Status == QuestStatus.Completed, "Completed status is sticky after resource spending");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, beforeHold));
                var core = Entity.Null;
                using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                    foreach (var b in buildings)
                        if (em.GetComponentData<BuildingHousingStats>(b).IsCore != 0)
                            core = b;
                var offer = QuestLifecycle.CreateQuest(em, root, QuestDefinitions.Find(em, root, "random_exploration_supplies"), Id(core));
                var offerId = Id(offer);
                Check(em.GetComponentData<Quest>(offer).StartTurn == 0 && em.GetComponentData<Quest>(offer).Deadline == 0 && QuestLifecycle.QuestCount(em) == 1, "Offered task has no timer or capacity consumption");
                Check(GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(offer), Mode = (QuestTrackingMode)1 }) == ResultCode.InvalidTarget, "Offered task cannot be pinned");
                Check(GameRequestExecution.Execute(em, root, new AcceptQuestRequest { Quest = Id(offer) }) == ResultCode.Success && QuestLifecycle.QuestCount(em) == 2, "Accept starts active task and consumes one capacity");
                Check(GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(offer), Mode = (QuestTrackingMode)1 }) == ResultCode.Success && QuestOps.Tracking(em, root).Target == offerId, "Manual tracking overrides automatic mainline");
                using var copy = em.GetBuffer<QuestProgress>(offer).ToNativeArray(Allocator.Temp);
                var requirements = copy.ToArray();
                Check(requirements.Length == 2 && requirements[0].Key != requirements[1].Key, "Grouped submit produces two stable individual requirements");
                ref var offerDefinition = ref QuestDefinitions.Get(em, root, em.GetComponentData<QuestDefinitionRef>(offer).Definition);
                foreach (var requirement in offerDefinition.Objectives.SubmittedItemObjectives.ToArray())
                    Stock(requirement.Item, requirement.Quantity);
                var first = requirements[0];
                var rule = offerDefinition.Objectives.SubmittedItemObjectives.ToArray().Single(requirement => requirement.Key == first.Key);
                var quote = QuestOps.Submission(em, root, offer, first.Key.ToString());
                var submit = new SubmitQuestRequest
                {
                    Quest = offerId,
                    Item = quote.Item,
                    Quantity = 1,
                    RequirementKey = first.Key,
                    ExpectedQuote = new FixedString128Bytes(quote.Stamp)
                };
                Check(GameRequestExecution.Execute(em, root, new SubmitQuestRequest { Quest = offerId }) == ResultCode.InvalidTarget, "Submission without an explicit requirement is refused");
                Check(GameRequestExecution.Execute(em, root, submit) == ResultCode.Success && em.GetBuffer<QuestProgress>(offer)[0].Amount == 1 && em.GetBuffer<QuestProgress>(offer)[1].Amount == 0, "Selected quantity only increments selected requirement");
                Check(InventoryOps.Count(em, root, rule.Item) == rule.Quantity - 1, "Submission removes precisely one normal item");
                Check(GameRequestExecution.Execute(em, root, submit) == ResultCode.Unavailable, "Repeated stale confirmation cannot charge twice");
                var original = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                offer = WorldQueries.Find(em, offerId);
                Check(original.SequenceEqual(SnapshotCodec.Capture(em, root)) && QuestOps.Tracking(em, root).Target == offerId && em.GetBuffer<QuestProgress>(offer)[0].Amount == 1, "Current schema roundtrip preserves partial submit and tracking");
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Forecast preserves quests, tracking, inventory and RNG");
                var rollback = SnapshotCodec.Decode(em, root, original);
                rollback.Tracking = new QuestTracking
                {
                    Mode = 2
                };
                var rollbackFailed = false;
                try
                {
                    SnapshotCodec.Restore(em, root, rollback, probe: stage =>
                    {
                        if (stage == "root-published")
                            throw new InvalidOperationException("Injected quest tracking publication fault");
                    });
                }
                catch (InvalidOperationException)
                {
                    rollbackFailed = true;
                }

                Check(rollbackFailed && original.SequenceEqual(SnapshotCodec.Capture(em, root)) && em.Exists(offer), "Published root failure restores original tracking, partial progress and Entity handles");
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

                Reject(s => s.Tracking.Target = ulong.MaxValue, "Invalid tracking target rejected atomically");
                Reject(s => s.Tracking.Mode = 9, "Invalid tracking mode rejected atomically");
                Reject(s => s.Records.OfType<QuestSnapshot>().Single(x => x.Identity.Id == offerId).Progress[1].Key = first.Key, "Duplicate saved requirement rejected atomically");
                Reject(s => s.Records.OfType<QuestSnapshot>().Single(x => x.Identity.Id == offerId).Progress[0].Amount = -1, "Negative saved progress rejected atomically");
                Reject(s => s.Records.OfType<QuestSnapshot>().Single(x => x.Identity.Id == offerId).Progress = Array.Empty<QuestProgress>(), "Missing saved requirements rejected atomically");
                Reorder(em, root, original, offerId, catalog);
                offer = WorldQueries.Find(em, offerId);
                var session = em.GetComponentData<Session>(root);
                GameClock sessionClock = em.GetComponentData<GameClock>(root);
                session.Phase = Phase.Night;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                }

                Check(GameRequestExecution.Execute(em, root, new SubmitQuestRequest { Quest = Id(offer) }) == ResultCode.WrongPhase && GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(offer) }) == ResultCode.WrongPhase && GameRequestExecution.Execute(em, root, new AbandonQuestRequest { Quest = Id(offer) }) == ResultCode.WrongPhase, "Night rejects submit, claim and abandon");
                session.Phase = Phase.Day;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                }

                foreach (var p in requirements)
                {
                    var q = QuestOps.Submission(em, root, offer, p.Key.ToString());
                    if (q.Maximum == 0)
                        continue;
                    Check(GameRequestExecution.Execute(em, root, new SubmitQuestRequest { Quest = offerId, Item = q.Item, Quantity = q.Maximum, RequirementKey = p.Key, ExpectedQuote = new FixedString128Bytes(q.Stamp) }) == ResultCode.Success, "Submit remaining requirement " + p.Key);
                }

                Check(em.GetComponentData<Quest>(offer).Status == QuestStatus.Completed && QuestLifecycle.QuestCount(em) == 2, "Completed random task retains capacity until reward");
                Check(GameRequestExecution.Execute(em, root, new AbandonQuestRequest { Quest = Id(offer) }) == ResultCode.Unavailable, "Completed task cannot be abandoned");
                // Force zero capacity without changing provider layout to verify the reward transaction only.
                var slots = em.GetBuffer<InventorySlot>(root);
                var saved = slots.ToNativeArray(Allocator.Temp).ToArray();
                slots.Clear();
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(offer) }) == ResultCode.NoCapacity && em.Exists(offer) && em.GetBuffer<InventorySlot>(root).Length == 0, "Full inventory leaves all rewards and completed quest unclaimed");
                Check(QuestOps.Tracking(em, root).Target == offerId, "Failed reward claim retains current tracking");
                slots = em.GetBuffer<InventorySlot>(root);
                foreach (var s in saved)
                    slots.Add(s);
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(offer) }) == ResultCode.Success && !em.Exists(offer), "Random reward claim consumes task once");
                Check(QuestLifecycle.QuestCount(em) == 1, "Claim releases ordinary slot while mainline retains its shared slot");
                Check(QuestOps.Tracking(em, root).Mode == 0 && QuestOps.Tracking(em, root).Target == Id(Quest("main_collect_building_materials")), "Claimed manual task falls back to highest-value accepted task");
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = 0, Mode = (QuestTrackingMode)2 });
                var unpinned = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, unpinned));
                QuestLifecycle.EvaluateQuests(em, root);
                Check(QuestOps.Tracking(em, root).Mode == 2 && QuestOps.Tracking(em, root).Target == 0, "Explicit unpin survives load and evaluation");
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = 0, Mode = (QuestTrackingMode)0 });
                Check(QuestOps.Tracking(em, root).Target != 0, "Automatic mainline tracking can be restored");
                // Continue the formal chain with authored buildings and actual requirement evaluation.
                materials = Quest("main_collect_building_materials");
                foreach (var requirement in materialDefinition.Objectives.OwnedItemObjectives.ToArray())
                    Stock(requirement.Item, requirement.Quantity);
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(materials) }) == ResultCode.Success, "Material mainline reward unlocks farm task");
                Entity MakeBuilding(string name, bool operational)
                {
                    var def = BuildingDefinitions.Find(em, root, name);
                    var grid = em.GetComponentData<GridData>(root);
                    var cell = default(Unity.Mathematics.int2);
                    var found = false;
                    for (var y = 0; y < grid.Value.Value.Size.y && !found; y++)
                        for (var x = 0; x < grid.Value.Value.Size.x && !found; x++)
                        {
                            cell = grid.Value.Value.Min + new Unity.Mathematics.int2(x, y);
                            found = GridOps.CanPlace(em, root, def, cell, 0);
                        }

                    Check(found, "Legal chain fixture footprint " + name);
                    return BuildingCreation.Create(em, root, def, cell, 0, 1, operational);
                }

                var farms = new[]
                {
                    MakeBuilding("b农田", false),
                    MakeBuilding("b农田", false),
                    MakeBuilding("b农田", false)
                };
                var farmQuest = Quest("main_build_farms_3");
                QuestLifecycle.EvaluateQuests(em, root);
                Check(em.GetComponentData<Quest>(farmQuest).Status == QuestStatus.Active && em.GetBuffer<QuestProgress>(farmQuest)[0].Amount == 3, "Construction counts for farm objective but relative turn still required");
                {
                    session = em.GetComponentData<Session>(root);
                    sessionClock = em.GetComponentData<GameClock>(root);
                }

                sessionClock.Turn++;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                }

                QuestLifecycle.EvaluateQuests(em, root);
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(farmQuest) }) == ResultCode.Success, "Relative turn releases next mainline");
                foreach (var b in farms)
                {
                    var state = em.GetComponentData<Building>(b);
                    BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(b);
                    state.Stage = LifeStage.Operational;
                    stateFarming.Crop = CropDefinitions.Find(em, root, "crop.wheat");
                    {
                        em.SetComponentData(b, state);
                        em.SetComponentData(b, stateFarming);
                    }
                }

                QuestLifecycle.EvaluateQuests(em, root);
                var cropQuest = Quest("main_plant_farms_3");
                Check(em.GetComponentData<Quest>(cropQuest).Status == QuestStatus.Completed, "Three planted farms meet crop objective");
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(cropQuest) }) == ResultCode.Success, "Planting reward unlocks residential blueprint and task");
                var houses = new[]
                {
                    MakeBuilding("b居民房", false),
                    MakeBuilding("b居民房", false),
                    MakeBuilding("b居民房", false)
                };
                var houseQuest = Quest("main_build_residential_houses_3");
                QuestLifecycle.EvaluateQuests(em, root);
                Check(em.GetComponentData<Quest>(houseQuest).Status == QuestStatus.Active, "Unfinished houses do not satisfy operational-only objective");
                foreach (var b in houses)
                {
                    var state = em.GetComponentData<Building>(b);
                    state.Stage = LifeStage.Operational;
                    em.SetComponentData(b, state);
                }

                QuestLifecycle.EvaluateQuests(em, root);
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(houseQuest) }) == ResultCode.Success && ResearchOps.Unlocked(em, root), "Completed housing reward grants real technology permission");
                var techQuest = Quest("main_select_technology");
                Check(em.GetComponentData<Quest>(techQuest).Status == QuestStatus.Active, "No current research does not satisfy selection objective");
                Check(GameRequestExecution.Execute(em, root, new QueueResearchRequest { Technology = TechnologyDefinitions.Find(em, root, "TN_3_1_启蒙") }) == ResultCode.Success && em.GetComponentData<Quest>(techQuest).Status == QuestStatus.Completed, "Actual current research command completes final tutorial");
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(techQuest) }) == ResultCode.Success && QuestOps.Tracking(em, root).Target == 0, "Formal six-task chain ends with no placeholder or repeated reward");
                Check(QuestOps.RewardValue(em, root, QuestDefinitions.Find(em, root, "random_supply_wood")) == 15 && QuestOps.RewardValue(em, root, QuestDefinitions.Find(em, root, "random_supply_stone")) == 16 && QuestOps.RewardValue(em, root, QuestDefinitions.Find(em, root, "random_supply_soil")) == 45, "Reward value uses priced item amounts after baking multiplier exactly once");
                Check(QuestOps.RewardValue(em, root, QuestDefinitions.Find(em, root, "main_select_technology")) == 0, "Non-item unlocks do not invent monetary value");
                Entity palace = Entity.Null;
                using (var buildings = WorldQueries.OrderedEntities<Building>(em))
                    foreach (var e in buildings)
                        if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                            palace = e;
                var extra = new Entity[3];
                var names = new[]
                {
                    "random_supply_wood",
                    "random_supply_stone",
                    "random_supply_soil"
                };
                for (var i = 0; i < 3; i++)
                {
                    extra[i] = QuestLifecycle.CreateQuest(em, root, QuestDefinitions.Find(em, root, names[i]), Id(palace), i);
                    Check(GameRequestExecution.Execute(em, root, new AcceptQuestRequest { Quest = Id(extra[i]) }) == ResultCode.Success, "Accepted value-ranked fixture " + i);
                    var q = em.GetComponentData<Quest>(extra[i]);
                    q.Status = QuestStatus.Completed;
                    em.SetComponentData(extra[i], q);
                }

                var valueSorted = extra.OrderBy(e => e, System.Collections.Generic.Comparer<Entity>.Create((a, b) => QuestOps.CompareValue(em, root, a, b))).ToArray();
                Check(valueSorted[0] == extra[2] && valueSorted[1] == extra[1] && valueSorted[2] == extra[0], "Shared invitation comparator sorts reward value descending");
                var hs = em.GetComponentData<Quest>(houseQuest);
                hs.Status = QuestStatus.Completed;
                Check(QuestLifecycle.BindQuestContainer(em, ref hs), "Reactivated mainline fixture binds the fourth shared slot");
                em.SetComponentData(houseQuest, hs);
                em.DestroyEntity(techQuest);
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(houseQuest), Mode = (QuestTrackingMode)1 });
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(houseQuest) }) == ResultCode.Success, "Claim hands off even when all shared slots are occupied");
                techQuest = Quest("main_select_technology");
                var ts = em.GetComponentData<Quest>(techQuest);
                Check(ts.Container == hs.Container && ts.ContainerSlot == hs.ContainerSlot && QuestOps.Tracking(em, root).Target == Id(techQuest), "Same-slot successor takes priority over more valuable unrelated accepted tasks");
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(techQuest) }) == ResultCode.Success && QuestOps.Tracking(em, root).Target == Id(extra[2]), "No successor follows highest-value accepted task including ready rewards");
                var trackingSave = SnapshotCodec.Capture(em, root);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, trackingSave));
                Check(trackingSave.SequenceEqual(SnapshotCodec.Capture(em, root)), "Automatic selected target survives snapshot restore");
                using (var tasks = WorldQueries.OrderedEntities<Quest>(em))
                    for (var i = 0; i < 3; i++)
                        foreach (var e in tasks)
                            if (em.GetComponentData<QuestDefinitionRef>(e).Definition == QuestDefinitions.Find(em, root, names[i]))
                                extra[i] = e;
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(extra[0]), Mode = (QuestTrackingMode)1 });
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(extra[2]), Mode = (QuestTrackingMode)1 });
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(extra[0]), Mode = (QuestTrackingMode)2 });
                Check(QuestOps.Tracking(em, root).Target == Id(extra[2]), "Late uncheck of another card does not clear new tracked target");
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(extra[2]), Mode = (QuestTrackingMode)2 });
                Check(QuestOps.Tracking(em, root).Mode == 2 && QuestOps.Tracking(em, root).Target == 0, "Targeted uncheck clears matching tracking");
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(extra[0]), Mode = (QuestTrackingMode)1 });
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(extra[1]) }) == ResultCode.Success && QuestOps.Tracking(em, root).Target == Id(extra[0]), "Claiming another card preserves explicit current tracking");
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = 0, Mode = (QuestTrackingMode)2 });
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(extra[0]) }) == ResultCode.Success && QuestOps.Tracking(em, root).Mode == 2 && QuestOps.Tracking(em, root).Target == 0, "Claiming an untracked task respects explicit uncheck");
                GameRequestExecution.Execute(em, root, new TrackQuestRequest { Quest = Id(extra[2]), Mode = (QuestTrackingMode)1 });
                Check(GameRequestExecution.Execute(em, root, new ClaimQuestRequest { Quest = Id(extra[2]) }) == ResultCode.Success && QuestOps.Tracking(em, root).Target == 0, "Claiming last accepted task clears HUD without tracking invitations");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Reorder(EntityManager em, Entity root, byte[] original, ulong questId, QuestCatalogAsset source)
        {
            var clone = CatalogFixture.Clone(source);
            var old = em.GetComponentData<QuestCatalog>(root);
            try
            {
                var rules = clone.Definitions.Single(asset => asset.Metadata.Id == "random_exploration_supplies").Objectives.SubmittedItemObjectives;
                var indices = Enumerable.Range(0, rules.Length).ToArray();
                (rules[indices[0]].Order, rules[indices[1]].Order) = (rules[indices[1]].Order, rules[indices[0]].Order);
                using var blob = Compile(clone);
                em.SetComponentData(root, new QuestCatalog { Value = blob });
                var decoded = SnapshotCodec.Decode(em, root, original);
                var p = decoded.Records.OfType<QuestSnapshot>().Single(x => x.Identity.Id == questId).Progress;
                Check(p[0].Amount == 1 && blob.Value.Definitions[QuestDefinitions.Find(em, root, "random_exploration_supplies").Index].Objectives.SubmittedItemObjectives[1].Key == p[0].Key, "Stable IDs remap saved progress when authored requirements reorder");
                SnapshotCodec.Restore(em, root, decoded);
                Check(em.GetBuffer<QuestProgress>(WorldQueries.Find(em, questId))[0].Amount == 1, "Reordered content restores successfully");
                em.SetComponentData(root, old);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            }
            finally
            {
                em.SetComponentData(root, old);
                foreach (var asset in clone.Definitions)
                    UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }
    }
}
#endif
