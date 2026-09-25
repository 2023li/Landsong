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

}
#endif
