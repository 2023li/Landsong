using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestBuildingObjectiveSource : QuestObjectiveSource
    {
        [LabelText("建筑")]
        public BuildingDefinitionAsset Building;
        [LabelText("数量")]
        public int Count;
        [LabelText("最低等级")]
        public int MinimumLevel;
        [LabelText("仅已完工")]
        public bool CompletedOnly;
    }
}
