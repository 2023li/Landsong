#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static partial class SoldierAnimationSetup
    {
        public const string SourceModel = "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/People_M/Rigs_M/使用/Man_Soldier_Roman_Empire_Rig.prefab";
        public const string Output = ContentAssetPaths.Units + "/Soldier/militia";
        const string Male = "Assets/Landsong/Art/Animations/Kevin Iglesias/Human Animations/Animations/Male/";
        public static readonly string[] ClipPaths = {
            Male + "Idles/HumanM@Idle01.fbx",
            Male + "Movement/Walk/HumanM@Walk01_Forward.fbx",
            Male + "Movement/Run/HumanM@Run01_Forward.fbx",
            "Assets/Landsong/Art/Animations/Standing Melee Attack Downward.fbx",
            Male + "Idles/HumanM@IdleDamage01.fbx",
            Male + "Combat/HumanM@Death01.fbx",
            "Assets/Landsong/Art/Animations/举火把Standing Torch Walk Forward.fbx",
            "Assets/Landsong/Art/Animations/拔剑Withdrawing Sword.fbx",
            "Assets/Landsong/Art/Animations/收剑Sheathing Sword.fbx"
        };

        [MenuItem("Landsong/动画/检查罗马士兵资源")]
        public static string Inspect()
        {
            var report = new StringBuilder();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourceModel);
            if (source == null) throw new InvalidOperationException("士兵模型不存在：" + SourceModel);
            var animator = source.GetComponentInChildren<Animator>(true);
            report.AppendLine($"Avatar={animator?.avatar?.name}; valid={animator?.avatar?.isValid}; human={animator?.avatar?.isHuman}; rootMotion={animator?.applyRootMotion}");
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
            {
                var skin = renderer as SkinnedMeshRenderer;
                report.AppendLine($"Renderer={renderer.name}; bounds={renderer.bounds}; vertices={skin?.sharedMesh?.vertexCount}; bones={skin?.bones.Length}");
                foreach (var material in renderer.sharedMaterials)
                    report.AppendLine($"Material={AssetDatabase.GetAssetPath(material)}; shader={material?.shader?.name}; texture={AssetDatabase.GetAssetPath(material?.mainTexture)}");
            }
            foreach (var path in ClipPaths.Concat(new[] { "Assets/Landsong/Art/Animations/Standing Melee Without Skin.fbx", "Assets/Landsong/Art/Animations/Shooting Arrow Without Skin.fbx" }))
            {
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
                    report.AppendLine($"{path}: clip={clip.name}; length={clip.length:F3}; fps={clip.frameRate}; human={clip.isHumanMotion}; loop={clip.isLooping}; rootMotion={clip.hasRootCurves}");
            }
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText("Library/LandsongEcs/soldier-animation-inspection.txt", report.ToString());
            return report.ToString();
        }
    }
}
#endif
