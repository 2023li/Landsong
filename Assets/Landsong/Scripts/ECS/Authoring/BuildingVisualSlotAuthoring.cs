using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent, AddComponentMenu("Landsong/ECS/Building Visual Slot")]
    public sealed class BuildingVisualSlotAuthoring : MonoBehaviour
    {
        [LabelText("表现用途")] public BuildingVisualPurpose Purpose = BuildingVisualPurpose.Operational;
        [Min(1)] [LabelText("适用等级")] public int Level = 1;
        [Min(0), Tooltip("0 为通用施工表现；其他值匹配当前施工/修复回合，缺项回退较早阶段。")] [LabelText("施工或修复阶段")] public int Step;
        [LabelText("皮肤标识")] public string SkinId;
        [Tooltip("仅标记待美术替换，不改变玩法。优先使用同条件下的正式模型。")] [LabelText("占位模型")] public bool Placeholder;
    }
}
