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
    [Serializable] public sealed class Request { public string Id, Action; }
    [Serializable] public sealed class Response { public string Id, Action, Status, Details; }
    const string DirectoryPath = "Library/LandsongEcs";
    const string RequestPath = DirectoryPath + "/request.json";
    static double next;
    static EcsEditorAutomation() { EditorApplication.update += Tick; }
    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        next = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(RequestPath)) return;
        Request request;
        try { request = JsonUtility.FromJson<Request>(File.ReadAllText(RequestPath)); }
        catch (Exception) { return; } // The writer may not have finished publishing the request.
        if (request == null || string.IsNullOrEmpty(request.Id) || SessionState.GetString("Landsong.ECS.Request", "") == request.Id) return;
        SessionState.SetString("Landsong.ECS.Request", request.Id);
        var response = new Response { Id = request.Id, Action = request.Action, Status = "success" };
        try
        {
            switch (request.Action)
            {
                case "InspectEditor":
                    response.Details = "playing=" + EditorApplication.isPlaying + "; changingPlayMode=" + EditorApplication.isPlayingOrWillChangePlaymode + "; paused=" + EditorApplication.isPaused + "; sceneFlowTest=" + SessionState.GetBool("Landsong.ECS.SceneFlowTest", false); break;
                case "InspectScenes":
                    response.Details = string.Join("\n", Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i => { var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i); return scene.path + " | dirty=" + scene.isDirty; })); break;
                case "Refresh": AssetDatabase.Refresh(); response.Details = "Refresh requested"; break;
                case "MigrateApplicationUi": response.Details = Landsong.ECS.Editor.ApplicationUiMigration.Run(); break;
                case "FinishApplicationUi": response.Details = Landsong.ECS.Editor.ApplicationUiMigration.FinishSceneInstallation(); break;
                case "MigrateGameUi": response.Details = Landsong.ECS.Editor.GamePanelRefactorMigration.Run(); break;
                case "FinalizeGameUi": response.Details = Landsong.ECS.Editor.FinalizeGameMigration.Run(); break;
                case "ConfigureSharedUiPreviews": response.Details = Landsong.ECS.Editor.SharedUiPreviewRecipes.Run(); break;
                case "FinalizeSharedUi": response.Details = Landsong.ECS.Editor.FinalizeSharedUiAssets.Run(); break;
                case "RefreshGameUiPreviews": Landsong.ECS.Editor.FinalizeGameMigration.RebindPreviewRecipes(); response.Details = "游戏预览配方与样例已刷新。"; break;
                case "RepairGameUiNames": response.Details = Landsong.ECS.Editor.GameObjectNameRepair.Run(); break;
                case "FinalizeGameUiPreviews": response.Details = Landsong.ECS.Editor.FinalizeGamePreviewAssets.Run(); break;
                case "FixGameHudLayout": response.Details = Landsong.ECS.Editor.GameHudResponsiveLayout.Run(); break;
                case "MigrateGameNavigation": response.Details = Landsong.ECS.Editor.GamePanelNavigationMigration.Run(); break;
                case "MigrateRowInteraction": response.Details = Landsong.ECS.Editor.GameRowInteractionMigration.Run(); break;
                case "CompleteGameFeaturePreviews": response.Details = Landsong.ECS.Editor.GameFeaturePreviewRecipes.Run(); break;
                case "MigrateWorldPresentation": response.Details = Landsong.ECS.Editor.WorldPresentationMigration.Run(); break;
                case "RenderUiPreviews": response.Details = Landsong.ECS.Editor.UiVisualVerification.RenderAll(); break;
                case "VerifyUiFramework": RunFramework(request); response.Status = "running"; response.Details = "正在异步验证框架生命周期。"; break;
                case "SaveOpenScenes": response.Details = UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes() ? "Open scenes saved" : "No open scene changes saved"; break;
                case "Validate": Landsong.ECS.Editor.ContentValidation.Validate(); response.Details = "Content validation passed"; break;
                case "VerifyAll": response.Details = Landsong.ECS.Editor.ProjectVerification.RunAll(); break;
                case "VerifyNonUi": response.Details = Landsong.ECS.Editor.RewardAuthoringVerification.Run() + "\n"
                    + Landsong.ECS.Editor.EntitlementRewardVerification.Run() + "\n"
                    + Landsong.ECS.Editor.SessionBoundaryVerification.Run() + "\n"
                    + Landsong.ECS.Editor.EffectVerification.Run() + "\n"
                    + Landsong.ECS.Editor.ResearchAuthorityVerification.Run(); break;
                case "SampleSimulationQueries": response.Details = Landsong.ECS.Editor.SimulationQueryBenchmark.Run(); break;
                case "VerifyPersistenceContracts": response.Details = Landsong.ECS.Editor.PersistenceContractVerification.Run() + "\n" + Landsong.ECS.Editor.ArchiveApplicationServiceVerification.Run(); break;
                case "VerifyContentInspector": Landsong.ECS.Editor.ContentInspectorVerification.RunGui(); response.Details="Scheduled Odin inspector GUI verification."; break;
                case "VerifyPortraitImport": response.Details = Landsong.ECS.Editor.PortraitImportVerification.Run(); break;
                case "OpenPortraitImport": Landsong.ECS.Editor.PortraitImportWindow.Open(); response.Details = "Opened portrait part import window."; break;
                case "VerifyArchitecture": response.Details = Landsong.ECS.Editor.ArchitectureVerification.Run(); break;
                case "VerifyUiConfiguration": response.Details = Landsong.ECS.Editor.UiConfigurationVerification.Run(); break;
                case "VerifyPauseMenu": response.Details = Landsong.ECS.Editor.PauseMenuVerification.Run(); break;
                case "SceneFlowPlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.Run(); break;
                case "SceneFlowInterfacePlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.InterfacePlay(); break;
                case "SceneFlowGarrisonPlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.GarrisonPlay(); break;
                case "SceneFlowHudPanelsPlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.HudPanelsPlay(); break;
                case "StopSceneFlowTest":
                    if (!SessionState.GetBool("Landsong.ECS.SceneFlowTest", false)) throw new InvalidOperationException("No owned scene-flow test to stop.");
                    EditorApplication.ExitPlaymode(); response.Details = "Stopping owned test; its handler restores the original scene setup."; break;
                case "Build": response.Details = Landsong.ECS.Editor.EcsVerification.Build(); break;
                case "BuildRelease": response.Details = Landsong.ECS.Editor.EcsVerification.BuildRelease(); break;
                case "ExitEditor":
                    if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before restarting the editor.");
                    for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                        if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save modified scenes before restarting the editor.");
                    EditorApplication.Exit(0);
                    response.Details = "Editor restart prepared; no unsaved scene changes."; break;
                case "SyncPresentationText": response.Details = Landsong.ECS.Editor.LanguageContentTools.SyncText(); break;
                default: throw new InvalidOperationException("Unknown editor request: " + request.Action);
            }
        }
        catch (Exception error) { response.Status = "failed"; response.Details = error.ToString(); Debug.LogException(error); }
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/response.json", JsonUtility.ToJson(response, true));
    }
    static async void RunFramework(Request request)
    {
        var result = new Response { Id = request.Id, Action = request.Action, Status = "success" };
        try { result.Details = await Moyo.Unity.Editor.UIFrameworkVerification.RunAsync(); }
        catch (Exception error) { result.Status = "failed"; result.Details = error.ToString(); Debug.LogException(error); }
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/response.json", JsonUtility.ToJson(result, true));
        File.WriteAllText(DirectoryPath + "/ui-framework-verification.txt", result.Status + "\n" + result.Details);
    }
}
#endif
