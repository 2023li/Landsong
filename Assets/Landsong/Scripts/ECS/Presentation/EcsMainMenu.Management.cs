using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsMainMenu
    {
        GameObject managementModal;
        CanvasGroup menuGroup;
        ArchiveBrowser browser;
        InterfaceSettingsPanel settings;
        GameObject returnSelection;
        int preferenceRevision = -1;
        Vector2 originalResolution;

        void InitializeManagement()
        {
            var canvas = GetComponentInParent<Canvas>();
            menuGroup = canvas.GetComponent<CanvasGroup>();
            if (menuGroup == null) menuGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            var panel = (RectTransform)StartButton.transform.parent;
            Place(panel, new Vector2(.28f, .06f), new Vector2(.72f, .94f));
            var title = panel.Find("Title").GetComponent<TextMeshProUGUI>();
            title.fontSize = 42;
            title.alignment = TextAlignmentOptions.Center;
            Place(title.rectTransform, new Vector2(.06f, .84f), new Vector2(.94f, .98f));
            Place(Status.rectTransform, new Vector2(.07f, .02f), new Vector2(.93f, .16f));
            Status.alignment = TextAlignmentOptions.Center;
            Status.fontSize = 16;
            var rows = InterfaceWidgets.Scroll(panel, "Main menu actions", new Vector2(.10f, .18f), new Vector2(.90f, .82f));
            var scroll = rows.GetComponentInParent<ScrollRect>();
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.GetComponent<Image>().color = Color.clear;
            ConfigureMenuButton(ContinueButton, rows, "继续游戏");
            ConfigureMenuButton(StartButton, rows, "开始新王朝");
            LoadButton = InterfaceWidgets.Button("加载游戏", rows, Status.font, OpenArchives, 56);
            SettingsButton = InterfaceWidgets.Button("设置", rows, Status.font, OpenSettings, 56);
            ConfigureMenuButton(QuitButton, rows, "退出到桌面");
            foreach (var button in rows.GetComponentsInChildren<Button>())
                button.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            InitializeNewDynasty(canvas.transform);
        }

        static void Place(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        void ConfigureMenuButton(Button button, Transform parent, string label)
        {
            button.transform.SetParent(parent, false);
            var layout = button.GetComponent<LayoutElement>();
            if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 56;
            button.targetGraphic.color = new Color(.17f, .23f, .31f, .98f);
            button.GetComponentInChildren<TextMeshProUGUI>().text = label;
        }

        void Modal(string name, out RectTransform card)
        {
            CloseManagement();
            returnSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            managementModal = InterfaceWidgets.Modal(name, GetComponentInParent<Canvas>().transform, 500, out card);
            menuGroup.interactable = false;
        }

        internal void CloseManagement()
        {
            if (MapSelection.IsExpanded) MapSelection.Hide();
            if (DifficultySelection != null && DifficultySelection.IsExpanded) DifficultySelection.Hide();
            if (managementModal != null)
            {
                managementModal.SetActive(false);
                if (managementModal != NewDynastyPanel) Destroy(managementModal);
            }
            managementModal = null;
            browser = null;
            settings = null;
            if (menuGroup != null) menuGroup.interactable = !EcsSceneFlow.Busy;
            if (returnSelection != null && returnSelection.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(returnSelection);
            returnSelection = null;
        }

        void OpenArchives()
        {
            if (EcsSceneFlow.Busy) return;
            Modal("Load game", out var card);
            browser = card.gameObject.AddComponent<ArchiveBrowser>();
            browser.Store = ArchiveStore;
            browser.Initialize(Status.font, () => { CloseManagement(); RefreshContinue(); });
            browser.Load = (run, slot, backupOnly) => TryStart(() => EcsSceneFlow.ContinueRun(run, slot, backupOnly, ArchiveStore));
            browser.Begin();
        }

        void OpenSettings()
        {
            if (EcsSceneFlow.Busy) return;
            Modal("Application settings", out var card);
            settings = card.gameObject.AddComponent<InterfaceSettingsPanel>();
            settings.Initialize(Status.font, CloseManagement);
            settings.Begin();
        }

        void ConfirmMenuQuit()
        {
            if (EcsSceneFlow.Busy) return;
            Modal("Exit confirmation", out var card);
            var rows = InterfaceWidgets.Scroll(card, "Confirmation", new Vector2(.05f, .1f), new Vector2(.95f, .9f));
            AddCaption(rows, "确定退出到桌面？", 70, 26);
            AddCaption(rows, "已有王朝和存档将保留。", 60);
            InterfaceWidgets.Button("确认退出", rows, Status.font, QuitNow);
            InterfaceWidgets.Button("取消", rows, Status.font, CloseManagement).Select();
        }

        void Update()
        {
            if (!EcsSceneFlow.Busy && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && managementModal != null)
            {
                if (MapSelection.IsExpanded) { MapSelection.Hide(); return; }
                if (DifficultySelection != null && DifficultySelection.IsExpanded) { DifficultySelection.Hide(); return; }
                if (settings != null && settings.CancelRebind()) return;
                if (browser != null) browser.Back(); else CloseManagement();
            }
            if (preferenceRevision != InterfaceSettings.Revision)
            {
                preferenceRevision = InterfaceSettings.Revision;
                var scaler = GetComponentInParent<Canvas>().GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    if (originalResolution == Vector2.zero) originalResolution = scaler.referenceResolution;
                    scaler.referenceResolution = originalResolution / InterfaceSettings.Current.UiScale;
                }
            }
        }
    }
}
