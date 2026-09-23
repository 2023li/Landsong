using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Landsong.ECS.Definitions;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class QuestCatalogValidation
    {
        public static void Validate(QuestCatalogAsset catalog)
        {
            var members = new HashSet<QuestDefinitionAsset>(catalog.Definitions);
            var marks = new Dictionary<QuestDefinitionAsset, byte>();
            foreach (var asset in catalog.Definitions)
            {
                var source = asset;
                void Fail(string reason) => throw new InvalidOperationException(source.Metadata.Id + "：" + reason);
                if ((source.Behavior & ~(QuestBehaviorFlags.Mainline | QuestBehaviorFlags.Draft)) != 0 || (byte)source.OfferType > 3 || source.DeadlineTurns < 0 || source.Intensity < 0 || source.Intensity > 3 || !math.isfinite(source.OfferWeight) || source.OfferWeight < 0 || !math.isfinite(source.ItemQuantityScale) || source.ItemQuantityScale <= 0)
                    Fail("任务标记、类型、期限、强度、抽取权重或数量倍率无效。");
                if ((source.Behavior & QuestBehaviorFlags.Draft) != 0)
                    continue;
                var prerequisites = source.Prerequisites;
                if (prerequisites == null || source.Objectives == null)
                    Fail("缺少任务前置或目标配置。");
                if (prerequisites.BuildingRequirements.Length != 0 || prerequisites.BuffRequirements.Length != 0 || prerequisites.FeatureRequirements.Length != 0 || prerequisites.ExpeditionRequirements.Length != 0)
                    Fail("任务前置只支持已领取任务或已完成科技。");
                if (source.Objectives.Requirements == null || source.Objectives.Requirements.Any(objective => objective == null))
                    Fail("任务要求列表不能包含空元素。");
                if (source.Objectives.BuildingObjectives.Length + source.Objectives.PlantedBuildingObjectives.Length + source.Objectives.OwnedItemObjectives.Length + source.Objectives.SubmittedItemObjectives.Length + source.Objectives.TechnologyObjectives.Length + source.Objectives.CameraMoveObjectives.Length + source.Objectives.CameraZoomObjectives.Length + source.Objectives.TurnObjectives.Length != source.Objectives.Requirements.Count)
                    Fail("任务要求列表包含不支持的目标类型。");
                var parents = new HashSet<QuestDefinitionAsset>();
                foreach (var requirement in prerequisites.QuestRequirements)
                    if (requirement == null || requirement.Quest == null || requirement.Required != 1 || requirement.Quest == asset || !members.Contains(requirement.Quest) || !parents.Add(requirement.Quest) || (requirement.Quest.Behavior & QuestBehaviorFlags.Draft) != 0)
                        Fail("任务前置无效、重复、引用自身或草稿。");
                var technologies = new HashSet<TechnologyDefinitionAsset>();
                foreach (var requirement in prerequisites.TechnologyRequirements)
                    if (requirement == null || requirement.Technology == null || requirement.Required != 1 || !technologies.Add(requirement.Technology))
                        Fail("科技前置无效或重复。");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                void Objective(string key, int quantity)
                {
                    if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) > 61 || key.Contains("|") || !keys.Add(key))
                        Fail("目标需要唯一、长度不超过61字节且不含竖线的稳定标识。");
                    if (quantity <= 0)
                        Fail("目标数量必须为正数。");
                }

                void Scaled(int quantity)
                {
                    var value = (double)quantity * source.ItemQuantityScale;
                    if (math.round(value) < 1 || value > int.MaxValue)
                        Fail("任务物品数量缩放后越界。");
                }

                foreach (var objective in source.Objectives.BuildingObjectives)
                {
                    Objective(objective.Key, objective.Count);
                    if (objective.Building == null || objective.MinimumLevel < 0 || objective.MinimumLevel > objective.Building.MaximumLevel)
                        Fail("建筑目标的定义或最低等级无效。");
                }

                foreach (var objective in source.Objectives.PlantedBuildingObjectives)
                    Objective(objective.Key, objective.Count);
                foreach (var objective in source.Objectives.OwnedItemObjectives)
                {
                    Objective(objective.Key, objective.Quantity);
                    Scaled(objective.Quantity);
                }

                foreach (var objective in source.Objectives.SubmittedItemObjectives)
                {
                    Objective(objective.Key, objective.Quantity);
                    Scaled(objective.Quantity);
                }

                foreach (var objective in source.Objectives.TechnologyObjectives)
                    Objective(objective.Key, objective.Count);
                foreach (var objective in source.Objectives.CameraMoveObjectives)
                    Objective(objective.Key, objective.Count);
                foreach (var objective in source.Objectives.CameraZoomObjectives)
                    Objective(objective.Key, objective.Count);
                foreach (var objective in source.Objectives.TurnObjectives)
                    Objective(objective.Key, objective.Turns);
                if (keys.Count == 0)
                    Fail("没有目标的任务必须标记为草稿。");
                foreach (var reward in source.Rewards.Items)
                    Scaled(reward.Quantity);
                foreach (var penalty in source.FailurePenalties)
                {
                    if (penalty.Quantity <= 0)
                        Fail("失败惩罚数量必须为正。");
                    Scaled(penalty.Quantity);
                }
            }

            void Visit(QuestDefinitionAsset asset)
            {
                if (marks.TryGetValue(asset, out var mark))
                {
                    if (mark == 1)
                        throw new InvalidOperationException(asset.Metadata.Id + "：任务前置存在循环。");
                    return;
                }

                marks[asset] = 1;
                foreach (var parent in asset.Prerequisites.QuestRequirements)
                    Visit(parent.Quest);
                marks[asset] = 2;
            }

            foreach (var asset in catalog.Definitions)
                if ((asset.Behavior & QuestBehaviorFlags.Draft) == 0)
                    Visit(asset);
        }
    }
}
