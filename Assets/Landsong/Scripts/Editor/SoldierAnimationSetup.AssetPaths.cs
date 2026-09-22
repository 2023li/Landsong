#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Editor;
using UnityEditor;

namespace Landsong.EditorTools
{
    // Paths of the retained militia example, used only by diagnostics and preview.
    public static partial class SoldierAnimationSetup
    {
        public const string PrefabPath = Output + "/militia.prefab";
        public const string ViewPrefabPath = Output + "/militiaView.prefab";
        public const string ControllerPath = Output + "/militia.controller";
        public const string LeftHandMaskPath = Output + "/LeftHand.mask";
        public const string RightHandMaskPath = Output + "/RightHand.mask";
        public const string DefinitionPath = "Assets/Landsong/ECSContent/Definitions/Soldier/militia.asset";
        public const string VictorySource = "Assets/Landsong/Art/Animations/庆祝Victory.fbx";
        public const string VictoryClipPath = Output + "/Clips/Victory.anim";
        public const string CelebrationMaskPath = Output + "/CelebrationArms.mask";

        public static SoldierDefinitionAsset Militia()
            => ContentAuthoringContext.Content().Get<SoldierCatalogAsset>().Definitions.Single(item => item != null && item.Metadata.Id == "militia");

        public static string CurrentPackage()
            => (Path.GetDirectoryName(AssetDatabase.GetAssetPath(Militia().Prefab)) ?? string.Empty).Replace('\\', '/');
    }
}
#endif
