using System;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Landsong.ECS.Presentation
{
    [DefaultExecutionOrder(-9000)]
    public sealed class ApplicationUiRoot : MonoBehaviour, IApplicationUi
    {
        [LabelText("界面管理器"), Required]
        public UIManager Manager;
        [LabelText("应用流程"), Required]
        public GameApplicationFlow Flow;
        [LabelText("音频服务"), Required]
        public AudioRuntime Audio;
        [LabelText("本地化服务"), Required]
        public LocalizationRuntime Localization;
        static ApplicationUiRoot installed;
        int appliedPreferences = -1;
        Vector2 baseResolution;
        bool handlingBack;
        Task shutdown;
        bool allowQuit;
        public EventSystem InputEvents => Manager.EventSystem;

        public static ApplicationUiRoot Install(ApplicationUiRoot template)
        {
            if (template == null)
                throw new InvalidOperationException("未配置应用 UI 根。");
            if (installed != null)
                return installed;
            var root = Instantiate(template);
            if (installed != root)
                throw new InvalidOperationException("应用 UI 根没有完成初始化。");
            return root;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            installed = null;
        }

        void Awake()
        {
            if (installed != null && installed != this)
                throw new InvalidOperationException("只能配置一个应用 UI 根。");
            if (Manager == null || Flow == null || Audio == null || Localization == null || !Localization.transform.IsChildOf(transform) || Manager.gameObject != gameObject || Flow.gameObject != gameObject || !Audio.transform.IsChildOf(transform))
                throw new InvalidOperationException("UI 根必须显式绑定同对象管理器和流程。");
            installed = this;
            Manager.Initialize();
            Flow.Configure(this);
            baseResolution = Manager.Scaler.referenceResolution;
            Application.wantsToQuit += WantsToQuit;
        }

        void Update()
        {
            if (appliedPreferences != InterfaceSettings.Revision)
            {
                appliedPreferences = InterfaceSettings.Revision;
                Manager.Scaler.referenceResolution = baseResolution / InterfaceSettings.Current.UiScale;
            }

            var top = Manager.TopFocusedPanel;
            UiInputState.GlobalModalOpen = top is UI_SettingPanel || top is UI_SavePanel || top is UI_ConfirmPanel
                || top is UI_LoadingPanel || top is UI_DebugPanel;
            if (!handlingBack && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                HandleBack();
        }

        async void HandleBack()
        {
            handlingBack = true;
            UiInputState.BackHandledFrame = Time.frameCount;
            try
            {
                await Manager.BackAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                handlingBack = false;
            }
        }

        public void OpenSettings() => Observe(Manager.OpenAsync<UI_SettingPanel>());
        public void OpenArchives(ArchiveOpenRequest request)
        {
            request.Navigation = this;
            Observe(Manager.OpenAsync<UI_SavePanel>(request));
        }

        public void Confirm(string title, string message, Action confirmed, Func<bool> stillValid = null) => Observe(Manager.OpenAsync<UI_ConfirmPanel>(new ConfirmationOpenContext { Title = title, Message = message, Confirmed = confirmed, StillValid = stillValid }));
        async void Observe(Task operation)
        {
            try
            {
                await operation;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        public async Task CloseSharedAsync()
        {
            await Manager.CloseAsync<UI_ConfirmPanel>();
            await Manager.CloseAsync<UI_SettingPanel>();
            await Manager.CloseAsync<UI_SavePanel>();
        }

        public void Quit() => Quit(0);
        public void Quit(int exitCode)
        {
            shutdown ??= QuitAsync(exitCode);
        }

        bool WantsToQuit()
        {
            if (allowQuit)
                return true;
            Quit();
            return false;
        }

        async Task QuitAsync(int exitCode)
        {
            // wantsToQuit must return before requesting the approved quit. Cleanup can
            // complete synchronously, and Unity ignores a nested Quit in that callback.
            await Task.Yield();
            // Stopping Editor Play can destroy the root during the yield. Unity already
            // owns teardown then; do not resume navigation on its released UIManager.
            if (this == null || !Application.isPlaying)
                return;
            try
            {
                await Flow.ShutdownAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }

            try
            {
                await Manager.ShutdownAsync();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }

            allowQuit = true;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(exitCode);
#endif
        }

        void OnDestroy()
        {
            Application.wantsToQuit -= WantsToQuit;
            if (installed == this)
                installed = null;
            UiInputState.GlobalModalOpen = false;
        }
    }
}
