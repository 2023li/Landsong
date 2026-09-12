using Sirenix.OdinInspector;
using Landsong.ECS.Authoring;
using UnityEngine;

namespace Landsong.GridSystem
{
    // Authoring only: this never spawns a runtime building or owns gameplay state.
    [DisallowMultipleComponent]
    [AddComponentMenu("Landsong/Map/Initial Building Preview")]
    public sealed class InitialBuildingPreview : MonoBehaviour
    {
        [LabelText("建筑内容定义")] public GameDefinitionAsset Definition;
        [Min(1)] [LabelText("初始等级")] public int Level = 1;
    }
}
