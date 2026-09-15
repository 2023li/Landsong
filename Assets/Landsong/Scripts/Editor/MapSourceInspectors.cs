using Landsong.GridSystem;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [CustomEditor(typeof(MapTerrainRules))]
    public sealed class MapTerrainRulesEditor : OdinEditor { }

    [CustomEditor(typeof(MapSpawnRegionAuthoring))]
    public sealed class MapSpawnRegionEditor : OdinEditor { }

    [CustomEditor(typeof(MapProjectileRegionAuthoring))]
    public sealed class MapProjectileRegionEditor : OdinEditor { }

    [CustomEditor(typeof(TileWorldCreatorMapBakeProfile))]
    public sealed class MapBakeProfileEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("这是从源场景地形规则生成的烘焙快照，请通过 MapContentAuthoring 编辑规则。", MessageType.Info);
            using (new EditorGUI.DisabledScope(true)) base.OnInspectorGUI();
        }
    }
}
