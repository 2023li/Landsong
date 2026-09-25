#if UNITY_EDITOR
using System;
using Landsong.Animation;
using Landsong.ECS.Editor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [Serializable]
    public sealed class UnitAnimationClips
    {
        [LabelText("待机"), Required] public AnimationClip Idle;
        [LabelText("行走"), Required] public AnimationClip Walk;
        [LabelText("奔跑"), Required] public AnimationClip Run;
        [LabelText("攻击"), Required] public AnimationClip Attack;
        [LabelText("远程攻击（可选）")] public AnimationClip RangedAttack;
        [LabelText("受击（动物可留空）")] public AnimationClip Hit;
        [LabelText("死亡"), Required] public AnimationClip Death;
        [LabelText("庆祝（动物不使用）")] public AnimationClip Celebrate;
    }

    // Temporary creation input only. Definitions, prefabs and controllers are the saved truth.
    [Serializable]
    public sealed class UnitCreationInput
    {
        [LabelText("稳定 ID")] public string StableId;
        [LabelText("显示名称")] public string DisplayName;
        [LabelText("领域定义模板"), Required, Tooltip("选择已注册的 Soldier/Hero/Enemy 定义。复制其数值及逻辑 AI，再单独编辑新单位；不修改模板。")] public ScriptableObject DefinitionTemplate;
        [LabelText("动作配置类型"), ValueDropdown(nameof(CombatProfiles))] public UnitAnimationProfile Profile = UnitAnimationProfile.Basic;
        static UnitAnimationProfile[] CombatProfiles => new[] { UnitAnimationProfile.SwordAndTorch, UnitAnimationProfile.Basic, UnitAnimationProfile.GenericCreature };
        [LabelText("模型预制体"), Required] public GameObject ModelPrefab;
        [LabelText("Rukhanka 蒙皮着色器"), Required] public Shader AnimatedShader;
        [LabelText("模型局部位置")] public Vector3 ModelOffset = new Vector3(0, -.5f, 0);
        [LabelText("模型统一缩放"), MinValue(.01f)] public float ModelScale = 1;
        [LabelText("蒙皮可见范围")] public Bounds SkinBounds = new Bounds(new Vector3(0, .65f, 0), new Vector3(4, 4, 4));
        [LabelText("公共动作"), InlineProperty] public UnitAnimationClips Clips = new UnitAnimationClips();
        bool UsesEquipment => Profile == UnitAnimationProfile.SwordAndTorch;
        [LabelText("持火把动作"), Required, ShowIf(nameof(UsesEquipment))] public AnimationClip Torch;
        [LabelText("拔剑动作"), Required, ShowIf(nameof(UsesEquipment))] public AnimationClip Draw;
        [LabelText("收剑动作"), Required, ShowIf(nameof(UsesEquipment))] public AnimationClip Sheathe;
        [LabelText("剑模型"), Required, ShowIf(nameof(UsesEquipment))] public GameObject SwordPrefab;
        [LabelText("火把模型"), Required, ShowIf(nameof(UsesEquipment))] public GameObject TorchPrefab;
        [LabelText("右手挂点位置"), ShowIf(nameof(UsesEquipment))] public Vector3 SwordPosition;
        [LabelText("右手挂点角度"), ShowIf(nameof(UsesEquipment))] public Vector3 SwordRotation;
        [LabelText("左手挂点位置"), ShowIf(nameof(UsesEquipment))] public Vector3 TorchPosition;
        [LabelText("左手挂点角度"), ShowIf(nameof(UsesEquipment))] public Vector3 TorchRotation;
    }

    public sealed class UnitCreationWindow : OdinEditorWindow
    {
        [ShowInInspector, InlineProperty, HideLabel] public UnitCreationInput Input = new UnitCreationInput();
        protected override void OnImGUI()
        {
            ContentAuthoringContext.DrawContext();
            base.OnImGUI();
            EditorGUILayout.HelpBox("填写一次后生成固定目录和一个单位专用 Controller。之后直接编辑 Definition、逻辑 Prefab 和 View；不保存额外配方资产。重复创建只验证已有结果。", MessageType.Info);
            if (GUILayout.Button("创建并注册单位"))
            {
                try { Selection.activeObject = UnitAuthoringWorkflow.Create(Input); }
                catch (Exception error) { Debug.LogError(error.Message); }
            }
        }

        [MenuItem("Landsong/内容制作/创建单位")]
        public static void Open()
        {
            ContentCreationAssets.RequireEditMode();
            GetWindow<UnitCreationWindow>("创建单位").Show();
        }
    }
}
#endif
