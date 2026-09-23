using System;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_DebugPanel : UIPanelBase
    {
        [SerializeField, LabelText("关闭调试面板"), Required] private Button btn_关闭调试面板;
        [SerializeField, LabelText("内容面板根节点"), Required] private RectTransform rt_内容面板root;
        [SerializeField, LabelText("打开天气调试"), Required] private Button btn_打开天气调试;
        [SerializeField, LabelText("天气调试面板"), Required] private GameObject go_天气调试面板;
        [SerializeField, LabelText("天气调试组件"), Required] private UI_DebugPanel_Weather weatherPanel;

        bool closing;
        float nextAvailabilityRefresh;

        protected override void ValidateLocalConfiguration()
        {
            base.ValidateLocalConfiguration();
            if (btn_关闭调试面板 == null || rt_内容面板root == null || btn_打开天气调试 == null
                || go_天气调试面板 == null || !rt_内容面板root.IsChildOf(transform)
                || !go_天气调试面板.transform.IsChildOf(rt_内容面板root)
                || weatherPanel == null || weatherPanel.gameObject != go_天气调试面板)
                throw new InvalidOperationException("调试面板检查器引用不完整。");
        }

        public override Task OnCreateAsync()
        {
            ValidateConfiguration();
            btn_关闭调试面板.onClick.AddListener(Close);
            btn_打开天气调试.onClick.AddListener(OpenWeather);
            return Task.CompletedTask;
        }

        public override Task OnOpenAsync(object args)
        {
            closing = false;
            RefreshWeatherAvailability();
            if (btn_打开天气调试.interactable)
                OpenWeather();
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync()
        {
            closing = false;
            return Task.CompletedTask;
        }

        public void OpenWeather()
        {
            RefreshWeatherAvailability();
            if (!btn_打开天气调试.interactable)
                return;
            for (var i = 0; i < rt_内容面板root.childCount; i++)
                rt_内容面板root.GetChild(i).gameObject.SetActive(false);
            go_天气调试面板.SetActive(true);
            weatherPanel.RefreshStatus();
        }

        void Update()
        {
            if (Time.unscaledTime < nextAvailabilityRefresh) return;
            nextAvailabilityRefresh = Time.unscaledTime + .25f;
            RefreshWeatherAvailability();
        }

        void RefreshWeatherAvailability()
        {
            var available = UI_DebugPanel_Weather.HasGameWorld;
            btn_打开天气调试.interactable = available;
            if (!available)
                for (var i = 0; i < rt_内容面板root.childCount; i++)
                    rt_内容面板root.GetChild(i).gameObject.SetActive(false);
        }

        async void Close()
        {
            if (closing || Manager == null)
                return;
            closing = true;
            try
            {
                await Manager.CloseAsync<UI_DebugPanel>();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                closing = false;
            }
        }
    }
}
