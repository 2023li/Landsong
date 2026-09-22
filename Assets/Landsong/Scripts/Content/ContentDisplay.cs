using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.Content
{
    [Serializable]
    public sealed class ContentDisplay
    {
        [LabelText("稳定标识")]
        public string Id;
        [LabelText("说明")]
        public string Description;
        [LabelText("图标")]
        public Sprite Icon;
    }
}
