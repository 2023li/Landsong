using System;
using System.Linq;
using Landsong.ECS.Persistence;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using Dropdown = TMPro.TMP_Dropdown;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsMainMenu : MonoBehaviour
    {
        public EcsMapMenuCatalog Catalog;
        public Dropdown MapSelection;
        public Button StartButton;
        public Button ContinueButton, QuitButton;
        public Text Status;
        public Button LoadButton { get; private set; }
        public Button SettingsButton { get; private set; }
        // Allows development verification to use an isolated store.
        public RunArchiveStore Store { get; set; }
        RunArchiveStore ArchiveStore => Store ?? CheckpointSystem.DefaultStore;
        bool HasMaps => Catalog != null && Catalog.Maps != null && Catalog.Maps.Length > 0;
        void Start()
        {
            MapSelection.ClearOptions();
            if (HasMaps) MapSelection.AddOptions(Catalog.Maps.Select(m => m.DisplayName).ToList());
            InitializeManagement();
            Status.text = EcsSceneFlow.TakeMenuMessage() ?? "建立新的王朝，或重返你的山河。";
            RefreshContinue();
            StartButton.onClick.AddListener(OpenNewDynasty);
            ContinueButton.onClick.AddListener(() => TryStart(() => EcsSceneFlow.ContinueGame(ArchiveStore)));
            QuitButton.onClick.AddListener(ConfirmMenuQuit);
        }
        internal void RefreshContinue()
        {
            bool available = false;
            try
            {
                if (ArchiveStore.HasContinue)
                {
                    var archive = ArchiveStore.ReadContinue(out _);
                    SnapshotCodec.ReadSummary(archive.Current);
                    available = true;
                }
            }
            catch (Exception error) when (error is System.IO.IOException || error is System.IO.InvalidDataException || error is ArgumentException || error is UnauthorizedAccessException)
            { /* Stale pointers and unreadable records do not offer quick continue. */ }
            ContinueButton.gameObject.SetActive(available);
            ContinueButton.interactable = available;
        }
        void QuitNow()
        {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
        }
        void TryStart(Action action)
        {
            if (EcsSceneFlow.Busy) return;
            try
            {
                action();
                if (EcsSceneFlow.Busy)
                {
                    menuGroup.interactable = false;
                    if (managementModal != null) managementModal.GetComponent<CanvasGroup>().interactable = false;
                }
            }
            catch (Exception error)
            {
                var message = "无法开始游戏：" + error.Message;
                Status.text = message;
                if (managementModal != null) InterfaceWidgets.Text(message,
                    InterfaceWidgets.Rect("Load error", managementModal.transform, new Vector2(.12f, 0), new Vector2(.88f, .08f)), Status.font, 16);
                RefreshContinue();
            }
        }
    }
}
