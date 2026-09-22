#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Local developer automation. Requests are explicit and one-shot; migration is an explicit action only.
[InitializeOnLoad]
public static class EcsEditorAutomation
{
    [Serializable]
    public sealed class Request
    {
        public string Id, Action;
    }

    [Serializable]
    public sealed class Response
    {
        public string Id, Action, Status, Details;
    }

    const string DirectoryPath = "Library/LandsongEcs";
    const string RequestPath = DirectoryPath + "/request.json";
    static double next;
    static EcsEditorAutomation()
    {
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        next = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(RequestPath))
            return;
        Request request;
        try
        {
            request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath));
        }
        catch (Exception)
        {
            return;
        } // The writer may not have finished publishing the request.

        if (request == null || string.IsNullOrEmpty(request.Id) || SessionState.GetString("Landsong.ECS.Request", "") == request.Id)
            return;
        // SessionState is lost after a crash/restart. A completed request must remain one-shot.
        try
        {
            var resultPath = DirectoryPath + "/response.json";
            if (File.Exists(resultPath))
            {
                var completed = JsonUtility.FromJson<Response>(File.ReadAllText(resultPath));
                if (completed != null && completed.Id == request.Id && completed.Status != "running")
                {
                    SessionState.SetString("Landsong.ECS.Request", request.Id);
                    return;
                }
            }
        }
        catch (Exception)
        {
            return;
        } // Wait for a concurrently written response to finish.

        SessionState.SetString("Landsong.ECS.Request", request.Id);
        var response = new Response
        {
            Id = request.Id,
            Action = request.Action,
            Status = "success"
        };
        try
        {
            if (EditorUtility.scriptCompilationFailed && request.Action != "Refresh" && request.Action != "InspectEditor")
                throw new InvalidOperationException("Script compilation failed; refresh and resolve compiler errors before running automation against stale assemblies.");
            switch (request.Action)
            {
                case "InspectTransportWorkerAssets":
                    response.Details = Landsong.EditorTools.TransportWorkerContentAuthoring.Inspect();
                    break;
                case "CreateTransportWorker":
                    response.Details = Landsong.EditorTools.TransportWorkerContentAuthoring.Create();
                    break;
                case "MigrateContentResources":
                    response.Details = Landsong.EditorTools.ContentResourceOrganizer.Execute();
                    break;
                case "VerifyTransportWorker":
                    response.Details = Landsong.EditorTools.TransportWorkerVerification.Run();
                    break;
                case "VerifyTransportWorkerAnimationPlay":
                    response.Details = Landsong.EditorTools.TransportWorkerAnimationPlayVerification.Start();
                    break;
                case "InspectWolfAssets":
                    response.Details = Landsong.EditorTools.WolfContentAuthoring.Inspect();
                    break;
                case "CreateWolf":
                    response.Details = Landsong.EditorTools.WolfContentAuthoring.Create();
                    break;
                case "VerifyWolf":
                    response.Details = Landsong.EditorTools.WolfVerification.Run();
                    break;
                case "VerifyWolfAnimationPlay":
                    response.Details = Landsong.EditorTools.WolfAnimationPlayVerification.Start();
                    break;
                case "PreviewWolf":
                    response.Details = Landsong.EditorTools.WolfAnimationPlayVerification.Preview();
                    break;
                case "ConfigureWolfRig":
                    response.Details = Landsong.EditorTools.WolfContentAuthoring.ConfigureRig();
                    break;
                case "InspectWolfPreview":
                    response.Details = Landsong.EditorTools.WolfAnimationPlayVerification.Inspect();
                    break;
                case "VerifyNightLighting":
                    response.Details = Landsong.ECS.Editor.NightLightingVerification.Run();
                    break;
                case "SceneFlowLightingPlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.LightingPlay();
                    break;
                case "ConfigureNightFlow":
                    response.Details = Landsong.EditorTools.NightFlowSetup.Configure();
                    break;
                case "VerifyNightFlow":
                    response.Details = Landsong.ECS.Editor.NightPlanningVerification.Run() + "\n" + Landsong.ECS.Editor.PeacefulVerification.Run() + "\n" + Landsong.ECS.Editor.CoreRulesVerification.Run();
                    break;
                case "VerifyNightIntegration":
                    response.Details = Landsong.ECS.Editor.UiConfigurationVerification.Run() + "\n" + Landsong.ECS.Editor.EcsVerification.Run();
                    break;
                case "InspectSoldierAnimations":
                    response.Details = Landsong.EditorTools.SoldierAnimationSetup.Inspect();
                    break;
                case "VerifySoldierAnimations":
                    response.Details = Landsong.EditorTools.SoldierAnimationVerification.Run();
                    break;
                case "VerifySoldierAnimationVisibilityPlay":
                    response.Details = Landsong.EditorTools.SoldierAnimationVisibilityPlayVerification.Start();
                    break;
                case "VerifySoldierAnimationLifecyclePlay":
                    response.Details = Landsong.EditorTools.SoldierAnimationLifecyclePlayVerification.Start();
                    break;
                case "VerifyAnimationCombatRegression":
                    response.Details = Landsong.ECS.Editor.CombatVerification.Run() + "\n" + Landsong.ECS.Editor.SoldierVerification.Run() + "\n" + Landsong.ECS.Editor.PresentationVerification.Run();
                    break;
                case "CreateSoldierAnimationPreview":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.Create();
                    break;
                case "StartSoldierAnimationPreview":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.Start();
                    break;
                case "CaptureSoldierAnimationPreview":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.InspectAndCapture();
                    break;
                case "StopSoldierAnimationPreview":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.Stop();
                    break;
                case "SoldierAnimationPreview1":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetCount(1);
                    break;
                case "SoldierAnimationPreview50":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetCount(50);
                    break;
                case "SoldierAnimationPreview100":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetCount(100);
                    break;
                case "SoldierAnimationPreview200":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetCount(200);
                    break;
                case "SoldierAnimationPreviewTorch":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetFocusSlot(6);
                    break;
                case "SoldierAnimationPreviewAttack":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetFocusSlot(3);
                    break;
                case "SoldierAnimationPreviewDraw":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetFocusSlot(7);
                    break;
                case "SoldierAnimationPreviewSheathe":
                    response.Details = Landsong.EditorTools.SoldierAnimationPreview.SetFocusSlot(8);
                    break;
                case "InspectRecoveredMap":
                {
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var content = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Landsong.GridSystem.MapContentAuthoring>(true)).Single();
                    Landsong.EditorTools.GameMapWorkflow.Validate(content);
                    Landsong.EditorTools.GameMapWorkflow.ValidateCandidate(content, content.TargetMap);
                    response.Details = "Graphics API=" + SystemInfo.graphicsDeviceType + "; scene=" + scene.path + "; source and saved baked map validation passed; authored buildings=" + content.Previews.Length + "; baked buildings=" + content.TargetMap.InitialBuildings.Length + "; baked cells=" + content.TargetMap.Cells.Length + "; dirty=" + scene.isDirty;
                    break;
                }

                case "VerifyRecoveredMapBake":
                {
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11)
                        throw new InvalidOperationException("Recovery bake requires the verified DX11 editor session.");
                    var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    var content = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Landsong.GridSystem.MapContentAuthoring>(true)).Single();
                    Landsong.EditorTools.GameMapWorkflow.Bake(content);
                    response.Details = "DX11 full bake passed and saved: " + scene.path + "; buildings=" + content.TargetMap.InitialBuildings.Length;
                    break;
                }

                case "VerifyMapPopulation":
                    response.Details = Landsong.EditorTools.MapPopulationVerification.Run();
                    break;
                case "ConfigureMapPopulation":
                    response.Details = Landsong.EditorTools.MapPopulationVerification.ConfigureCurrent();
                    break;
                case "InspectInitialBuildings":
                    response.Details = Landsong.EditorTools.InitialBuildingDiagnostics.Inspect();
                    break;
                case "RepairInitialBuildings":
                    response.Details = Landsong.EditorTools.InitialBuildingDiagnostics.RepairCurrent();
                    break;
                case "VerifyInitialBuildingSetup":
                    response.Details = Landsong.EditorTools.InitialBuildingSetupVerification.Run();
                    break;
                case "VerifyMapBoundary":
                    response.Details = Landsong.EditorTools.MapBoundaryVerification.Run();
                    break;
                case "VerifyDynamicSpawn":
                    response.Details = Landsong.ECS.Editor.DynamicSpawnVerification.Run();
                    break;
                case "InspectMapBoundary":
                    response.Details = Landsong.EditorTools.MapBoundaryTools.Inspect(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Landsong.GridSystem.MapContentAuthoring>(true)).Single());
                    break;
                case "VerifyTerrainEnum":
                    response.Details = Landsong.EditorTools.TerrainEnumVerification.Run();
                    break;
                case "InspectCurrentTerrain":
                    response.Details = Landsong.EditorTools.TerrainEnumVerification.InspectCurrent();
                    break;
                case "VerifyBuildingModules":
                    response.Details = Landsong.ECS.Editor.BuildingModuleVerification.Run();
                    break;
                case "InspectBareGroundTiles":
                    response.Details = Landsong.EditorTools.BareGroundTileTools.Inspect();
                    break;
                case "ConfigureBareGroundTiles":
                    response.Details = Landsong.EditorTools.BareGroundTileTools.Configure();
                    break;
                case "PreviewBareGroundTiles":
                    response.Details = Landsong.EditorTools.BareGroundTileTools.Preview();
                    break;
                case "InspectSlopeReferences":
                    response.Details = Landsong.EditorTools.SlopeAssetTools.InspectReferences();
                    break;
                case "AlignSlopeVisualOffsets":
                    response.Details = Landsong.EditorTools.ProtrudingSlopeSetup.AlignCurrentVisualOffsets();
                    break;
                case "ExportSlopeReferences":
                    response.Details = Landsong.EditorTools.SlopeAssetTools.ExportReferences();
                    break;
                case "ImportSlopeAssets":
                    response.Details = Landsong.EditorTools.SlopeAssetTools.ImportAndVerify();
                    break;
                case "PreviewSlopeAssets":
                    response.Details = Landsong.EditorTools.SlopeAssetTools.CreatePreview();
                    break;
                case "ConfigureProtrudingSlopes":
                    Landsong.EditorTools.ProtrudingSlopeSetup.ConfigureCurrent();
                    response.Details = "My斜坡 and current map Blueprint/build layers configured.";
                    break;
                case "VerifyProtrudingSlopes":
                    response.Details = Landsong.EditorTools.ProtrudingSlopeVerification.Run();
                    break;
                case "PreviewProtrudingSlopes":
                    response.Details = Landsong.EditorTools.ProtrudingSlopePreview.Run();
                    break;
                case "ConfigureLayerTerrain":
                    Landsong.EditorTools.LayerTerrainSetup.ConfigureCurrent();
                    response.Details = "Layer terrain rules bound to the active source scene; scene left unsaved.";
                    break;
                case "VerifyLayerTerrain":
                    response.Details = Landsong.ECS.Editor.LayerTerrainVerification.Run();
                    break;
                case "VerifyTerrainConnections":
                    response.Details = Landsong.ECS.Editor.TerrainConnectionVerification.Run();
                    break;
                case "VerifyMapAuthoring":
                    response.Details = Landsong.ECS.Editor.GameMapWorkflowVerification.Run();
                    break;
                case "GenerateRuntimeMaps":
                    response.Details = Landsong.EditorTools.RuntimeMapArtifacts.EnsureAllCurrent(true);
                    break;
                case "RebuildRuntimeMaps":
                    response.Details = Landsong.EditorTools.RuntimeMapArtifacts.RebuildAll();
                    break;
                case "VerifyRuntimeMapArtifacts":
                    response.Details = Landsong.ECS.Editor.RuntimeMapArtifactVerification.Run();
                    break;
                case "InspectBillUi":
                    response.Details = Landsong.ECS.Editor.BillUiAssets.Inspect();
                    break;
                case "VerifyBill":
                    response.Details = Landsong.ECS.Editor.BillVerification.Run();
                    break;
                case "RenderBillUi":
                    response.Details = Landsong.ECS.Editor.UiVisualVerification.RenderBills();
                    break;
                case "VerifyInventoryUi":
                    response.Details = Landsong.ECS.Editor.InventoryVerification.Run() + "\n" + Landsong.ECS.Editor.InventoryUiVerification.Run();
                    break;
                case "InspectEditor":
                    response.Details = "playing=" + EditorApplication.isPlaying + "; changingPlayMode=" + EditorApplication.isPlayingOrWillChangePlaymode + "; paused=" + EditorApplication.isPaused + "; sceneFlowTest=" + SessionState.GetBool("Landsong.ECS.SceneFlowTest", false) + "; compilationFailed=" + EditorUtility.scriptCompilationFailed + "; snapshotVersion=" + Landsong.ECS.Persistence.SnapshotCodec.CurrentVersion;
                    break;
                case "InspectScenes":
                    response.Details = string.Join("\n", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i =>
                    {
                        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                        return scene.path + " | dirty=" + scene.isDirty;
                    }));
                    break;
                case "Refresh":
                    AssetDatabase.Refresh();
                    response.Details = "Refresh requested";
                    break;
                case "ConfigureSharedUiPreviews":
                    response.Details = Landsong.ECS.Editor.SharedUiPreviewRecipes.Run();
                    break;
                case "FinalizeSharedUi":
                    response.Details = Landsong.ECS.Editor.FinalizeSharedUiAssets.Run();
                    break;
                case "RefreshGameUiPreviews":
                    Landsong.ECS.Editor.GamePreviewAuthoring.RebindPreviewRecipes();
                    response.Details = "游戏预览配方与样例已刷新。";
                    break;
                case "RepairGameUiNames":
                    response.Details = Landsong.ECS.Editor.GameObjectNameRepair.Run();
                    break;
                case "FinalizeGameUiPreviews":
                    response.Details = Landsong.ECS.Editor.FinalizeGamePreviewAssets.Run();
                    break;
                case "FixGameHudLayout":
                    response.Details = Landsong.ECS.Editor.GameHudResponsiveLayout.Run();
                    break;
                case "CompleteGameFeaturePreviews":
                    response.Details = Landsong.ECS.Editor.GameFeaturePreviewRecipes.Run();
                    break;
                case "RenderInventoryUi":
                    response.Details = Landsong.ECS.Editor.UiVisualVerification.RenderInventory();
                    break;
                case "RenderUiPreviews":
                    response.Details = Landsong.ECS.Editor.UiVisualVerification.RenderAll();
                    break;
                case "VerifyUiFramework":
                    RunFramework(request);
                    response.Status = "running";
                    response.Details = "正在异步验证框架生命周期。";
                    break;
                case "SaveOpenScenes":
                    response.Details = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes() ? "Open scenes saved" : "No open scene changes saved";
                    break;
                case "Validate":
                    Landsong.ECS.Editor.ContentValidation.Validate();
                    response.Details = "Content validation passed";
                    break;
                case "VerifyPeaceful":
                    response.Details = Landsong.ECS.Editor.PeacefulVerification.Run();
                    break;
                case "VerifyCompilation":
                    response.Details = Landsong.ECS.Editor.ContentCompilationVerification.Run();
                    break;
                case "VerifyDomainCatalogs":
                    response.Details = "Compiled domain definitions: " + Landsong.ECS.Editor.DomainCatalogBuild.Validate();
                    break;
                case "CompileDisplayCatalogs":
                    Landsong.ECS.Editor.DisplayCatalogBuild.CompileCurrent();
                    response.Details = "Domain display catalogs compiled.";
                    break;
                case "VerifyGameplayRequests":
                    response.Details = Landsong.ECS.Editor.GameplayRequestVerification.Run();
                    break;
                case "VerifyAll":
                    response.Details = Landsong.ECS.Editor.ProjectVerification.RunAll();
                    break;
                case "VerifyIntelligence":
                    response.Details = Landsong.ECS.Editor.IntelligenceVerification.Run();
                    break;
                case "VerifyContentAuthoring":
                    response.Details = Landsong.ECS.Editor.ContentAuthoringWorkflowVerification.Run();
                    break;
                case "VerifyDocumentation":
                    response.Details = Landsong.ECS.Editor.DocumentationVerification.Run();
                    break;
                case "VerifyNonUi":
                    response.Details = Landsong.ECS.Editor.RewardAuthoringVerification.Run() + "\n" + Landsong.ECS.Editor.EntitlementRewardVerification.Run() + "\n" + Landsong.ECS.Editor.SessionBoundaryVerification.Run() + "\n" + Landsong.ECS.Editor.GameplayRequestVerification.Run() + "\n" + Landsong.ECS.Editor.EffectVerification.Run() + "\n" + Landsong.ECS.Editor.ResearchAuthorityVerification.Run();
                    break;
                case "SampleSimulationQueries":
                    response.Details = Landsong.ECS.Editor.SimulationQueryBenchmark.Run();
                    break;
                case "VerifyPersistenceContracts":
                    response.Details = Landsong.ECS.Editor.PersistenceContractVerification.Run() + "\n" + Landsong.ECS.Editor.ArchiveApplicationServiceVerification.Run();
                    break;
                case "VerifyInterfaceArchive":
                    response.Details = Landsong.ECS.Editor.InterfaceArchiveVerification.Run();
                    break;
                case "VerifyContentInspector":
                    Landsong.ECS.Editor.ContentInspectorVerification.RunGui();
                    response.Details = "Scheduled Odin inspector GUI verification.";
                    break;
                case "VerifyPortraitImport":
                    response.Details = Landsong.ECS.Editor.PortraitImportVerification.Run();
                    break;
                case "OpenPortraitImport":
                    Landsong.ECS.Editor.PortraitImportWindow.Open();
                    response.Details = "Opened portrait part import window.";
                    break;
                case "VerifyArchitecture":
                    response.Details = Landsong.ECS.Editor.ArchitectureVerification.Run();
                    break;
                case "VerifyUiConfiguration":
                    response.Details = Landsong.ECS.Editor.UiConfigurationVerification.Run();
                    break;
                case "VerifyPauseMenu":
                    response.Details = Landsong.ECS.Editor.PauseMenuVerification.Run();
                    break;
                case "InventoryUiPlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.InventoryPlay();
                    break;
                case "SceneFlowPlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.Run();
                    break;
                case "MapLoadingPlay":
                    Landsong.ECS.Editor.GameMapLoadingVerification.Run();
                    response.Details = "Scheduled generated-map loading and release verification.";
                    break;
                case "SceneFlowAudioPlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.AudioPlay();
                    break;
                case "SceneFlowInterfacePlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.InterfacePlay();
                    break;
                case "SceneFlowGarrisonPlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.GarrisonPlay();
                    break;
                case "SceneFlowSoldierPlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.SoldierPlay();
                    break;
                case "SceneFlowHudPanelsPlay":
                    response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.HudPanelsPlay();
                    break;
                case "StopSceneFlowTest":
                    if (!SessionState.GetBool("Landsong.ECS.SceneFlowTest", false))
                        throw new InvalidOperationException("No owned scene-flow test to stop.");
                    EditorApplication.ExitPlaymode();
                    response.Details = "Stopping owned test; its handler restores the original scene setup.";
                    break;
                case "Build":
                    response.Details = Landsong.ECS.Editor.EcsVerification.Build();
                    break;
                case "BuildRelease":
                    response.Details = Landsong.ECS.Editor.EcsVerification.BuildRelease();
                    break;
                case "ExitEditor":
                    if (EditorApplication.isPlayingOrWillChangePlaymode)
                        throw new InvalidOperationException("Exit Play Mode before restarting the editor.");
                    for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                        if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                            throw new InvalidOperationException("Save modified scenes before restarting the editor.");
                    EditorApplication.Exit(0);
                    response.Details = "Editor restart prepared; no unsaved scene changes.";
                    break;
                case "SyncPresentationText":
                    response.Details = Landsong.ECS.Editor.LanguageContentTools.SyncText();
                    break;
                default:
                    throw new InvalidOperationException("Unknown editor request: " + request.Action);
            }
        }
        catch (Exception error)
        {
            response.Status = "failed";
            response.Details = error.ToString();
            Debug.LogException(error);
        }

        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/response.json", JsonUtility.ToJson(response, true));
    }

    static async void RunFramework(Request request)
    {
        var result = new Response
        {
            Id = request.Id,
            Action = request.Action,
            Status = "success"
        };
        try
        {
            result.Details = await Moyo.Unity.Editor.UIFrameworkVerification.RunAsync();
        }
        catch (Exception error)
        {
            result.Status = "failed";
            result.Details = error.ToString();
            Debug.LogException(error);
        }

        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/response.json", JsonUtility.ToJson(result, true));
        File.WriteAllText(DirectoryPath + "/ui-framework-verification.txt", result.Status + "\n" + result.Details);
    }
}
#endif
