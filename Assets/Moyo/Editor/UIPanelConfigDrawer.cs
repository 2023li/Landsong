using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace Moyo.Unity.Editor
{
    // 使用 Odin 属性树绘制所有嵌套配置；无效类型保持原值，由验证器报告。
    [CustomEditor(typeof(UIConfig))]
    public sealed class UIConfigEditor : OdinEditor { }

    [CustomEditor(typeof(UIPanelAsset))]
    public sealed class UIPanelAssetEditor : OdinEditor { }

    [CustomEditor(typeof(UIManager), true)]
    public sealed class UIManagerEditor : OdinEditor { }

    [CustomEditor(typeof(UIViewBase), true)]
    public sealed class UIViewEditor : OdinEditor { }
}
