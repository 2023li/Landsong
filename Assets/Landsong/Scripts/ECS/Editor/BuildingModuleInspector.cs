#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingModuleInspector
    {
        public static void DrawSummary(BuildingDefinitionAsset asset)
        {
            var source = asset;
            try
            {
                BuildingCatalogValidation.Validate(source);
            }
            catch (Exception error)
            {
                EditorGUILayout.HelpBox(error.Message, MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox("配置检查通过。开局驻军仅用于新游戏中的初始建筑。", MessageType.Info);
            EditorGUILayout.LabelField("等级能力预览", EditorStyles.boldLabel);
            var modules = source.Capabilities;
            for (int level = 1; level <= source.MaximumLevel; level++)
            {
                var parts = new List<string>();
                bool Active(int authored) => authored == 0 || authored == level;
                void Add(string label, int value)
                {
                    if (value > 0)
                        parts.Add(label + " " + value);
                }

                if (modules.Housing.Enabled)
                    Add("人口", modules.Housing.Population.Where(row => Active(row.Level)).Sum(row => row.Population) + modules.Housing.Residences.Where(row => Active(row.Level)).Sum(row => row.Capacity));
                if (modules.Workforce.Enabled)
                    Add("岗位", modules.Workforce.Levels.Where(row => Active(row.Level)).Sum(row => row.Capacity));
                if (modules.Storage.Enabled)
                    Add("库存格", modules.Storage.Warehouses.Where(row => Active(row.Level)).Sum(row => row.Slots));
                if (modules.Research.Enabled)
                    Add("科研/回合", modules.Research.Levels.Where(row => Active(row.Level)).Sum(row => row.PointsPerTurn));
                if (modules.Garrison.Enabled)
                {
                    Add("驻军槽", modules.Garrison.Levels.Where(row => Active(row.Level)).Sum(row => row.Capacity));
                    Add("开局驻军", modules.Garrison.InitialUnits.Where(row => Active(row.Level)).Sum(row => row.Count));
                }

                if (modules.Quests.Enabled)
                {
                    Add("任务槽", modules.Quests.Capacity.Where(row => Active(row.Level)).Sum(row => row.Slots));
                    Add("邀约槽", modules.Quests.Invitations.Where(row => Active(row.Level)).Sum(row => row.Slots));
                }

                EditorGUILayout.LabelField("等级 " + level, parts.Count == 0 ? "无上述容量产出" : string.Join(" · ", parts), EditorStyles.wordWrappedLabel);
            }
        }
    }
}
#endif
