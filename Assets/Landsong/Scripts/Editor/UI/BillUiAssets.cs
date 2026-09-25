#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class BillUiAssets
    {
        public const string GamePath = "Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab";
        public const string HistoryPath = "Assets/Landsong/UI/Prefabs/GamePanel/Views/UI_GamePanel_History.prefab";
        public static string Inspect()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var text = new StringBuilder("stage=" + stage?.assetPath + " dirty=" + stage?.scene.isDirty + "\n");
            var go = PrefabUtility.LoadPrefabContents(GamePath);
            try
            {
                foreach (var view in go.GetComponentsInChildren<UI_GamePanel_View>(true).Where(v => v.PanelId == GamePanelId.Inventory || v.PanelId == GamePanelId.History))
                    foreach (var t in view.GetComponentsInChildren<Transform>(true))
                        text.AppendLine(AnimationUtility.CalculateTransformPath(t, go.transform) + " | " + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            File.WriteAllText("Library/LandsongEcs/bill-ui-hierarchy.txt", text.ToString());
            return text.ToString();
        }
    }
}
#endif
