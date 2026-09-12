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
        public static string InterfacePlay(){SessionState.SetBool(Key+".InterfaceOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".InterfaceOnly",false);throw;}}
        [MenuItem("Landsong/ECS/Verify research HUD and panels (Play)")]
        public static string HudPanelsPlay(){SessionState.SetBool(Key+".HudPanelsOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".HudPanelsOnly",false);throw;}}
        public static string GarrisonPlay(){SessionState.SetBool(Key+".GarrisonOnly",true);try{return Run();}catch{SessionState.SetBool(Key+".GarrisonOnly",false);throw;}}
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
                smoke.GarrisonOnly=SessionState.GetBool(Key+".GarrisonOnly",false);
                smoke.HudPanelsOnly=SessionState.GetBool(Key+".HudPanelsOnly",false);
                smoke.Completed = (passed, detail) =>
                {
                    Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText(smoke.GarrisonOnly?"Library/LandsongEcs/garrison-ui-verification.txt":smoke.HudPanelsOnly?"Library/LandsongEcs/hud-panels-verification.txt":smoke.InterfaceOnly?"Library/LandsongEcs/interface-ui-verification.txt":"Library/LandsongEcs/scene-flow-verification.txt", (passed ? "PASS " : "FAIL ") + DateTime.Now.ToString("O") + "\n" + detail);
                    EditorApplication.ExitPlaymode();
                };
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                SessionState.SetBool(Key+".InterfaceOnly",false);
                SessionState.SetBool(Key+".HudPanelsOnly",false);
                SessionState.SetBool(Key+".GarrisonOnly",false);
                var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Key + ".Setup", ""));
                if (setup != null) EditorSceneManager.RestoreSceneManagerSetup(setup.Scenes);
            }
        }
    }
}
#endif
