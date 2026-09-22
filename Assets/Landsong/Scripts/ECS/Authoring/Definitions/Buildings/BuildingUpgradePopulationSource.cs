using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingUpgradePopulationSource
    {
        [LabelText("升级目标等级（0 = 所有升级）")]
        [MinValue(0)]
        public int TargetLevel = 1;
        [LabelText("所需人数")]
        [MinValue(0)]
        public int Required;
    }
}
