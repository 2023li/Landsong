using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent, AddComponentMenu("Landsong/ECS/Building State Visual Part")]
    public sealed class BuildingVisualPartAuthoring : MonoBehaviour
    {
        [Min(0), Tooltip("0 不限制。用于资源堆剩余 3/2/1 次的模型。")] [LabelText("剩余采集次数")] public int HarvestRemaining;
        [Tooltip("指定时，仅该作物正在生长时显示；同一节点的阶段范围均使用生长百分比。")] [LabelText("作物内容标识")] public string CropId;
        [Range(0, 1)] [LabelText("生长比例下限")] public float GrowthFrom;
        [Range(0, 1)] [LabelText("生长比例上限")] public float GrowthTo = 1;
        [LabelText("仅运营阶段显示")] public bool OperationalOnly = true;
    }
}
