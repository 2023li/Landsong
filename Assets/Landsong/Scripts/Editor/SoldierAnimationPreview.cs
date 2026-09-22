#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.Animation;
using Landsong.AnimationPreview;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Editor;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Scenes;
using Unity.Transforms;
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
    public static class SoldierAnimationPreview
    {
        public static string ScenePath => SoldierAnimationSetup.CurrentPackage() + "/Preview/SoldierAnimationPreview.unity";
        internal static string EntityScenePath => SoldierAnimationSetup.CurrentPackage() + "/Preview/SoldierAnimationEntities.unity";
        const string RestoreKey = "Landsong.AnimationPreview.Restore";
        [Serializable] sealed class Setup { public SceneSetup[] Scenes; }
        static SoldierAnimationPreview()
        {
            EditorApplication.playModeStateChanged += OnMode;
            EditorApplication.update += () => {
                if (!string.IsNullOrEmpty(SessionState.GetString(RestoreKey, "")) && EditorApplication.isPlaying && !EditorApplication.isPaused)
                { Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate(); }
            };
        }
        static void OnMode(PlayModeStateChange mode)
        {
            if (mode == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(SessionState.GetString(RestoreKey, "")))
                EditorApplication.ExecuteMenuItem("Window/General/Game");
            if (mode != PlayModeStateChange.EnteredEditMode) return;
            var saved = SessionState.GetString(RestoreKey, "");
            if (string.IsNullOrEmpty(saved)) return;
            SessionState.EraseString(RestoreKey);
            EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<Setup>(saved).Scenes);
        }

        [MenuItem("Landsong/动画/创建动画预览场景")]
        public static string Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出运行模式。");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)); AssetDatabase.Refresh();
            var previousScene = SceneManager.GetActiveScene();
            var entityScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("Animation preview population"); SceneManager.MoveGameObjectToScene(root, entityScene);
                var preview = root.AddComponent<AnimationPreviewAuthoring>();
                var militia = SoldierAnimationSetup.Militia();
                preview.SoldierPrefab = militia.Prefab.GetComponent<SoldierAnimationAuthoring>().VisualPrefab; preview.Count = 9;
                EditorSceneManager.SaveScene(entityScene, EntityScenePath);
            }
            finally { EditorSceneManager.CloseScene(entityScene, true); SceneManager.SetActiveScene(previousScene); }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                GameObject Make(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
                var sub = Make("Soldier animation entities").AddComponent<SubScene>();
                sub.SceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(EntityScenePath); sub.AutoLoadScene = true;
                var camera = Make("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(7, 8, -12); camera.transform.LookAt(new Vector3(0, .7f, 1.5f));
                camera.orthographic = true; camera.orthographicSize = 6.8f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.09f, .12f, .17f);
                camera.nearClipPlane = .1f; camera.farClipPlane = 150; camera.allowHDR = false;
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                var light = Make("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
                light.transform.rotation = Quaternion.Euler(45, -35, 0); light.shadows = LightShadows.Soft;
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name = "Ground"; SceneManager.MoveGameObjectToScene(ground, scene);
                Object.DestroyImmediate(ground.GetComponent<Collider>()); ground.transform.localScale = Vector3.one * 8;
                var groundPath = Path.GetDirectoryName(ScenePath) + "/Ground.mat";
                var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(groundPath);
                if (groundMaterial == null)
                {
                    groundMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    groundMaterial.SetColor("_BaseColor", new Color(.21f, .25f, .28f)); groundMaterial.SetFloat("_Smoothness", 0);
                    AssetDatabase.CreateAsset(groundMaterial, groundPath);
                }
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previousScene); }
            return ScenePath + "\nRows: Idle / Patrol Walk / Alert Run; Attack / Hit / Death; Torch Walk / Draw / Sheathe.";
        }

        [MenuItem("Landsong/动画/运行动画预览")]
        public static string Start()
            => Start(ScenePath);

        internal static string Start(string scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("编辑器已经在运行。");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("当前场景有未保存改动，预览不会覆盖这些改动。");
            if (!File.Exists(scenePath))
            {
                if (scenePath != ScenePath) throw new FileNotFoundException(scenePath);
                Create();
            }
            SessionState.SetString(RestoreKey, JsonUtility.ToJson(new Setup { Scenes = setup }));
            EditorSceneManager.OpenScene(scenePath);
            EditorApplication.EnterPlaymode();
            return "Starting actual Rukhanka preview; previous scenes will be restored on exit.";
        }

        public static string InspectAndCapture()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (!EditorApplication.isPlaying || world == null) throw new InvalidOperationException("预览尚未运行。");
            var em = world.EntityManager; em.CompleteAllTrackedJobs();
            using var query = em.CreateEntityQuery(typeof(AnimationPreviewUnit), typeof(SoldierAnimationBinding));
            using var units = query.ToEntityArray(Allocator.Temp);
            if (units.Length == 0) throw new InvalidOperationException("预览实体尚未加载。");
            var report = new StringBuilder(); report.AppendLine("Native animated units=" + units.Length);
            foreach (var entity in units.Take(9))
            {
                var binding = em.GetComponentData<SoldierAnimationBinding>(entity);
                var rig = binding.Rig;
                var layer = em.GetBuffer<AnimatorControllerLayerComponent>(rig)[0];
                report.AppendLine($"slot={em.GetComponentData<AnimationPreviewUnit>(entity).Slot}; state={layer.rtd.srcState.id}; time={layer.rtd.srcState.normalizedDuration:F3}; rig={rig}");
                if (em.HasComponent<LocalToWorld>(binding.TorchMount))
                {
                    var matrix = em.GetComponentData<LocalToWorld>(binding.TorchMount).Value;
                    report.AppendLine($"torch position=({matrix.c3.x:F3},{matrix.c3.y:F3},{matrix.c3.z:F3}); "
                        + $"axes X=({matrix.c0.x:F3},{matrix.c0.y:F3},{matrix.c0.z:F3}) "
                        + $"Y=({matrix.c1.x:F3},{matrix.c1.y:F3},{matrix.c1.z:F3}) "
                        + $"Z=({matrix.c2.x:F3},{matrix.c2.y:F3},{matrix.c2.z:F3})");
                }
            }
            var camera = Camera.main;
            var texture = new RenderTexture(1400, 900, 24); texture.Create();
            var previous = RenderTexture.active;
            var readback = new Texture2D(1400, 900, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                RenderTexture.active = texture; readback.ReadPixels(new Rect(0, 0, 1400, 900), 0, 0); readback.Apply();
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllBytes("Library/LandsongEcs/soldier-animation-preview.png", readback.EncodeToPNG());
                File.WriteAllText("Library/LandsongEcs/soldier-animation-preview.txt", report.ToString());
            }
            finally { RenderTexture.active = previous; texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(readback); }
            return report.ToString();
        }

        public static string Stop()
        {
            if (string.IsNullOrEmpty(SessionState.GetString(RestoreKey, ""))) throw new InvalidOperationException("不是本工具启动的预览。");
            EditorApplication.ExitPlaymode(); return "Stopping preview and restoring previous scenes.";
        }

        [MenuItem("Landsong/动画/预览数量/1 个")]
        static void Count1() => SetCount(1);
        [MenuItem("Landsong/动画/预览数量/6 种动作")]
        static void Count6() => SetCount(6);
        [MenuItem("Landsong/动画/预览数量/9 种动作")]
        static void Count9() => SetCount(9);
        [MenuItem("Landsong/动画/预览数量/50 个")]
        static void Count50() => SetCount(50);
        [MenuItem("Landsong/动画/预览数量/100 个")]
        static void Count100() => SetCount(100);
        [MenuItem("Landsong/动画/预览数量/200 个")]
        static void Count200() => SetCount(200);

        [MenuItem("Landsong/动画/聚焦动作/举火把行走")]
        static void FocusTorchWalk() => SetFocusSlot(6);
        [MenuItem("Landsong/动画/聚焦动作/持剑攻击")]
        static void FocusSwordAttack() => SetFocusSlot(3);
        [MenuItem("Landsong/动画/聚焦动作/拔剑")]
        static void FocusDrawSword() => SetFocusSlot(7);
        [MenuItem("Landsong/动画/聚焦动作/收剑")]
        static void FocusSheatheSword() => SetFocusSlot(8);

        public static string SetCount(int count)
        {
            if (!EditorApplication.isPlaying || string.IsNullOrEmpty(SessionState.GetString(RestoreKey, ""))) throw new InvalidOperationException("请先运行动画预览。");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var query = em.CreateEntityQuery(typeof(AnimationPreviewSettings));
            var entity = query.GetSingletonEntity();
            var settings = em.GetComponentData<AnimationPreviewSettings>(entity);
            settings.Count = count;
            settings.SlotOffset = 0;
            em.SetComponentData(entity, settings);
            Time.timeScale = 1;
            var columns = count == 1 ? 1 : count <= 9 ? 3 : Mathf.CeilToInt(Mathf.Sqrt(count));
            var camera = Camera.main;
            camera.transform.position = new Vector3(columns * 2.3f, columns * 2.7f, -columns * 4f);
            camera.transform.LookAt(new Vector3(0, .7f, (Mathf.CeilToInt(count / (float)columns) - 1) * 1.6f));
            camera.orthographicSize = count <= 9 ? 6.8f : columns * 2.05f;
            return "Requested native animated units=" + count;
        }

        public static string SetFocusSlot(int slot)
        {
            if (!EditorApplication.isPlaying || string.IsNullOrEmpty(SessionState.GetString(RestoreKey, "")))
                throw new InvalidOperationException("请先运行动画预览。");
            if (slot < 0 || slot > 8) throw new ArgumentOutOfRangeException(nameof(slot));
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var query = em.CreateEntityQuery(typeof(AnimationPreviewSettings));
            var entity = query.GetSingletonEntity();
            var settings = em.GetComponentData<AnimationPreviewSettings>(entity);
            settings.Count = 1;
            settings.Spawned = -1;
            settings.SlotOffset = slot;
            em.SetComponentData(entity, settings);
            Time.timeScale = slot >= 7 ? .25f : 1;
            var camera = Camera.main;
            camera.transform.position = new Vector3(3.2f, 2.6f, -5.5f);
            camera.transform.LookAt(new Vector3(0, 1.05f, 0));
            camera.orthographicSize = 2.1f;
            return "Focused animation slot=" + slot;
        }
    }
}
#endif
