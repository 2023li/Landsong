using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // Owns language initialization and preference changes independently of audio playback.
    [DefaultExecutionOrder(-8000)]
    public sealed class LocalizationRuntime : MonoBehaviour
    {
        [SerializeField, LabelText("语言目录"), Required]
        LocalizationCatalog catalog;
        public LocalizationCatalog Configuration => catalog;

        int preferences = -1;
        public static string LanguageDirectory => System.IO.Path.Combine(Application.persistentDataPath, "ExternalLanguagePacks");

        void Awake()
        {
            if (catalog == null)
                throw new InvalidOperationException("本地化服务未配置语言目录。");
            PresentationText.Initialize(catalog);
            PresentationText.Discover(LanguageDirectory);
            ApplyPreferences();
        }

        void Update()
        {
            if (preferences != InterfaceSettings.Revision)
                ApplyPreferences();
        }

        void ApplyPreferences()
        {
            preferences = InterfaceSettings.Revision;
            PresentationText.SetLanguage(InterfaceSettings.Current.Language);
        }
    }
}
