using System;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using UnityEngine;

namespace Landsong.Content
{
    // Entry order is compiled and validated against the PolicyGroup catalog only.
    public sealed class PolicyGroupDisplayCatalog : ScriptableObject
    {
        [LabelText("显示条目")]
        public ContentDisplay[] Entries = Array.Empty<ContentDisplay>();
        public ContentDisplay Get(PolicyGroupId id) => id.IsValid && id.Index < Entries.Length ? Entries[id.Index] : null;
    }
}
