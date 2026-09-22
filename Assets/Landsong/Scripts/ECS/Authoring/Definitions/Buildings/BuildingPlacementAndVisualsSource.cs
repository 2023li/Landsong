using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingPlacementAndVisualsSource
    {
        [LabelText("建筑分类")]
        public BuildingCategory Category;

        [LabelText("刷怪间距／巡逻移动力")]
        [Tooltip("刷怪排除区域按格扩展；驻军巡逻将此值作为导航移动力预算，并遵循道路及其他通行消耗。")]
        [MinValue(0)]
        [MaxValue(256)]
        public int SpawnExclusionPadding = 6;
        [LabelText("菜单排序")]
        public int MenuOrder;
        [LabelText("资源提供优先级")]
        public int ProviderPriority;
        [LabelText("允许移动")]
        public bool CanMove = true;
        [LabelText("允许旋转")]
        public bool CanRotate = true;
        [LabelText("移动材料费用比例")]
        [Range(0, 1)]
        public float MoveMaterialRatio = .3f;
        [LabelText("移动经验损耗比例")]
        [Range(0, 1)]
        public float MoveExperienceRatio = .3f;
        [LabelText("废墟通行消耗")]
        [MinValue(.01f)]
        public float RuinMovementCost = 4;
        [LabelText("默认皮肤标识")]
        public string DefaultSkin = "";
    }
}
