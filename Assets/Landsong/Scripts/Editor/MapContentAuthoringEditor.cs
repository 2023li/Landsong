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
            base.OnInspectorGUI();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "TWC 只负责制图。烘焙会更新地形并吸附 ECS 初始建筑预览；更新游戏地形请选中 MapAsset 执行地形导入。预览的初始建筑只有点击下方同步按钮才写入 MapAsset。",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                var content = (MapContentAuthoring)target;
                if (GUILayout.Button("将初始建筑预览同步到 ECS MapAsset"))
                {
                    if (content.TargetMap == null || !content.TryCollectInitialBuildings(out _, out _))
                    { EditorUtility.DisplayDialog("不能同步", "请绑定 Target Map，并先修正初始建筑定义与占地。", "确定"); return; }
                    content.TryCollectInitialBuildings(out var buildings, out _);
                    var candidate = Object.Instantiate(content.TargetMap);
                    try
                    {
                        candidate.InitialBuildings = buildings;
                        Landsong.ECS.Editor.EcsMapIncrementalImport.ValidateInitialBuildings(candidate, new Landsong.ECS.Editor.EcsMapIncrementalImport.TerrainSnapshot
                        { Min = candidate.Min, Size = candidate.Size, Cells = candidate.Cells, Origin = candidate.Origin, CellSize = candidate.CellSize });
                        Undo.RecordObject(content.TargetMap, "Sync native initial buildings");
                        content.TargetMap.InitialBuildings = buildings;
                        EditorUtility.SetDirty(content.TargetMap); AssetDatabase.SaveAssets();
                    }
                    catch (System.Exception syncError) { EditorUtility.DisplayDialog("不能同步", syncError.Message, "确定"); }
                    finally { Object.DestroyImmediate(candidate); }
                    return;
                }
                if (!GUILayout.Button("烘焙地图并吸附初始建筑到网格", GUILayout.Height(32f)))
                {
                    return;
                }

                if (!TileWorldCreatorMapBaker.BakeAndSnapMapContent(
                        content,
                        out var map,
                        out var error))
                {
                    Debug.LogError($"地图烘焙与初始建筑吸附未完成：{error}", content);
                    EditorUtility.DisplayDialog("地图烘焙与吸附未完成", error, "确定");
                    return;
                }

                EditorGUIUtility.PingObject(map);
                Debug.Log(
                    $"地图烘焙与初始建筑吸附完成：{map.Cells.Count} 个逻辑格。请保存当前场景。",
                    content);
            }
        }
    }
}
