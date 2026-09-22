#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using Landsong.Animation;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools
{
    public static class TransportWorkerContentAuthoring
    {
        public const string Root = ContentAssetPaths.Root;
        public const string Presentation = Root + "/Units/Worker/transport_worker";
        public const string Logic = Presentation;
        public const string Template = Root + "/World/GameWorldTemplate.prefab";
        const string People = "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/People_M/Rigs_M/使用/";
        const string Female = "Assets/Landsong/Art/Animations/Kevin Iglesias/Human Animations/Animations/Female/";
        internal static readonly string[] Models = { People + "Man_Empire_Rig.prefab", People + "Woman_Empire_Rig.prefab" };
        internal static readonly string[] Motions = {
            Female + "Idles/HumanF@Idle01.fbx", Female + "Movement/Walk/HumanF@Walk01_Forward.fbx",
            Female + "Work/Carry/HumanF@Carry01_Idle01.fbx", Female + "Work/Carry/HumanF@Carry01_PickUp01.fbx",
            Female + "Work/Carry/HumanF@Carry01_Drop01.fbx", Female + "Combat/HumanF@Death01.fbx",
            Female + "Idles/HumanF@IdleDamage01.fbx"
        };
        internal static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
        public static string Inspect()
        {
            var report = new StringBuilder();
            foreach (var path in Models)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = model.GetComponentInChildren<Animator>(true);
                report.AppendLine($"{path}: avatar={animator?.avatar?.name}, valid={animator?.avatar?.isValid}, human={animator?.avatar?.isHuman}");
                foreach (var bone in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand })
                    report.AppendLine($"{bone}={animator.GetBoneTransform(bone)?.name}");
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    report.AppendLine($"{renderer.name}: {renderer.bounds}; materials={string.Join(",", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath))}");
                report.AppendLine("Scripts=" + string.Join(",", model.GetComponentsInChildren<MonoBehaviour>(true).Select(c => c == null ? "missing" : c.GetType().Name)));
            }
            foreach (var path in Motions)
            {
                var clip = Clip(path);
                report.AppendLine($"{clip.name}: human={clip.isHumanMotion}; length={clip.length}; loop={clip.isLooping}");
            }
            return report.ToString();
        }

        [MenuItem("Landsong/内容制作/创建运输工人")]
        public static string Create()
        {
            ContentCreationAssets.RequireEditMode();
            var registered = AssetDatabase.LoadAssetAtPath<GameObject>(Template).GetComponent<TransportWorkerSettingsAuthoring>();
            var existingMale = registered != null ? registered.Male : null;
            var existingFemale = registered != null ? registered.Female : null;
            if (existingMale != null && existingFemale != null) { Register(existingMale, existingFemale); return "运输工人已存在，保留现有资产和 GUID。"; }
            using var assets = new ContentCreationAssets();
            using var staging = new ContentCreationScene();
            var controller = Controller(assets);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ContentAssetPaths.AnimatedUnitShader);
            if (shader == null) throw new InvalidOperationException("Missing animated shader");
            var cargoMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CargoGrey", color = new Color(.45f, .45f, .45f), enableInstancing = true };
            assets.Create(cargoMaterial, Presentation + "/Materials/CargoGrey.mat");
            var variants = new GameObject[2];
            for (int i = 0; i < 2; i++)
            {
                string id = "transport_worker_" + (i == 0 ? "male" : "female");
                var view = staging.Create(id + "View");
                var input = new UnitCreationInput { StableId = id, Profile = UnitAnimationProfile.TransportWorker,
                    ModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Models[i]), AnimatedShader = shader };
                UnitVisualBuilder.Configure(view, input, controller, assets, staging, Presentation, i == 0 ? "Male" : "Female");
                var visual = view.GetComponent<SoldierAnimationVisualAuthoring>();
                visual.CelebrationLayer = 0;
                var animator = visual.Animator;
                var bones = animator.GetComponentsInChildren<Transform>(true);
                var poses = bones.Select(t => (t.localPosition, t.localRotation, t.localScale)).ToArray();
                // Fit the same box between both hands in the authored carry pose.
                Clip(Motions[2]).SampleAnimation(animator.gameObject, .5f);
                var right = animator.GetBoneTransform(HumanBodyBones.RightHand);
                var left = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                var mount = staging.Create("CargoMount").transform;
                mount.SetParent(right, false);
                mount.position = (right.position + left.position) * .5f + new Vector3(0, .08f, 0);
                mount.rotation = animator.transform.rotation;
                var box = staging.Create("CargoGreybox", typeof(MeshFilter), typeof(MeshRenderer));
                box.transform.SetParent(mount, false);
                box.transform.localScale = new Vector3(.46f, .32f, .36f);
                box.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                box.GetComponent<MeshRenderer>().sharedMaterial = cargoMaterial;
                for (int j = 0; j < bones.Length; j++) { bones[j].localPosition = poses[j].localPosition; bones[j].localRotation = poses[j].localRotation; bones[j].localScale = poses[j].localScale; }
                var cargo = view.AddComponent<TransportCargoAuthoring>(); cargo.Mount = mount;
                var viewAsset = assets.Prefab(view, Presentation + "/" + id + "View.prefab");
                var logic = staging.Create(id);
                var animation = logic.AddComponent<SoldierAnimationAuthoring>();
                animation.Profile = UnitAnimationProfile.TransportWorker; animation.VisualPrefab = viewAsset;
                animation.DeathSeconds = 2; animation.HitSeconds = Clip(Motions[6]).length;
                variants[i] = assets.Prefab(logic, Logic + "/" + id + ".prefab");
                UnityEngine.Object.DestroyImmediate(view); UnityEngine.Object.DestroyImmediate(logic);
            }
            Register(variants[0], variants[1]);
            assets.Complete();
            return "运输工人：两个模型、一个固定动画器、手部灰盒、正式世界模板引用已创建。";
        }

        static void Register(GameObject male, GameObject female)
        {
            var root = PrefabUtility.LoadPrefabContents(Template);
            try
            {
                var settings = root.GetComponent<TransportWorkerSettingsAuthoring>() ?? root.AddComponent<TransportWorkerSettingsAuthoring>();
                if (settings.Male != male || settings.Female != female)
                {
                    settings.Male = male; settings.Female = female;
                    PrefabUtility.SaveAsPrefabAsset(root, Template);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            SyncMaps();
        }

        // Existing generated scenes contain a template snapshot. Synchronize only this new module;
        // do not rebake terrain or overwrite other map-specific authoring settings.
        [MenuItem("Landsong/内容制作/同步运输工人到地图")]
        public static string SyncMaps()
        {
            ContentCreationAssets.RequireEditMode();
            var previous = SceneManager.GetActiveScene();
            int count = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/GameMaps" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith("_Entities.unity", StringComparison.Ordinal)))
            {
                var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
                if (!opened && scene.isDirty) throw new InvalidOperationException("请先保存地图实体场景再同步：" + path);
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Template).GetComponent<TransportWorkerSettingsAuthoring>();
                    foreach (var template in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameWorldTemplateAuthoring>(true)))
                    {
                        var settings = template.GetComponent<TransportWorkerSettingsAuthoring>() ?? template.gameObject.AddComponent<TransportWorkerSettingsAuthoring>();
                        EditorUtility.CopySerialized(source, settings); count++;
                    }
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
            }
            return "已同步运输工人配置到 " + count + " 个地图模拟根。";
        }

        static AnimatorController Controller(ContentCreationAssets assets)
        {
            AnimationClip Copy(int index, string name, bool loop)
            {
                var source = Clip(Motions[index]);
                if (!source.isHumanMotion || source.length <= 0) throw new InvalidOperationException("Invalid humanoid carry motion: " + Motions[index]);
                var copy = UnityEngine.Object.Instantiate(source); copy.name = name;
                var settings = AnimationUtility.GetAnimationClipSettings(copy); settings.loopTime = settings.loopBlend = loop;
                AnimationUtility.SetAnimationClipSettings(copy, settings); AnimationUtility.SetAnimationEvents(copy, Array.Empty<AnimationEvent>());
                assets.Create(copy, Presentation + "/Clips/" + name + ".anim"); return copy;
            }
            var idleClip = Copy(0, "Idle", true); var walkClip = Copy(1, "Walk", true); var carryClip = Copy(2, "Carry", true);
            var loadClip = Copy(3, "PickUp", false); var dropClip = Copy(4, "Drop", false); var deathClip = Copy(5, "Death", false); var hitClip = Copy(6, "Hit", false);
            string path = Presentation + "/Controllers/transport_worker.controller";
            assets.Reserve(path); var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (string name in new[] { "Speed", "LocomotionRate", "AttackSpeed" })
                controller.AddParameter(new AnimatorControllerParameter { name = name, type = AnimatorControllerParameterType.Float, defaultFloat = name == "Speed" ? 0 : 1 });
            foreach (string name in new[] { "Alerted", "Dead", "Celebrate" }) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (string name in new[] { "Attack", "Hit" }) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Handling", AnimatorControllerParameterType.Int);
            var machine = controller.layers[0].stateMachine;
            AnimatorState State(string name, AnimationClip clip) { var state = machine.AddState(name); state.motion = clip; return state; }
            var idle = State("Idle", idleClip); var walk = State("Walk", walkClip); var load = State("PickUp", loadClip); var drop = State("Drop", dropClip);
            var death = State("Death", deathClip); var hit = State("Hit", hitClip); machine.defaultState = idle;
            walk.speedParameter = "LocomotionRate"; walk.speedParameterActive = true;
            void Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float value)
            {
                var t = from.AddTransition(to); t.hasExitTime = false; t.duration = .08f;
                t.AddCondition(mode, value, parameter); t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            Transition(idle, walk, "Speed", AnimatorConditionMode.Greater, .15f);
            Transition(walk, idle, "Speed", AnimatorConditionMode.Less, .15f);
            void Any(AnimatorState state, string parameter, AnimatorConditionMode mode, float value)
            {
                var t = machine.AddAnyStateTransition(state); t.hasExitTime = false; t.duration = .04f; t.canTransitionToSelf = false;
                t.AddCondition(mode, value, parameter);
                if (state != death) t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            Any(death, "Dead", AnimatorConditionMode.If, 0); Any(load, "Handling", AnimatorConditionMode.Equals, 1); Any(drop, "Handling", AnimatorConditionMode.Equals, 2);
            Any(hit, "Hit", AnimatorConditionMode.If, 0);
            Transition(load, idle, "Handling", AnimatorConditionMode.NotEqual, 1); Transition(drop, idle, "Handling", AnimatorConditionMode.NotEqual, 2);
            var endHit = hit.AddTransition(idle); endHit.hasExitTime = true; endHit.exitTime = 1; endHit.duration = .05f; endHit.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var mask = new AvatarMask { name = "CarryArms" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,
                i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.RightArm || i == (int)AvatarMaskBodyPart.LeftFingers || i == (int)AvatarMaskBodyPart.RightFingers);
            assets.Create(mask, Presentation + "/Masks/CarryArms.mask");
            var arms = new AnimatorStateMachine { name = "Carry Arms" }; AssetDatabase.AddObjectToAsset(arms, controller);
            var pose = arms.AddState("Carry"); pose.motion = carryClip; arms.defaultState = pose;
            controller.layers = new[] { controller.layers[0], new AnimatorControllerLayer { name = "Carry Arms", avatarMask = mask, stateMachine = arms, defaultWeight = 0, blendingMode = AnimatorLayerBlendingMode.Override } };
            EditorUtility.SetDirty(controller); AssetDatabase.SaveAssetIfDirty(controller); return controller;
        }
    }
}
#endif
