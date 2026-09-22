using System;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using UnityEngine;

namespace Landsong.Content
{
    // Entry order is compiled and validated against the TalentSlot catalog only.
    public sealed class TalentSlotDisplayCatalog : ScriptableObject
    {
        [LabelText("显示条目")]
        public ContentDisplay[] Entries = Array.Empty<ContentDisplay>();
        public ContentDisplay Get(TalentSlotId id) => id.IsValid && id.Index < Entries.Length ? Entries[id.Index] : null;
    }
}
