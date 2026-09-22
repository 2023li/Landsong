using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentWageSource
    {
        [LabelText("基础数量")]
        public int BaseAmount;
        [LabelText("每级增加量")]
        public int PerLevel;
    }
}
