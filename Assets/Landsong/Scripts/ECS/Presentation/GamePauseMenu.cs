using System;
using System.IO;
using Landsong.ECS.Persistence;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    // Scene-authored UGUI. Only commands mutate simulation; this component owns modal presentation.
    public sealed partial class GamePauseMenu : MonoBehaviour
    {
        public EcsGameView View;
        public GameObject Overlay, MainPage, SettingsPage, SavesPage, ConfirmPage;
        public Button OpenButton, SaveButton, QuickSaveButton, SettingsButton, ResumeButton, MenuButton, QuitButton;
        public Button CreateSlotButton, SavesBackButton, SettingsBackButton, ConfirmButton, CancelButton;
        public Text Status, Confirmation;
        public Slider Volume;
        public Toggle Fullscreen;
        public RectTransform SlotRows;
        public Button SlotTemplate;
        bool previouslyPaused, closing;
        CanvasGroup backgroundGroup;
        bool previousInteraction;
        Action confirmed;
        GameObject confirmationOrigin;
        float nextSlots;
        InterfaceSettingsPanel fullSettings;
        public bool IsOpen => Overlay != null && Overlay.activeSelf;
        void Awake()
        {
            var canvasRoot = View.GetComponentInParent<Canvas>().gameObject;
            backgroundGroup = canvasRoot.GetComponent<CanvasGroup>(); if (backgroundGroup == null) backgroundGroup = canvasRoot.AddComponent<CanvasGroup>();
            var modalGroup = Overlay.GetComponent<CanvasGroup>(); if (modalGroup == null) modalGroup = Overlay.AddComponent<CanvasGroup>(); modalGroup.ignoreParentGroups = true;
            Overlay.SetActive(false); OpenButton.onClick.AddListener(Open);
            SaveButton.onClick.AddListener(() => { Page(SavesPage); RefreshSlots(); });
            QuickSaveButton.onClick.AddListener(() => View.Send(CommandKind.Save));
            CreateSlotButton.onClick.AddListener(() => View.Send(CommandKind.Save, argument: 1,text:slotName!=null?slotName.text:null));
            ResumeButton.onClick.AddListener(Close); SettingsButton.onClick.AddListener(() => { Page(SettingsPage); fullSettings.Begin(); });
            SavesBackButton.onClick.AddListener(() => Page(MainPage)); SettingsBackButton.onClick.AddListener(() => Page(MainPage));
            MenuButton.onClick.AddListener(() => Confirm("返回主菜单？未保存的白天操作会丢失；夜晚只保留开始节点，不保存实时战斗。", EcsSceneFlow.ReturnToMenu));
            QuitButton.onClick.AddListener(() => Confirm("退出游戏？未保存的进度会丢失。不会删除王朝存档。", Quit));
            ConfirmButton.onClick.AddListener(() => { var action = confirmed; confirmed = null; action?.Invoke(); });
            CancelButton.onClick.AddListener(() => { confirmed = null; Page(confirmationOrigin ?? MainPage); });
            Volume.onValueChanged.AddListener(value => { var data=InterfaceSettings.Current.Copy();data.Master=value;InterfaceSettings.Apply(data); });
            Fullscreen.onValueChanged.AddListener(value => { var data=InterfaceSettings.Current.Copy();data.Fullscreen=value?1:0;InterfaceSettings.Apply(data); });
            // Keep authored references for compatibility with artists and existing tests; the full page uses the shared settings editor.
            foreach(Transform child in SettingsPage.transform)child.gameObject.SetActive(false);
            var settingsLayout=SettingsPage.GetComponent<VerticalLayoutGroup>();if(settingsLayout!=null)settingsLayout.enabled=false;
            fullSettings=SettingsPage.AddComponent<InterfaceSettingsPanel>();fullSettings.Initialize(Status.font,()=>Page(MainPage));
            InitializeSlotManager();
        }
        bool State(out EntityManager em, out Entity root, out Session session)
        {
            em = default; root = Entity.Null; session = default; var world = World.DefaultGameObjectInjectionWorld;
            if (!EcsSceneFlow.GameReady || world == null || !world.IsCreated) return false;
            em = world.EntityManager; root = Sim.Root(em); if (root == Entity.Null) return false; session = em.GetComponentData<Session>(root); return true;
        }
        public void Open()
        {
            if (IsOpen || !State(out _, out _, out var s)) return;
            previouslyPaused = s.Paused != 0; closing = false; Overlay.SetActive(true); Overlay.transform.SetAsLastSibling();
            previousInteraction = backgroundGroup.interactable; backgroundGroup.interactable = false;
            var canvas = Overlay.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 500; Page(MainPage);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            if (s.Paused == 0) View.Send(CommandKind.Pause, argument: 1);
        }
        public void Escape()
        {
            if(IsOpen&&SettingsPage.activeSelf&&fullSettings.CancelRebind())return;
            if (!IsOpen) { Open(); return; }
            if (!MainPage.activeSelf) { confirmed = null; Page(ConfirmPage.activeSelf ? confirmationOrigin ?? MainPage : MainPage); return; }
            Close();
        }
        public void Close() { if (IsOpen) closing = true; }
        void Update()
        {
            if (!IsOpen || !State(out _, out _, out var s)) return;
            if (closing)
            {
                if (s.CheckpointPending != 0) return;
                if (s.Phase != Phase.Ended && s.Paused != (previouslyPaused ? 1 : 0)) View.Send(CommandKind.Pause, argument: previouslyPaused ? 1 : 2);
                Overlay.SetActive(false); backgroundGroup.interactable = previousInteraction; closing = false; return;
            }
            if (s.Paused == 0 && s.Phase != Phase.Ended && s.CheckpointPending == 0) View.Send(CommandKind.Pause, argument: 1);
            bool canSave = s.Phase == Phase.Day && s.CheckpointPending == 0;
            QuickSaveButton.interactable = CreateSlotButton.interactable = canSave;
            Status.text = (s.Phase == Phase.Day ? "保存创建独立槽；快速保存覆盖当前槽（首次会新建）。" : "夜晚及过渡阶段不能保存；白天/黄昏回退节点独立保留。") + "\n" + View.Message.text;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (SavesPage.activeSelf && Time.unscaledTime >= nextSlots && (mouse == null || !mouse.leftButton.isPressed && !mouse.leftButton.wasReleasedThisFrame)) RefreshSlots();
        }
        void Page(GameObject page)
        { foreach (var p in new[] { MainPage, SettingsPage, SavesPage, ConfirmPage }) p.SetActive(p == page); nextSlots = 0; if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null); }
        void Confirm(string message, Action action) { confirmationOrigin=SavesPage.activeSelf?SavesPage:MainPage;confirmed = action; Confirmation.text = message; Page(ConfirmPage); }
        void RefreshSlots()
        {
            nextSlots = Time.unscaledTime + 1;
            if (!State(out var em, out var root, out var s) || !em.HasComponent<RunPersistence>(root)) return;
            try
            {
                var system = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>();
                var store = system.Store ?? CheckpointSystem.DefaultStore; string run = em.GetComponentData<RunPersistence>(root).RunId.ToString();
                RefreshSlotManager(store,run,s,system);
            }
            catch (Exception error) { View.Message.text = "无法读取存档槽：" + error.Message; }
        }
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
