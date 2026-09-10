#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Presentation
{
    // Explicit development test. Only in-memory snapshots; never reads/writes/deletes a dynasty save.
    public sealed partial class EcsPlayerSmoke : MonoBehaviour
    {
        public Action<bool, string> Completed;
        string errors = "";
        bool finished;
        public bool InterfaceOnly;
        public bool HudPanelsOnly;
        public bool GarrisonOnly;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (Application.isEditor || !Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), "-landsong-ecs-smoke") < 0) return;
            var runner = new GameObject("ECS Development Player Verification");
            DontDestroyOnLoad(runner); runner.AddComponent<EcsPlayerSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors += message + "\n";
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (!finished && stack.Count > 0)
            {
                object current = null;
                try
                {
                    var sequence = stack.Peek();
                    if (!sequence.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    current = sequence.Current;
                    if (current is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception error)
                {
                    while(stack.Count>0)try{(stack.Pop() as IDisposable)?.Dispose();}catch(Exception cleanup){Debug.LogWarning("Owned smoke cleanup: "+cleanup.Message);}
                    Finish(false, error.ToString()); break;
                }
                yield return current;
            }
        }
        IEnumerator Run()
        {
            Require(FindFirstObjectByType<EcsBootScreen>() != null, "Boot is a separate splash scene");
            yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "Boot to Start");
            yield return null; // Menu.Start binds the buttons.
            var menu = FindFirstObjectByType<EcsMainMenu>();
            Require(SceneManager.GetActiveScene().path == EcsSceneFlow.Menu && menu.Catalog.Maps.Length == 2, "Start UGUI and two-map selection");
            Require(Released(), "Menu has no gameplay authority");
            yield return MainMenuUi(menu);
            menu.StartButton.onClick.Invoke();
            menu.MapSelection.Show(); yield return null;
            Require(menu.MapSelection.IsExpanded && menu.GetComponentInParent<Canvas>().GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == menu.MapSelection.options[0].text), "TMP map dropdown opens with readable options");
            menu.MapSelection.Hide(); yield return new WaitForSecondsRealtime(.2f);
            menu.CloseManagement();
            RequireNoLegacyManagers();
            var ids = Array.ConvertAll(menu.Catalog.Maps, m => m.Id);
            Require(Array.IndexOf(ids, "Map_Test2") >= 0, "Workflow acceptance map Map_Test2 is registered");
            for (var map = 0; map < ids.Length; map++)
            {
                // User-designated workflow acceptance map; Map_Test01 remains available for sandbox tests.
                if (ids[map] != "Map_Test2") continue;
                menu = FindFirstObjectByType<EcsMainMenu>(); menu.MapSelection.value = map;
                EcsSceneFlow.Begin(new EcsSceneFlow.Request(ids[map])); // Explicit non-persistent verification run.
                yield return WaitFor(() => EcsSceneFlow.GameReady, "new game " + ids[map], true);
                var em = World.DefaultGameObjectInjectionWorld.EntityManager; var root = Sim.Root(em);
                Require(SceneManager.GetActiveScene().path == EcsSceneFlow.Game && em.GetComponentData<MapIdentity>(root).Id.ToString() == ids[map], "Common Game scene loads selected map " + ids[map]);
                var view = FindFirstObjectByType<EcsGameView>();
                Require(view != null && view.Camera.isActiveAndEnabled && view.PrimaryRows != null && view.SecondaryRows != null, "Game camera and native UI bindings");
                RequireNoLegacyManagers();
                using (var roots = Sim.Entities<Session>(em)) Require(roots.Length == 1, "Exactly one simulation root");
                using (var owned = Sim.Entities<SimulationOwner>(em)) foreach (var e in owned) Require(em.GetComponentData<SimulationOwner>(e).Root == root, "Runtime owner belongs to current map", false);
                var settings = em.GetComponentData<GameSettings>(root);
                var session = em.GetComponentData<Session>(root); session.NightKind = NightKind.Peaceful; session.NightDuration = 10; em.SetComponentData(root, session);
                em.GetBuffer<NightWave>(root).Clear();
                yield return null;
                if(GarrisonOnly){yield return BuildingDetailsUi(view,em,root);yield return GarrisonUi(view,em,root);Finish(errors.Length==0,errors.Length==0?"Building output, garrison recruitment/assignment and soldier detail UI":errors);yield break;}
                if(HudPanelsOnly){yield return HudPanelsUi(view,em,root);Finish(errors.Length==0,errors.Length==0?"Research HUD, dismissible panels, royal family, requests and portrait customization UI":errors);yield break;}
                if(InterfaceOnly){yield return PauseMenuUi(view,em,root);yield return InterfaceUi(view,em,root);Finish(errors.Length==0,errors.Length==0?"Pause and 14 targeted UI workflow only":errors);yield break;}
                yield return BuildingCatalogUi(view, em, root);
                yield return CourtUi(view, em, root, ids[map]);
                yield return NightUi(view, em, root, ids[map]);
                yield return SoldierUi(view, em, root, ids[map]);
                yield return HeroUi(view, em, root, ids[map]);
                yield return CombatUi(view, em, root, ids[map]);
                yield return IntelligenceUi(view, em, root, ids[map]);
                yield return PeacefulUi(view, em, root, ids[map]);
                yield return PauseMenuUi(view, em, root);
                yield return InterfaceUi(view, em, root);
                yield return PresentationUi(view, em, root);
                yield return InvitationExpeditionUi(view, em, root, ids[map]);
                // Older UI fixtures test mechanics after permission acquisition, not the onboarding gates.
                foreach (var permission in new[] { "feature.Inventory", "feature.Building" }) Sim.Grant(em, root, Sim.FindDefinition(em, root, new Unity.Collections.FixedString128Bytes(permission)));
                yield return InventoryUi(view, em, root, settings.Gold, ids[map]);
                yield return WorkforceUi(view, em, root, ids[map]);
                yield return TechnologyUi(view, em, root, ids[map]);
                yield return QuestUi(view, em, root, ids[map]);
                Require(!view.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Text>(true).Any()
                    && !view.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.InputField>(true).Any()
                    && !view.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Dropdown>(true).Any(), "All exercised dynamic panels use TMP, including inventory, military, technology and quest controls");
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = settings.Gold, Amount = 5 });
                var beforeReview = SnapshotCodec.Capture(em, root);
                yield return null; // View.Update binds the current root before real UGUI button dispatch.
                view.OpenPanel("库存");
                yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.StartsWith("经济总览")), "economy entry in inventory UI");
                ClickRow(view, "经济总览 · 最近账本 / 白天预测");
                yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.StartsWith("切换：白天参考预测")), "actual economy panel");
                ClickRow(view, "切换：白天参考预测");
                yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.StartsWith("刷新预测")), "forecast panel");
                ClickRow(view, "刷新预测（不扣资源，不推进回合）");
                yield return WaitFor(() => em.HasComponent<EconomyForecastState>(root) && !em.GetComponentData<EconomyForecastState>(root).Fingerprint.IsEmpty, "forecast command execution");
                Require(beforeReview.SequenceEqual(SnapshotCodec.Capture(em, root)), "Real UGUI forecast preserves day resources and RNG");
                yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.Contains("条件核对通过")), "forecast read model displayed");
                view.Advance.onClick.Invoke();
                yield return WaitFor(() => view.Panel == "入夜确认", "post-settlement confirmation UI");
                Require(view.PrimaryRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.Contains("尚未扣款")), "UGUI explains non-mutating settlement preview");
                ClickRow(view, "返回整理库存");
                Require(view.Panel == "库存" && beforeReview.SequenceEqual(SnapshotCodec.Capture(em, root)), "UGUI cancel preserves exact day, RNG and settlement costs");
                view.Advance.onClick.Invoke();
                yield return WaitFor(() => view.Panel == "入夜确认", "reopen night review");
                var staleToken = em.GetComponentData<NightEntryReview>(root).Token;
                var changedPool = em.GetBuffer<PendingItem>(root); var changedPending = changedPool[0]; changedPending.Amount++; changedPool[0] = changedPending;
                ClickRow(view, "确认放弃以上物资与士兵并入夜");
                yield return WaitFor(() => view.Panel == "入夜确认" && em.GetComponentData<NightEntryReview>(root).Token != staleToken, "stale confirmation refreshes UI");
                Require(em.GetComponentData<Session>(root).Phase == Phase.Day && InventoryOps.PendingCount(em, root, settings.Gold) == 6, "Stale UGUI consent cannot discard changed resources");
                ClickRow(view, "确认放弃以上物资与士兵并入夜");
                yield return WaitFor(() => em.GetComponentData<Session>(root).Turn == 2, "day/night/dawn");
                Require(em.GetComponentData<Session>(root).Phase == Phase.Day, "Dawn returns to day");
                var discarded = false; foreach (var entry in em.GetBuffer<EconomyEntry>(root)) if (entry.Reason == EconomyReason.NightDiscard && entry.Pending == 1) discarded = true;
                Require(discarded, "Committed economy journal includes reviewed night discard");
                var bytes = SnapshotCodec.Capture(em, root);
                var menuArchive = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<CheckpointSystem>().Export(root);
                Require(SnapshotCodec.ReadMapId(bytes) == ids[map], "Snapshot routing header");
                view.PauseMenu.OpenButton.onClick.Invoke(); view.PauseMenu.MenuButton.onClick.Invoke(); view.PauseMenu.ConfirmButton.onClick.Invoke();
                yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "return to Start");
                yield return null;
                Require(Released() && FindFirstObjectByType<EcsBootScreen>() == null, "Return releases entities without replaying Boot");
                RequirePresentationReleased();
                yield return MainMenuArchivesUi(FindFirstObjectByType<EcsMainMenu>(), menuArchive);
                EcsSceneFlow.Begin(new EcsSceneFlow.Request(ids[map], bytes));
                yield return WaitFor(() => EcsSceneFlow.GameReady, "restore via loading screen", true);
                em = World.DefaultGameObjectInjectionWorld.EntityManager; root = Sim.Root(em);
                Require(em.GetComponentData<Session>(root).Turn == 2 && em.GetComponentData<Session>(root).Phase == Phase.Day, "Loading restores saved turn before enabling UI");
                // Exercise a complete archive through unloading/rebaking, not only same-world rewind.
                var checkpoints = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<CheckpointSystem>();
                settings = em.GetComponentData<GameSettings>(root); settings.InvasionChance = 1; settings.FirstInvasion = 1; settings.FirstBoss = 99999; em.SetComponentData(root, settings);
                Sim.Set(em, root, new NightPlanState { BossDefinition = -1 }); NightOps.Plan(em, root, false); Sim.Emit(em, root, EventKind.DayCheckpoint, default); checkpoints.Update();
                Require(NightOps.Begin(em, root, true) == ResultCode.Success, "Recovery fixture reaches dusk"); checkpoints.Update();
                session = em.GetComponentData<Session>(root); session.Phase = Phase.Night; session.CheckpointPending = 0; em.SetComponentData(root, session);
                using (var buildings = Sim.Entities<Building>(em)) foreach (var building in buildings) if (em.GetComponentData<BuildingStats>(building).IsCore != 0)
                    CombatOps.ApplyDamage(em, root, new DamageRequest { Target = building, Amount = float.MaxValue });
                checkpoints.Update();
                var archive = RunArchiveCodec.Decode(RunArchiveCodec.Encode(checkpoints.Export(root)));
                Require(archive.Recovery.AwaitingDecision != 0, "Core loss has durable pending-choice representation");
                var liveBefore = SnapshotCodec.Capture(em, root); var originalSession = em.GetComponentData<Session>(root);
                Entity originalCore = Entity.Null;
                using (var buildings = Sim.Entities<Building>(em)) foreach (var building in buildings) if (em.GetComponentData<BuildingStats>(building).IsCore != 0) originalCore = building;
                var rollbackReached = false;
                try { SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, archive.Day), probe: step => { if (step == "root-published") throw new System.IO.IOException("Owned smoke rollback probe"); }); }
                catch (System.IO.IOException) { rollbackReached = true; }
                Require(rollbackReached && em.Exists(originalCore) && originalSession.Equals(em.GetComponentData<Session>(root)) && liveBefore.SequenceEqual(SnapshotCodec.Capture(em, root)), "Real rendered world survives failure after root publication with original entities intact");
                EcsSceneFlow.ReturnToMenu();
                yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "return with pending core loss");
                yield return null;
                EcsSceneFlow.Begin(new EcsSceneFlow.Request(ids[map], archive: archive));
                yield return WaitFor(() => EcsSceneFlow.GameReady, "cold archive recovery", true);
                em = World.DefaultGameObjectInjectionWorld.EntityManager; root = Sim.Root(em);
                Require(em.GetComponentData<Session>(root).Phase == Phase.GameOver && em.GetComponentData<RecoveryState>(root).Seed == archive.Recovery.Seed, "Fresh world restores exact pending decision and seed");
                yield return WaitFor(() => FindFirstObjectByType<EcsGameView>().Panel == "王朝终局", "game-over panel");
                em.GetBuffer<Command>(root).Add(new Command { Kind = CommandKind.RetryDay });
                yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Day, "day recovery command");
                yield return WaitFor(() => FindFirstObjectByType<EcsGameView>().Panel == "建筑", "recovery closes obsolete game-over UI");
                Require(!FindFirstObjectByType<EcsGameView>().Message.text.Contains("聚落核心失守"), "Recovery clears withdrawn loss message");
                Require(em.GetComponentData<Session>(root).Turn == 2 && em.GetComponentData<RecoveryState>(root).LossCount == 1, "Recovery retains turn and assistance outside rewindable state");
                checkpoints = World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<CheckpointSystem>();
                var recovered = RunArchiveCodec.Decode(RunArchiveCodec.Encode(checkpoints.Export(root)));
                var lockedAfterRecovery = SnapshotCodec.Capture(em, root);
                checkpoints.Import(root, recovered, false);
                Require(lockedAfterRecovery.SequenceEqual(SnapshotCodec.Capture(em, root)), "Continuing recovered day preserves locked plan and all day state without reroll");
                EcsSceneFlow.ReturnToMenu();
                yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "return after restore");
                yield return null;
                Require(Released(), "Restored map fully released");
                RequirePresentationReleased();
            }
            yield return FontReloadProbe();
            EcsSceneFlow.Begin(new EcsSceneFlow.Request("missing-map-test"));
            yield return WaitFor(() => FindFirstObjectByType<EcsLoadingScreen>()?.Failed == true && FindFirstObjectByType<EcsLoadingScreen>().CancelButton.interactable, "invalid map recovery");
            Require(Released() && !EcsSceneFlow.GameReady, "Failed load leaves no simulation or interactive Game");
            FindFirstObjectByType<EcsLoadingScreen>().CancelButton.onClick.Invoke();
            yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "error to Start");
            yield return null;
            EcsSceneFlow.Begin(new EcsSceneFlow.Request(ids[0], new byte[] { 0 }));
            yield return WaitFor(() => FindFirstObjectByType<EcsLoadingScreen>()?.Failed == true && FindFirstObjectByType<EcsLoadingScreen>().CancelButton.interactable, "corrupt snapshot recovery");
            Require(Released(), "Corrupt snapshot cleanup");
            FindFirstObjectByType<EcsLoadingScreen>().CancelButton.onClick.Invoke();
            yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "corrupt snapshot to Start");
            yield return null;
            EcsSceneFlow.Begin(new EcsSceneFlow.Request(ids[0]));
            yield return WaitFor(() => FindFirstObjectByType<EcsLoadingScreen>() != null, "cancel loading");
            FindFirstObjectByType<EcsLoadingScreen>().Cancel();
            yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "cancel to Start");
            Require(Released() && !EcsSceneFlow.GameReady, "Cancellation releases the map");
            Finish(errors.Length == 0, errors.Length == 0 ? "Four scenes + Map_Test2 workflow + military-A military quantity/recruit/name/real-slot/dual-scroll/unassign/fill/cancel-dismiss/restore/actual DBP patrol and garrison recall + night-planning real entry/DBP navigation/moon-only/pause/ten-second celebration/report-open/confirm/dawn/restore + court social task/recruit/paid appointment/underage crown/cancel execution/policy/court intel/restore + invitations-expeditions permission gates/invitation source/filter/recruit/cancel/expedition preview/dispatch/abandon/claim/restore + real UGUI quest tracking/unpin/accept/quantity submit/cancel/reward/restore + technology license/graph/drag/zoom/search/path preview/cancel/confirmation/retained progress/rewards/restore + workforce slider/ticks/recruitment/sources/repair preview + inventory drag/split/pending/discard/cancel/stale consent/sort + economy forecast without mutation + journal + settlement review/cancel/stale consent + day/night + live restore rollback + snapshot and cold archive recovery + persistent pending choice + repeat entry + invalid map + corrupt snapshot + cancellation + cleanup" : errors);
        }
        static IEnumerator TechnologyUi(EcsGameView view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root); var catalog = view.BuildingCatalog;
            Require(view.TechnologyButton != null && !view.TechnologyButton.gameObject.activeSelf && !ResearchOps.Unlocked(em, root), "Technology HUD hidden before mainline license");
            var first = catalog.Find("TN_3_1_启蒙"); var wood = catalog.Find("TN_4_2_木工术"); var future = catalog.Find("TN_3_18_未来");
            view.OpenPanel("科技"); Require(view.Panel != "科技", "Direct panel entry cannot bypass technology license");
            var quest = Array.FindIndex(catalog.Definitions, d => d.Data.Kind == ContentKind.Quest && d.Data.Rules.Any(r => r.Kind == RuleKind.RewardFeature && r.Target == ResearchOps.FeatureId));
            Require(quest >= 0 && ProgressionOps.Reward(em, root, quest), "Actual mainline reward grants technology access");
            yield return WaitFor(() => view.TechnologyButton.gameObject.activeSelf, "technology HUD appears after entitlement");
            view.TechnologyButton.onClick.Invoke();
            yield return WaitFor(() => view.TechnologyTree != null && view.TechnologyTree.gameObject.activeInHierarchy && view.TechnologyTree.NodeCount == 56, "actual UGUI technology graph");
            var tree = view.TechnologyTree; Require(tree.EdgeCount > 55 && tree.GraphScroll.horizontal && tree.GraphScroll.vertical, "Full prerequisite connections and two-axis scrolling");
            Require(tree.Selected == first, "First opening focuses the available root instead of catalog order");
            var graphWidth = tree.GraphScroll.content.rect.width;
            tree.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text == "＋").onClick.Invoke();
            Require(tree.GraphScroll.content.rect.width > graphWidth, "Technology zoom changes graph and node layout");
            tree.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text == "－").onClick.Invoke();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = new Vector2(Screen.width / 2f, Screen.height / 2f) };
            var beforeDrag = tree.GraphScroll.content.anchoredPosition;
            ExecuteEvents.Execute(tree.GraphScroll.gameObject, pointer, ExecuteEvents.beginDragHandler); pointer.position += new Vector2(-100, 0); ExecuteEvents.Execute(tree.GraphScroll.gameObject, pointer, ExecuteEvents.dragHandler); ExecuteEvents.Execute(tree.GraphScroll.gameObject, pointer, ExecuteEvents.endDragHandler);
            Require(tree.GraphScroll.content.anchoredPosition != beforeDrag, "Real EventSystem drag pans technology viewport"); tree.GraphScroll.StopMovement();
            tree.NodeButton(wood).onClick.Invoke();
            yield return WaitFor(() => tree.Selected == wood && HasRow(tree.DetailRows, "规划至此科技（预览前置路径）"), "locked node offers prerequisite path");
            Require(!HasRow(tree.DetailRows, "加入研究队尾"), "Unavailable single enqueue is disabled");
            var before = SnapshotCodec.Capture(em, root); ClickIn(tree.DetailRows, "规划至此科技（预览前置路径）");
            Require(view.BuildingConfirmPanel.activeSelf && view.BuildingConfirmRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.Contains("1. 启蒙")), "Research replacement modal previews ordered prerequisite");
            ClickIn(view.BuildingConfirmRows, "取消"); Require(before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Cancel path preview changes no simulation state");
            ClickIn(tree.DetailRows, "规划至此科技（预览前置路径）"); ClickIn(view.BuildingConfirmRows, "确认");
            yield return WaitFor(() => ResearchOps.Queue(em, root).Count == 2, "confirmed path command reaches ECS");
            Require(ResearchOps.Queue(em, root).Select(r => r.Definition).SequenceEqual(new[] { first, wood }), "UI plan follows prerequisites and keeps original point balance");
            var s = em.GetComponentData<Session>(root); s.ResearchPoints = 2; em.SetComponentData(root, s); ResearchOps.Settle(em, root);
            tree.NodeButton(first).onClick.Invoke(); yield return WaitFor(() => tree.Selected == first && HasRow(tree.DetailRows, "取消该项排队（保留进度）"), "current research detail actions");
            ClickIn(tree.DetailRows, "取消该项排队（保留进度）");
            yield return WaitFor(() => ResearchOps.Entry(em, root, first).QueueOrder == 0, "cancel research through UGUI");
            Require(ResearchOps.Entry(em, root, first).Progress == 2, "UI cancellation preserves invested progress");
            tree.Search.text = "未来"; yield return WaitFor(() => tree.Selected == future, "search selects matching far node");
            Require(tree.GraphScroll.horizontalNormalizedPosition > .8f, "Search scrolls to far-right technology");
            tree.NodeButton(wood).onClick.Invoke(); tree.Focus(wood);
            yield return WaitFor(() => tree.Selected == wood && HasRow(tree.DetailRows, "规划至此科技（预览前置路径）"), "return to woodwork");
            ClickIn(tree.DetailRows, "规划至此科技（预览前置路径）"); ClickIn(view.BuildingConfirmRows, "确认");
            yield return WaitFor(() => ResearchOps.Queue(em, root).Count == 2, "rebuild partial research path");
            s = em.GetComponentData<Session>(root); s.ResearchPoints = 20; em.SetComponentData(root, s); ResearchOps.Settle(em, root);
            yield return WaitFor(() => tree.NodeButton(wood).GetComponentInChildren<TMPro.TextMeshProUGUI>().text.Contains("已完成"), "completion changes graph node state");
            Require(tree.DetailRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.Contains("已领取")) && HasRow(tree.DetailRows, "前往建造目录 · 木材加工厂"), "Completed reward detail exposes building unlock feedback");
            tree.Search.SetTextWithoutNotify(""); tree.Focus(wood);
            if (Application.isEditor) { yield return new WaitForSecondsRealtime(.3f); ScreenCapture.CaptureScreenshot("Library/LandsongEcs/technology-technology-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            ClickIn(tree.DetailRows, "前往建造目录 · 木材加工厂"); Require(view.Panel == "建筑" && !tree.gameObject.activeSelf, "Blueprint link returns to building menu");
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            yield return WaitFor(() => !view.TechnologyButton.gameObject.activeSelf, "restored earlier node hides technology entry");
            Require(original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Technology UI fixture restores complete original day");
        }
        static IEnumerator WorkforceUi(EcsGameView view, EntityManager em, Entity root, string map)
        { yield return BuildingDetailsUi(view,em,root); }
        static IEnumerator QuestUi(EcsGameView view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root); view.OpenPanel("建筑");
            yield return WaitFor(() => view.QuestHudRows != null && view.QuestHudRows.gameObject.activeInHierarchy, "automatic quest tracking HUD");
            Require(QuestOps.Tracking(em, root).Target != 0, "Active mainline automatically tracked in actual game");
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/quests-hud-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            ClickIn(view.QuestHudRows, "取消追踪");
            yield return WaitFor(() => QuestOps.Tracking(em, root).Mode == 2, "HUD unpin command");
            yield return WaitFor(() => HasRow(view.QuestHudRows, "恢复自动追踪"), "unpinned HUD recovery action");
            ClickIn(view.QuestHudRows, "恢复自动追踪");
            yield return WaitFor(() => QuestOps.Tracking(em, root).Mode == 0, "HUD automatic tracking command");
            var core = Entity.Null; using (var all = Sim.OrderedEntities<Building>(em)) foreach (var e in all) if (em.GetComponentData<BuildingStats>(e).IsCore != 0) core = e;
            var mainId=QuestOps.Tracking(em,root).Target;view.SelectQuest(mainId);
            yield return WaitFor(()=>view.FindQuestCard(mainId)!=null && view.FindQuestCard(mainId).Body.gameObject.activeSelf,"Mainline expands inside palace slot");
            Require(em.GetComponentData<Quest>(Sim.Find(em,mainId)).Container==em.GetComponentData<Identity>(core).Id,"Mainline slot binds the actual palace");
            Require(em.GetComponentData<Quest>(Sim.Find(em,mainId)).Status==QuestStatus.Completed || !view.FindQuestCard(mainId).Action.gameObject.activeSelf,"Active mainline has no abandon action");
            if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/quests-mainline.png");yield return new WaitForEndOfFrame();}
            var definition = -1; var content = em.GetComponentData<ContentCatalog>(root).Value; for (var i = 0; i < content.Value.Definitions.Length; i++) if (content.Value.Definitions[i].Id.ToString() == "random_exploration_supplies") definition = i;
            var offeredIds=new ulong[3];var offerNames=new[]{"random_supply_wood","random_supply_stone","random_supply_soil"};
            var marketDefinition=Sim.FindDefinition(em,root,new Unity.Collections.FixedString128Bytes("b市场"));
            for(var fixture=0;fixture<3;fixture++)
            {
                var grid=em.GetComponentData<GridData>(root);Entity market=Entity.Null;
                for(var cellIndex=0;cellIndex<grid.Value.Value.Cells.Length;cellIndex++)
                {
                    var cell=grid.Value.Value.Min+new Unity.Mathematics.int2(cellIndex%grid.Value.Value.Size.x,cellIndex/grid.Value.Value.Size.x);
                    if(!GridOps.CanPlace(em,root,marketDefinition,cell,0))continue;
                    market=BuildingOps.Create(em,root,marketDefinition,cell,0,1,true);break;
                }
                Require(market!=Entity.Null,"Invitation sorting fixture has valid market footprint");
                var offerDefinition=Sim.FindDefinition(em,root,new Unity.Collections.FixedString128Bytes(offerNames[fixture]));
                var offer=ProgressionOps.CreateQuest(em,root,offerDefinition,em.GetComponentData<Identity>(market).Id,0);offeredIds[fixture]=em.GetComponentData<Identity>(offer).Id;
            }
            view.OpenInvitations();yield return WaitFor(()=>view.FindQuestCard(offeredIds[2])!=null,"All invitation values appear under their sources");
            Require(view.FindQuestCard(offeredIds[2]).Rect.GetSiblingIndex()<view.FindQuestCard(offeredIds[1]).Rect.GetSiblingIndex() && view.FindQuestCard(offeredIds[1]).Rect.GetSiblingIndex()<view.FindQuestCard(offeredIds[0]).Rect.GetSiblingIndex(),"Actual invitation list sorts values 45, 16, 15 descending");
            for(var type=0;type<4;type++)view.QuestTypeToggle(type).isOn=false;
            yield return new WaitForSecondsRealtime(.4f);Require(view.QuestPoolRows.Cast<Transform>().All(t=>!t.gameObject.activeSelf),"No checked category displays no invitations or empty slots");
            view.QuestTypeToggle(0).isOn=true;view.QuestTypeToggle(3).isOn=true;
            yield return new WaitForSecondsRealtime(.4f);Require(view.QuestTypeToggle(0).isOn && view.QuestTypeToggle(3).isOn && !view.QuestTypeToggle(1).isOn && view.FindQuestCard(offeredIds[2])!=null,"Category toggles remain independently selected across refresh");
            view.QuestTypeToggle(0).isOn=false;yield return new WaitForSecondsRealtime(.4f);Require(view.FindQuestCard(offeredIds[2])==null,"Unchecking trade hides trade invitations");
            view.QuestTypeToggle(0).isOn=true;
            if(Application.isEditor){yield return new WaitForSecondsRealtime(.4f);ScreenCapture.CaptureScreenshot("Library/LandsongEcs/quest-invitation-toggles.png");yield return new WaitForEndOfFrame();}
            var task = ProgressionOps.CreateQuest(em, root, definition, em.GetComponentData<Identity>(core).Id); var id = em.GetComponentData<Identity>(task).Id;
            view.SelectQuest(id);
            yield return WaitFor(() => view.QuestDetailRows != null && HasRow(view.QuestDetailRows, "签约"), "two-column task offer and detail UI");
            Require(view.QuestWindow.activeSelf && !view.PrimaryRows.gameObject.activeInHierarchy, "Quest window replaces primary list without overlapping it");
            ClickIn(view.QuestDetailRows, "签约"); Require(view.BuildingConfirmPanel.activeSelf, "Accept displays confirmation and deadline warning"); ClickIn(view.BuildingConfirmRows, "取消");
            Require(em.GetComponentData<Quest>(task).Status == QuestStatus.Offered, "Cancelled acceptance leaves offer unchanged");
            ClickIn(view.QuestDetailRows, "签约"); ClickIn(view.BuildingConfirmRows, "确认");
            yield return WaitFor(() => em.GetComponentData<Quest>(task).Status == QuestStatus.Active, "Actual UGUI accepts task");
            yield return WaitFor(() => view.FindQuestCard(id)!=null && view.FindQuestCard(id).Rect.IsChildOf(view.QuestListRows), "Accepted invitation moves into left slot");
            var card=view.FindQuestCard(id); Require(card.Source.text.Contains("提供"),"Accepted slot displays capacity provider");
            card.Expand.onClick.Invoke(); yield return new WaitForSecondsRealtime(.4f);
            Require(!view.FindQuestCard(id).Body.gameObject.activeSelf,"Task click collapses inline details");
            view.FindQuestCard(id).Expand.onClick.Invoke(); yield return new WaitForSecondsRealtime(.4f);
            Require(view.FindQuestCard(id).Body.gameObject.activeSelf,"Task click expands inline details");
            view.FindQuestCard(id).Action.onClick.Invoke(); Require(view.BuildingConfirmPanel.activeSelf,"X asks before abandoning task"); ClickIn(view.BuildingConfirmRows,"取消");
            Require(em.Exists(task),"Abandon cancellation retains accepted task");
            view.SelectQuest(id);
            yield return WaitFor(() => view.FindQuestCard(id)!=null && view.FindQuestCard(id).Tracking.gameObject.activeSelf,"Task exposes actual tracking toggle");
            view.FindQuestCard(id).Tracking.isOn=true;
            yield return WaitFor(()=>QuestOps.Tracking(em,root).Target==id && !view.FindQuestCard(mainId).Tracking.isOn,"Checking card tracks it");
            Require(!view.FindQuestCard(mainId).Tracking.isOn,"Only current tracked card remains checked");
            view.FindQuestCard(id).Tracking.isOn=false;
            yield return WaitFor(()=>QuestOps.Tracking(em,root).Mode==2 && QuestOps.Tracking(em,root).Target==0,"Unchecking clears tracking");
            yield return new WaitForSecondsRealtime(.35f);Require(!view.FindQuestCard(id).Tracking.isOn,"Refresh does not re-enable unchecked task");
            view.FindQuestCard(id).Tracking.isOn=true;view.FindQuestCard(id).Tracking.isOn=false;
            yield return WaitFor(()=>em.GetBuffer<Command>(root).Length==0,"Rapid check and uncheck commands processed");
            yield return new WaitForSecondsRealtime(.35f);Require(QuestOps.Tracking(em,root).Mode==2 && !view.FindQuestCard(id).Tracking.isOn,"Rapid check then uncheck retains final player intent");
            view.FindQuestCard(id).Tracking.isOn=true;
            yield return WaitFor(() => QuestOps.Tracking(em, root).Target == id, "Actual UGUI manually tracks task");
            var slots = em.GetBuffer<InventorySlot>(root); for (var i = 0; i < slots.Length; i++) { var s = slots[i]; s.Item = -1; s.Count = 0; s.LossRemainder = 0; slots[i] = s; }
            QuestProgress[] progress; using (var copied = em.GetBuffer<QuestProgress>(task).ToNativeArray(Unity.Collections.Allocator.Temp)) progress = copied.ToArray();
            foreach (var p in progress) { var rule = Sim.GetRule(em, root, p.RuleIndex); Require(InventoryOps.Add(em, root, rule.Target, rule.Amount) == rule.Amount, "Quest UI fixture stocks each requested item"); }
            var first = progress[0]; view.ConfirmQuestSubmission(id, first.Key.ToString());
            Require(view.QuestAmountInput != null && view.QuestAmountInput.gameObject.activeInHierarchy, "Submission modal contains editable quantity");
            view.QuestAmountInput.text = "1"; ClickIn(view.BuildingConfirmRows, "取消");
            Require(em.GetBuffer<QuestProgress>(task)[0].Amount == 0, "Cancelled submission does not consume or progress");
            if (Application.isEditor) { view.ConfirmQuestSubmission(id, first.Key.ToString()); ScreenCapture.CaptureScreenshot("Library/LandsongEcs/quests-submit-" + map + ".png"); yield return new WaitForEndOfFrame(); ClickIn(view.BuildingConfirmRows, "取消"); }
            view.ConfirmQuestSubmission(id, first.Key.ToString()); view.QuestAmountInput.text = "1"; ClickIn(view.BuildingConfirmRows, "确认");
            yield return WaitFor(() => em.GetBuffer<QuestProgress>(task)[0].Amount == 1, "Actual quantity input submits exactly one item");
            Require(em.GetBuffer<QuestProgress>(task)[1].Amount == 0, "Submission does not batch second requirement");
            yield return new WaitForSecondsRealtime(.3f);
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/quests-quests-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            var saved = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved)); task = Sim.Find(em, id);
            Require(QuestOps.Tracking(em, root).Target == id && em.GetBuffer<QuestProgress>(task)[0].Amount == 1, "Actual UI selection state persists via ECS snapshot");
            foreach (var p in progress) { view.ConfirmQuestSubmission(id, p.Key.ToString()); ClickIn(view.BuildingConfirmRows, "确认"); yield return WaitFor(() => em.GetBuffer<Command>(root).Length == 0, "remaining UI submission"); }
            yield return WaitFor(() => view.FindQuestCard(id)!=null && view.FindQuestCard(id).Action.GetComponentInChildren<TMPro.TextMeshProUGUI>().text=="✓", "completed task replaces X with reward checkmark");
            Require(view.FindQuestCard(id).Background.color.g>view.FindQuestCard(id).Background.color.r,"Completed task uses green background");
            Require(ProgressionOps.QuestCount(em)==2,"Mainline and completed UI task both occupy shared slots");
            if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/quests-completed.png");yield return new WaitForEndOfFrame();}
            view.FindQuestCard(id).Action.onClick.Invoke(); ClickIn(view.BuildingConfirmRows, "取消"); Require(em.Exists(task), "Cancelled reward leaves completed task");
            view.ClosePanel();yield return WaitFor(()=>HasRow(view.QuestHudRows,"领取奖励"),"Completed tracked task offers direct HUD reward claim");
            Require(!HasRow(view.QuestHudRows,"查看并领取奖励"),"HUD no longer routes rewards through task panel");
            if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/quest-hud-reward.png");yield return new WaitForEndOfFrame();}
            ClickIn(view.QuestHudRows,"领取奖励");
            yield return WaitFor(() => Sim.Find(em, id) == Entity.Null, "Actual HUD claims reward once");
            Require(!view.IsPanelOpen && QuestOps.Tracking(em,root).Target==mainId,"HUD claim stays in world and automatically follows best accepted task");
            view.OpenPanel("建筑"); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            Require(original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Quest UI fixture restores complete original day");
        }
        static IEnumerator InventoryUi(EcsGameView view, EntityManager em, Entity root, int item, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++) { var s = slots[i]; s.Item = -1; s.Count = 0; s.LossRemainder = 0; slots[i] = s; }
            em.GetBuffer<PendingItem>(root).Clear();
            var keys = new List<InventorySlot>(); foreach (var s in slots) if (s.Unavailable == 0 && InventoryOps.Accepts(em, root, s.SlotType, item)) keys.Add(s);
            Require(keys.Count >= 2, "Inventory UI fixture has two compatible real slots");
            var first = keys[0]; var second = keys[1];
            first.Item = item; first.Count = 8; first.LossRemainder = .8f;
            slots[InventoryOps.SlotIndex(em, root, first.Provider, first.Index)] = first;
            view.OpenPanel("库存");
            yield return WaitFor(() => SlotView(view, first)?.Count == 8, "inventory grid binds stable keys");
            Require(SlotView(view, first).Label.text.Contains(em.GetComponentData<Identity>(Sim.Find(em, first.Provider)).Name.ToString()), "Inventory grid shows provider building name");
            DragSlot(SlotView(view, first), SlotView(view, second));
            yield return WaitFor(() => Slot(em, root, second).Count == 8 && SlotView(view, second)?.Count == 8, "real UGUI whole-stack drag command");
            Require(Slot(em, root, first).Count == 0 && Mathf.Abs(Slot(em, root, second).LossRemainder - .8f) < .0001f, "Drag preserves source/destination quantity and accrued loss");
            SlotView(view, second).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => HasRow(view.PrimaryRows, "移动/存入所选数量：选择目标格"), "selected inventory actions");
            view.PrimaryRows.GetComponentInChildren<TMPro.TMP_InputField>().text = "3";
            ClickRow(view, "移动/存入所选数量：选择目标格");
            SlotView(view, first).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => Slot(em, root, first).Count == 3 && SlotView(view, first)?.Count == 3, "quantity input and target selection split stack");
            Require(Slot(em, root, second).Count == 5 && Mathf.Abs(Slot(em, root, first).LossRemainder - .3f) < .0001f, "UGUI split applies selected amount with proportional loss");
            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = item, Amount = 4, LossRemainder = .4f });
            yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<InventorySlotView>().Any(s => s.Pending && s.Item == item), "pending pool grid");
            DragSlot(view.PrimaryRows.GetComponentsInChildren<InventorySlotView>().Single(s => s.Pending && s.Item == item), SlotView(view, first));
            yield return WaitFor(() => InventoryOps.PendingCount(em, root, item) == 0 && SlotView(view, first)?.Count == 7, "pending pool drag to explicit slot");
            Require(Mathf.Abs(Slot(em, root, first).LossRemainder - .7f) < .0001f, "Pending drag retains accrued loss");
            SlotView(view, first).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => HasRow(view.PrimaryRows, "丢弃所选数量…"), "discard action");
            var beforeDiscard = InventoryOps.Fingerprint(em, root);
            view.PrimaryRows.GetComponentInChildren<TMPro.TMP_InputField>().text = "2";
            ClickRow(view, "丢弃所选数量…");
            Require(view.BuildingConfirmPanel.activeSelf, "Discard requires visible confirmation");
            Require(view.BuildingConfirmRows.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(t => t.text.Contains("× 2 将永久损失")), "Immediate discard reads newly entered quantity without waiting for UI refresh");
            ClickIn(view.BuildingConfirmRows, "取消");
            Require(InventoryOps.Fingerprint(em, root) == beforeDiscard, "Discard cancellation is non-mutating");
            view.PrimaryRows.GetComponentInChildren<TMPro.TMP_InputField>().text = "3";
            ClickRow(view, "丢弃所选数量…");
            var changed = Slot(em, root, first); changed.Count++; changed.LossRemainder += .1f;
            var changedSlots = em.GetBuffer<InventorySlot>(root); changedSlots[InventoryOps.SlotIndex(em, root, first.Provider, first.Index)] = changed;
            ClickIn(view.BuildingConfirmRows, "确认");
            yield return WaitFor(() => em.GetBuffer<Command>(root).Length == 0, "stale discard handled");
            Require(Slot(em, root, first).Count == 8, "Changed inventory invalidates visible discard consent");
            yield return WaitFor(() => SlotView(view, first)?.Count == 8, "fresh inventory selection");
            SlotView(view, first).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return WaitFor(() => HasRow(view.PrimaryRows, "丢弃所选数量…"), "fresh discard action");
            ClickRow(view, "丢弃所选数量…"); ClickIn(view.BuildingConfirmRows, "确认");
            yield return WaitFor(() => Slot(em, root, first).Count == 5 && SlotView(view, first)?.Count == 5, "confirmed quantity discard");
            Require(Mathf.Abs(Slot(em, root, first).LossRemainder - .5f) < .0001f, "Confirmed discard removes only selected quantity and debt");
            ClickRow(view, "整理库存（合并同类，优先低损耗槽）");
            yield return WaitFor(() => em.GetBuffer<Command>(root).Length == 0, "sort button command");
            Require(InventoryOps.Count(em, root, item) == 10, "Real UGUI sort conserves stock");
            if (Application.isEditor)
            {
                yield return new WaitForSecondsRealtime(.3f);
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/inventory-inventory-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            Require(original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Inventory UI fixture restores complete original day");
        }
        static InventorySlot Slot(EntityManager em, Entity root, InventorySlot key) => em.GetBuffer<InventorySlot>(root)[InventoryOps.SlotIndex(em, root, key.Provider, key.Index)];
        static InventorySlotView SlotView(EcsGameView view, InventorySlot key) => view.PrimaryRows.GetComponentsInChildren<InventorySlotView>().FirstOrDefault(s => !s.Pending && s.Provider == key.Provider && s.Index == key.Index);
        static bool HasRow(RectTransform rows, string label) => rows != null && rows.gameObject.activeInHierarchy && rows.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b => b.gameObject.activeInHierarchy && b.interactable && b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text == label);
        static void ClickIn(RectTransform rows, string label)
        {
            var button = rows.GetComponentsInChildren<UnityEngine.UI.Button>().FirstOrDefault(b => b.interactable && b.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text == label);
            Require(button != null, "Usable confirmation row: " + label, false); button.onClick.Invoke();
        }
        static void DragSlot(InventorySlotView source, InventorySlotView target)
        {
            Require(source != null && target != null && EventSystem.current != null, "Real EventSystem drag bindings", false);
            var data = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width / 2f, Screen.height / 2f), pointerDrag = source.gameObject };
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(target.gameObject, data, ExecuteEvents.dropHandler);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.endDragHandler);
        }
        static void ClickRow(EcsGameView view, string label)
        {
            foreach (var button in view.PrimaryRows.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.interactable && button.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text == label) { button.onClick.Invoke(); return; }
            throw new InvalidOperationException("Missing usable UI row: " + label);
        }
        static IEnumerator WaitFor(Func<bool> condition, string label, bool checkGate = false)
        {
            var deadline = Time.realtimeSinceStartup + 150;
            bool sawLoading = false;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(label);
                var loading = FindFirstObjectByType<EcsLoadingScreen>();
                if (loading != null) sawLoading = true;
                if (checkGate && !EcsSceneFlow.GameReady)
                {
                    var host = FindFirstObjectByType<EcsGameHost>();
                    Require(host == null || !host.Visible, "Input and presentation gated while loading", false);
                    if (loading != null && loading.Failed) throw new InvalidOperationException(loading.Status.text);
                }
                yield return null;
            }
            if (checkGate) Require(sawLoading, "Dedicated LoadingTransition used");
        }
        static bool Released()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return true;
            using var roots = Sim.Entities<Session>(world.EntityManager);
            using var owned = Sim.Entities<SimulationOwner>(world.EntityManager);
            return roots.Length == 0 && owned.Length == 0;
        }
        static void RequireNoLegacyManagers()
        {
            var forbidden = new[] { "GameSystem", "DataManager", "IOManager", "AudioPlayer", "LSDebugManager", "AppManager" };
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Require(Array.IndexOf(forbidden, behaviour.GetType().Name) < 0, "Legacy manager must not start: " + behaviour.GetType().Name, false);
            Require(Array.FindAll(FindObjectsByType<AudioListener>(FindObjectsSortMode.None), listener => listener.isActiveAndEnabled).Length == 1, "Exactly one active presentation audio listener");
        }
        static void Require(bool passed, string label, bool log = true)
        {
            if (!passed) throw new InvalidOperationException(label);
            if (log) Debug.Log("[ECS PLAYER CHECK] " + label);
        }
        void Finish(bool passed, string detail)
        {
            if(passed&&!InterfaceOnly&&!HudPanelsOnly&&!GarrisonOnly)detail="15 shared audio buses/cue caps/pause/BGM/TMP language/input-name isolation/family/portrait/policy/model/FX ownership/unload + "+detail;
            if (passed && !InterfaceOnly && !HudPanelsOnly && !GarrisonOnly) detail = "12 intelligence button / unread / mode isolation / running combat / pause / exit / manual knowledge + three identical font reload probes + " + detail;
            if (passed && !InterfaceOnly && !HudPanelsOnly && !GarrisonOnly) detail = "13 peaceful TMP markers / actual DBP interception / pause / no combat XP / special auto-collection / complete report / committed history + " + detail;
            if (passed && !InterfaceOnly && !HudPanelsOnly && !GarrisonOnly) detail = "14 camera/mouse/touch/modal/settings/slot management/history + Game pause Esc / input blocking / independent slots / quick save / settings / confirmations / prior pause / night freeze + 11C actual DBP target / ground projectile warning / pause / impact / participation + 11B hero HUD / temple quote / paid wake / portrait selection / actual DBP movement / retained anchor / peaceful zero combat XP + " + detail;
            finished = true; Debug.Log("[ECS PLAYER SMOKE] " + (passed ? "PASS " : "FAIL ") + detail);
            if (Completed != null) Completed(passed, detail);
            else if (!Application.isEditor) Application.Quit(passed ? 0 : 1);
        }
    }
}
#endif
