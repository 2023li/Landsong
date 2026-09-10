using UnityEngine;

namespace Landsong.ECS.Authoring
{
    public enum BuildingVisualPurpose : byte { Construction, Operational, Preview, Ruined, Repairing }
    [DisallowMultipleComponent, AddComponentMenu("Landsong/ECS/Building Visual Slot")]
    public sealed class BuildingVisualSlotAuthoring : MonoBehaviour
    {
        public BuildingVisualPurpose Purpose = BuildingVisualPurpose.Operational;
        [Min(1)] public int Level = 1;
        [Min(0), Tooltip("0 为通用施工表现；其他值匹配当前施工/修复回合，缺项回退较早阶段。")] public int Step;
        public string SkinId;
        [Tooltip("仅标记待美术替换，不改变玩法。优先使用同条件下的正式模型。")] public bool Placeholder;
    }
}
