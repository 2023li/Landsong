using TMPro;
using UnityEngine;

namespace Landsong.UISystem
{
    [CreateAssetMenu(
        menuName = "Landsong/UI/Resource Rich Text Settings",
        fileName = ResourcesAssetName)]
    public sealed class ResourceRichTextSettings : ScriptableObject
    {
        public const string ResourcesAssetName = "ResourceRichTextSettings";
        private const string ResourcesLoadPath = ResourcesAssetName;

        private static ResourceRichTextSettings instance;
        private static bool hasLoadedInstance;

        [SerializeField] private TMP_SpriteAsset resourceSpriteAsset;
        [SerializeField] private bool showNameAfterIcon;

        public static ResourceRichTextSettings Instance
        {
            get
            {
                if (instance == null)
                {
                    if (hasLoadedInstance)
                    {
                        return null;
                    }

                    hasLoadedInstance = true;
                    instance = Resources.Load<ResourceRichTextSettings>(ResourcesLoadPath);
                }

                return instance;
            }
        }

        public static TMP_SpriteAsset GlobalSpriteAsset => Instance == null ? null : Instance.resourceSpriteAsset;
        public static bool GlobalShowNameAfterIcon => Instance != null && Instance.showNameAfterIcon;

        public static void SetGlobalSettings(ResourceRichTextSettings settings)
        {
            instance = settings;
            hasLoadedInstance = true;
        }

        public static void ClearGlobalSettings()
        {
            instance = null;
            hasLoadedInstance = false;
        }
    }
}
