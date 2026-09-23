using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class QuestObjectivesCompiler
    {
        public static void Compile(ref BlobBuilder builder, QuestObjectivesSource source, ref global::Landsong.ECS.Definitions.QuestObjectives target, BuildingCatalogIndex buildingIndex, ItemCatalogIndex itemIndex, TechnologyCatalogIndex technologyIndex, float itemQuantityScale = 1)
        {
            if (source == null)
                throw new InvalidOperationException("缺少 QuestObjectives 配置。");
            if (source.Requirements == null || source.Requirements.Any(objective => objective == null))
                throw new InvalidOperationException("任务要求列表不能包含空元素。");
            if (source.BuildingObjectives == null)
                throw new InvalidOperationException("建筑目标列表不能为空引用。");
            var orderedBuildingObjectives = source.BuildingObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var BuildingObjectives = builder.Allocate(ref target.BuildingObjectives, orderedBuildingObjectives.Length);
            for (int i = 0; i < orderedBuildingObjectives.Length; i++)
            {
                QuestBuildingObjectiveCompiler.Compile(ref builder, orderedBuildingObjectives[i], ref BuildingObjectives[i], buildingIndex);
            }

            if (source.PlantedBuildingObjectives == null)
                throw new InvalidOperationException("种植作物建筑目标列表不能为空引用。");
            var orderedPlantedBuildingObjectives = source.PlantedBuildingObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var PlantedBuildingObjectives = builder.Allocate(ref target.PlantedBuildingObjectives, orderedPlantedBuildingObjectives.Length);
            for (int i = 0; i < orderedPlantedBuildingObjectives.Length; i++)
            {
                QuestPlantedBuildingObjectiveCompiler.Compile(ref builder, orderedPlantedBuildingObjectives[i], ref PlantedBuildingObjectives[i], buildingIndex);
            }

            if (source.OwnedItemObjectives == null)
                throw new InvalidOperationException("持有物品目标列表不能为空引用。");
            var orderedOwnedItemObjectives = source.OwnedItemObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var OwnedItemObjectives = builder.Allocate(ref target.OwnedItemObjectives, orderedOwnedItemObjectives.Length);
            for (int i = 0; i < orderedOwnedItemObjectives.Length; i++)
            {
                QuestOwnedItemObjectiveCompiler.Compile(ref builder, orderedOwnedItemObjectives[i], ref OwnedItemObjectives[i], itemIndex);
                OwnedItemObjectives[i].Quantity = ItemQuantityScaling.Apply(OwnedItemObjectives[i].Quantity, itemQuantityScale);
            }

            if (source.SubmittedItemObjectives == null)
                throw new InvalidOperationException("提交物品目标列表不能为空引用。");
            var orderedSubmittedItemObjectives = source.SubmittedItemObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var SubmittedItemObjectives = builder.Allocate(ref target.SubmittedItemObjectives, orderedSubmittedItemObjectives.Length);
            for (int i = 0; i < orderedSubmittedItemObjectives.Length; i++)
            {
                QuestSubmittedItemObjectiveCompiler.Compile(ref builder, orderedSubmittedItemObjectives[i], ref SubmittedItemObjectives[i], itemIndex);
                SubmittedItemObjectives[i].Quantity = ItemQuantityScaling.Apply(SubmittedItemObjectives[i].Quantity, itemQuantityScale);
            }

            if (source.TechnologyObjectives == null)
                throw new InvalidOperationException("科技目标列表不能为空引用。");
            var orderedTechnologyObjectives = source.TechnologyObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var TechnologyObjectives = builder.Allocate(ref target.TechnologyObjectives, orderedTechnologyObjectives.Length);
            for (int i = 0; i < orderedTechnologyObjectives.Length; i++)
            {
                QuestTechnologyObjectiveCompiler.Compile(ref builder, orderedTechnologyObjectives[i], ref TechnologyObjectives[i], technologyIndex);
            }

            if (source.CameraMoveObjectives == null)
                throw new InvalidOperationException("移动镜头目标列表不能为空引用。");
            var orderedCameraMoveObjectives = source.CameraMoveObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var CameraMoveObjectives = builder.Allocate(ref target.CameraMoveObjectives, orderedCameraMoveObjectives.Length);
            for (int i = 0; i < orderedCameraMoveObjectives.Length; i++)
            {
                QuestCameraMoveObjectiveCompiler.Compile(ref builder, orderedCameraMoveObjectives[i], ref CameraMoveObjectives[i]);
            }

            if (source.CameraZoomObjectives == null)
                throw new InvalidOperationException("缩放镜头目标列表不能为空引用。");
            var orderedCameraZoomObjectives = source.CameraZoomObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var CameraZoomObjectives = builder.Allocate(ref target.CameraZoomObjectives, orderedCameraZoomObjectives.Length);
            for (int i = 0; i < orderedCameraZoomObjectives.Length; i++)
            {
                QuestCameraZoomObjectiveCompiler.Compile(ref builder, orderedCameraZoomObjectives[i], ref CameraZoomObjectives[i]);
            }

            if (source.TurnObjectives == null)
                throw new InvalidOperationException("回合目标列表不能为空引用。");
            var orderedTurnObjectives = source.TurnObjectives.OrderBy(entry => entry == null ? throw new InvalidOperationException("列表中存在空条目。") : entry.Order).ToArray();
            var TurnObjectives = builder.Allocate(ref target.TurnObjectives, orderedTurnObjectives.Length);
            for (int i = 0; i < orderedTurnObjectives.Length; i++)
            {
                QuestTurnObjectiveCompiler.Compile(ref builder, orderedTurnObjectives[i], ref TurnObjectives[i]);
            }
        }
    }
}
