using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestObjectivesSource
    {
        [LabelText("建筑目标")]
        public QuestBuildingObjectiveSource[] BuildingObjectives = Array.Empty<QuestBuildingObjectiveSource>();
        [LabelText("种植作物建筑目标")]
        public QuestPlantedBuildingObjectiveSource[] PlantedBuildingObjectives = Array.Empty<QuestPlantedBuildingObjectiveSource>();
        [LabelText("持有物品目标")]
        public QuestOwnedItemObjectiveSource[] OwnedItemObjectives = Array.Empty<QuestOwnedItemObjectiveSource>();
        [LabelText("提交物品目标")]
        public QuestSubmittedItemObjectiveSource[] SubmittedItemObjectives = Array.Empty<QuestSubmittedItemObjectiveSource>();
        [LabelText("科技目标")]
        public QuestTechnologyObjectiveSource[] TechnologyObjectives = Array.Empty<QuestTechnologyObjectiveSource>();
        [LabelText("移动镜头目标")]
        public QuestCameraMoveObjectiveSource[] CameraMoveObjectives = Array.Empty<QuestCameraMoveObjectiveSource>();
        [LabelText("缩放镜头目标")]
        public QuestCameraZoomObjectiveSource[] CameraZoomObjectives = Array.Empty<QuestCameraZoomObjectiveSource>();
        [LabelText("回合目标")]
        public QuestTurnObjectiveSource[] TurnObjectives = Array.Empty<QuestTurnObjectiveSource>();
    }
}
