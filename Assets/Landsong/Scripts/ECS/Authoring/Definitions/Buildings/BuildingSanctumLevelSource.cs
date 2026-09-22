using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingSanctumLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("英雄")]
        public HeroDefinitionAsset Hero;
        [LabelText("供奉所需工人")]
        [MinValue(0)]
        public int RequiredWorkers;
    }
}
