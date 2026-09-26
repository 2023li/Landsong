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
        [SerializeField, LabelText("天气调试组件"), Required] private UI_DebugPanel_Weather weatherPanel;


        [SerializeField, LabelText("打开一般调试"), Required]
        private Button btn_打开一般调试;
        [SerializeField, LabelText("一般调试组件"), Required]
        private UI_DebugPanel_Common commonPanel;


        bool closing;
        float nextAvailabilityRefresh;

        public override Task OnCreateAsync()
        {
            ValidateConfiguration();
            btn_关闭调试面板.onClick.AddListener(Close);
            btn_打开天气调试.onClick.AddListener(OpenWeather);
            btn_打开一般调试.onClick.AddListener(OpenCommon);
            return Task.CompletedTask;
        }

        public override Task OnOpenAsync(object args)
        {
            closing = false;
            RefreshWeatherAvailability();
            if (btn_打开天气调试.interactable)
                OpenWeather();
            else
                OpenCommon();
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
            weatherPanel.gameObject.SetActive(true);
            weatherPanel.RefreshStatus();
        }

        public void OpenCommon()
        {
            for (var i = 0; i < rt_内容面板root.childCount; i++)
                rt_内容面板root.GetChild(i).gameObject.SetActive(false);
            commonPanel.gameObject.SetActive(true);
            commonPanel.RefreshStatus();
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
            if (!available && weatherPanel.gameObject.activeSelf)
                OpenCommon();
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
