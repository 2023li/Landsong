#if UNITY_EDITOR
using Landsong.Animation;
using Landsong.Content;
using Landsong.ECS.Presentation;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(SoldierAnimationAuthoring))]
    public sealed class UnitAnimationAuthoringEditor : OdinEditor { }

    [CustomEditor(typeof(SoldierAnimationVisualAuthoring))]
    public sealed class UnitAnimationVisualAuthoringEditor : OdinEditor { }

    [CustomEditor(typeof(BuildingVisualSlotAuthoring))]
    public sealed class BuildingVisualSlotAuthoringEditor : OdinEditor { }

    [CustomEditor(typeof(WorldVisualCatalog))]
    public sealed class LegacyWorldVisualCatalogEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("这里只保留 boss、raider、titan、invader 的遗留外观。新增单位请使用 Landsong → 内容制作 → 创建单位；本表不再接受新内容。", MessageType.Info);
            base.OnInspectorGUI();
        }
    }
}
#endif
