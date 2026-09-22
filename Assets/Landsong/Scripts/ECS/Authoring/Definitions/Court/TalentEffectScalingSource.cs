using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentEffectScalingSource
    {
        [LabelText("缩放方式")]
        public TalentScalingKind Kind;
        [LabelText("缩放来源物品")]
        public ItemDefinitionAsset SourceItem;
        [LabelText("缩放来源建筑")]
        public BuildingDefinitionAsset SourceBuilding;
    }
}
