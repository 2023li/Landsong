using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Moyo.Unity
{

    public abstract class SingletonScriptableObject<T> : ScriptableObject where T : SingletonScriptableObject<T>
    {
        private static T instance;
        private static Task<T> loadingTask;
        private static AsyncOperationHandle<T> handle;
        private static bool hasHandle;

        /// <summary>
        /// 是否已经加载完成
        /// </summary>
        public static bool IsLoaded => instance != null;

        /// <summary>
        /// 已加载后的同步访问。
        /// 如果还没有加载，会返回 null。
        /// </summary>
        public static T Instance => instance;

        /// <summary>
        /// 获取单例。如果没有加载，会使用 Addressables 加载。
        /// </summary>
        protected static Task<T> GetInstanceAsync(string addressableKey)
        {
            if (instance != null)
            {
                return Task.FromResult(instance);
            }

            if (loadingTask != null)
            {
                return loadingTask;
            }

            loadingTask = LoadInternalAsync(addressableKey);
            return loadingTask;
        }

        private static async Task<T> LoadInternalAsync(string addressableKey)
        {
            handle = Addressables.LoadAssetAsync<T>(addressableKey);
            hasHandle = true;

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                loadingTask = null;

                if (hasHandle && handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                hasHandle = false;

                throw new Exception(
                    $"Addressable ScriptableObject singleton load failed. Type: {typeof(T).Name}, Key: {addressableKey}"
                );
            }

            instance = handle.Result;
            loadingTask = null;

            instance.OnLoaded();

            return instance;
        }

        /// <summary>
        /// 加载完成后的回调，子类可重写。
        /// </summary>
        protected virtual void OnLoaded()
        {
        }

        /// <summary>
        /// 释放单例。
        /// 一般配置类可以不主动释放，常驻到游戏退出。
        /// </summary>
        public static void ReleaseInstance()
        {
            instance = null;
            loadingTask = null;

            if (hasHandle && handle.IsValid())
            {
                Addressables.Release(handle);
            }

            hasHandle = false;
        }
    }
}
