#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Landsong.Content;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Landsong.EditorTools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class SeasonWeatherVerification
    {
        [MenuItem("Landsong/ECS/Verification/Season and weather integration")]
        public static string Run()
        {
            var report = new StringBuilder();
            var checks = 0;
            void Check(bool condition, string label)
            {
                if (!condition) throw new InvalidOperationException("FAIL " + label);
                checks++;
                report.AppendLine("PASS " + label);
            }
            try
            {
                var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/BuildingCatalog.asset");
                Check(catalog != null, "Building catalog loads");
                var station = catalog.Definitions.Single(d => d.Metadata.Id == "b消防站");
                var temple = catalog.Definitions.Single(d => d.Metadata.Id == "b雷神殿");
                BuildingAuthoringWorkflow.Validate(station);
                BuildingAuthoringWorkflow.Validate(temple);
                BuildingCatalogValidation.Validate(station);
                BuildingCatalogValidation.Validate(temple);
                Check(station.Footprint == new Vector2Int(2, 2) && station.ConstructionTurns == 2 && station.ResourceConnectionActionPower == 30
                    && station.Capabilities.Workforce.Levels.Single().Capacity == 4, "Fire station authored parameters");
                Check(temple.Footprint == new Vector2Int(2, 2) && temple.MaximumCount == 1 && temple.ConstructionTurns == 1
                    && !temple.Capabilities.Workforce.Enabled && !temple.Capabilities.Effects.Enabled, "Temple is buildable placeholder only");
                var architecture = AssetDatabase.LoadAssetAtPath<TechnologyDefinitionAsset>("Assets/Landsong/ECSContent/Definitions/Technology/TN_4_8_建筑学.asset");
                Check(architecture.Rewards.Blueprints.Any(row => row.Building == station), "Architecture unlocks fire station");
                var content = AssetDatabase.LoadAssetAtPath<GameContentSetAsset>("Assets/Landsong/ECSContent/World/GameContentSet.asset");
                Check(content != null, "Content set loads");
                using (var blob = BuildingCatalogBaking.Compile(content))
                    Check(blob.Value.Definitions.Length == catalog.Definitions.Length, "Full building catalog compiles with new definitions");

                Check(SeasonWeatherOps.Season(1) == SeasonKind.Spring && SeasonWeatherOps.Season(30) == SeasonKind.Spring
                    && SeasonWeatherOps.Season(31) == SeasonKind.Summer && SeasonWeatherOps.Season(50) == SeasonKind.Summer
                    && SeasonWeatherOps.Season(51) == SeasonKind.Autumn && SeasonWeatherOps.Season(80) == SeasonKind.Autumn
                    && SeasonWeatherOps.Season(81) == SeasonKind.Winter && SeasonWeatherOps.Season(100) == SeasonKind.Winter
                    && SeasonWeatherOps.Season(101) == SeasonKind.Spring, "30/20/30/20 season boundaries");
                Check(CropGrowthOps.PerSettlement(SeasonKind.Spring) == 12 && CropGrowthOps.PerSettlement(SeasonKind.Winter) == 5
                    && CropGrowthOps.RemainingSettlements(10 * 5, 10, SeasonKind.Spring) == 5,
                    "Winter ten turns retain half growth and spring needs five more turns");

                var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
                using var store = new BlobAssetStore(128);
                using var world = new World("Season/weather verification", WorldFlags.Game);
                try
                {
                    EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                    var em = world.EntityManager;
                    var root = WorldQueries.Root(em);
                    Check(root != Entity.Null, "Map bakes with weather settings");
                    var ids = em.GetComponentData<FireSettings>(root);
                    Check(ids.Station.IsValid && ids.Temple.IsValid
                        && BuildingDefinitions.Get(em, root, ids.Station).Metadata.Id == "b消防站"
                        && BuildingDefinitions.Get(em, root, ids.Temple).Metadata.Id == "b雷神殿", "Fire and temple IDs resolve at bake");
                    WorldInitialization.Initialize(em, root);
                    var weather = em.GetComponentData<SeasonWeatherState>(root);
                    Check(weather.Initialized != 0 && weather.DayTurn == 1 && weather.Season == SeasonKind.Spring && weather.Temperature == 8, "New game starts spring day 1 at base temperature");
                    var settings = em.GetComponentData<SeasonWeatherSettings>(root);
                    for (var turn = 2; turn <= 201; turn++)
                    {
                        var clock = em.GetComponentData<GameClock>(root);
                        clock.Turn = turn;
                        em.SetComponentData(root, clock);
                        SeasonWeatherOps.Dawn(em, root);
                        weather = em.GetComponentData<SeasonWeatherState>(root);
                        var index = (int)weather.Season;
                        Check(weather.DayTurn == turn && weather.Season == SeasonWeatherOps.Season(turn)
                            && weather.Temperature >= settings.MinimumTemperature[index]
                            && weather.Temperature <= settings.MaximumTemperature[index]
                            && (weather.Weather != WeatherKind.Snow || weather.Temperature < 0)
                            && (weather.Weather != WeatherKind.Rain || weather.Temperature >= 0), "Daily state " + turn);
                    }
                    weather.Weather = WeatherKind.Rain;
                    weather.DayElapsed = 0;
                    weather.NextThunderAt = 1;
                    weather.LightningLimit = 3;
                    weather.LightningCount = 0;
                    em.SetComponentData(root, weather);
                    em.SetComponentData(root, new LightningViewport());
                    LightningOps.Tick(em, root, 1);
                    Check(em.GetBuffer<LightningVisualEvent>(root).Length == 1
                        && em.GetBuffer<LightningVisualEvent>(root)[0].Kind == LightningVisualKind.Flash
                        && em.GetComponentData<SeasonWeatherState>(root).LightningCount == 0,
                        "Off-map thunder flashes without consuming a strike");
                    var snapshot = SnapshotCodec.Capture(em, root);
                    var restored = SnapshotCodec.Decode(em, root, snapshot);
                    Check(restored.Weather.DayTurn == em.GetComponentData<SeasonWeatherState>(root).DayTurn
                        && restored.Weather.RandomState == em.GetComponentData<SeasonWeatherState>(root).RandomState,
                        "v35 snapshot retains daily weather and random stream");

                    var grid = em.GetComponentData<GridData>(root);
                    int2 Free(BuildingId definition)
                    {
                        for (var i = 0; i < grid.Value.Value.Cells.Length; i++)
                        {
                            var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                            if (GridOps.CanPlace(em, root, definition, cell, 0)) return cell;
                        }
                        throw new InvalidOperationException("No free verification cell");
                    }
                    var roadDefinition = BuildingDefinitions.Find(em, root, "b泥路");
                    var roadBuilding = BuildingCreation.Create(em, root, roadDefinition, Free(roadDefinition), 0, 1, true);
                    var roadCell = em.GetComponentData<BuildingPlacementState>(roadBuilding).Cell;
                    var road = em.GetBuffer<Occupancy>(root)[GridOps.Index(grid, roadCell)];
                    Check(road.Owner == em.GetComponentData<Identity>(roadBuilding).Id, "Road occupies expected grid cell");
                    weather = em.GetComponentData<SeasonWeatherState>(root);
                    weather.Weather = WeatherKind.Rain;
                    em.SetComponentData(root, weather);
                    var normalRoadCost = new RoadWeatherCostOps.Context(em, root).Effective(road);
                    weather.Weather = WeatherKind.Snow;
                    em.SetComponentData(root, weather);
                    Check(math.abs(new RoadWeatherCostOps.Context(em, root).Effective(road) - normalRoadCost * 1.3f) < .001f,
                        "Snow raises road traversal cost by 30 percent");
                    weather.Weather = WeatherKind.Sunny;
                    weather.LightningLimit = 0;
                    weather.LightningCount = 0;
                    weather.NextThunderAt = 0;
                    em.SetComponentData(root, weather);

                    var fireStation = BuildingCreation.Create(em, root, ids.Station, Free(ids.Station), 0, 1, true);
                    var fireTarget = BuildingCreation.Create(em, root, ids.Temple, Free(ids.Temple), 0, 1, true);
                    weather = em.GetComponentData<SeasonWeatherState>(root);
                    weather.Weather = WeatherKind.Sunny;
                    em.SetComponentData(root, weather);
                    using var sunnyReach = BuildingRangeOps.Reach(em, root, fireStation, Allocator.Temp);
                    var roadIndex = GridOps.Index(grid, roadCell);
                    var sunnyDistance = sunnyReach[roadIndex];
                    float RoadNodeCost()
                    {
                        foreach (var node in em.GetBuffer<SurfaceNavNode>(root))
                            if (math.all(node.Cell == roadCell) && node.Corridor == 0)
                                return node.Cost;
                        throw new InvalidOperationException("Road navigation node missing");
                    }
                    SurfaceNavigationGraph.Invalidate(em, root);
                    SurfaceNavigationGraph.Ensure(em, root);
                    var sunnyNavCost = RoadNodeCost();
                    weather.Weather = WeatherKind.Snow;
                    em.SetComponentData(root, weather);
                    using var snowReach = BuildingRangeOps.Reach(em, root, fireStation, Allocator.Temp);
                    SurfaceNavigationGraph.Invalidate(em, root);
                    SurfaceNavigationGraph.Ensure(em, root);
                    var snowNavCost = RoadNodeCost();
                    Check(math.isfinite(sunnyDistance) && snowReach[roadIndex] > sunnyDistance,
                        "Snow shrinks weighted building service reach across roads");
                    Check(math.abs(snowNavCost - sunnyNavCost * 1.3f) < .001f,
                        "Snow updates actual navigation node cost");
                    weather.Weather = WeatherKind.Sunny;
                    em.SetComponentData(root, weather);
                    SurfaceNavigationGraph.Invalidate(em, root);
                    var staff = em.GetComponentData<BuildingWorkforceState>(fireStation);
                    staff.Workers = 1;
                    em.SetComponentData(fireStation, staff);
                    var cameraObject = new GameObject("Weather verification camera");
                    try
                    {
                        var camera = cameraObject.AddComponent<Camera>();
                        camera.orthographic = true;
                        camera.orthographicSize = 6;
                        camera.aspect = 1;
                        camera.nearClipPlane = .3f;
                        camera.farClipPlane = 100;
                        camera.transform.position = (Vector3)EntityState.Position(em, fireTarget) + Vector3.up * 30;
                        camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                        var matrix = camera.projectionMatrix * camera.worldToCameraMatrix;
                        float4 Column(Vector4 value) => new float4(value.x, value.y, value.z, value.w);
                        em.SetComponentData(root, new LightningViewport
                        {
                            Available = 1,
                            CameraPosition = camera.transform.position,
                            Forward = camera.transform.forward,
                            Near = camera.nearClipPlane,
                            Far = camera.farClipPlane,
                            ViewProjection = new float4x4(Column(matrix.GetColumn(0)), Column(matrix.GetColumn(1)), Column(matrix.GetColumn(2)), Column(matrix.GetColumn(3))),
                        });
                        uint Seed(bool ground)
                        {
                            for (uint value = 1; value < 10000; value++)
                            {
                                var candidate = math.max(1u, math.hash(new uint2(value, 0xA47B37D3u)));
                                var random = new Unity.Mathematics.Random(candidate);
                                if (random.NextInt(100) >= 70 && random.NextBool() == ground)
                                    return candidate;
                            }
                            throw new InvalidOperationException("No deterministic lightning seed");
                        }
                        void Strike(bool ground)
                        {
                            var day = em.GetComponentData<SeasonWeatherState>(root);
                            day.Weather = WeatherKind.Rain;
                            day.LightningLimit = 1;
                            day.LightningCount = 0;
                            day.DayElapsed = 0;
                            day.NextThunderAt = 1;
                            day.RandomState = Seed(ground);
                            em.SetComponentData(root, day);
                            LightningOps.Tick(em, root, 1);
                        }
                        Strike(true);
                        Check(em.GetComponentData<SeasonWeatherState>(root).LightningCount == 1
                            && em.GetBuffer<LightningVisualEvent>(root).Length == 1
                            && em.GetBuffer<LightningVisualEvent>(root)[0].Kind == LightningVisualKind.Ground,
                            "Visible ground strike consumes one landing without damaging buildings");
                        Strike(false);
                        var strike = em.GetBuffer<LightningVisualEvent>(root)[0];
                        Check(em.GetComponentData<SeasonWeatherState>(root).LightningCount == 1
                            && strike.Kind == LightningVisualKind.Building
                            && WorldQueries.Find(em, strike.Target) != Entity.Null,
                            "Visible target branch can ignite an operational building");
                        var struck = WorldQueries.Find(em, strike.Target);
                        Check(em.GetComponentData<BuildingFireState>(struck).Burning != 0, "Targeted building enters fire state");
                        BuildingFireOps.Extinguish(em, root, struck);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(cameraObject); }
                    weather = em.GetComponentData<SeasonWeatherState>(root);
                    weather.Weather = WeatherKind.Sunny;
                    weather.LightningLimit = 0;
                    weather.LightningCount = 0;
                    weather.NextThunderAt = 0;
                    em.SetComponentData(root, weather);
                    Check(BuildingFireOps.Ignite(em, root, fireTarget) && !BuildingStatus.Operational(em, fireTarget),
                        "Lightning fire suspends a normal building");
                    FirefighterOps.Tick(em, root);
                    var response = Entity.Null;
                    using (var responders = WorldQueries.Entities<Firefighter>(em))
                        foreach (var responder in responders)
                            if (em.GetComponentData<Firefighter>(responder).Fire == em.GetComponentData<Identity>(fireTarget).Id)
                            {
                                response = responder;
                                break;
                            }
                    Check(response != Entity.Null && em.GetComponentData<Firefighter>(response).Stage == FirefighterStage.Outbound,
                        "Fire station automatically dispatches an available worker");
                    BuildingFireOps.EnterNight(em, root);
                    Check(em.GetComponentData<BuildingFireState>(fireTarget).Burning != 0
                        && em.GetComponentData<Building>(fireTarget).Stage == LifeStage.Operational,
                        "Outbound firefighter extends fire deadline through dusk");
                    var responderTransform = em.GetComponentData<LocalTransform>(response);
                    responderTransform.Position = em.GetComponentData<Firefighter>(response).Destination;
                    em.SetComponentData(response, responderTransform);
                    FirefighterOps.Tick(em, root);
                    Check(em.GetComponentData<BuildingFireState>(fireTarget).Burning == 0
                        && BuildingStatus.Operational(em, fireTarget)
                        && em.GetComponentData<Firefighter>(response).Stage == FirefighterStage.Returning,
                        "Firefighter arrival extinguishes fire and starts return");
                    var fireSnapshot = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                    Check(fireSnapshot.Records.Any(record => record is FirefighterSnapshot),
                        "v35 snapshot stores returning firefighter");
                    var fireTargetId = em.GetComponentData<Identity>(fireTarget).Id;
                    var responseId = em.GetComponentData<Identity>(response).Id;
                    SnapshotCodec.Restore(em, root, fireSnapshot);
                    fireTarget = WorldQueries.Find(em, fireTargetId);
                    response = WorldQueries.Find(em, responseId);
                    Check(fireTarget != Entity.Null && response != Entity.Null
                        && em.GetComponentData<BuildingFireState>(fireTarget).Burning == 0
                        && em.GetComponentData<Firefighter>(response).Stage == FirefighterStage.Returning,
                        "v35 restore reconstructs extinguished building and returning responder");
                    Check(BuildingFireOps.Ignite(em, root, fireTarget), "Building can burn again after extinguishing");
                    BuildingFireOps.EnterNight(em, root);
                    Check(em.GetComponentData<Building>(fireTarget).Stage == LifeStage.Ruined,
                        "Unrescued fire becomes repairable ruin at next major phase");

                    var farmDefinition = BuildingDefinitions.Find(em, root, "b农田");
                    var farm = BuildingCreation.Create(em, root, farmDefinition, Free(farmDefinition), 0, 1, true);
                    var farming = em.GetComponentData<BuildingFarmingState>(farm);
                    farming.Crop = CropDefinitions.Find(em, root, "crop.wheat");
                    farming.Seed = 1;
                    farming.AutoHarvest = 0;
                    em.SetComponentData(farm, farming);
                    var farmWorkers = em.GetComponentData<BuildingWorkforceState>(farm);
                    farmWorkers.Workers = 2;
                    em.SetComponentData(farm, farmWorkers);
                    var productionStep = typeof(BuildingProductionSettlement).GetMethod("Settle", BindingFlags.Static | BindingFlags.NonPublic);
                    Check(productionStep != null, "Farm settlement entry exists");
                    void Grow(int turn)
                    {
                        var clock = em.GetComponentData<GameClock>(root);
                        clock.Turn = turn;
                        em.SetComponentData(root, clock);
                        productionStep.Invoke(null, new object[] { em, root, farm });
                    }
                    Grow(81);
                    Grow(82);
                    Grow(83);
                    Check(em.GetComponentData<BuildingFarmingState>(farm).Progress == 15,
                        "Three winter settlements contribute 1.5 growth to a 3-turn crop");
                    Grow(101);
                    Check(em.GetComponentData<BuildingFarmingState>(farm).Progress == 27,
                        "Spring immediately adds 1.2 growth to winter progress");
                    Grow(102);
                    Check(em.GetComponentData<BuildingFarmingState>(farm).Progress == 30,
                        "Cross-season crop reaches its 3.0 growth threshold without resetting");
                    var treeDefinition = BuildingDefinitions.Find(em, root, "b树木1");
                    var tree = BuildingCreation.Create(em, root, treeDefinition, Free(treeDefinition), 0, 1, true);
                    var treeSlot = BuildingVisualResolver.Select(em, tree);
                    Check(treeSlot != Entity.Null && em.HasComponent<LocalTransform>(treeSlot), "Existing tree selects a baked visual slot");
                    em.SetComponentData(tree, new BuildingVisualSelection { Slot = treeSlot });
                    var treeRotation = em.GetComponentData<LocalTransform>(treeSlot).Rotation;
                    var swayObject = new GameObject("Tree wind verification");
                    try
                    {
                        var sway = swayObject.AddComponent<WorldPresentationView>();
                        typeof(WorldPresentationView).GetField("em", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sway, em);
                        typeof(WorldPresentationView).GetField("windTime", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sway, .7f);
                        var swayTree = typeof(WorldPresentationView).GetMethod("SwayTree", BindingFlags.Instance | BindingFlags.NonPublic);
                        var windDay = em.GetComponentData<SeasonWeatherState>(root);
                        windDay.Wind = WindKind.Strong;
                        windDay.WindDegrees = 45;
                        swayTree.Invoke(sway, new object[] { tree, em.GetComponentData<Identity>(tree).Id, windDay });
                        var moved = em.GetComponentData<LocalTransform>(treeSlot).Rotation;
                        Check(math.abs(math.dot(treeRotation.value, moved.value)) < .999999f,
                            "Strong wind changes the selected existing tree visual rotation");
                        windDay.Wind = WindKind.Calm;
                        swayTree.Invoke(sway, new object[] { tree, em.GetComponentData<Identity>(tree).Id, windDay });
                        var calm = em.GetComponentData<LocalTransform>(treeSlot).Rotation;
                        Check(math.abs(math.dot(treeRotation.value, calm.value)) > .999999f,
                            "Calm wind restores the selected tree visual rotation");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(swayObject); }
                    var visualCamera = new GameObject("Weather presentation verification camera");
                    try
                    {
                        SceneManager.MoveGameObjectToScene(visualCamera, scene);
                        var camera = visualCamera.AddComponent<Camera>();
                        Check(SceneManager.GetActiveScene() != camera.gameObject.scene,
                            "Weather test camera is outside the active loading scene");
                        using var visual = new WeatherPresentationController(camera);
                        var visualRoot = (GameObject)typeof(WeatherPresentationController)
                            .GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual);
                        Check(visualRoot.scene == camera.gameObject.scene,
                            "Weather placeholders belong to the camera scene instead of the active loading scene");
                        var states = new[] { SeasonKind.Spring, SeasonKind.Summer, SeasonKind.Autumn, SeasonKind.Winter };
                        var profiles = new VolumeProfile[4];
                        for (var i = 0; i < states.Length; i++)
                        {
                            var day = em.GetComponentData<SeasonWeatherState>(root);
                            day.Season = states[i];
                            day.Weather = i == 0 ? WeatherKind.Rain : i == 3 ? WeatherKind.Snow : WeatherKind.Sunny;
                            visual.Tick(day, false, .016f, em.GetBuffer<LightningVisualEvent>(root));
                            profiles[i] = visualRoot.GetComponent<Volume>().sharedProfile;
                            if (i == 0) Check(visualRoot.transform.Find("Rain").gameObject.activeSelf, "Rain placeholder emits in rain");
                            if (i == 3) Check(visualRoot.transform.Find("Snow").gameObject.activeSelf, "Snow placeholder emits in snow");
                        }
                        Check(profiles.Distinct().Count() == 4, "Four seasons have distinct URP postprocess profiles");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(visualCamera); }

                    var strikeUnit = typeof(LightningOps).GetMethod("StrikeUnit", BindingFlags.Static | BindingFlags.NonPublic);
                    Check(strikeUnit != null && response != Entity.Null && em.Exists(response), "Lightning unit strike entry and visible responder exist");
                    uint UnitSeed(bool survives)
                    {
                        for (uint value = 1; value < 10000; value++)
                        {
                            var candidate = math.max(1u, math.hash(new uint2(value, 0x6A49B317u)));
                            var random = new Unity.Mathematics.Random(candidate);
                            if ((random.NextInt(100) == 0) == survives)
                                return candidate;
                        }
                        throw new InvalidOperationException("No deterministic unit lightning seed");
                    }
                    void HitUnit(bool survives)
                    {
                        var random = new Unity.Mathematics.Random(UnitSeed(survives));
                        strikeUnit.Invoke(null, new object[] { em, root, response, random });
                    }
                    var templeId = em.GetComponentData<FireSettings>(root).Temple;
                    Check(!BuildingBlueprints.Has(em, root, templeId), "Temple blueprint starts locked");
                    HitUnit(true);
                    Check(BuildingBlueprints.Has(em, root, templeId) && em.GetComponentData<Health>(response).Current == 1,
                        "One-percent lightning survivor keeps identity and gains temple blueprint at one health");
                    var goldId = em.GetComponentData<CurrencySettings>(root).Gold;
                    var goldBefore = InventoryOps.Count(em, root, goldId) + InventoryOps.PendingCount(em, root, goldId);
                    HitUnit(true);
                    Check(InventoryOps.Count(em, root, goldId) + InventoryOps.PendingCount(em, root, goldId) == goldBefore + 1000,
                        "Repeat temple blueprint reward gives 1000 gold including pending storage");
                    var populationBefore = PopulationOps.Population(em, root);
                    var stationId = em.GetComponentData<Firefighter>(response).Station;
                    HitUnit(false);
                    var stationAfter = WorldQueries.Find(em, stationId);
                    Check(em.GetComponentData<Firefighter>(response).Stage == FirefighterStage.Dead
                        && em.GetComponentData<Health>(response).Current == 0
                        && PopulationOps.Population(em, root) == populationBefore - 1
                        && em.GetComponentData<BuildingWorkforceState>(stationAfter).Workers == 0,
                        "Lethal lightning kills the firefighter and removes one station worker and resident");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
                report.AppendLine("Assertions: " + checks);
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
                File.WriteAllText("Library/LandsongEcs/weather-verification.txt", report.ToString());
            }
        }
    }
}
#endif
