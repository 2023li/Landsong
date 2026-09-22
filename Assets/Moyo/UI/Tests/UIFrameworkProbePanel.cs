#if UNITY_EDITOR
using System;
using System.Threading.Tasks;

namespace Moyo.Unity.Tests
{
    /// <summary>仅编辑器验证使用；不编译进入正式 Player。</summary>
    public abstract class UIFrameworkProbePanel : UIPanelBase
    {
        public static Func<UIFrameworkProbePanel, object, Task> Opening;
        public static Action<UIFrameworkProbePanel> Releasing;
        [NonSerialized] public int Creates;
        [NonSerialized] public int Opens;
        [NonSerialized] public int Closes;
        [NonSerialized] public int Releases;
        [NonSerialized] public int Focuses;
        [NonSerialized] public int Blurs;
        [NonSerialized] public bool ConsumeLocalBack;
        [NonSerialized] public bool AllowBack = true;
        public override bool CanCloseByBack => AllowBack;
        public override Task OnCreateAsync() { Creates++; return Task.CompletedTask; }
        public override async Task OnOpenAsync(object args) { Opens++; if (Opening != null) await Opening(this, args); }
        public override Task OnCloseAsync() { Closes++; return Task.CompletedTask; }
        public override Task OnReleaseAsync() { Releases++; Releasing?.Invoke(this); return Task.CompletedTask; }
        public override Task OnFocusAsync() { Focuses++; return Task.CompletedTask; }
        public override Task OnBlurAsync() { Blurs++; return Task.CompletedTask; }
        public override Task<bool> TryHandleBackAsync() => Task.FromResult(ConsumeLocalBack);
    }
}
#endif
