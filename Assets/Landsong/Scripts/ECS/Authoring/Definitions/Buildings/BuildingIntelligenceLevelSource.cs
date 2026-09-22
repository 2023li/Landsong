using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingIntelligenceLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("所需科技（可选）")]
        public TechnologyDefinitionAsset Technology;
        [LabelText("情报点数")]
        [MinValue(0)]
        public int Points;
        [LabelText("所需工人")]
        [MinValue(0)]
        public int RequiredWorkers;
    }
}
