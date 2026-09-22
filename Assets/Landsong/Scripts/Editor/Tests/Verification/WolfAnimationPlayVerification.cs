#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.Animation;
using Landsong.AnimationPreview;
using Landsong.ECS.Authoring.Definitions;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Scenes;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    [InitializeOnLoad]
    public static class WolfAnimationPlayVerification
    {
        const string Key = "Landsong.Wolf.PlayVerification";
        static string Folder => (Path.GetDirectoryName(AssetDatabase.GetAssetPath(WolfContentAuthoring.Definition().Prefab)) ?? string.Empty).Replace('\\', '/') + "/Preview";
        public static string ScenePath => Folder + "/WolfAnimationPreview.unity";
        static string EntityPath => Folder + "/WolfAnimationEntities.unity";
        const string Report = "Library/LandsongEcs/wolf-animation-play.txt";
        static int stage, nextFrame, assertions;
        static float nextTime;
        static double deadline;
        static string error;
        static readonly StringBuilder log = new();
        static WolfAnimationPlayVerification()
        {
            EditorApplication.playModeStateChanged += mode => {
                if (!SessionState.GetBool(Key, false)) return;
                if (mode == PlayModeStateChange.EnteredPlayMode)
                { stage = nextFrame = assertions = 0; nextTime = 0; error = null; log.Clear(); deadline = EditorApplication.timeSinceStartup + 120; }
                if (mode == PlayModeStateChange.ExitingPlayMode)
                { SessionState.EraseBool(Key); File.WriteAllText(Report, "CANCELLED\n" + log); }
            };
            Application.logMessageReceived += (message, trace, type) => {
                if (SessionState.GetBool(Key, false) && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) error = message;
            };
            EditorApplication.update += Tick;
        }

        public static void CreatePreview()
        {
            ContentCreationAssets.RequireEditMode();
            if (File.Exists(ScenePath) && File.Exists(EntityPath)) return;
            var previous = SceneManager.GetActiveScene();
            if (!File.Exists(SoldierAnimationPreview.ScenePath)) SoldierAnimationPreview.Create();
            using var assets = new ContentCreationAssets();
            var sourceEntities = SoldierAnimationPreview.EntityScenePath;
            assets.Reserve(EntityPath);
            if (!AssetDatabase.CopyAsset(sourceEntities, EntityPath)) throw new IOException("Cannot create wolf preview SubScene");
            var entities = EditorSceneManager.OpenScene(EntityPath, OpenSceneMode.Additive);
            try
            {
                var preview = entities.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AnimationPreviewAuthoring>()).Single();
                preview.SoldierPrefab = WolfContentAuthoring.Definition().Prefab.GetComponent<SoldierAnimationAuthoring>().VisualPrefab;
                preview.Count = 3;
                EditorSceneManager.MarkSceneDirty(entities);
                EditorSceneManager.SaveScene(entities);
            }
            finally { EditorSceneManager.CloseScene(entities, true); SceneManager.SetActiveScene(previous); }
            assets.Reserve(ScenePath);
            if (!AssetDatabase.CopyAsset(SoldierAnimationPreview.ScenePath, ScenePath)) throw new IOException("Cannot create wolf preview scene");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var sub = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<SubScene>()).Single();
                sub.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntityPath);
                var camera = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).Single();
                camera.transform.position = new Vector3(5, 4, -8); camera.transform.LookAt(new Vector3(0, .6f, 0)); camera.orthographicSize = 3.8f;
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
            assets.Complete();
        }

        [MenuItem("Landsong/动画/狼/运行动画预览")]
        public static string Preview() { CreatePreview(); return SoldierAnimationPreview.Start(ScenePath); }

        public static string Inspect()
        {
            var result = new StringBuilder();
            foreach (var camera in Camera.allCameras) result.AppendLine($"Camera {camera.name}: {camera.transform.position}, forward={camera.transform.forward}, mask={camera.cullingMask}, scene={camera.gameObject.scene.path}");
            result.AppendLine("Graphics=" + SystemInfo.graphicsDeviceType);
            var em = World.DefaultGameObjectInjectionWorld.EntityManager; em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(SkinnedMeshRendererComponent), typeof(LocalToWorld));
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                result.AppendLine($"Skin {entity}: {em.GetComponentData<LocalToWorld>(entity).Value}; worldBounds={(em.HasComponent<WorldRenderBounds>(entity) ? em.GetComponentData<WorldRenderBounds>(entity).Value.ToString() : "none")}");
                var skin = em.GetComponentData<SkinnedMeshRendererComponent>(entity);
                var rig = skin.animatedRigEntity;
                result.AppendLine($"Skin rig={rig}; exists={em.Exists(rig)}; GPUtag={em.HasComponent<GPUAnimationEngineTag>(rig)}; prefab={em.HasComponent<Prefab>(rig)}; rootBone={skin.rootBoneIndexInRig}");
            }
            Capture("wolf-inspect");
            return result.ToString();
        }

        [MenuItem("Landsong/动画/狼/验证动画 (Play)")]
        public static string Start()
        {
            CreatePreview(); SessionState.SetBool(Key, true);
            try { return SoldierAnimationPreview.Start(ScenePath); }
            catch { SessionState.EraseBool(Key); throw; }
        }
        static void Check(bool condition, string label)
        { if (!condition) throw new InvalidOperationException(label); assertions++; log.AppendLine("PASS " + label); }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (error != null) throw new InvalidOperationException(error);
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Wolf Play verification timed out");
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || Time.frameCount < nextFrame || Time.time < nextTime) return;
                var em = world.EntityManager; em.CompleteAllTrackedJobs();
                using var query = em.CreateEntityQuery(typeof(AnimationPreviewUnit), typeof(SoldierAnimationBinding));
                if (query.IsEmptyIgnoreFilter) return;
                using var units = query.ToEntityArray(Allocator.Temp);
                if (stage == 0) { SoldierAnimationPreview.SetFocusSlot(0); stage++; nextFrame = Time.frameCount + 45; nextTime = Time.time + .5f; return; }
                var rig = em.GetComponentData<SoldierAnimationBinding>(units[0]).Rig;
                var layers = em.GetBuffer<AnimatorControllerLayerComponent>(rig);
                Check(em.HasComponent<RigDefinitionComponent>(rig) && em.HasComponent<GPUAnimationEngineTag>(rig), "Stage " + stage + " has a fully baked animation rig");
                Check(layers.Length == 1 && layers[0].rtd.srcState.normalizedDuration > 0, "Stage " + stage + " Generic controller advances on the real player loop");
                Check(layers[0].rtd.srcState.id == new[] { 0, 0, 2, 3, 4 }[stage], "Stage " + stage + " reaches the expected motion state");
                using var skins = em.CreateEntityQuery(typeof(MaterialMeshInfo), typeof(DeformedMeshIndex), typeof(SkinnedMeshRendererComponent));
                Check(skins.CalculateEntityCount() == 1, "Stage " + stage + " has one renderable Rukhanka skinned wolf");
                log.AppendLine("State=" + layers[0].rtd.srcState.id + "; normalizedTime=" + layers[0].rtd.srcState.normalizedDuration);
                Capture("wolf-" + new[] { "", "idle", "run", "attack", "death" }[stage]);
                if (stage == 4) { Finish(true, ""); return; }
                SoldierAnimationPreview.SetFocusSlot(new[] { 0, 2, 3, 5 }[stage]);
                stage++; nextFrame = Time.frameCount + 35;
                nextTime = Time.time + (stage == 4 ? 1.8f : stage == 3 ? .9f : .5f);
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }
        static void Capture(string name)
        {
            var target = new RenderTexture(1000, 700, 24); target.Create();
            var previous = RenderTexture.active; var pixels = new Texture2D(1000, 700, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(Camera.main, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1000, 700), 0, 0); pixels.Apply();
                File.WriteAllBytes("Library/LandsongEcs/" + name + ".png", pixels.EncodeToPNG());
                if (SessionState.GetBool(Key, false)) Check(pixels.GetPixels32().Select(p => p.r << 16 | p.g << 8 | p.b).Distinct().Take(33).Count() > 32, name + " capture contains visible shaded geometry");
            }
            finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels); }
        }
        static void Finish(bool passed, string detail)
        {
            SessionState.EraseBool(Key);
            log.AppendLine("Assertions: " + assertions);
            log.AppendLine((passed ? "PASS " : "FAIL ") + DateTimeOffset.Now.ToString("O") + "\n" + detail);
            File.WriteAllText(Report, log.ToString()); SoldierAnimationPreview.Stop();
        }
    }
}
#endif
