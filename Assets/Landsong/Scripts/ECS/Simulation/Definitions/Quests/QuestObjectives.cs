using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct QuestObjectives
    {
        public BlobArray<QuestBuildingObjective> BuildingObjectives;
        public BlobArray<QuestPlantedBuildingObjective> PlantedBuildingObjectives;
        public BlobArray<QuestOwnedItemObjective> OwnedItemObjectives;
        public BlobArray<QuestSubmittedItemObjective> SubmittedItemObjectives;
        public BlobArray<QuestTechnologyObjective> TechnologyObjectives;
        public BlobArray<QuestTechnologyCompletedObjective> TechnologyCompletedObjectives;
        public BlobArray<QuestPopulationObjective> PopulationObjectives;
        public BlobArray<QuestCameraMoveObjective> CameraMoveObjectives;
        public BlobArray<QuestCameraZoomObjective> CameraZoomObjectives;
        public BlobArray<QuestTurnObjective> TurnObjectives;
    }

    public struct QuestTechnologyCompletedObjective
    {
        public int Order;
        public FixedString64Bytes Key;
        public TechnologyId Technology;
        public int Count;
    }

    public struct QuestPopulationObjective
    {
        public int Order;
        public FixedString64Bytes Key;
        public int Count;
    }
}
