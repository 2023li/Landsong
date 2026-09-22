#if UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Landsong.ECS.Presentation;

namespace Landsong.ECS.Editor
{
    public static class SceneRequestVerification
    {
        public static string Run()
        {
            int assertions = 0;
            void Check(bool value, string label) { assertions++; if (!value) throw new InvalidOperationException(label); }
            var context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            Func<EcsSceneFlow.Request, Task> handler = null;
            try
            {
                var completion = new TaskCompletionSource<bool>();
                handler = _ => completion.Task;
                EcsSceneFlow.EnterMenu(""); EcsSceneFlow.Bind(handler);
                var operation = EcsSceneFlow.BeginAsync(new EcsSceneFlow.Request("test"));
                Check(EcsSceneFlow.Busy && !operation.IsCompleted && EcsSceneFlow.CurrentTransition == operation, "Transition is owned until asynchronous completion");
                Check(!EcsSceneFlow.TryBegin(null), "Busy requests are explicitly rejected");
                completion.SetException(new InvalidOperationException("loading prefab failed before opening"));
                try { operation.GetAwaiter().GetResult(); } catch (InvalidOperationException) { }
                Check(operation.IsFaulted && !EcsSceneFlow.Busy && !EcsSceneFlow.GameReady && EcsSceneFlow.Pending == null && EcsSceneFlow.LastFailure != null,
                    "Late loading failure leaves a retryable terminal state");
                EcsSceneFlow.Unbind(handler);
                handler = _ => { EcsSceneFlow.EnterGame(); return Task.CompletedTask; };
                EcsSceneFlow.Bind(handler);
                EcsSceneFlow.BeginAsync(new EcsSceneFlow.Request("retry")).GetAwaiter().GetResult();
                Check(EcsSceneFlow.GameReady && !EcsSceneFlow.Busy && EcsSceneFlow.LastFailure == null, "A new transition succeeds after failure");
                EcsSceneFlow.Unbind(handler);
                handler = _ => throw new InvalidOperationException("synchronous guard");
                EcsSceneFlow.Bind(handler); EcsSceneFlow.EnterGame();
                try { EcsSceneFlow.BeginAsync(null); } catch (InvalidOperationException) { }
                Check(EcsSceneFlow.GameReady && !EcsSceneFlow.Busy, "Synchronous rejection preserves the existing game");
                EcsSceneFlow.Unbind(handler);
                handler = _ => Task.FromCanceled(new CancellationToken(true)); EcsSceneFlow.Bind(handler);
                try { EcsSceneFlow.BeginAsync(null).GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
                Check(!EcsSceneFlow.Busy && EcsSceneFlow.Pending == null, "Cancellation releases request ownership");
                EcsSceneFlow.PrepareMenu("recovery diagnostic"); EcsSceneFlow.EnterMenu();
                Check(EcsSceneFlow.TakeMenuMessage() == "recovery diagnostic", "Menu entry preserves recovery diagnostics until consumed");
            }
            finally { if (handler != null) EcsSceneFlow.Unbind(handler); EcsSceneFlow.EnterMenu(""); SynchronizationContext.SetSynchronizationContext(context); }
            return "Assertions: " + assertions;
        }
    }
}
#endif
