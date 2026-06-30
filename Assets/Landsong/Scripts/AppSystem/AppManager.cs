using System;
using System.Threading.Tasks;
using Landsong.BuildingSystem;
using Landsong.GridSystem;
using Landsong.InventorySystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.AppSystem
{
    public enum AppRuntimePlatform
    {
        Unknown = 0,
        Editor = 1,
        Windows = 2,
        MacOS = 3,
        Linux = 4,
        Android = 5,
        IOS = 6,
        WebGL = 7
    }

    [DisallowMultipleComponent]
    public sealed class AppManager : MonoSingleton<AppManager>
    {
        [Header("App")]
        [SerializeField, LabelText("版本号覆盖")] private string versionOverride = string.Empty;

        [Header("Config")]
        [SerializeField, LabelText("地图目录 Addressables Key")] private string mapDataCatalogAddressKey = "MapCatalog";
        [SerializeField, LabelText("物品目录 Addressables Key")] private string itemCatalogAddressKey = "ItemCatalog";
        [SerializeField, LabelText("建筑目录 Addressables Key")] private string buildingCatalogAddressKey = "BuildingCatalog";

        [ShowInInspector, ReadOnly, LabelText("运行平台")]
        public AppRuntimePlatform RuntimePlatform => ResolveRuntimePlatform();

        [ShowInInspector, ReadOnly, LabelText("Unity 平台")]
        public RuntimePlatform UnityRuntimePlatform => Application.platform;

        [ShowInInspector, ReadOnly, LabelText("版本号")]
        public string Version => string.IsNullOrWhiteSpace(versionOverride)
            ? Application.version
            : versionOverride.Trim();

        public bool IsEditor => Application.isEditor;
        public bool IsMobilePlatform => Application.isMobilePlatform || RuntimePlatform == AppRuntimePlatform.Android || RuntimePlatform == AppRuntimePlatform.IOS;
        public bool IsDesktopPlatform => RuntimePlatform == AppRuntimePlatform.Windows || RuntimePlatform == AppRuntimePlatform.MacOS || RuntimePlatform == AppRuntimePlatform.Linux;
        public string BuildLabel => $"{Version} ({RuntimePlatform})";

    

        [ShowInInspector, ReadOnly, LabelText("配置单例加载中")]
        public bool IsLoadingConfigSingletons { get; private set; }

        [ShowInInspector, ReadOnly, LabelText("配置单例已加载")]
        public bool HasLoadedConfigSingletons { get; private set; }

       

        protected async override void Init()
        {
            //加载基础配置
            await Task.WhenAll(
                    MapDataCatalog.LoadAsync(mapDataCatalogAddressKey),
                    ItemCatalog.LoadAsync(itemCatalogAddressKey),
                    BuildingCatalog.LoadAsync(buildingCatalogAddressKey));
        }

      

     
        public bool CanQuit()
        {
            return true;
        }

       

     

     

        private static AppRuntimePlatform ResolveRuntimePlatform()
        {
            if (Application.isEditor)
            {
                return AppRuntimePlatform.Editor;
            }

            switch (Application.platform)
            {
                case UnityEngine.RuntimePlatform.WindowsPlayer:
                    return AppRuntimePlatform.Windows;
                case UnityEngine.RuntimePlatform.OSXPlayer:
                    return AppRuntimePlatform.MacOS;
                case UnityEngine.RuntimePlatform.LinuxPlayer:
                    return AppRuntimePlatform.Linux;
                case UnityEngine.RuntimePlatform.Android:
                    return AppRuntimePlatform.Android;
                case UnityEngine.RuntimePlatform.IPhonePlayer:
                    return AppRuntimePlatform.IOS;
                case UnityEngine.RuntimePlatform.WebGLPlayer:
                    return AppRuntimePlatform.WebGL;
                default:
                    return AppRuntimePlatform.Unknown;
            }
        }

        internal void ContinueGame()
        {
            throw new NotImplementedException();
        }

        internal void ExitApp()
        {
            throw new NotImplementedException();
        }

        internal void StartNewGame(GridMapBehaviour map)
        {
            throw new NotImplementedException();
        }
    }
}
