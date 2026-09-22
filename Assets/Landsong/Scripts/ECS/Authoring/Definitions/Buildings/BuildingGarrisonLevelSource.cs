using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingGarrisonLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("驻军容量")]
        [MinValue(0)]
        public int Capacity;
        [LabelText("每批出勤人数")]
        [MinValue(0)]
        public int DeploymentBatchSize = 1;
    }
}
