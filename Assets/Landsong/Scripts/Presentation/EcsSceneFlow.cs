using System;
using System.IO;
using System.Threading.Tasks;
using Landsong.ECS.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Presentation
{
    // Routing/IO boundary only. Never owns a second copy of live gameplay state.
    public static class EcsSceneFlow
    {
        public const string Boot = ApplicationSceneCatalog.Boot;
        public const string Menu = ApplicationSceneCatalog.Menu;
        public const string Loading = ApplicationSceneCatalog.Loading;
        public const string Game = ApplicationSceneCatalog.Game;
        public static readonly string[] BuildScenes = ApplicationSceneCatalog.BuildScenes;
        public sealed class Request
        {
            public readonly string MapId;
            public readonly byte[] Snapshot;
            public readonly RunArchive Archive;
            public readonly bool Persist, RecoveredBackup;
            public readonly string DynastyName, Slot;
            public readonly RunArchiveStore Store;
            public Request(string mapId, byte[] snapshot = null, RunArchive archive = null, bool persist = false, bool recoveredBackup = false, string dynastyName = null, string slot = null, RunArchiveStore store = null)
            {
                MapId = mapId;
                Snapshot = snapshot;
                Archive = archive;
                Persist = persist;
                RecoveredBackup = recoveredBackup;
                DynastyName = dynastyName;
                Slot = slot;
                Store = store;
            }
        }

        public static Request Pending { get; private set; }
        public static bool Busy { get; private set; }
        public static bool GameReady { get; private set; }
        public static string MenuMessage { get; private set; }
        public static Task CurrentTransition { get; private set; } = Task.CompletedTask;
        public static Exception LastFailure { get; private set; }

        static Func<Request, Task> beginTransition;
        static long generation;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            generation++;
            Pending = null;
            Busy = GameReady = false;
            MenuMessage = null;
            beginTransition = null;
            LastFailure = null;
            CurrentTransition = Task.CompletedTask;
        }

        internal static void Bind(Func<Request, Task> handler)
        {
            if (beginTransition != null)
                throw new InvalidOperationException("场景流程已注册。");
            beginTransition = handler;
        }

        internal static void Unbind(Func<Request, Task> handler)
        {
            if (beginTransition == handler)
            {
                beginTransition = null;
                generation++;
                Fail(new OperationCanceledException("应用流程已释放。"));
            }
        }

        public static bool IsAppScene(string path) => Array.IndexOf(BuildScenes, path) >= 0;
        public static void NewGame(string mapId, string dynastyName = null) => Begin(new Request(mapId, persist: true, dynastyName: string.IsNullOrWhiteSpace(dynastyName) ? "无名王朝" : RunArchiveStore.DisplayName(dynastyName)));
        public static void ContinueRun(string run, string slot = null, bool backupOnly = false, RunArchiveStore store = null)
        {
            store ??= CheckpointSystem.DefaultStore;
            bool backup = backupOnly;
            var data = backupOnly ? store.ReadBackup(run, slot) : slot == null ? store.Read(run, out backup) : store.ReadSlot(run, slot, out backup);
            Begin(new Request(SnapshotCodec.ReadMapId(data.Current), archive: data, persist: true, recoveredBackup: backup, slot: slot, store: store));
        }

        public static void ContinueGame(RunArchiveStore store = null)
        {
            store ??= CheckpointSystem.DefaultStore;
            var archive = store.ReadContinue(out var backup);
            Begin(new Request(SnapshotCodec.ReadMapId(archive.Current), archive: archive, persist: true, recoveredBackup: backup, store: store));
        }

        public static void ReturnToMenu() => Begin(null);
        // Also used by development-only tests to verify restore without touching the player's save file.
        public static void Begin(Request request)
        {
            if (!TryBegin(request))
                throw new InvalidOperationException("场景切换正在进行，请等待当前操作完成。");
        }

        public static bool TryBegin(Request request)
        {
            if (Busy)
                return false;
            _ = ObserveFailureAsync(BeginAsync(request));
            return true;
        }

        public static Task BeginAsync(Request request)
        {
            if (Busy)
                throw new InvalidOperationException("场景切换正在进行，请等待当前操作完成。");
            if (beginTransition == null)
                throw new InvalidOperationException("应用流程未配置：场景必须绑定 ApplicationSceneEntry 与 UI 根。");
            bool previousBusy = Busy, previousReady = GameReady;
            var previousPending = Pending;
            Busy = true;
            GameReady = false;
            Pending = request;
            LastFailure = null;
            try
            {
                return CurrentTransition = TrackAsync(beginTransition(request), ++generation);
            }
            catch
            {
                Busy = previousBusy;
                GameReady = previousReady;
                Pending = previousPending;
                throw;
            }
        }

        static async Task TrackAsync(Task operation, long token)
        {
            try
            {
                await operation;
                if (token == generation && Busy)
                    throw new InvalidOperationException("场景操作结束时未发布最终状态。");
            }
            catch (Exception error)
            {
                if (token == generation)
                    Fail(error);
                throw;
            }
        }

        static async Task ObserveFailureAsync(Task operation)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        internal static void Fail(Exception error)
        {
            LastFailure = error;
            GameReady = Busy = false;
            Pending = null;
            MenuMessage = "加载失败：" + error.Message;
        }

        internal static void EnterGame()
        {
            GameReady = true;
            Busy = false;
            Pending = null;
        }

        internal static void PrepareMenu(string message)
        {
            GameReady = false;
            Busy = true;
            Pending = null;
            MenuMessage = message;
        }

        internal static void EnterMenu(string message = null)
        {
            GameReady = Busy = false;
            Pending = null;
            if (message != null)
                MenuMessage = message;
        }

        public static string TakeMenuMessage()
        {
            var value = MenuMessage;
            MenuMessage = null;
            return value;
        }
    }
}
