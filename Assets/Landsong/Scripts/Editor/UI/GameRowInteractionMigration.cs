#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class GameRowInteractionMigration
    {
        const string Folder = "Assets/Landsong/Objects/Prefabs/UI/GamePanel";
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后配置条目交互。");
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Folder }).Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path.Contains("/Items/") ? 0 : path.EndsWith("/UI_GamePanel.prefab", StringComparison.Ordinal) ? 2 : 1).ThenBy(path => path).ToArray();
            int count = 0;
            foreach (var path in paths)
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var locks = new HashSet<UI_GamePanel_InteractionLock>();
                    UI_GamePanel_InteractionLock Own(Component owner)
                    {
                        var state = owner.GetComponent<UI_GamePanel_InteractionLock>();
                        if (state == null) state = owner.gameObject.AddComponent<UI_GamePanel_InteractionLock>();
                        locks.Add(state); return state;
                    }
                    foreach (var row in contents.GetComponentsInChildren<UI_GamePanel_Row>(true))
                    { row.Interaction = Own(row); Record(row); }
                    foreach (var card in contents.GetComponentsInChildren<UI_GamePanel_QuestSlot>(true))
                    { card.Interaction = Own(card); Record(card); }
                    foreach (var hero in contents.GetComponentsInChildren<UI_GamePanel_HeroHudItem>(true))
                    { hero.Interaction = Own(hero); Record(hero); }
                    foreach (var marker in contents.GetComponentsInChildren<UI_GamePanel_NightMarker>(true))
                    { marker.Interaction = Own(marker); Record(marker); }
                    foreach (var hud in contents.GetComponentsInChildren<UI_GamePanel_BattleHud>(true))
                    { hud.FocusInteraction = Own(hud.DefenseFocus); Record(hud); }
                    if (locks.Count == 0) continue;
                    foreach (var state in locks)
                    {
                        var bindings = new List<UI_GamePanel_RowPointerBinding>();
                        foreach (var control in state.GetComponentsInChildren<Selectable>(true))
                        {
                            // Editor-only lookups author the exact ownership used by runtime pointer events.
                            if (control.GetComponentInParent<UI_GamePanel_InteractionLock>(true) != state) continue;
                            var binding = control.GetComponent<UI_GamePanel_RowPointerBinding>();
                            if (binding == null) binding = control.gameObject.AddComponent<UI_GamePanel_RowPointerBinding>();
                            binding.Target = state; binding.LockWhileSelected = control is TMP_InputField;
                            Record(binding); bindings.Add(binding); count++;
                        }
                        state.Bindings = bindings.ToArray(); Record(state); state.ValidateConfiguration();
                    }
                    foreach (var slot in contents.GetComponentsInChildren<UI_GamePanel_InventorySlot>(true))
                    { slot.InteractionOwner = slot.GetComponentInParent<UI_GamePanel_Row>(true); Record(slot); }
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            return "Game 动态行、任务、英雄与夜间标记交互已显式配置：" + count + " 个控件。";
        }
        static void Record(UnityEngine.Object value)
        {
            EditorUtility.SetDirty(value);
            if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
    }
}
#endif
