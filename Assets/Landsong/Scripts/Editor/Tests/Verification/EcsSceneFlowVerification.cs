#if UNITY_EDITOR
using System;
using System.IO;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Editor
{
    [InitializeOnLoad]
    public static class EcsSceneFlowVerification
    {
        const string Key = "Landsong.ECS.SceneFlowTest";
        [Serializable] sealed class Setup { public SceneSetup[] Scenes; }
        static EcsSceneFlowVerification() { EditorApplication.playModeStateChanged += Changed; EditorApplication.update += DriveOwnedTest; }
        static void DriveOwnedTest()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isPaused) return;
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
        }
        [MenuItem("Landsong/ECS/Verify four-scene flow (Play)")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            for (var i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save modified scenes before testing.");
            SessionState.SetString(Key + ".Setup", JsonUtility.ToJson(new Setup { Scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(EcsSceneFlow.Boot, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode(); return "Scheduled four-scene integration test.";
        }
        // Start a real rendered editor with -executeMethod and without -batchmode/-quit.
        public static void RunCommandLine()
        {
            // A command-line editor may start without any loaded/active scene to restore.
            if (!Array.Exists(EditorSceneManager.GetSceneManagerSetup(), scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.OpenScene(EcsSceneFlow.Boot, OpenSceneMode.Single);
            SessionState.SetBool(Key + ".ExitWhenDone", true);
            try { Run(); } catch { SessionState.SetBool(Key + ".ExitWhenDone", false); throw; }
        }
        public static string InventoryPlay(){SessionState.SetBool(Key+".InventoryOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".InventoryOnly",false);throw;}}
        public static void RequestsCommandLine()
        {
            SessionState.SetBool(Key + ".SceneRequestsOnly", true);
            try { RunCommandLine(); } catch { SessionState.SetBool(Key + ".SceneRequestsOnly", false); throw; }
        }
        public static string InterfacePlay(){SessionState.SetBool(Key+".InterfaceOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".InterfaceOnly",false);throw;}}
        [MenuItem("Landsong/ECS/Verify research HUD and panels (Play)")]
        public static string HudPanelsPlay(){SessionState.SetBool(Key+".HudPanelsOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".HudPanelsOnly",false);throw;}}
        public static string GarrisonPlay(){SessionState.SetBool(Key+".GarrisonOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".GarrisonOnly",false);throw;}}
        public static string SoldierPlay(){SessionState.SetBool(Key+".SoldierOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".SoldierOnly",false);throw;}}
        public static string LightingPlay(){SessionState.SetBool(Key+".LightingOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".LightingOnly",false);throw;}}
        [MenuItem("Landsong/ECS/Verify camera audio scene flow (Play)")]
        public static string AudioPlay(){SessionState.SetBool(Key+".AudioOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".AudioOnly",false);throw;}}
        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                // Unity suspends WaitForEndOfFrame when a Scene/Inspector tab owns the play workspace.
                // The visual smoke needs the actual Game view selected so screenshot frames can complete.
                EditorApplication.ExecuteMenuItem("Window/General/Game");
                var go = new GameObject("Four Scene Flow Verification"); UnityEngine.Object.DontDestroyOnLoad(go);
                var smoke=go.AddComponent<EcsPlayerSmoke>();smoke.InterfaceOnly=SessionState.GetBool(Key+".InterfaceOnly",false);
                smoke.InventoryOnly=SessionState.GetBool(Key+".InventoryOnly",false);
                smoke.GarrisonOnly=SessionState.GetBool(Key+".GarrisonOnly",false);
                smoke.SoldierOnly=SessionState.GetBool(Key+".SoldierOnly",false);
                smoke.LightingOnly=SessionState.GetBool(Key+".LightingOnly",false);
                smoke.HudPanelsOnly=SessionState.GetBool(Key+".HudPanelsOnly",false);
                smoke.AudioOnly=SessionState.GetBool(Key+".AudioOnly",false);
                smoke.SceneRequestsOnly=SessionState.GetBool(Key+".SceneRequestsOnly",false);
                smoke.Completed = (passed, detail) =>
                {
                    SessionState.SetBool(Key + ".Passed", passed);
                    Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText(smoke.LightingOnly?"Library/LandsongEcs/night-lighting-play-verification.txt":smoke.SceneRequestsOnly?"Library/LandsongEcs/scene-requests-play-verification.txt":smoke.AudioOnly?"Library/LandsongEcs/camera-audio-verification.txt":smoke.InventoryOnly?"Library/LandsongEcs/inventory-ui-play-verification.txt":smoke.GarrisonOnly?"Library/LandsongEcs/garrison-ui-verification.txt":smoke.SoldierOnly?"Library/LandsongEcs/soldier-night-play-verification.txt":smoke.HudPanelsOnly?"Library/LandsongEcs/hud-panels-verification.txt":smoke.InterfaceOnly?"Library/LandsongEcs/interface-ui-verification.txt":"Library/LandsongEcs/scene-flow-verification.txt", (passed ? "PASS " : "FAIL ") + DateTime.Now.ToString("O") + "\n" + detail);
                    EditorApplication.ExitPlaymode();
                };
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                SessionState.SetBool(Key+".InventoryOnly",false);
                SessionState.SetBool(Key+".InterfaceOnly",false);
                SessionState.SetBool(Key+".HudPanelsOnly",false);
                SessionState.SetBool(Key+".GarrisonOnly",false);
                SessionState.SetBool(Key+".SoldierOnly",false);
                SessionState.SetBool(Key+".LightingOnly",false);
                SessionState.SetBool(Key+".AudioOnly",false);
                SessionState.SetBool(Key+".SceneRequestsOnly",false);
                var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Key + ".Setup", ""));
                if (setup != null) EditorSceneManager.RestoreSceneManagerSetup(setup.Scenes);
                if (SessionState.GetBool(Key + ".ExitWhenDone", false))
                {
                    SessionState.SetBool(Key + ".ExitWhenDone", false);
                    EditorApplication.Exit(SessionState.GetBool(Key + ".Passed", false) ? 0 : 1);
                }
            }
        }
    }
}
#endif
