#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Landsong.ECS.Presentation;

namespace Landsong.ECS.Editor
{
    public static class ApplicationCleanupRecoveryVerification
    {
        public static string Run()
        {
            int assertions = 0;
            void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException("清理恢复验证失败：" + message); }
            var recovery = new ApplicationCleanupRecovery();
            int cleanups = 0, menus = 0, menuEntered = 0, activeOperations = 0;
            var order = new List<string>();
            Task Cleanup()
            {
                Check(activeOperations == 0, "重试不会启动重叠的清理操作");
                activeOperations++; cleanups++; order.Add("cleanup:" + cleanups);
                Check(recovery.BlocksNewSession && !recovery.RequestRetry(), "正在清理时不能重复重试或解锁新会话");
                activeOperations--;
                return cleanups == 1 ? Task.FromException(new TimeoutException("native operation is still pending")) : Task.CompletedTask;
            }
            Task Menu()
            {
                Check(activeOperations == 0 && cleanups >= 2, "只有顺序清理成功后才能开始返回菜单");
                menus++; order.Add("menu:" + menus);
                Check(recovery.BlocksNewSession, "菜单尚未完成时仍拒绝新会话");
                if (menus == 1) return Task.FromException(new TimeoutException("menu operation is still pending"));
                menuEntered++; return Task.CompletedTask;
            }
            void Changed(ApplicationCleanupRecovery state)
            {
                if (state.State == ApplicationCleanupState.Failed)
                {
                    bool rejected = false;
                    try { state.EnsureNewSessionAllowed(); } catch (InvalidOperationException) { rejected = true; }
                    Check(rejected && menuEntered == 0, "初次/重复清理失败或菜单失败后，新 Begin 共用的 guard 保持锁定");
                    Check(state.LastFailure != null, "失败原因可被读取与展示");
                    Check(state.RequestRetry() && !state.RequestRetry(), "同一次点击只排队一次重试");
                }
            }
            recovery.RunAsync(new TimeoutException("initial cleanup failed"), Cleanup, Menu,
                () => throw new InvalidOperationException("测试已明确排队重试，不应等真实帧。"), Changed).GetAwaiter().GetResult();
            Check(string.Join(",", order) == "cleanup:1,cleanup:2,menu:1,cleanup:3,menu:2", "菜单失败后也先重新 drain/清理，再重新返回菜单");
            Check(recovery.State == ApplicationCleanupState.Completed && !recovery.BlocksNewSession && menuEntered == 1,
                "只有清理与返回菜单成功才完成恢复并允许新会话");
            recovery.EnsureNewSessionAllowed();
            Check(recovery.LastFailure == null && recovery.AttemptCount == 3 && !recovery.RequestRetry(), "完成后清理诊断并拒绝多余重试");

            var waiting = new ApplicationCleanupRecovery();
            var nativeDrain = new TaskCompletionSource<bool>();
            int waitingMenus = 0;
            var waitingTask = waiting.RunAsync(new Exception("pending native cleanup"), () => nativeDrain.Task,
                () => { waitingMenus++; return Task.CompletedTask; },
                () => throw new InvalidOperationException("重试已排队。"),
                state => { if (state.State == ApplicationCleanupState.Failed) state.RequestRetry(); });
            Check(!waitingTask.IsCompleted && waiting.State == ApplicationCleanupState.Cleaning
                && waitingMenus == 0 && !waiting.RequestRetry(), "native drain 尚未完成时不启动菜单或第二个清理任务");
            bool waitingRejected = false;
            try { waiting.EnsureNewSessionAllowed(); } catch (InvalidOperationException) { waitingRejected = true; }
            Check(waitingRejected, "异步清理等待期间共享 Begin guard 仍拒绝新会话");
            nativeDrain.SetResult(true);
            waitingTask.GetAwaiter().GetResult();
            Check(waitingMenus == 1 && !waiting.BlocksNewSession, "native drain 完成后才进入菜单并解除锁定");

            var stopped = new ApplicationCleanupRecovery();
            try
            {
                stopped.RunAsync(new Exception("cleanup failed"), () => Task.CompletedTask, () => Task.CompletedTask,
                    () => Task.FromException(new OperationCanceledException("application stopped")), _ => { }).GetAwaiter().GetResult();
                throw new InvalidOperationException("停止帧等待应中断恢复等待。");
            }
            catch (OperationCanceledException) { }
            Check(stopped.BlocksNewSession && stopped.State == ApplicationCleanupState.Failed,
                "等待中断不能把未清理状态当成完成解锁");
            return "ApplicationCleanupRecovery Assertions: " + assertions + " passed";
        }
    }
}
#endif
