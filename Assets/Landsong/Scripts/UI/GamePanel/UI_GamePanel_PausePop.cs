using System;
using Landsong.ECS.Persistence;
using Sirenix.OdinInspector;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_PausePop : MonoBehaviour
    {
        [LabelText("游戏界面"), Required]
        public UI_GamePanel View;
        [LabelText("模态遮罩"), Required]
        public GameObject Overlay;
        [LabelText("暂停主页"), Required]
        public GameObject MainPage;
        [LabelText("弹窗交互组"), Required]
        public CanvasGroup ModalGroup;
        [LabelText("游戏交互组"), Required]
        public CanvasGroup BackgroundGroup;
        [LabelText("遮罩图片"), Required]
        public Image OverlayImage;
        [LabelText("暂停入口"), Required]
        public Button OpenButton;
        [LabelText("保存入口"), Required]
        public Button SaveButton;
        [LabelText("快速保存"), Required]
        public Button QuickSaveButton;
        [LabelText("设置入口"), Required]
        public Button SettingsButton;
        [LabelText("继续游戏"), Required]
        public Button ResumeButton;
        [LabelText("返回主菜单"), Required]
        public Button MenuButton;
        [LabelText("退出游戏"), Required]
        public Button QuitButton;
        [LabelText("暂停状态说明"), Required]
        public TMP_Text Status;
        IApplicationUi navigation;
        EntityManager manager;
        Entity root;
        World world;
        bool previouslyPaused, closing, visible, previousInteraction;
        public bool IsOpen => visible;

        public void Bind(IApplicationUi navigation, EntityManager manager, Entity root)
        {
            this.navigation = navigation;
            this.manager = manager;
            this.root = root;
            world = manager.World;
        }

        public void Unbind()
        {
            closing = false;
            SetVisible(false);
            navigation = null;
            world = null;
            root = Entity.Null;
        }

        void Awake()
        {
            ValidateConfiguration();
            SetVisible(false);
            OpenButton.onClick.AddListener(Open);
            SaveButton.onClick.AddListener(OpenSaves);
            QuickSaveButton.onClick.AddListener(() => Queue(CommandRequests.QuickSave()));
            ResumeButton.onClick.AddListener(Close);
            SettingsButton.onClick.AddListener(() => navigation.OpenSettings());
            MenuButton.onClick.AddListener(() => navigation.Confirm("返回主菜单", "未保存的白天操作会丢失；夜晚只保留开始节点。返回主菜单？", EcsSceneFlow.ReturnToMenu, () => Valid));
            QuitButton.onClick.AddListener(() => navigation.Confirm("退出游戏", "未保存的进度会丢失。不会删除王朝存档。退出游戏？", navigation.Quit, () => Valid));
        }

        public void ValidateConfiguration()
        {
            foreach (var value in new UnityEngine.Object[]
            {
                View,
                Overlay,
                MainPage,
                ModalGroup,
                BackgroundGroup,
                OverlayImage,
                OpenButton,
                SaveButton,
                QuickSaveButton,
                SettingsButton,
                ResumeButton,
                MenuButton,
                QuitButton,
                Status
            }

            )
                if (value == null)
                    throw new InvalidOperationException("暂停弹窗检查器引用不完整。");
        }

        bool Valid => EcsSceneFlow.GameReady && world != null && world.IsCreated && manager.Exists(root);

        bool CanWrite => Valid && manager.GetComponentData<Session>(root).Phase == Phase.Day && manager.GetComponentData<Session>(root).CheckpointPending == 0;

        public void Open()
        {
            if (IsOpen || !Valid || !View.InputPolicy.Capture().CanTogglePause)
                return;
            var state = manager.GetComponentData<Session>(root);
            previouslyPaused = state.Paused != 0;
            closing = false;
            previousInteraction = BackgroundGroup.interactable;
            BackgroundGroup.interactable = false;
            SetVisible(true);
            transform.SetAsLastSibling();
            MainPage.SetActive(true);
            navigation.InputEvents.SetSelectedGameObject(null);
            if (state.Paused == 0)
                Queue(CommandRequests.Pause(true));
        }

        public void Escape()
        {
            if (!View.InputPolicy.Capture().CanTogglePause)
                return;
            if (IsOpen)
                Close();
            else
                Open();
        }

        public void Close()
        {
            if (IsOpen)
                closing = true;
        }

        void Update()
        {
            if (!IsOpen || !Valid)
                return;
            var state = manager.GetComponentData<Session>(root);
            if (closing)
            {
                if (state.CheckpointPending != 0)
                    return;
                if (state.Phase != Phase.Ended && state.Paused != (previouslyPaused ? 1 : 0))
                    Queue(CommandRequests.Pause(previouslyPaused));
                SetVisible(false);
                BackgroundGroup.interactable = previousInteraction;
                closing = false;
                return;
            }

            if (state.Paused == 0 && state.Phase != Phase.Ended && state.CheckpointPending == 0)
                Queue(CommandRequests.Pause(true));
            QuickSaveButton.interactable = CanWrite;
            Status.text = (state.Phase == Phase.Day ? "保存创建独立槽；快速保存覆盖当前槽（首次会新建）。" : "夜晚及过渡阶段不能保存；白天/黄昏回退节点独立保留。") + "\n" + View.Hud.Message.text;
        }

        void OpenSaves()
        {
            if (!Valid || !manager.HasComponent<RunPersistence>(root))
                return;
            var checkpoint = world.GetExistingSystemManaged<CheckpointSystem>();
            var store = checkpoint.Store ?? CheckpointSystem.DefaultStore;
            string run = manager.GetComponentData<RunPersistence>(root).RunId.ToString();
            navigation.OpenArchives(new ArchiveOpenRequest { Mode = ArchiveOpenMode.Save, Store = store, CurrentRun = run, IsSessionValid = () => Valid, CanWrite = () => CanWrite, Save = (slot, stamp, name) => Queue(slot == null ? CommandRequests.CreateSave(name) : CommandRequests.OverwriteSave(slot, stamp)), Load = (targetRun, slot, backup) =>
            {
                if (!CanWrite)
                    throw new InvalidOperationException("当前阶段不能载入。");
                if (targetRun == run && slot != null)
                {
                    Queue(CommandRequests.LoadSave(slot, backup));
                }
                else
                    EcsSceneFlow.ContinueRun(targetRun, slot, backup, store);
            }, ManageSlot = (slot, name, stamp, delete) => checkpoint.ManageSlot(root, slot, name, stamp, delete) });
        }

        void Queue(Command command)
        {
            if (!View.Commands.TryQueue(command))
                throw new InvalidOperationException("当前界面或会话不能提交此操作。");
        }

        void SetVisible(bool value)
        {
            visible = value;
            ModalGroup.alpha = value ? 1 : 0;
            ModalGroup.interactable = ModalGroup.blocksRaycasts = value;
            OverlayImage.enabled = value;
        }

        void OnDisable()
        {
            if (BackgroundGroup != null && visible)
                BackgroundGroup.interactable = previousInteraction;
            closing = false;
            if (ModalGroup != null && OverlayImage != null)
                SetVisible(false);
        }
    }
}
