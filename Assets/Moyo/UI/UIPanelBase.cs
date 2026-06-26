using System.Threading.Tasks;
using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>
    /// 所有 UI 面板的基类。
    /// 
    /// 新规则：
    /// 1. PanelId 固定等于脚本类名。
    /// 2. Panel 不应该手动放在场景中直接使用。
    /// 3. 如果场景中存在未通过 UIManager 创建的 Panel，会自动请求 UIManager 重新打开，然后销毁自身。
    /// 4. 子类不要重写 Start 做初始化，应该使用 OnCreateAsync / OnOpenAsync / OnCloseAsync 等生命周期方法。
    /// </summary>
    public class UIPanelBase : MonoBehaviour
    {
        /// <summary>
        /// PanelId 固定等于当前 Panel 脚本类名。
        /// 例如 UIPanel_Setting。
        /// </summary>
        public string PanelId => GetType().Name;

        /// <summary>
        /// 当前 Panel 所属的 UIManager。
        /// 只有通过 UIManager 创建或缓存的 Panel 才会有 Manager。
        /// </summary>
        public UIManager Manager { get; private set; }

        /// <summary>
        /// 是否由 UIManager 管理。
        /// </summary>
        public bool IsManagedByUIManager => Manager != null;

        private bool autoReopenStarted;

        /// <summary>
        /// 是否允许未通过 UIManager 打开的场景实例自动销毁并重新通过 UIManager 打开。
        /// 默认开启。
        /// 
        /// 如果某个特殊 Panel 确实允许手动存在于场景中，可以在子类中 override 返回 false。
        /// 但一般不建议这么做。
        /// </summary>
        protected virtual bool AutoReopenWhenNotManaged => true;

        /// <summary>
        /// 由 UIManager 调用。
        /// 外部不要手动调用。
        /// </summary>
        internal void BindToManager(UIManager manager)
        {
            Manager = manager;
        }

        /// <summary>
        /// 场景中手动存在的 Panel 会在 Start 时自动重新通过 UIManager 打开。
        /// 注意:子类重写必须调用base.Start
        /// </summary>
        protected virtual async void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (IsManagedByUIManager)
            {
                return;
            }

            if (!AutoReopenWhenNotManaged)
            {
                return;
            }

            if (autoReopenStarted)
            {
                return;
            }

            autoReopenStarted = true;

            await ReopenThroughManagerThenDestroySelfAsync();
        }

        private async Task ReopenThroughManagerThenDestroySelfAsync()
        {
            var waitFrame = 0;

            while (UIManager.Instance == null && waitFrame < 120)
            {
                waitFrame++;
                await Task.Yield();

                if (this == null)
                {
                    return;
                }
            }

            if (this == null)
            {
                return;
            }

            var manager = UIManager.Instance;
            if (manager == null)
            {
                Debug.LogError($"场景中存在未通过 UIManager 打开的 Panel：{PanelId}，但场景中没有 UIManager。", this);
                return;
            }

            var opened = await manager.ReopenUnmanagedScenePanelAsync(this);

            if (this == null)
            {
                return;
            }

            if (!opened)
            {
                Debug.LogError($"场景中存在未通过 UIManager 打开的 Panel：{PanelId}，但 UIManager 无法重新打开它。请检查 UIConfig 和 Addressables。", this);
                return;
            }

            if (MoyoConfig.Instance.UI.LogUnmanagedPanelRepair)
            {
                Debug.LogWarning($"检测到未通过 UIManager 打开的 Panel：{PanelId}。已通过 UIManager 重新打开，并销毁场景中的旧实例。", this);
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 面板被创建后调用一次。
        /// 适合做组件引用缓存、事件注册等一次性初始化。
        /// </summary>
        public virtual Task OnCreateAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 面板被打开时调用。
        /// 适合刷新数据、播放打开动画、接收 args。
        /// </summary>
        public virtual Task OnOpenAsync(object args)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 面板获得焦点时调用。
        /// </summary>
        public virtual Task OnFocusAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 面板失去焦点时调用。
        /// </summary>
        public virtual Task OnBlurAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 面板关闭时调用。
        /// 注意：
        /// 不要在这里默认 SetActive(false)，由 UIManager 根据缓存策略统一控制。
        /// </summary>
        public virtual Task OnCloseAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 面板真正释放前调用。
        /// 适合注销事件、释放资源。
        /// </summary>
        public virtual Task OnReleaseAsync()
        {
            return Task.CompletedTask;
        }
    }
}
