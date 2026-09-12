using System;
using System.Threading.Tasks;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public enum ApplicationCleanupState
    {
        [LabelText("空闲")] Idle,
        [LabelText("清理失败")] Failed,
        [LabelText("正在清理")] Cleaning,
        [LabelText("正在返回主菜单")] ReturningToMenu,
        [LabelText("恢复完成")] Completed
    }

    /// <summary>Serializes recovery attempts; new sessions stay blocked until cleanup and menu restoration both succeed.</summary>
    public sealed class ApplicationCleanupRecovery
    {
        bool retryRequested;
        public ApplicationCleanupState State { get; private set; }
        public Exception LastFailure { get; private set; }
        public int AttemptCount { get; private set; }
        public bool BlocksNewSession => State != ApplicationCleanupState.Idle && State != ApplicationCleanupState.Completed;

        public void EnsureNewSessionAllowed()
        {
            if (BlocksNewSession) throw new InvalidOperationException("场景清理尚未完成，请先完成清理重试。");
        }

        public bool RequestRetry()
        {
            if (State != ApplicationCleanupState.Failed || retryRequested) return false;
            retryRequested = true;
            return true;
        }

        public async Task RunAsync(Exception initialFailure, Func<Task> cleanup, Func<Task> returnToMenu,
            Func<Task> nextFrame, Action<ApplicationCleanupRecovery> stateChanged)
        {
            EnsureNewSessionAllowed();
            if (initialFailure == null || cleanup == null || returnToMenu == null || nextFrame == null || stateChanged == null)
                throw new ArgumentException("清理恢复需要完整的操作与状态回调。");
            retryRequested = false; AttemptCount = 0;
            LastFailure = initialFailure; State = ApplicationCleanupState.Failed; stateChanged(this);
            while (true)
            {
                while (!retryRequested) await nextFrame();
                retryRequested = false; AttemptCount++;
                State = ApplicationCleanupState.Cleaning; stateChanged(this);
                try
                {
                    await cleanup();
                    State = ApplicationCleanupState.ReturningToMenu; stateChanged(this);
                    await returnToMenu();
                    State = ApplicationCleanupState.Completed; LastFailure = null;
                    stateChanged(this);
                    return;
                }
                catch (Exception failure)
                {
                    LastFailure = failure;
                    State = ApplicationCleanupState.Failed;
                    stateChanged(this);
                }
            }
        }
    }
}
