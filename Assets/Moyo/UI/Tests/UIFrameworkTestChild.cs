#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
namespace Moyo.Unity.Tests
{
    public sealed class UIFrameworkTestChild : UIViewBase
    {
        [NonSerialized] public int Creates;
        [NonSerialized] public int Closes;
        [NonSerialized] public int Releases;
        public override Task OnCreateAsync() { Creates++; return Task.CompletedTask; }
        public override Task OnCloseAsync() { Closes++; return Task.CompletedTask; }
        public override Task OnReleaseAsync() { Releases++; return Task.CompletedTask; }
    }
}
#endif
