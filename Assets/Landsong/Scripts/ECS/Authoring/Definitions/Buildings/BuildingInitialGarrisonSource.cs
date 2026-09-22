using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingInitialGarrisonSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("兵种")]
        public SoldierDefinitionAsset Soldier;
        [LabelText("人数")]
        [MinValue(0)]
        public int Count = 1;
    }
}
