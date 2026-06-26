using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Landsong.BuildingSystem
{
    public static class BuildingPrefabLoader
    {
        public static AsyncOperationHandle<GameObject> LoadPrefabAsync(BuildingDefinition definition)
        {
            return Addressables.LoadAssetAsync<GameObject>(GetPrefabAddressableKey(definition));
        }

        public static AsyncOperationHandle<GameObject> InstantiatePrefabAsync(
            BuildingDefinition definition,
            Vector3 position,
            Transform parent = null)
        {
            return Addressables.InstantiateAsync(GetPrefabAddressableKey(definition), position, Quaternion.identity, parent);
        }

        public static void ReleasePrefab(AsyncOperationHandle<GameObject> handle)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        public static bool ReleaseInstance(GameObject instance)
        {
            return instance != null && Addressables.ReleaseInstance(instance);
        }

        private static string GetPrefabAddressableKey(BuildingDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.HasPrefabAddress)
            {
                throw new ArgumentException($"Building definition '{definition.name}' has no prefab Addressable key.", nameof(definition));
            }

            return definition.PrefabAddressableKey;
        }
    }
}
