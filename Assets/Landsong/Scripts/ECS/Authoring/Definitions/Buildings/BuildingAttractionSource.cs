using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingAttractionSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("附近居民范围")]
        [MinValue(0)]
        public float Radius;
        [LabelText("每名居民吸引力加成")]
        public float PerResidentBonus;
    }
}
