using System;
using Landsong.ECS.Persistence;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_StartPanel : UIPanelBase
    {
        [LabelText("开始游戏弹窗"), Required]
        public UI_StartPanel_GameStartPop NewDynasty;
        [LabelText("开始游戏按钮"), Required]
        public Button StartButton;
        [LabelText("继续游戏按钮"), Required]
        public Button ContinueButton;
        [LabelText("退出按钮"), Required]
        public Button QuitButton;
        [LabelText("加载游戏按钮"), Required]
        public Button LoadButton;
        [LabelText("设置按钮"), Required]
        public Button SettingsButton;
        [LabelText("主菜单说明"), Required]
        public TMP_Text Status;
        [LabelText("加载错误说明"), Required]
        public TMP_Text LoadError;
        IApplicationUi navigation;
        public RunArchiveStore Store { get; set; }

        RunArchiveStore ArchiveStore => Store ?? CheckpointSystem.DefaultStore;
        public EcsMapMenuCatalog Catalog => NewDynasty.Catalog;
        public TMP_Dropdown MapSelection => NewDynasty.MapSelection;
        public TMP_Dropdown DifficultySelection => NewDynasty.DifficultySelection;
        public TMP_InputField DynastyName => NewDynasty.DynastyName;
        public TMP_Text MapInfo => NewDynasty.MapInfo;
        public Image MapPreview => NewDynasty.MapPreview;
        public Button CreateDynastyButton => NewDynasty.CreateDynastyButton;
        public Button NewDynastyBackButton => NewDynasty.BackButton;
        public GameObject NewDynastyPanel => NewDynasty.gameObject;

        public override Task OnCreateAsync()
        {
            ValidateConfiguration();
            StartButton.onClick.AddListener(OpenNewDynasty);
            ContinueButton.onClick.AddListener(() => TryStart(() => EcsSceneFlow.ContinueGame(ArchiveStore)));
            QuitButton.onClick.AddListener(ConfirmMenuQuit);
            LoadButton.onClick.AddListener(OpenArchives);
            SettingsButton.onClick.AddListener(OpenSettings);
            NewDynasty.Bind(TryStart, CloseManagement);
            return base.OnCreateAsync();
        }

        public override Task OnOpenAsync(object args)
        {
            var context = args as MenuOpenContext ?? throw new InvalidOperationException("主菜单缺少应用上下文。");
            navigation = context.Navigation ?? throw new InvalidOperationException("主菜单未注入导航服务。");
            Store = context.Store;
            CloseManagement();
            Status.text = EcsSceneFlow.TakeMenuMessage() ?? "建立新的王朝，或重返你的山河。";
            LoadError.gameObject.SetActive(false);
            RefreshContinue();
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync()
        {
            NewDynasty.Close();
            navigation = null;
            Store = null;
            return Task.CompletedTask;
        }

        public override Task<bool> TryHandleBackAsync()
        {
            if (!NewDynasty.gameObject.activeSelf)
                return Task.FromResult(false);
            NewDynasty.Back();
            return Task.FromResult(true);
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
            catch (Exception error)when (error is System.IO.IOException || error is System.IO.InvalidDataException || error is ArgumentException || error is UnauthorizedAccessException)
            {
            }

            ContinueButton.gameObject.SetActive(available);
            ContinueButton.interactable = available;
        }

        internal void TryStart(Action action)
        {
            if (EcsSceneFlow.Busy)
                return;
            try
            {
                action();
                if (EcsSceneFlow.Busy)
                    SetHomeInteractable(false);
            }
            catch (Exception error)
            {
                Status.text = "无法开始游戏：" + error.Message;
                LoadError.text = Status.text;
                LoadError.gameObject.SetActive(true);
                RefreshContinue();
            }
        }

        void OpenNewDynasty()
        {
            if (EcsSceneFlow.Busy)
                return;
            SetHomeInteractable(false);
            NewDynasty.Open();
        }

        protected override void ValidateLocalConfiguration()
        {
            base.ValidateLocalConfiguration();
            foreach (var value in new UnityEngine.Object[]
            {
                NewDynasty,
                StartButton,
                ContinueButton,
                QuitButton,
                LoadButton,
                SettingsButton,
                Status,
                LoadError
            }

            )
                if (value == null)
                    throw new InvalidOperationException("主菜单检查器引用不完整。");
            NewDynasty.ValidateConfiguration();
        }

        internal void CloseManagement()
        {
            NewDynasty.Close();
            SetHomeInteractable(!EcsSceneFlow.Busy);
        }

        public void RefreshAvailability()
        {
            RefreshContinue();
            SetHomeInteractable(!EcsSceneFlow.Busy && !NewDynasty.gameObject.activeSelf);
        }

        void SetHomeInteractable(bool value)
        {
            StartButton.interactable = LoadButton.interactable = SettingsButton.interactable = QuitButton.interactable = value;
            ContinueButton.interactable = value && ContinueButton.gameObject.activeSelf;
        }

        void OpenArchives()
        {
            if (EcsSceneFlow.Busy)
                return;
            navigation.OpenArchives(new ArchiveOpenRequest { Mode = ArchiveOpenMode.Load, Store = ArchiveStore, Load = (run, slot, backup) => TryStart(() => EcsSceneFlow.ContinueRun(run, slot, backup, ArchiveStore)) });
        }

        void OpenSettings()
        {
            if (!EcsSceneFlow.Busy)
                navigation.OpenSettings();
        }

        void ConfirmMenuQuit()
        {
            if (!EcsSceneFlow.Busy)
                navigation.Confirm("退出游戏", "退出到桌面？", navigation.Quit);
        }
    }
}
