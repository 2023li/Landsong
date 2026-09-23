using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public abstract class QuestObjectiveSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("目标稳定标识")]
        public string Key = "";
    }

    [Serializable]
    public sealed class QuestObjectivesSource
    {
        [SerializeReference, LabelText("任务目标"), ListDrawerSettings(ShowIndexLabels = true)]
        public List<QuestObjectiveSource> Requirements = new List<QuestObjectiveSource>();

        T[] ByType<T>() where T : QuestObjectiveSource => Requirements?.OfType<T>().ToArray();

        // The blob compiler groups requirements by type; the authoring inspector keeps one list.
        public QuestBuildingObjectiveSource[] BuildingObjectives => ByType<QuestBuildingObjectiveSource>();
        public QuestPlantedBuildingObjectiveSource[] PlantedBuildingObjectives => ByType<QuestPlantedBuildingObjectiveSource>();
        public QuestOwnedItemObjectiveSource[] OwnedItemObjectives => ByType<QuestOwnedItemObjectiveSource>();
        public QuestSubmittedItemObjectiveSource[] SubmittedItemObjectives => ByType<QuestSubmittedItemObjectiveSource>();
        public QuestTechnologyObjectiveSource[] TechnologyObjectives => ByType<QuestTechnologyObjectiveSource>();
        public QuestCameraMoveObjectiveSource[] CameraMoveObjectives => ByType<QuestCameraMoveObjectiveSource>();
        public QuestCameraZoomObjectiveSource[] CameraZoomObjectives => ByType<QuestCameraZoomObjectiveSource>();
        public QuestTurnObjectiveSource[] TurnObjectives => ByType<QuestTurnObjectiveSource>();
    }
}
