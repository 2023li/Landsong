#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.Animation;
using Landsong.AnimationPreview;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    [InitializeOnLoad]
    public static class TransportWorkerAnimationPlayVerification
    {
        const string Folder = TransportWorkerContentAuthoring.Presentation + "/Preview";
        public const string ScenePath = Folder + "/TransportWorkerPreview.unity";
        const string EntityPath = Folder + "/TransportWorkerEntities.unity";
        const string Key = "Landsong.TransportWorker.PlayVerification";
        public const string Report = "Library/LandsongEcs/transport-worker-play.txt";
        static int stage, assertions; static double deadline; static float nextTime; static StringBuilder log; static string error;
        static TransportWorkerAnimationPlayVerification()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += mode => {
                if (mode == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
                { stage = -1; assertions = 0; deadline = EditorApplication.timeSinceStartup + 180; nextTime = 0; log = new StringBuilder(); error = null; }
            };
            Application.logMessageReceived += (message, stack, kind) => {
                if (SessionState.GetBool(Key, false) && EditorApplication.isPlaying && (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)) error = message;
            };
        }
        static void CreatePreview()
        {
            if (File.Exists(ScenePath) && File.Exists(EntityPath)) return;
            var previous = SceneManager.GetActiveScene();
            using var assets = new ContentCreationAssets();
            assets.Reserve(EntityPath);
            if (!AssetDatabase.CopyAsset(SoldierAnimationPreview.EntityScenePath, EntityPath)) throw new IOException("Worker preview entity scene copy failed");
            var entities = EditorSceneManager.OpenScene(EntityPath, OpenSceneMode.Additive);
            try
            {
                var source = entities.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AnimationPreviewAuthoring>()).Single();
                var root = source.gameObject; Object.DestroyImmediate(source);
                var settings = root.AddComponent<TransportWorkerPreviewAuthoring>();
                settings.MaleView = AssetDatabase.LoadAssetAtPath<GameObject>(TransportWorkerContentAuthoring.Presentation + "/transport_worker_maleView.prefab");
                settings.FemaleView = AssetDatabase.LoadAssetAtPath<GameObject>(TransportWorkerContentAuthoring.Presentation + "/transport_worker_femaleView.prefab");
                EditorSceneManager.MarkSceneDirty(entities); EditorSceneManager.SaveScene(entities);
            }
            finally { EditorSceneManager.CloseScene(entities, true); SceneManager.SetActiveScene(previous); }
            assets.Reserve(ScenePath);
            if (!AssetDatabase.CopyAsset(SoldierAnimationPreview.ScenePath, ScenePath)) throw new IOException("Worker preview scene copy failed");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SubScene>()).Single().SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntityPath);
                var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
                camera.transform.position = new Vector3(3.6f, 2.8f, -6); camera.transform.LookAt(new Vector3(0, .8f, 0)); camera.orthographicSize = 2.1f;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
            assets.Complete();
        }
        [MenuItem("Landsong/动画/预览运输工人")]
        public static string Preview() { CreatePreview(); return SoldierAnimationPreview.Start(ScenePath); }
        public static string Start()
        {
            CreatePreview(); SessionState.SetBool(Key, true);
            try { return SoldierAnimationPreview.Start(ScenePath); }
            catch { SessionState.EraseBool(Key); throw; }
        }
        static void Check(bool value, string text) { if (!value) throw new InvalidOperationException(text); assertions++; log.AppendLine("PASS " + text); }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || log == null) return;
            try
            {
                if (error != null) throw new InvalidOperationException(error);
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Worker animation Play verification timed out");
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || Time.time < nextTime) return;
                var em = world.EntityManager; em.CompleteAllTrackedJobs();
                using var settingsQuery = em.CreateEntityQuery(typeof(TransportWorkerPreviewSettings));
                if (settingsQuery.IsEmptyIgnoreFilter) return;
                var owner = settingsQuery.GetSingletonEntity(); var settings = em.GetComponentData<TransportWorkerPreviewSettings>(owner);
                if (stage >= 0)
                {
                    using var unitQuery = em.CreateEntityQuery(typeof(TransportWorkerPreviewUnit)); using var units = unitQuery.ToEntityArray(Allocator.Temp);
                    Check(units.Length == 2, "Mode " + stage + " shows both model variants");
                    foreach (var unit in units)
                    {
                        var rig = em.GetComponentData<SoldierAnimationBinding>(unit).Rig;
                        Check(em.HasComponent<RigDefinitionComponent>(rig) && em.HasComponent<GPUAnimationEngineTag>(rig), "Mode " + stage + " has complete real skinning rig");
                        var layers = em.GetBuffer<AnimatorControllerLayerComponent>(rig);
                        Check(layers.Length == 2 && layers[0].rtd.srcState.normalizedDuration > 0 && layers[0].rtd.srcState.id == new[] { 0, 1, 1, 2, 3, 4 }[stage], "Mode " + stage + " advances the expected controller state");
                    }
                    Capture(stage);
                }
                if (++stage > 5) { Finish(true, "Both workers rendered in six motions"); return; }
                settings.Mode = stage; settings.SpawnedMode = -1; em.SetComponentData(owner, settings);
                nextTime = Time.time + (stage == 3 || stage == 4 ? .55f : 1.3f);
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        static void Capture(int mode)
        {
            var target = new RenderTexture(1000, 700, 24); var previous = RenderTexture.active; var pixels = new Texture2D(1000, 700, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0); pixels.Apply();
                File.WriteAllBytes("Library/LandsongEcs/worker-" + new[] { "carry-idle", "carry-walk", "empty-walk", "pickup", "drop", "death" }[mode] + ".png", pixels.EncodeToPNG());
                Check(pixels.GetPixels32().Select(p => p.r << 16 | p.g << 8 | p.b).Distinct().Take(33).Count() > 32, "Mode " + mode + " capture contains shaded geometry");
            }
            finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
        }
        static void Finish(bool passed, string detail)
        {
            SessionState.EraseBool(Key); log.AppendLine("Assertions: " + assertions); log.AppendLine((passed ? "PASS " : "FAIL ") + DateTimeOffset.Now.ToString("O") + "\n" + detail);
            File.WriteAllText(Report, log.ToString()); SoldierAnimationPreview.Stop();
        }
    }
}
#endif
