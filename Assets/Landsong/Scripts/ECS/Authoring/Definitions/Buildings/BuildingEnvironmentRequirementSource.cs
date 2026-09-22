using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingEnvironmentRequirementSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("需求数值")]
        [MinValue(0)]
        public int RequiredValue;
        [LabelText("环境种类")]
        public BuildingEnvironmentKind Type;
    }
}
