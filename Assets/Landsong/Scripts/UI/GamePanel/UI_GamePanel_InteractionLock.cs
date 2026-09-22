using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    /// <summary>Retains a view's identity through pointer-up/click dispatch and text editing.</summary>
    public sealed class UI_GamePanel_InteractionLock : MonoBehaviour
    {
        [LabelText("交互控件绑定")] public UI_GamePanel_RowPointerBinding[] Bindings = Array.Empty<UI_GamePanel_RowPointerBinding>();
        public UI_GamePanel_InteractionLock Parent { get; set; }
        static int revision;
        static int pendingReleaseFrame = -1;
        public static int Revision
        {
            get
            {
                // Update may consume the release before Unity dispatches click in the same frame.
                // Publish once more after the click snapshot expires so deferred rows are rendered.
                if (pendingReleaseFrame >= 0 && Time.frameCount > pendingReleaseFrame)
                { pendingReleaseFrame = -1; unchecked { revision++; } }
                return revision;
            }
        }
        int active;
        int releasedFrame = -1;
        public bool IsPinned => active > 0 || releasedFrame == Time.frameCount;
        public void Begin() { active++; if (Parent != null) Parent.Begin(); }
        public void End()
        {
            if (active <= 0) return;
            active--; releasedFrame = Time.frameCount;
            if (Parent != null) Parent.End();
            unchecked { revision++; }
            pendingReleaseFrame = Time.frameCount;
        }
        public void ValidateConfiguration()
        {
            if (Bindings == null || Bindings.Length == 0) throw new InvalidOperationException("界面交互锁未配置控件。");
            foreach (var binding in Bindings)
                if (binding == null || binding.Target != this) throw new InvalidOperationException("控件的交互归属引用不一致。");
        }
    }
}
