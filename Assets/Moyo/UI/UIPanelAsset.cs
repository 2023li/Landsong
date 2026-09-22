using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>加载单元。保存组件类型的 Prefab 引用，实例化无需查找根控制器。</summary>
    [CreateAssetMenu(fileName = "UIPanelAsset", menuName = "Moyo/UI/面板资源描述")]
    public sealed class UIPanelAsset : ScriptableObject
    {
        [SerializeField, LabelText("预制体根控制器"), Required] private UIPanelBase prefabRoot;
        public UIPanelBase PrefabRoot => prefabRoot;
        public string PanelId => prefabRoot != null ? prefabRoot.PanelId : string.Empty;
        public void Configure(UIPanelBase root) { prefabRoot = root; }
        public void Validate(string expectedPanelId = null)
        {
            if (prefabRoot == null) throw new InvalidOperationException($"{name} 未绑定预制体根控制器。");
            if (prefabRoot.transform.parent != null) throw new InvalidOperationException($"{name} 引用的控制器不是预制体根对象。");
            if (!string.IsNullOrEmpty(expectedPanelId) && expectedPanelId != PanelId)
                throw new InvalidOperationException($"{name} 面板类型为 {PanelId}，注册类型为 {expectedPanelId}。");
            prefabRoot.ValidateConfiguration();
        }
    }
}
