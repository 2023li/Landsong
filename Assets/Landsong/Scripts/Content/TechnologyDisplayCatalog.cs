using System;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using UnityEngine;

namespace Landsong.Content
{
    [Serializable]
    public sealed class TechnologyDisplay
    {
        [LabelText("稳定标识")]
        public string Id;
        [LabelText("说明")]
        public string Description;
        [LabelText("图标")]
        public Sprite Icon;
        [LabelText("指定树状位置")]
        public bool HasTreePosition;
        [LabelText("树状位置")]
        public Vector2 TreePosition;
    }

    // Entry order is compiled and validated against the Technology catalog only.
    public sealed class TechnologyDisplayCatalog : ScriptableObject
    {
        [LabelText("科技显示条目")]
        public TechnologyDisplay[] Entries = Array.Empty<TechnologyDisplay>();
        public TechnologyDisplay Get(TechnologyId id) => id.IsValid && id.Index < Entries.Length ? Entries[id.Index] : null;
    }
}
