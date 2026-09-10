using System;
using System.Collections;
using Landsong.ECS.Persistence;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed class EcsLoadingScreen : MonoBehaviour
    {
        public Text Status;
        public Slider Progress;
        public Button CancelButton;
        [Min(10)] public float TimeoutSeconds = 120;
        public bool Failed { get; private set; }
        bool cancelled;
        string error;
        public void Cancel() => cancelled = true;
        IEnumerator Start()
        {
            CancelButton.onClick.AddListener(Cancel);
            var sequence = Load();
            // Drive the iterator here so exceptions after yields become a recoverable loading error.
            while (true)
            {
                object current;
                try { if (!sequence.MoveNext()) yield break; current = sequence.Current; }
                catch (Exception exception) { error = exception.Message; break; }
                yield return current;
            }
            (sequence as IDisposable)?.Dispose();
            Failed = true; Status.text = "加载失败：" + error + "\n正在释放地图…"; CancelButton.interactable = false;
            var game = SceneManager.GetSceneByPath(EcsSceneFlow.Game);
            if (game.IsValid() && game.isLoaded) yield return SceneManager.UnloadSceneAsync(game);
            // Unity scene ownership + SimulationLifetimeSystem release runtime entities.
            while (!Released()) yield return null;
            Status.text = "加载失败：" + error + "\n原存档没有修改。";
            CancelButton.GetComponentInChildren<Text>().text = "返回主菜单";
            CancelButton.interactable = true;
            cancelled = false;
            while (!cancelled) yield return null;
            EcsSceneFlow.EnterMenu("上次加载未完成：" + error);
            SceneManager.LoadSceneAsync(EcsSceneFlow.Menu);
        }
        IEnumerator Load()
        {
            var request = EcsSceneFlow.Pending;
            Stage("正在释放上一场景…", 0);
            var deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!Released()) { CheckDeadline(deadline); yield return null; }
            if (request == null || cancelled) { OpenMenu(); yield break; }
            Stage("正在加载运行场景…", .1f);
            var operation = SceneManager.LoadSceneAsync(EcsSceneFlow.Game, LoadSceneMode.Additive);
            while (!operation.isDone) { Progress.value = .1f + operation.progress * .3f; yield return null; }
            var host = FindFirstObjectByType<EcsGameHost>();
            if (host == null) throw new InvalidOperationException("Game 场景缺少 EcsGameHost。");
            if (cancelled) { yield return SceneManager.UnloadSceneAsync(EcsSceneFlow.Game); while (!Released()) yield return null; OpenMenu(); yield break; }
            Stage("正在加载地图实体与渲染资源…", .4f);
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) throw new InvalidOperationException("ECS World 不可用。");
            var sceneEntity = host.LoadMap(request.MapId, world);
            deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            while (!SceneSystem.IsSceneLoaded(world.Unmanaged, sceneEntity))
            {
                if (cancelled) break;
                CheckDeadline(deadline); yield return null;
            }
            if (cancelled) { yield return SceneManager.UnloadSceneAsync(EcsSceneFlow.Game); while (!Released()) yield return null; OpenMenu(); yield break; }
            Stage("正在初始化王朝…", .75f);
            var em = world.EntityManager;
            Entity root;
            while ((root = Sim.Root(em)) == Entity.Null || !em.HasComponent<SimulationReady>(root)) { if (cancelled) break; CheckDeadline(deadline); yield return null; }
            if (cancelled) { yield return SceneManager.UnloadSceneAsync(EcsSceneFlow.Game); while (!Released()) yield return null; OpenMenu(); yield break; }
            if (em.GetComponentData<MapIdentity>(root).Id.ToString() != request.MapId) throw new InvalidOperationException("加载的地图与请求不一致。");
            if (request.Archive != null)
            {
                Stage("正在恢复王朝与回退节点…", .85f);
                var checkpoint=world.GetOrCreateSystemManaged<CheckpointSystem>();if(request.Store!=null)checkpoint.Store=request.Store;
                var data=request.Archive.Copy();
                if(request.Slot!=null)
                {
                    // Cold slot loading keeps knowledge earned later in this same night, like in-game loading.
                    try {var latest=(checkpoint.Store??CheckpointSystem.DefaultStore).Read(data.RunId,out _);if(latest.Recovery.Turn==data.Recovery.Turn){var known=Math.Max(latest.Recovery.KnownIntel,data.Recovery.KnownIntel);if(latest.Recovery.LossCount>data.Recovery.LossCount)data.Recovery=latest.Recovery;data.Recovery.KnownIntel=known;data.Recovery.AwaitingDecision=0;}}
                    catch(Exception error) when(error is System.IO.IOException || error is System.IO.InvalidDataException) { /* The selected slot remains independently loadable. */ }
                }
                checkpoint.Import(root,data,request.Persist);
                if(request.Persist)(checkpoint.Store??CheckpointSystem.DefaultStore).Activate(data.RunId,request.Slot);
                if (request.RecoveredBackup) Sim.Emit(em, root, EventKind.Message, "正式记录损坏，已从备份恢复；损坏原件尚未修改。");
                yield return null;
            }
            else if (request.Snapshot != null)
            {
                Stage("正在恢复白天存档…", .85f);
                var snapshot = SnapshotCodec.Decode(em, root, request.Snapshot);
                if (snapshot.Session.Phase != Phase.Day) throw new InvalidOperationException("只能从白天存档继续游戏。");
                SnapshotCodec.Restore(em, root, snapshot);
                Sim.Emit(em, root, EventKind.DayCheckpoint, default);
                yield return null; // Checkpoint system captures the restored day, not the fresh map.
            }
            else
            {
                if(!string.IsNullOrWhiteSpace(request.DynastyName)){var state=em.GetComponentData<Session>(root);state.DynastyName=RunArchiveStore.DisplayName(request.DynastyName);em.SetComponentData(root,state);}
                if(request.Persist){var checkpoint=world.GetOrCreateSystemManaged<CheckpointSystem>();if(request.Store!=null)checkpoint.Store=request.Store;checkpoint.OpenNewRun(root);}
            }
            Stage("准备完成", 1);
            host.FocusCore(em, root);
            yield return null;
            if (cancelled) { yield return SceneManager.UnloadSceneAsync(EcsSceneFlow.Game); while (!Released()) yield return null; OpenMenu(); yield break; }
            SceneManager.SetActiveScene(SceneManager.GetSceneByPath(EcsSceneFlow.Game));
            // Remove the loading EventSystem before enabling the game's, preventing duplicate input owners.
            foreach (var events in GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>()) events.gameObject.SetActive(false);
            var activeEvents = UnityEngine.EventSystems.EventSystem.current;
            if (activeEvents != null && activeEvents.gameObject.scene == gameObject.scene) activeEvents.gameObject.SetActive(false);
            foreach (var go in gameObject.scene.GetRootGameObjects())
                foreach (var listener in go.GetComponentsInChildren<AudioListener>()) listener.enabled = false;
            EcsSceneFlow.EnterGame(); host.SetVisible(true);
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }
        void OpenMenu() { EcsSceneFlow.EnterMenu(); SceneManager.LoadSceneAsync(EcsSceneFlow.Menu); }
        void Stage(string text, float progress) { Status.text = text; Progress.value = progress; }
        static bool Released()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return true;
            var em = world.EntityManager;
            using var roots = Sim.Entities<Session>(em);
            using var owned = Sim.Entities<SimulationOwner>(em);
            return roots.Length == 0 && owned.Length == 0;
        }
        static void CheckDeadline(float deadline) { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("地图加载超时，请检查 SubScene 烘焙和资源引用。"); }
    }
}
