using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingBasePopulationSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("基础人口数量")]
        [MinValue(0)]
        public int Population;
        [LabelText("是否王国核心")]
        public bool IsCore;
    }
}
