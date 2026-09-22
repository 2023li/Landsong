using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingQuestCapacitySource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("承接任务槽位")]
        [MinValue(0)]
        public int Slots;
    }
}
