using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingExperienceSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("每回合经验")]
        [MinValue(0)]
        public int ExperiencePerTurn;
        [LabelText("升级所需经验")]
        [MinValue(0)]
        public int UpgradeExperience;
        [LabelText("获取经验所需工人")]
        [MinValue(0)]
        public int RequiredWorkers;
    }
}
