using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestPlantedBuildingObjectiveSource : QuestObjectiveSource
    {
        [LabelText("建筑")]
        public BuildingDefinitionAsset Building;
        [LabelText("数量")]
        public int Count;
    }
}
