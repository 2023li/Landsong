using System;
using System.IO;
using System.Linq;
using Landsong.EditorTools;
using Landsong.ECS.Presentation;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Editor
{
    // Focused map-loading integration test. Does not replace the full UI/audio/scene smoke.
    [InitializeOnLoad]
    public static class GameMapLoadingVerification
    {
        const string Key = "Landsong.GameMapLoadingVerification";
        const string Report = "Library/LandsongEcs/map-loading-play-verification.txt";
        [Serializable]
        sealed class Setup
        {
            public SceneSetup[] Scenes;
        }

        static string[] ids;
        static int index, stage, assertions;
        static double deadline;
        static GameMapLoadingVerification()
        {
            EditorApplication.playModeStateChanged += Changed;
            EditorApplication.update += Tick;
        }

        [MenuItem("Landsong/ECS/Verify map loading (Play)")]
        public static void Run()
        {
            GameMapWorkflow.RequireEditMode();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("请先保存场景。");
            SessionState.SetString(Key + ".Setup", JsonUtility.ToJson(new Setup { Scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Finished", false);
            File.WriteAllText(Report, "Started " + DateTimeOffset.Now.ToString("O") + "\n");
            EditorSceneManager.OpenScene(EcsSceneFlow.Menu);
            EditorApplication.EnterPlaymode();
        }

        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false))
                return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                var menu = AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu);
                ids = menu.Maps.Select(m => m.Id).Concat(menu.Maps.Take(1).Select(m => m.Id)).ToArray();
                index = stage = assertions = 0;
                deadline = EditorApplication.timeSinceStartup + 120;
                Application.runInBackground = true;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
            }

            if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (RuntimeMapArtifactPlayGuard.Preparing)
                    return;
                if (!SessionState.GetBool(Key + ".Finished", false))
                    File.AppendAllText(Report, "INCOMPLETE: Play was stopped before completion.\n");
                SessionState.SetBool(Key, false);
                var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Key + ".Setup", ""));
                if (setup != null)
                    EditorSceneManager.RestoreSceneManagerSetup(setup.Scenes);
            }
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || SessionState.GetBool(Key + ".Finished", false) || !EditorApplication.isPlaying || ids == null)
                return;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Map loading stage " + stage + " / " + ids[index]);
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                    return;
                var em = world.EntityManager;
                if (stage == 0)
                {
                    if (SceneManager.GetActiveScene().path != EcsSceneFlow.Menu || EcsSceneFlow.Busy || UnityEngine.Object.FindFirstObjectByType<UI_StartPanel>() == null)
                        return;
                    if (WorldQueries.Root(em) != Entity.Null)
                        throw new InvalidOperationException("Menu still owns a simulation root.");
                    EcsSceneFlow.Begin(new EcsSceneFlow.Request(ids[index]));
                    stage = 1;
                    deadline = EditorApplication.timeSinceStartup + 120;
                }
                else if (stage == 1)
                {
                    if (!EcsSceneFlow.GameReady)
                        return;
                    var root = WorldQueries.Root(em);
                    if (root == Entity.Null || em.GetComponentData<MapIdentity>(root).Id.ToString() != ids[index] || !em.HasComponent<SimulationReady>(root))
                        throw new InvalidOperationException("Loaded map identity/readiness mismatch.");
                    using var roots = WorldQueries.Entities<Session>(em);
                    if (roots.Length != 1 || SceneManager.GetActiveScene().path != EcsSceneFlow.Game)
                        throw new InvalidOperationException("A map must own exactly one simulation in Game.");
                    assertions += 2;
                    File.AppendAllText(Report, "PASS load " + ids[index] + ": correct map, ready simulation, single owner\n");
                    EcsSceneFlow.ReturnToMenu();
                    stage = 2;
                    deadline = EditorApplication.timeSinceStartup + 120;
                }
                else if (stage == 2)
                {
                    if (SceneManager.GetActiveScene().path != EcsSceneFlow.Menu || EcsSceneFlow.Busy || UnityEngine.Object.FindFirstObjectByType<UI_StartPanel>() == null)
                        return;
                    using var owned = WorldQueries.Entities<SimulationOwner>(em);
                    if (WorldQueries.Root(em) != Entity.Null || owned.Length != 0)
                        throw new InvalidOperationException("Returning to menu did not release the map.");
                    assertions++;
                    File.AppendAllText(Report, "PASS release " + ids[index] + "\n");
                    index++;
                    if (index == ids.Length)
                    {
                        Finish(null);
                        return;
                    }

                    stage = 0;
                    deadline = EditorApplication.timeSinceStartup + 120;
                }
            }
            catch (Exception error)
            {
                Finish(error);
            }
        }

        static void Finish(Exception error)
        {
            File.AppendAllText(Report, (error == null ? "PASS" : "FAIL " + error) + " " + DateTimeOffset.Now.ToString("O") + "\nAssertions: " + assertions + "\n");
            SessionState.SetBool(Key + ".Finished", true);
            EditorApplication.ExitPlaymode();
        }
    }
}
