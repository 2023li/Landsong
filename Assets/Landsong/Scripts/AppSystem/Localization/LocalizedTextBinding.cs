using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Landsong.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTextBinding : MonoBehaviour
    {
        [SerializeField] private string tableName = LocalizationTables.Ui;
        [SerializeField] private string key = string.Empty;
        [SerializeField, TextArea] private string sourceFallback = string.Empty;

        private TMP_Text target;

        public string TableName => tableName;
        public string Key => key;
        public string SourceFallback => sourceFallback;

        public void Configure(string newTableName, string newKey, string newSourceFallback)
        {
            tableName = newTableName ?? string.Empty;
            key = newKey ?? string.Empty;
            sourceFallback = newSourceFallback ?? string.Empty;
            if (Application.isPlaying)
            {
                Refresh();
            }
        }

        private void Awake()
        {
            target = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
            Refresh();
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
        }

        public void Refresh()
        {
            target ??= GetComponent<TMP_Text>();
            if (target == null || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            target.text = L10n.Get(tableName, key, sourceFallback);
        }

        private void HandleSelectedLocaleChanged(Locale _)
        {
            Refresh();
        }
    }
}
