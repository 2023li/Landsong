using System;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_ConfirmPanel : UIPanelBase
    {
        [LabelText("标题"), Required]
        public TMP_Text Title;
        [LabelText("说明"), Required]
        public TMP_Text Message;
        [LabelText("确认按钮"), Required]
        public Button ConfirmButton;
        [LabelText("取消按钮"), Required]
        public Button CancelButton;
        ConfirmationOpenContext context;
        bool submitting;
        public override Task OnCreateAsync()
        {
            if (Title == null || Message == null || ConfirmButton == null || CancelButton == null)
                throw new InvalidOperationException("确认弹窗检查器引用不完整。");
            ConfirmButton.onClick.AddListener(Confirm);
            CancelButton.onClick.AddListener(Cancel);
            return base.OnCreateAsync();
        }

        public override Task OnOpenAsync(object args)
        {
            context = args as ConfirmationOpenContext ?? throw new InvalidOperationException("确认弹窗缺少操作上下文。");
            submitting = false;
            Title.text = context.Title;
            Message.text = context.Message;
            ConfirmButton.interactable = true;
            CancelButton.Select();
            return Task.CompletedTask;
        }

        async void Confirm()
        {
            if (submitting || context == null)
                return;
            submitting = true;
            try
            {
                if (context.StillValid != null && !context.StillValid())
                {
                    Message.text = "操作对象或状态已变化，请返回后重新确认。";
                    ConfirmButton.interactable = false;
                    return;
                }

                var action = context.Confirmed;
                await Manager.CloseAsync<UI_ConfirmPanel>();
                action?.Invoke();
            }
            catch (Exception error)
            {
                if (context != null)
                {
                    Message.text = "操作无法完成：" + error.Message;
                    ConfirmButton.interactable = false;
                }
                else
                    UnityEngine.Debug.LogException(error);
            }
            finally
            {
                submitting = false;
            }
        }

        async void Cancel()
        {
            try
            {
                await Manager.CloseAsync<UI_ConfirmPanel>();
            }
            catch (Exception error)
            {
                UnityEngine.Debug.LogException(error);
            }
        }

        public override Task OnCloseAsync()
        {
            context = null;
            return Task.CompletedTask;
        }
    }
}
