using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    /// <summary>Application input ownership. Targets register themselves; no hierarchy discovery.</summary>
    public static class UiInputState
    {
        static readonly HashSet<TMP_InputField> focused = new();
        public static bool GlobalModalOpen { get; internal set; }
        public static int BackHandledFrame { get; internal set; } = -1;
        public static bool TextFocused
        {
            get
            {
                focused.RemoveWhere(input => input == null || !input.isActiveAndEnabled || !input.isFocused);
                return focused.Count != 0;
            }
        }
        public static void Select(TMP_InputField target) { if (target != null) focused.Add(target); }
        public static void Deselect(TMP_InputField target) { if (target != null) focused.Remove(target); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { focused.Clear(); GlobalModalOpen = false; BackHandledFrame = -1; }
    }
}
