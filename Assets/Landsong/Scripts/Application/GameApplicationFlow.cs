using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Landsong.ECS.Persistence;
using Moyo.Unity;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Presentation
{
    // Owns scene/session transactions. Views report intent and receive an explicit session binding.
    public sealed class GameApplicationFlow : MonoBehaviour
    {
        readonly Dictionary<ApplicationSceneRole, ApplicationSceneEntry> scenes = new Dictionary<ApplicationSceneRole, ApplicationSceneEntry>();
        readonly List<AsyncOperation> pendingSceneOperations = new List<AsyncOperation>();
        readonly ApplicationCleanupRecovery cleanupRecovery = new ApplicationCleanupRecovery();
        ApplicationUiRoot ui;
        UIScope gameScope;
        UI_GamePanel gameView;
        UI_LoadingPanel loading;
        bool transitioning, cancelled;
        long generation;
        float timeout = 120;
        public bool Transitioning => transitioning || cleanupRecovery.BlocksNewSession;
        public ApplicationCleanupState CleanupState => cleanupRecovery.State;
        public string CleanupDiagnostic => cleanupRecovery.LastFailure?.ToString();
        public void Configure(ApplicationUiRoot owner)
        {
            if (ui != null) throw new InvalidOperationException("应用流程被重复初始化。");
            ui = owner ?? throw new ArgumentNullException(nameof(owner));
            EcsSceneFlow.Bind(Begin);
        }
        public void Register(ApplicationSceneEntry entry)
        {
            if (scenes.TryGetValue(entry.Role, out var previous) && previous != null && previous != entry)
                throw new InvalidOperationException("场景职责重复注册：" + entry.Role);
            scenes[entry.Role] = entry;
            if (Transitioning) return;
            if (entry.Role == ApplicationSceneRole.Boot) Observe(BootAsync());
            else if (entry.Role == ApplicationSceneRole.Menu) Observe(OpenMenuAsync());
            else EcsSceneFlow.ReturnToMenu();
        }
        public void Unregister(ApplicationSceneEntry entry)
        {
            if (scenes.TryGetValue(entry.Role, out var current) && current == entry) scenes.Remove(entry.Role);
        }
        async void Observe(Task task) { try { await task; } catch (Exception error) { Debug.LogException(error); } }
        async Task BootAsync()
        {
            long operation = ++generation;
            transitioning = true;
            try
            {
                var boot = await ui.Manager.OpenAsync<UI_BootPanel>();
                float until = Time.realtimeSinceStartup + boot.SplashSeconds;
                while (Time.realtimeSinceStartup < until) await Frame();
                await ui.Manager.CloseAsync<UI_BootPanel>();
                await LoadMenuAsync();
            }
            finally { if (operation == generation) transitioning = false; }
        }
        void Begin(EcsSceneFlow.Request request)
        {
            cleanupRecovery.EnsureNewSessionAllowed();
            if (Transitioning) throw new InvalidOperationException("场景切换尚未结束。");
            transitioning = true; cancelled = false; Observe(TransitionAsync(request, ++generation));
        }
        async Task TransitionAsync(EcsSceneFlow.Request request, long operation)
        {
            try
            {
                loading = await ui.Manager.OpenAsync<UI_LoadingPanel>((Action)(() => cancelled = true));
                timeout = Mathf.Max(10, loading.TimeoutSeconds);
                loading.Stage("正在释放上一场景…", 0);
                await CloseSessionAsync();
                await ui.Manager.CloseAsync<UI_StartPanel>();
                await Wait(SceneManager.LoadSceneAsync(EcsSceneFlow.Loading, LoadSceneMode.Single), "卸载上一场景");
                await WaitReleased();
                if (request == null || cancelled) { await LoadMenuAsync(); return; }
                loading.Stage("正在加载运行场景…", .1f);
                await Wait(SceneManager.LoadSceneAsync(EcsSceneFlow.Game, LoadSceneMode.Additive), "加载运行场景");
                if (!scenes.TryGetValue(ApplicationSceneRole.Game, out var entry) || entry == null || entry.GameHost == null)
                    throw new InvalidOperationException("Game 场景未注册显式游戏宿主。");
                var host = entry.GameHost;
                if (cancelled) { await CancelToMenuAsync(); return; }
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) throw new InvalidOperationException("ECS World 不可用。");
                loading.Stage("正在加载地图实体与渲染资源…", .4f);
                var sceneEntity = host.LoadMap(request.MapId, world);
                float deadline = Deadline();
                while (!SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity))
                { if (cancelled) break; CheckDeadline(deadline, "加载地图实体"); await Frame(); }
                if (cancelled) { await CancelToMenuAsync(); return; }
                loading.Stage("正在初始化王朝…", .75f);
                var em = world.EntityManager;
                Entity root;
                while ((root = Sim.Root(em)) == Entity.Null || !em.HasComponent<SimulationReady>(root))
                { if (cancelled) break; CheckDeadline(deadline, "初始化王朝"); await Frame(); }
                if (cancelled) { await CancelToMenuAsync(); return; }
                if (em.GetComponentData<MapIdentity>(root).Id.ToString() != request.MapId)
                    throw new InvalidOperationException("加载地图与请求不一致。");
                Restore(request, world, root);
                await Frame();
                if (cancelled) { await CancelToMenuAsync(); return; }
                host.FocusCore(em, root);
                gameScope = ui.Manager.CreateScope("游戏会话");
                gameView = await ui.Manager.OpenAsync<UI_GamePanel>(scope: gameScope);
                if (cancelled) { await CancelToMenuAsync(); return; }
                gameView.BindSession(em, root, host.Camera, ui.Manager.Scaler, ui.Manager.EventSystem);
                ui.Presentation.BindSession(em, root);
                gameView.WorldInteraction.WorldPresentation.BindSession(em, root, ui.Presentation, host.gameObject.scene);
                gameView.PauseMenu.Bind(ui, em, root);
                SceneManager.SetActiveScene(SceneManager.GetSceneByPath(EcsSceneFlow.Game));
                await Wait(SceneManager.UnloadSceneAsync(EcsSceneFlow.Loading), "卸载过渡场景");
                if (cancelled) { await CancelToMenuAsync(); return; }
                loading.Stage("准备完成", 1); host.SetVisible(true);
                await ui.Manager.CloseAsync<UI_LoadingPanel>(); loading = null;
                transitioning = false; EcsSceneFlow.EnterGame();
            }
            catch (Exception failure)
            {
                Debug.LogError("场景切换失败：" + failure.Message);
                if (loading != null) loading.ShowError("加载失败：" + failure.Message + "\n正在释放地图…", false);
                try { await CleanupGameAsync(); }
                catch (Exception cleanup)
                {
                    await RecoverCleanupToMenuAsync(failure, cleanup);
                    return;
                }
                if (loading == null) throw;
                loading.ShowError("加载失败：" + failure.Message, true); cancelled = false;
                while (!cancelled) await Frame();
                try { await LoadMenuAsync("上次加载未完成：" + failure.Message); }
                catch (Exception menuFailure) { await RecoverCleanupToMenuAsync(failure, menuFailure); }
            }
            finally { if (operation == generation && !cleanupRecovery.BlocksNewSession) transitioning = false; }
        }
        Task RecoverCleanupToMenuAsync(Exception initialFailure, Exception cleanupFailure)
        {
            return cleanupRecovery.RunAsync(cleanupFailure, CleanupGameAsync,
                () => LoadMenuAsync("上次加载未完成：" + initialFailure.Message), Frame, recovery =>
                {
                    if (recovery.State == ApplicationCleanupState.Failed)
                    {
                        Debug.LogException(recovery.LastFailure);
                        if (loading != null) loading.ShowCleanupError("场景清理失败：" + recovery.LastFailure.Message
                            + "\n新会话仍被锁定。可以重试清理，完成后返回主菜单。", () => recovery.RequestRetry());
                    }
                    else if (loading != null && recovery.State == ApplicationCleanupState.Cleaning)
                        loading.ShowCleanupPending("正在重试释放地图…");
                    else if (loading != null && recovery.State == ApplicationCleanupState.ReturningToMenu)
                        loading.ShowCleanupPending("地图已释放，正在返回主菜单…");
                });
        }
        void Restore(EcsSceneFlow.Request request, World world, Entity root)
        {
            var em = world.EntityManager;
            if (request.Archive != null)
            {
                loading.Stage("正在恢复王朝与回退节点…", .85f);
                var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>();
                if (request.Store != null) checkpoint.Store = request.Store;
                var data = request.Archive.Copy();
                if (request.Slot != null)
                {
                    try
                    {
                        var latest = (checkpoint.Store ?? CheckpointSystem.DefaultStore).Read(data.RunId, out _);
                        if (latest.Recovery.Turn == data.Recovery.Turn)
                        {
                            var known = Math.Max(latest.Recovery.KnownIntel, data.Recovery.KnownIntel);
                            if (latest.Recovery.LossCount > data.Recovery.LossCount) data.Recovery = latest.Recovery;
                            data.Recovery.KnownIntel = known; data.Recovery.AwaitingDecision = 0;
                        }
                    }
                    catch (Exception error) when (error is IOException || error is InvalidDataException) { }
                }
                checkpoint.Import(root, data, request.Persist);
                if (request.Persist) (checkpoint.Store ?? CheckpointSystem.DefaultStore).Activate(data.RunId, request.Slot);
                if (request.RecoveredBackup) Sim.Emit(em, root, EventKind.Message, "正式记录损坏，已从备份恢复；损坏原件尚未修改。");
            }
            else if (request.Snapshot != null)
            {
                var snapshot = SnapshotCodec.Decode(em, root, request.Snapshot);
                if (snapshot.Session.Phase != Phase.Day) throw new InvalidOperationException("只能从白天存档继续游戏。");
                SnapshotCodec.Restore(em, root, snapshot); Sim.Emit(em, root, EventKind.DayCheckpoint, default);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(request.DynastyName))
                { var state = em.GetComponentData<Session>(root); state.DynastyName = RunArchiveStore.DisplayName(request.DynastyName); em.SetComponentData(root, state); }
                if (request.Persist)
                {
                    var checkpoint = world.GetOrCreateSystemManaged<CheckpointSystem>();
                    if (request.Store != null) checkpoint.Store = request.Store;
                    checkpoint.OpenNewRun(root);
                }
            }
        }
        async Task CloseSessionAsync()
        {
            await ui.CloseSharedAsync();
            ui.Presentation.UnbindSession();
            if (gameView != null) { gameView.WorldInteraction.WorldPresentation.UnbindSession(); gameView.PauseMenu.Unbind(); gameView.UnbindSession(); gameView = null; }
            if (gameScope != null) { await ui.Manager.EndScopeAsync(gameScope); gameScope = null; }
        }
        async Task CleanupGameAsync()
        {
            await CloseSessionAsync();
            // A timeout cannot cancel Unity's native scene operation. Drain it before examining ownership.
            foreach (var pending in pendingSceneOperations.ToArray()) await Wait(pending, "等待未完成的场景操作清理");
            var game = SceneManager.GetSceneByPath(EcsSceneFlow.Game);
            if (game.IsValid() && game.isLoaded) await Wait(SceneManager.UnloadSceneAsync(game), "释放运行场景");
            await WaitReleased();
        }
        async Task CancelToMenuAsync() { await CleanupGameAsync(); await LoadMenuAsync(); }
        async Task LoadMenuAsync(string message = null)
        {
            EcsSceneFlow.PrepareMenu(message);
            await Wait(SceneManager.LoadSceneAsync(EcsSceneFlow.Menu, LoadSceneMode.Single), "返回主菜单");
            var menu = await ui.Manager.OpenAsync<UI_StartPanel>(new MenuOpenContext { Navigation = ui });
            await ui.Manager.CloseAsync<UI_LoadingPanel>(); loading = null;
            transitioning = false; EcsSceneFlow.EnterMenu(); menu.RefreshAvailability();
        }
        Task OpenMenuAsync() => ui.Manager.OpenAsync<UI_StartPanel>(new MenuOpenContext { Navigation = ui });
        async Task WaitReleased()
        {
            float deadline = Deadline();
            while (!Released()) { CheckDeadline(deadline, "释放地图所有权"); await Frame(); }
        }
        async Task Wait(AsyncOperation operation, string stage)
        {
            if (operation == null) throw new InvalidOperationException(stage + "没有创建异步操作。");
            if (!pendingSceneOperations.Contains(operation)) pendingSceneOperations.Add(operation);
            try
            {
                float deadline = Deadline();
                while (!operation.isDone) { CheckDeadline(deadline, stage); await Frame(); }
            }
            finally { if (operation.isDone) pendingSceneOperations.Remove(operation); }
        }
        static bool Released()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return true;
            return SimulationLifetimeSystem.IsReleased(world.EntityManager);
        }
        float Deadline() => Time.realtimeSinceStartup + timeout;
        static void CheckDeadline(float deadline, string stage)
        { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(stage + "超时，请检查场景烘焙与配置。"); }
        async Task Frame()
        { await Task.Yield(); if (this == null || !Application.isPlaying) throw new OperationCanceledException("应用流程已停止。"); }
        void OnDestroy() { EcsSceneFlow.Unbind(Begin); }
    }
}
