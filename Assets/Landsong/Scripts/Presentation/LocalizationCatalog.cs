using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/Presentation/Localization Catalog")]
    public sealed class LocalizationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Translation
        {
            [LabelText("语言表")]
            public string Table;
            [LabelText("文本键")]
            public string Key;
            [LabelText("中文内容"), TextArea]
            public string Zh;
            [LabelText("英文内容"), TextArea]
            public string En;
        }

        [LabelText("语言条目")]
        public Translation[] Text = Array.Empty<Translation>();
    }
}
