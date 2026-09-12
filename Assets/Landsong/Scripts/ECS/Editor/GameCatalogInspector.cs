#if UNITY_EDITOR
using Landsong.ECS.Authoring;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(GameCatalogAsset))]
    public sealed class GameCatalogInspector : OdinEditor { }
}
#endif
