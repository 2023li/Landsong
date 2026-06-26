using System;
using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>
    /// Moyo 各运行时模块共享的项目配置。
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "MoyoUnity/MoyoConfig")]
    public sealed class MoyoConfig : ScriptableObject
    {
        public const string ResourceName = "MoyoConfig";

        private static MoyoConfig instance;

        [InspectorName("场景切换")]
        [SerializeField]
        private SceneTransitionOptions sceneTransition = new SceneTransitionOptions();

        [InspectorName("UI 系统")]
        [SerializeField]
        private UIOptions ui = new UIOptions();

        public static MoyoConfig Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                instance = Resources.Load<MoyoConfig>(ResourceName);

                if (instance == null)
                {
                    instance = CreateInstance<MoyoConfig>();
                    Debug.LogWarning(
                        $"未找到 Resources/{ResourceName}.asset，将使用 Moyo 默认配置。");
                }

                return instance;
            }
        }

        public SceneTransitionOptions SceneTransition
        {
            get
            {
                sceneTransition ??= new SceneTransitionOptions();
                return sceneTransition;
            }
        }

        public UIOptions UI
        {
            get
            {
                ui ??= new UIOptions();
                return ui;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCachedInstance()
        {
            instance = null;
        }
    }

    [Serializable]
    public sealed class SceneTransitionOptions
    {
        [Tooltip("是否在 Console 中打印 SceneTransitionManager 当前执行的加载步骤。")]
        [InspectorName("打印加载流程")]
        [SerializeField]
        private bool logLoadingSteps = true;

        public bool LogLoadingSteps => logLoadingSteps;
    }

    [Serializable]
    public sealed class UIOptions
    {
        [Tooltip("是否在 UIManager 成功加载 UIConfig 后打印日志。")]
        [InspectorName("打印 UIConfig 加载成功日志")]
        [SerializeField]
        private bool logConfigLoaded = true;

        [Tooltip("是否打印场景内未受 UIManager 管理的 Panel 被自动替换的日志。")]
        [InspectorName("打印未受管理 Panel 修复日志")]
        [SerializeField]
        private bool logUnmanagedPanelRepair = true;

        public bool LogConfigLoaded => logConfigLoaded;
        public bool LogUnmanagedPanelRepair => logUnmanagedPanelRepair;
    }
}
