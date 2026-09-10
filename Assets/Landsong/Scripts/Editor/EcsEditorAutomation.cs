#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Local developer automation. Requests are explicit, one-shot, and never install or migrate content.
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
                case "Validate": Landsong.ECS.Editor.ContentValidation.Validate(); response.Details = "Content validation passed"; break;
                case "VerifyAll": response.Details = Landsong.ECS.Editor.ProjectVerification.RunAll(); break;
                case "VerifyPortraitImport": response.Details = Landsong.ECS.Editor.PortraitImportVerification.Run(); break;
                case "OpenPortraitImport": Landsong.ECS.Editor.PortraitImportWindow.Open(); response.Details = "Opened portrait part import window."; break;
                case "VerifyArchitecture": response.Details = Landsong.ECS.Editor.ArchitectureVerification.Run(); break;
                case "SceneFlowPlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.Run(); break;
                case "SceneFlowInterfacePlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.InterfacePlay(); break;
                case "SceneFlowGarrisonPlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.GarrisonPlay(); break;
                case "SceneFlowHudPanelsPlay": response.Details = Landsong.ECS.Editor.EcsSceneFlowVerification.HudPanelsPlay(); break;
                case "StopSceneFlowTest":
                    if (!SessionState.GetBool("Landsong.ECS.SceneFlowTest", false)) throw new InvalidOperationException("No owned scene-flow test to stop.");
                    EditorApplication.ExitPlaymode(); response.Details = "Stopping owned test; its handler restores the original scene setup."; break;
                case "Build": response.Details = Landsong.ECS.Editor.EcsVerification.Build(); break;
                case "SyncPresentationText": response.Details = Landsong.ECS.Editor.LanguageContentTools.SyncText(); break;
                default: throw new InvalidOperationException("Unknown editor request: " + request.Action);
            }
        }
        catch (Exception error) { response.Status = "failed"; response.Details = error.ToString(); Debug.LogException(error); }
        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(DirectoryPath + "/response.json", JsonUtility.ToJson(response, true));
    }
}
#endif
