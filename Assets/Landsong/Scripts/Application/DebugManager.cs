using System;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong.ECS.Presentation
{
    /// <summary>跨场景常驻的调试快捷键入口，不依赖游戏会话。</summary>
    public sealed class DebugManager : MonoBehaviour
    {
        static DebugManager installed;
        bool toggling;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => installed = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (installed != null)
                return;
            var owner = new GameObject(nameof(DebugManager));
            owner.AddComponent<DebugManager>();
            DontDestroyOnLoad(owner);
        }

        void Awake()
        {
            if (installed != null && installed != this)
            {
                Destroy(gameObject);
                return;
            }
            installed = this;
        }

        void Update()
        {
            if (toggling || Keyboard.current?.f8Key.wasPressedThisFrame != true
                || !UIManager.TryGetInstance(out var ui) || ui.IsShuttingDown)
                return;
            Toggle(ui);
        }

        async void Toggle(UIManager ui)
        {
            toggling = true;
            try
            {
                if (ui.IsOpened<UI_DebugPanel>())
                    await ui.CloseAsync<UI_DebugPanel>();
                else
                    await ui.OpenAsync<UI_DebugPanel>();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
            finally
            {
                toggling = false;
            }
        }

        void OnDestroy()
        {
            if (installed == this)
                installed = null;
        }
    }
}
