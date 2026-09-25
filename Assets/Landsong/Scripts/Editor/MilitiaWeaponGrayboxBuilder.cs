using Landsong.Animation;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class MilitiaWeaponGrayboxBuilder
    {
        const string PrefabPath = "Assets/Landsong/ECSContent/Units/士兵/民兵/militiaView.prefab";
        const string MaterialPath = "Assets/Landsong/ECSContent/Units/士兵/民兵/Materials/TorchGrayboxShaft.mat";
        const string ControllerPath = "Assets/Landsong/ECSContent/Units/士兵/民兵/militia.controller";
        const string RangedClipPath = "Assets/Landsong/ECSContent/Units/士兵/民兵/Clips/RangedAttack.anim";

        [MenuItem("Landsong/动画/补齐民兵武器灰盒")]
        public static string Run()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var authoring = root.GetComponent<SoldierAnimationVisualAuthoring>();
                if (authoring == null || authoring.SwordHandSocket == null)
                    throw new System.InvalidOperationException("民兵表现预制体缺少武器挂点。");
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null)
                    throw new System.InvalidOperationException("民兵灰盒材质缺失。");
                var hand = authoring.SwordHandSocket;
                var club = hand.Find("ClubGrayboxMount") ?? Mount(hand, "ClubGrayboxMount");
                var bow = hand.Find("BowGrayboxMount") ?? Mount(hand, "BowGrayboxMount");
                if (club.childCount == 0)
                {
                    Box(club, "Handle", new Vector3(0, .27f, 0), new Vector3(.1f, .54f, .1f), 0, material);
                    Box(club, "Head", new Vector3(0, .6f, 0), new Vector3(.2f, .21f, .19f), 0, material);
                }
                if (bow.childCount == 0)
                {
                    Box(bow, "Grip", Vector3.zero, new Vector3(.08f, .25f, .08f), 0, material);
                    Box(bow, "UpperLimb", new Vector3(.13f, .32f, 0), new Vector3(.07f, .48f, .07f), -25, material);
                    Box(bow, "LowerLimb", new Vector3(.13f, -.32f, 0), new Vector3(.07f, .48f, .07f), 25, material);
                    Box(bow, "String", new Vector3(.25f, 0, 0), new Vector3(.015f, .85f, .015f), 0, material);
                }
                club.localScale = Vector3.zero;
                bow.localScale = Vector3.zero;
                authoring.ClubMount = club;
                authoring.BowMount = bow;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                EnsureRangedAttack();
                return "民兵木棒和短弓灰盒已挂接右手，并接入远程攻击动画。";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void EnsureRangedAttack()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(RangedClipPath);
            if (clip == null)
            {
                var source = AssetDatabase.LoadAllAssetsAtPath("Assets/Landsong/Art/Animations/Shooting Arrow.fbx")
                    .OfType<AnimationClip>().FirstOrDefault(value => value.isHumanMotion && value.length > 0);
                if (source == null)
                    throw new System.InvalidOperationException("Shooting Arrow 缺少 Humanoid 动作。");
                clip = Object.Instantiate(source);
                clip.name = "RangedAttack";
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                AnimationUtility.SetAnimationEvents(clip, System.Array.Empty<AnimationEvent>());
                AssetDatabase.CreateAsset(clip, RangedClipPath);
            }
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                throw new System.InvalidOperationException("民兵 Animator Controller 缺失。");
            if (!controller.parameters.Any(p => p.name == "Shoot"))
                controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            if (machine.states.Any(s => s.state.name == "Ranged Attack"))
                return;
            var idle = machine.states.First(s => s.state.name == "Idle").state;
            var ranged = machine.AddState("Ranged Attack");
            ranged.motion = clip;
            ranged.speedParameter = "AttackSpeed";
            ranged.speedParameterActive = true;
            var enter = machine.AddAnyStateTransition(ranged);
            enter.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
            enter.hasExitTime = false;
            enter.duration = .04f;
            enter.canTransitionToSelf = false;
            var exit = ranged.AddTransition(idle);
            exit.hasExitTime = true;
            exit.exitTime = .9f;
            exit.duration = .05f;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        static Transform Mount(Transform hand, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(hand, false);
            return child.transform;
        }

        static void Box(Transform parent, string name, Vector3 position, Vector3 scale, float angle, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localRotation = Quaternion.Euler(0, 0, angle);
            box.transform.localScale = scale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
