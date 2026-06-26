using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Moyo.Unity
{
    /// <summary>
    /// 场景加载流程基类。
    /// 使用方式类似 Unity 生命周期函数：
    /// 子类只需要重写自己关心的阶段。
    /// </summary>
    public abstract class SceneLoadingPipeline
    {
        /// <summary>
        /// 目标场景名。
        /// 子类必须提供。
        /// </summary>
        public abstract string TargetSceneName { get; }

        /// <summary>
        /// 中转场景名。
        /// 返回空字符串表示不使用中转场景。
        /// </summary>
        public virtual string TransitionSceneName => "LoadingTransition";

        /// <summary>
        /// 是否需要等待玩家确认。
        /// 例如：加载完成后显示“按任意键继续”。
        /// </summary>
        public virtual bool NeedPlayerConfirm =>  true;

        public virtual float MinLoadTime => 2f;

        /// <summary>
        /// 1.准备切换场景。
        /// 例如：锁定输入、打开 Loading UI、开始淡出。
        /// </summary>
        public abstract IEnumerator OnPrepareTransition();
        

        /// <summary>
        /// 2.开始切换场景。
        /// 例如：通知系统当前进入 Loading 状态。
        /// </summary>
        public virtual IEnumerator OnStartTransition()
        {
            yield break;
        }

        /// <summary>
        /// 3.准备退出当前场景。
        /// 例如：保存数据、关闭当前 UI、停用角色控制。
        /// </summary>
        public virtual IEnumerator OnBeforeExitCurrentScene()
        {
            yield break;
        }

        /// <summary>
        /// 4.进入中转场景后调用。
        /// </summary>
        public virtual IEnumerator OnEnterTransitionScene()
        {
            yield break;
        }

        /// <summary>
        /// 5.卸载资源。
        /// 例如：释放 Addressables、清理缓存。
        /// </summary>
        public virtual IEnumerator OnUnloadResources()
        {
            yield break;
        }

        /// <summary>
        /// 6.预加载资源。
        /// 例如：预加载目标场景需要的 UI、音频、配置表。
        /// </summary>
        public virtual IEnumerator OnPreloadResources()
        {
            yield break;
        }

        /// <summary>
        /// 7.准备加载目标场景。
        /// </summary>
        public virtual IEnumerator OnBeforeLoadTargetScene()
        {
            yield break;
        }

        /// <summary>
        /// 8.目标场景加载完成。
        /// 例如：查找出生点、初始化玩家位置。
        /// </summary>
        public virtual IEnumerator OnTargetSceneLoaded()
        {
            yield break;
        }

        /// <summary>
        /// 9.等待目标场景初始化完成。
        /// 例如：等待场景管理器、任务系统、怪物生成器初始化。
        /// </summary>
        public virtual IEnumerator OnWaitTargetSceneInitialize()
        {
            yield break;
        }

        /// <summary>
        /// 10.开始等待玩家确认。
        /// 例如：显示“按任意键继续”、开启确认按钮、播放加载完成动画。
        /// </summary>
        public virtual IEnumerator OnBeginWaitPlayerConfirm()
        {
            yield break;
        }


        /// <summary>
        /// 11.玩家确认后调用。
        /// </summary>
        public abstract IEnumerator OnPlayerConfirmed();
       
        /// <summary>
        /// 12.场景切换完成。
        /// 例如：恢复输入、关闭 Loading UI、淡入画面。
        /// </summary>
        public virtual IEnumerator OnCompleted()
        {
            yield break;
        }
    }

    /// <summary>
    /// 场景切换管理器。
    /// 只负责执行 SceneLoadingPipeline，不写具体场景业务逻辑。
    /// </summary>
    public class SceneTransitionManager : MonoSingleton<SceneTransitionManager>
    {
        [Header("进度配置")]
        [SerializeField] private float prepareProgress = 0.05f;
        [SerializeField] private float startProgress = 0.10f;
        [SerializeField] private float beforeExitProgress = 0.15f;
        [SerializeField] private float transitionSceneProgress = 0.30f;
        [SerializeField] private float unloadResourcesProgress = 0.40f;
        [SerializeField] private float preloadResourcesProgress = 0.55f;
        [SerializeField] private float beforeLoadTargetProgress = 0.60f;
        [SerializeField] private float targetSceneLoadedProgress = 0.85f;
        [SerializeField] private float targetSceneInitializedProgress = 0.95f;
        [SerializeField] private float completedProgress = 1f;

        private float transitionStartTime;

        public bool IsTransitioning { get; private set; }
        public float Progress { get; private set; }
        public string CurrentStepName { get; private set; }

        public event Action<float> OnProgressChanged;
        public event Action<string> OnStepChanged;
        public event Action OnWaitPlayerConfirm;
        public event Action OnTransitionCompleted;

        private bool playerConfirmed;

        public bool StartTransition(SceneLoadingPipeline pipeline)
        {
            if (pipeline == null)
            {
                Debug.LogError("场景切换失败：SceneLoadingPipeline 为空。");
                return false;
            }

            if (IsTransitioning)
            {
                Debug.LogWarning("当前正在切换场景，新的切换请求被忽略。");
                return false;
            }

            StartCoroutine(TransitionRoutine(pipeline));
            return true;
        }

        public void Confirm()
        {
            playerConfirmed = true;
        }

        private IEnumerator TransitionRoutine(SceneLoadingPipeline pipeline)
        {
            IsTransitioning = true;
            playerConfirmed = false;
            transitionStartTime = Time.unscaledTime;

            SetProgress(0f);

            yield return RunStep("准备切换场景", prepareProgress, pipeline.OnPrepareTransition());
            yield return RunStep("开始切换场景", startProgress, pipeline.OnStartTransition());
            yield return RunStep("准备退出当前场景", beforeExitProgress, pipeline.OnBeforeExitCurrentScene());

            if (!string.IsNullOrEmpty(pipeline.TransitionSceneName))
            {
                SetStep("进入中转场景");

                yield return LoadSceneAsync(
                    pipeline.TransitionSceneName,
                    LoadSceneMode.Single,
                    Progress,
                    transitionSceneProgress
                );

                yield return RunStep("中转场景初始化", transitionSceneProgress, pipeline.OnEnterTransitionScene());
            }

            yield return RunStep("卸载资源", unloadResourcesProgress, UnloadResourcesRoutine(pipeline));
            yield return RunStep("预加载资源", preloadResourcesProgress, pipeline.OnPreloadResources());
            yield return RunStep("准备加载目标场景", beforeLoadTargetProgress, pipeline.OnBeforeLoadTargetScene());

            SetStep("加载目标场景");

            yield return LoadSceneAsync(
                pipeline.TargetSceneName,
                LoadSceneMode.Single,
                Progress,
                targetSceneLoadedProgress
            );

            yield return RunStep("目标场景加载完成", targetSceneLoadedProgress, pipeline.OnTargetSceneLoaded());
            yield return RunStep("等待目标场景初始化",targetSceneInitializedProgress,pipeline.OnWaitTargetSceneInitialize());

            yield return WaitMinLoadTime(pipeline);

            if (pipeline.NeedPlayerConfirm)
            {
                yield return RunStep("开始等待玩家确认",targetSceneInitializedProgress,pipeline.OnBeginWaitPlayerConfirm());

                SetStep("等待确认");
                OnWaitPlayerConfirm?.Invoke();

                while (!playerConfirmed)
                {
                    yield return null;
                }

                yield return RunStep(
                    "玩家确认",
                    targetSceneInitializedProgress,
                    pipeline.OnPlayerConfirmed()
                );
            }

            yield return RunStep(
                "加载完成",
                completedProgress,
                pipeline.OnCompleted()
            );

            IsTransitioning = false;
            OnTransitionCompleted?.Invoke();
        }

        private IEnumerator RunStep(string stepName, float targetProgress, IEnumerator routine)
        {
            SetStep(stepName);

            if (routine != null)
            {
                yield return routine;
            }

            SetProgress(targetProgress);
        }

        private IEnumerator LoadSceneAsync(
            string sceneName,
            LoadSceneMode loadSceneMode,
            float fromProgress,
            float toProgress)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, loadSceneMode);

            if (operation == null)
            {
                Debug.LogError($"场景加载失败：{sceneName}。请检查 Build Settings。");
                yield break;
            }

            while (!operation.isDone)
            {
                float normalizedProgress = Mathf.Clamp01(operation.progress / 0.9f);
                float progress = Mathf.Lerp(fromProgress, toProgress, normalizedProgress);

                SetProgress(progress);

                yield return null;
            }

            SetProgress(toProgress);
        }

        private IEnumerator UnloadResourcesRoutine(SceneLoadingPipeline pipeline)
        {
            yield return pipeline.OnUnloadResources();

            AsyncOperation operation = Resources.UnloadUnusedAssets();

            while (!operation.isDone)
            {
                yield return null;
            }

            GC.Collect();
        }

        private void SetStep(string stepName)
        {
            CurrentStepName = stepName;

            if (MoyoConfig.Instance.SceneTransition.LogLoadingSteps)
            {
                Debug.Log(stepName);
            }

            OnStepChanged?.Invoke(stepName);
        }

        private void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            OnProgressChanged?.Invoke(Progress);
        }

        private IEnumerator WaitMinLoadTime(SceneLoadingPipeline pipeline)
        {
            float minLoadTime = Mathf.Max(0f, pipeline.MinLoadTime);
            float elapsedTime = Time.unscaledTime - transitionStartTime;
            float remainingTime = minLoadTime - elapsedTime;

            if (remainingTime <= 0f)
            {
                yield break;
            }

            float endTime = Time.unscaledTime + remainingTime;

            while (Time.unscaledTime < endTime)
            {
                yield return null;
            }
        }
    }
}
