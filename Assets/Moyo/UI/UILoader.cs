using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Moyo.Unity
{
    internal sealed class UIPanelLease : IDisposable
    {
        private AsyncOperationHandle<UIPanelAsset> handle;
        private bool ownsHandle;
        internal UIPanelAsset Asset { get; private set; }
        internal UIPanelLease(UIPanelAsset asset) { Asset = asset; }
        internal UIPanelLease(AsyncOperationHandle<UIPanelAsset> handle)
        { this.handle = handle; ownsHandle = true; Asset = handle.Result; UILoader.RecordAcquired(); }
        public void Dispose()
        {
            Asset = null;
            if (!ownsHandle) return;
            ownsHandle = false;
            try { if (handle.IsValid()) Addressables.Release(handle); }
            finally { UILoader.RecordReleased(); }
        }
    }

    /// <summary>管理器拥有加载租约。直接配置与 Addressables 使用相同的根组件契约。</summary>
    public static class UILoader
    {
        public static int LiveAddressableLeaseCount { get; private set; }
        internal static void RecordAcquired() { LiveAddressableLeaseCount++; }
        internal static void RecordReleased() { LiveAddressableLeaseCount--; }

        internal static async Task<UIPanelLease> LoadAsync(UIPanelConfig config, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (config.asset != null) return new UIPanelLease(config.asset);
            return await AdoptOperationAsync(Addressables.LoadAssetAsync<UIPanelAsset>(config.assetAddress), token);
        }

        // 与资源请求分离，验证器可用真实 ResourceManager 操作测试释放，无需改玩家项目的地址目录。
        internal static async Task<UIPanelLease> AdoptOperationAsync(AsyncOperationHandle<UIPanelAsset> handle, CancellationToken token)
        {
            var transferred = false;
            try
            {
                await AwaitOrCancelAsync(handle.Task, token);
                token.ThrowIfCancellationRequested();
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                    throw new InvalidOperationException("面板资源描述加载失败。", handle.OperationException);
                var lease = new UIPanelLease(handle);
                transferred = true;
                return lease;
            }
            finally { if (!transferred && handle.IsValid()) Addressables.Release(handle); }
        }

        private static async Task AwaitOrCancelAsync(Task task, CancellationToken token)
        {
            if (!token.CanBeCanceled) { await task; return; }
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => canceled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, canceled.Task) != task)
                {
                    ObserveFailureAsync(task);
                    token.ThrowIfCancellationRequested();
                }
                await task;
            }
        }

        private static async void ObserveFailureAsync(Task task)
        { try { await task; } catch { /* 取消路径已经回报；这里只消费资源操作的迟到错误。 */ } }
    }
}
