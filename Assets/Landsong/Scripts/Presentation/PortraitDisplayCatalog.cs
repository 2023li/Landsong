using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/Presentation/Portrait Display Catalog")]
    public sealed class PortraitDisplayCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Portrait
        {
            [LabelText("内容稳定编号")]
            public string Definition;
            [LabelText("人物稳定编号"), Tooltip("0 表示默认画像，其他值覆盖指定人物的画像。")]
            public ulong Person;
            [LabelText("画像图片")]
            public Sprite Image;
        }

        [LabelText("人物画像")]
        public Portrait[] Portraits = Array.Empty<Portrait>();
        [LabelText("默认画像")]
        public Sprite DefaultPortrait;
        public Sprite Face(string definition, ulong person)
        {
            var exact = Array.Find(Portraits, p => p != null && p.Person == person && person != 0 && p.Image != null);
            if (exact != null)
                return exact.Image;
            return Array.Find(Portraits, p => p != null && p.Person == 0 && p.Definition == definition && p.Image != null)?.Image ?? DefaultPortrait;
        }
    }
}
