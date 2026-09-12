using System;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_LoadingPanel : UIPanelBase
    {
        [LabelText("加载状态"), Required]
        public TMP_Text Status;
        [LabelText("加载进度"), Required]
        public Slider Progress;
        [LabelText("取消按钮"), Required]
        public Button CancelButton;
        [LabelText("取消按钮文字"), Required]
        public TMP_Text CancelLabel;
        [LabelText("加载超时秒数"), Min(10)]
        public float TimeoutSeconds = 120;
        Action cancel, retryCleanup;
        public bool Failed { get; private set; }

        public override Task OnCreateAsync()
        {
            if (Status == null || Progress == null || CancelButton == null || CancelLabel == null)
                throw new InvalidOperationException("加载面板检查器引用不完整。");
            CancelButton.onClick.AddListener(Cancel);
            return base.OnCreateAsync();
        }

        public override Task OnOpenAsync(object args)
        {
            cancel = args as Action ?? throw new InvalidOperationException("加载面板缺少取消操作。");
            Failed = false; retryCleanup = null;
            CancelButton.interactable = true;
            CancelLabel.text = "取消加载";
            Stage("正在准备…", 0);
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync()
        {
            cancel = retryCleanup = null;
            return Task.CompletedTask;
        }

        public override Task<bool> TryHandleBackAsync()
        {
            // A failed cleanup requires the explicitly labelled retry action.
            if (retryCleanup == null) Cancel();
            return Task.FromResult(true);
        }

        public void Cancel()
        {
            if (!CancelButton.interactable) return;
            if (retryCleanup != null)
            {
                var retry = retryCleanup;
                retryCleanup = null;
                CancelButton.interactable = false;
                CancelLabel.text = "正在清理";
                retry();
            }
            else cancel?.Invoke();
        }

        public void Stage(string message, float value)
        {
            Status.text = message;
            Progress.value = value;
        }

        public void ShowError(string message, bool canReturn)
        {
            retryCleanup = null;
            Failed = true;
            Status.text = message;
            CancelLabel.text = canReturn ? "返回主菜单" : "正在清理";
            CancelButton.interactable = canReturn;
        }
        public void ShowCleanupError(string message, Action retry)
        {
            retryCleanup = retry ?? throw new ArgumentNullException(nameof(retry));
            Failed = true; Status.text = message;
            CancelLabel.text = "重试清理并返回";
            CancelButton.interactable = true;
        }
        public void ShowCleanupPending(string message)
        {
            retryCleanup = null;
            Failed = true; Status.text = message;
            CancelLabel.text = "正在清理";
            CancelButton.interactable = false;
        }
    }
}
