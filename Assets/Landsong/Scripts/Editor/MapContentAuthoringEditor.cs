using System;
using Landsong.GridSystem;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [CustomEditor(typeof(MapContentAuthoring))]
    public sealed class MapContentAuthoringEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            var content = (MapContentAuthoring)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("初始化地图", GUILayout.Height(30)))
                    Run(() => GameMapWorkflow.Initialize(content), "初始化完成，请保存场景。");
                if (GUILayout.Button("校验地图"))
                    Run(() => GameMapWorkflow.Validate(content), "地图校验通过。");
                if (GUILayout.Button("检查地图范围与边缘"))
                    Run(() => Debug.Log(MapBoundaryTools.Inspect(content), content), "范围检查完成。");
                if (GUILayout.Button("生成临时地图预览"))
                    Run(() => GameMapWorkflow.GeneratePreview(content), "地图预览已生成；预览不会写入场景文件。");
                if (GUILayout.Button("烘焙地图", GUILayout.Height(34)))
                    Run(() => GameMapWorkflow.Bake(content), "地图与建筑已烘焙并保存；已收录地图的菜单资料已更新。");
                if (GUILayout.Button("添加到地图菜单"))
                    Run(() => GameMapWorkflow.AddToMapMenu(content), "已添加到地图菜单并同步游戏加载引用；重复点击会更新资料，不会重复添加。无需重新烘焙。");
                if (content.TwcConfiguration != null && GUILayout.Button("编辑 TWC 地形配置"))
                    AssetDatabase.OpenAsset(content.TwcConfiguration);
            }

            EditorGUILayout.HelpBox("通过本场景制作地图。Data/Source 保存实际绘图成果，必须保留；地图预览不写入场景，正式 Mesh 生成到 Git 忽略的 Assets/LandsongGenerated。进入 Play、烘焙和 Player Build 会补齐运行产物。", MessageType.Info);
            base.OnInspectorGUI();
        }

        void Run(Action action, string success)
        {
            try
            {
                action();
                Debug.Log(success, target);
            }
            catch (Exception error)
            {
                Debug.LogException(error, target);
                EditorUtility.DisplayDialog("地图操作未完成", error.Message, "确定");
            }
        }
    }
}
