#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class GameUiStableRowsVerification
    {
        public static string Run()
        {
            int assertions = 0;
            void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException("稳定条目验证失败：" + message); }
            var game = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab");
            var root = game.GetComponent<UI_GamePanel>();
            Check(root != null, "缺少游戏面板根组件");
            Check(root.FeaturePanels.OfType<UI_GamePanel_List>().All(panel => panel.RowTemplate != null), "每个功能面板必须持有自己的条目模板引用");
            Check(root.FeaturePanels.OfType<UI_GamePanel_List>().All(panel => EditorUtility.IsPersistent(panel.RowTemplate)), "功能面板条目模板必须来自独立资产而非根节点公共对象");
            Check(root.Buildings.ConfirmRowTemplate != null && root.BuildingDetails.RowTemplate != null, "建筑确认和建筑详情必须各自配置条目模板");
            var inventory = root.InventoryWindow;
            inventory.ValidateConfiguration();
            Check(inventory.BuildingTemplate.transform.parent == inventory.BuildingsScroll.content, "建筑模板属于建筑列表");
            Check(inventory.ResourceTemplate.transform.parent == inventory.ResourcesScroll.content, "资源模板属于资源列表");
            Check(inventory.PendingTemplate.transform.parent == inventory.PendingContent, "待存模板属于独立待存区");
            Check(!inventory.BuildingTemplate.gameObject.activeSelf && !inventory.ResourceTemplate.gameObject.activeSelf
                && !inventory.PendingTemplate.gameObject.activeSelf, "真实模板默认隐藏");
            Check(Array.TrueForAll(game.GetComponentsInChildren<MonoBehaviour>(true), component => component == null || component.GetType().Name != "UI_GamePanel_RowRenderer"), "根预制体不能保留公共行渲染器");
            return "GameUiStableRows Assertions: " + assertions + " passed";
        }
    }
}
#endif
