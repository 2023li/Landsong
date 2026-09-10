using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [DisallowMultipleComponent, AddComponentMenu("Landsong/ECS/Building State Visual Part")]
    public sealed class BuildingVisualPartAuthoring : MonoBehaviour
    {
        [Min(0), Tooltip("0 不限制。用于资源堆剩余 3/2/1 次的模型。")] public int HarvestRemaining;
        [Tooltip("指定时，仅该作物正在生长时显示；同一节点的阶段范围均使用生长百分比。")] public string CropId;
        [Range(0, 1)] public float GrowthFrom;
        [Range(0, 1)] public float GrowthTo = 1;
        public bool OperationalOnly = true;
    }
}
