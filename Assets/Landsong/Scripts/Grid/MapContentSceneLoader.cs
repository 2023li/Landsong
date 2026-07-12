using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Landsong.GridSystem
{
    public static class MapContentSceneLoader
    {
        private static AsyncOperationHandle<SceneInstance>? activeHandle;

        public static MapDefinition ActiveDefinition { get; private set; }
        public static MapContentAuthoring ActiveContent { get; private set; }

        public static IEnumerator LoadRoutine(
            MapDefinition definition,
            Action<bool, string> completed)
        {
            if (definition == null || !definition.IsValid)
            {
                completed?.Invoke(false, "MapDefinition 无效或没有绑定 Addressable Content Scene。");
                yield break;
            }

            if (activeHandle.HasValue)
            {
                yield return UnloadRoutine();
            }

            var handle = definition.ContentScene.LoadSceneAsync(LoadSceneMode.Additive, true);
            yield return handle;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                completed?.Invoke(false, $"地图 Content Scene 加载失败：{definition.MapId}");
                yield break;
            }

            var content = FindContent(handle.Result.Scene);
            if (content == null)
            {
                yield return Addressables.UnloadSceneAsync(handle, true);
                completed?.Invoke(false, $"地图 Content Scene 中缺少唯一的 MapContentAuthoring：{definition.MapId}");
                yield break;
            }

            activeHandle = handle;
            ActiveDefinition = definition;
            ActiveContent = content;
            completed?.Invoke(true, string.Empty);
        }

        public static IEnumerator UnloadRoutine()
        {
            MapRuntimeHost.Active?.Unbind();
            ActiveDefinition = null;
            ActiveContent = null;

            if (!activeHandle.HasValue)
            {
                yield break;
            }

            var handle = activeHandle.Value;
            activeHandle = null;
            if (handle.IsValid())
            {
                yield return Addressables.UnloadSceneAsync(handle, true);
            }
        }

        private static MapContentAuthoring FindContent(Scene scene)
        {
            MapContentAuthoring found = null;
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var candidates = roots[i].GetComponentsInChildren<MapContentAuthoring>(true);
                for (var j = 0; j < candidates.Length; j++)
                {
                    if (found != null)
                    {
                        Debug.LogError($"地图 Content Scene '{scene.name}' 中存在多个 MapContentAuthoring。", candidates[j]);
                        return null;
                    }

                    found = candidates[j];
                }
            }

            return found;
        }
    }
}
