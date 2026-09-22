#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator SceneRequestsUi()
        {
            const string map = VerificationMap.Id;
            byte[] snapshot = null;
            for (var cycle = 0; cycle < 2; cycle++)
            {
                var operation = EcsSceneFlow.BeginAsync(new EcsSceneFlow.Request(map, snapshot));
                Require(EcsSceneFlow.Busy && !EcsSceneFlow.TryBegin(null), "Busy request is explicitly rejected without replacing the active transition");
                yield return WaitFor(() => operation.IsCompleted, "tracked scene request", true);
                operation.GetAwaiter().GetResult();
                Require(EcsSceneFlow.GameReady && !EcsSceneFlow.Busy, "Tracked scene request publishes GameReady");
                var em = World.DefaultGameObjectInjectionWorld.EntityManager;
                snapshot = SnapshotCodec.Capture(em, WorldQueries.Root(em));
                EcsSceneFlow.ReturnToMenu();
                yield return WaitFor(() => MenuReady && EcsSceneFlow.CurrentTransition.IsCompleted, "tracked return to menu");
                EcsSceneFlow.CurrentTransition.GetAwaiter().GetResult();
                Require(Released(), "Returning to menu releases the previous simulation");
            }

            foreach (var request in new[]
            {
                new EcsSceneFlow.Request("missing-map-test"),
                new EcsSceneFlow.Request(map, new byte[] { 0 })
            }

            )
            {
                expectedErrors.Add(request.Snapshot == null ? "找不到地图：missing-map-test" : "不是 ECS 存档。");
                EcsSceneFlow.Begin(request);
                yield return WaitFor(() => Shared<UI_LoadingPanel>()?.Failed == true && Shared<UI_LoadingPanel>().CancelButton.interactable, "failed request cleanup");
                Require(Released() && !EcsSceneFlow.GameReady, "Failure releases gameplay before offering return to menu");
                Shared<UI_LoadingPanel>().CancelButton.onClick.Invoke();
                yield return WaitFor(() => MenuReady && EcsSceneFlow.CurrentTransition.IsCompleted, "failed request returns to menu");
                Require(!EcsSceneFlow.Busy && EcsSceneFlow.Pending == null, "Recovered request relinquishes routing ownership");
            }

            EcsSceneFlow.Begin(new EcsSceneFlow.Request(map));
            yield return WaitFor(() => Shared<UI_LoadingPanel>() != null, "cancelable request opens loading panel");
            Shared<UI_LoadingPanel>().Cancel();
            yield return WaitFor(() => MenuReady && EcsSceneFlow.CurrentTransition.IsCompleted, "canceled request returns to menu");
            Require(Released() && !EcsSceneFlow.Busy, "Cancellation releases the map and busy state");
            var retry = EcsSceneFlow.BeginAsync(new EcsSceneFlow.Request(map));
            yield return WaitFor(() => retry.IsCompleted, "retry after recovered failures", true);
            retry.GetAwaiter().GetResult();
            Require(EcsSceneFlow.GameReady && expectedErrors.Count == 0, "Retry succeeds and expected failure diagnostics were observed");
            EcsSceneFlow.ReturnToMenu();
            yield return WaitFor(() => MenuReady && EcsSceneFlow.CurrentTransition.IsCompleted, "final request cleanup");
            Require(Released(), "Focused scene verification leaves no gameplay authority");
        }
    }
}
#endif
