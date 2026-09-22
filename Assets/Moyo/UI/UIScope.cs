using System.Threading;

namespace Moyo.Unity
{
    /// <summary>只由所属 Manager 创建和结束。取消立即生效，释放由 Manager 的操作队列完成。</summary>
    public sealed class UIScope
    {
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        internal UIManager Owner { get; }
        public string Name { get; }
        public long Generation { get; }
        public CancellationToken Token => cancellation.Token;
        public bool IsEnded => cancellation.IsCancellationRequested;
        internal UIScope(UIManager owner, string name, long generation)
        { Owner = owner; Name = name; Generation = generation; }
        internal void Cancel() { if (!cancellation.IsCancellationRequested) cancellation.Cancel(); }
        public override string ToString() => $"{Name} #{Generation}";
    }
}
