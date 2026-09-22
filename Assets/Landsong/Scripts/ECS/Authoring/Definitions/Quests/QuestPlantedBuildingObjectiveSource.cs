using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestPlantedBuildingObjectiveSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("目标稳定标识")]
        public string Key = "";
        [LabelText("建筑")]
        public BuildingDefinitionAsset Building;
        [LabelText("数量")]
        public int Count;
    }
}
