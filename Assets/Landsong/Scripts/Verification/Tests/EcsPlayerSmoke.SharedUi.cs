#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Moyo.Unity;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        UIManager originalUiManager;
        Canvas originalCanvas;
        EventSystem originalEventSystem;
        UI_SettingPanel originalSettingsPanel;
        UI_SavePanel originalArchivePanel;
        UI_GamePanel trackedGameView;
        GameUiSession trackedGameSession;
        UIScope trackedGameScope;
        bool settingsOpenedInStart, archivesOpenedInStart, settingsOpenedInGame, archivesOpenedInGame;

        static T Shared<T>() where T : UIPanelBase
        {
            return UIManager.TryGetInstance(out var manager) && manager.TryGetActivePanel<T>(out var panel) ? panel : null;
        }

        static bool MenuReady => !EcsSceneFlow.Busy && !EcsSceneFlow.GameReady
            && UIManager.TryGetInstance(out var manager) && !manager.IsBusy
            && manager.TryGetActivePanel<UI_StartPanel>(out var menu) && menu.StartButton.IsInteractable();

        IEnumerator CaptureSharedUiContract()
        {
            Require(UIManager.TryGetInstance(out originalUiManager), "Initial Start owns the application UI manager");
            originalCanvas = originalUiManager.RootCanvas;
            originalEventSystem = originalUiManager.EventSystem;
            RequireApplicationUiStable("initial Start");
            // Preload runs real creation/preview cleanup while the panels remain hidden and have no archive request.
            var settings = originalUiManager.PreloadAsync<UI_SettingPanel>();
            yield return WaitTask(settings); originalSettingsPanel = settings.Result;
            Require(!originalSettingsPanel.gameObject.activeInHierarchy && !originalSettingsPanel.IsViewOpen, "Settings preload remains hidden before user opens it");
            RequirePreviewCleared(originalSettingsPanel, "preloaded settings", true);
            var archives = originalUiManager.PreloadAsync<UI_SavePanel>();
            yield return WaitTask(archives); originalArchivePanel = archives.Result;
            Require(!originalArchivePanel.gameObject.activeInHierarchy && !originalArchivePanel.IsViewOpen, "Archive preload remains hidden before an isolated request is supplied");
            RequirePreviewCleared(originalArchivePanel, "preloaded archives", true);
            VerifyDetachedSessionLifetime();
        }

        void RequireApplicationUiStable(string context)
        {
            Require(originalUiManager != null && UIManager.TryGetInstance(out var manager) && manager == originalUiManager
                && manager.RootCanvas == originalCanvas && manager.EventSystem == originalEventSystem,
                "Same Canvas, UIManager and EventSystem survive: " + context);
            Require(FindObjectsByType<UIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1
                && FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
                "Exactly one application manager and input system: " + context);
            // TMP adds a temporary Canvas when expanded and keeps an inactive template; call only after dropdown dismissal.
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(canvas => canvas.isActiveAndEnabled).ToArray();
            Require(canvases.Length == 1 && canvases[0] == originalCanvas, "Exactly one active application Canvas with no expanded dropdown: " + context);
            Canvas.ForceUpdateCanvases();
            var expected = new Vector3[4]; var actual = new Vector3[4];
            ((RectTransform)originalCanvas.transform).GetWorldCorners(expected);
            foreach (var panel in originalCanvas.GetComponentsInChildren<UIPanelBase>())
            {
                ((RectTransform)panel.transform).GetWorldCorners(actual);
                Require(Enumerable.Range(0, 4).All(index => Vector3.Distance(expected[index], actual[index]) < .1f),
                    "Loaded panel bounds match the actual application Canvas: " + panel.name + " / " + context);
            }
        }

        void ObserveSharedSettings(string context)
        {
            var panel = Shared<UI_SettingPanel>();
            Require(panel != null && panel == originalSettingsPanel && panel.Manager == originalUiManager, "Start and Game reuse the exact settings instance: " + context);
            if (EcsSceneFlow.GameReady) { Require(settingsOpenedInStart, "Game settings reuse follows an actual Start settings open"); settingsOpenedInGame = true; }
            else settingsOpenedInStart = true;
            RequirePreviewCleared(panel, context);
            RequireApplicationUiStable(context);
        }

        void ObserveSharedArchives(string context)
        {
            var panel = Shared<UI_SavePanel>();
            Require(panel != null && panel == originalArchivePanel && panel.Manager == originalUiManager, "Start and Game reuse the exact archive instance: " + context);
            if (EcsSceneFlow.GameReady) { Require(archivesOpenedInStart, "Game archive reuse follows an actual Start archive open"); archivesOpenedInGame = true; }
            else archivesOpenedInStart = true;
            RequirePreviewCleared(panel, context);
            RequireApplicationUiStable(context);
        }

        static void RequirePreviewCleared(UIViewBase panel, string context, bool beforeOpen = false)
        {
            var previews = panel.GetComponentsInChildren<UIPreviewOnly>(true);
            Require(previews.Length != 0, "Real prefab contains configured preview cleanup bindings: " + context);
            foreach (var preview in previews)
            {
                Require(preview.SampleObjects.All(sample => sample == null || !sample.activeSelf), "Editor preview rows are disabled before runtime use: " + context, false);
                if (!beforeOpen) continue; // Open/refresh is allowed to replace the reset text with real view data.
                for (int index = 0; index < preview.SampleTextTargets.Length; index++)
                    Require(preview.SampleTextTargets[index].text == (preview.RuntimeTexts[index] ?? string.Empty), "Editor sample text reset before panel open: " + context, false);
            }
            Require(true, "Configured editor preview content cleared: " + context);
        }

        void ObserveGameLifetime(UI_GamePanel view, string context)
        {
            Require(view != null && view.Session.IsBound && view.Scope != null && !view.Scope.IsEnded, "Game panel explicitly owns a live session: " + context);
            Require(!ReferenceEquals(view, trackedGameView), "A new game session creates a fresh Game panel: " + context);
            trackedGameView = view; trackedGameSession = view.Session; trackedGameScope = view.Scope;
            RequirePreviewCleared(view, context);
            RequireApplicationUiStable(context);
        }

        void RequireGameLifetimeReleased(string context)
        {
            Require(!ReferenceEquals(trackedGameView, null) && trackedGameView == null, "Old Game panel is destroyed after leaving its session: " + context);
            Require(trackedGameSession != null && !trackedGameSession.IsBound && trackedGameScope.IsEnded,
                "Old Game UI binding and scope are invalidated: " + context);
            Require(!originalUiManager.IsOpened<UI_GamePanel>() && Released(), "No Game panel or simulation authority remains: " + context);
            Require(originalSettingsPanel != null && originalArchivePanel != null, "Shared panel cache survives Game session release: " + context);
            RequireApplicationUiStable(context);
        }

        static void VerifyDetachedSessionLifetime()
        {
            var world = new World("Owned UI binding lifetime probe");
            var session = new GameUiSession();
            try
            {
                var root = world.EntityManager.CreateEntity(typeof(SimulationReady));
                session.Bind(world.EntityManager, root);
                Require(session.IsBound, "Explicit isolated UI session binds a live root");
                world.Dispose();
                Require(!session.IsBound, "Disposed World invalidates UI session without touching disposed EntityManager");
                session.Unbind(); session.Unbind();
                Require(!session.IsBound, "Repeated UI session unbind is safe after World disposal");
            }
            finally { session.Unbind(); if (world.IsCreated) world.Dispose(); }
        }

        static IEnumerator WaitTask(Task operation)
        {
            float deadline = Time.realtimeSinceStartup + 150;
            while (!operation.IsCompleted)
            {
                if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Shared UI navigation did not finish within 150 seconds.");
                yield return null;
            }
            operation.GetAwaiter().GetResult();
        }

        static IEnumerator CloseShared<T>() where T : UIPanelBase
        {
            Require(UIManager.TryGetInstance(out var manager), "Application UI manager remains installed");
            yield return WaitTask(manager.CloseAsync<T>());
            yield return null; // Application input routing observes the completed focus change.
        }

        static void ClickArchive(UI_SavePanel browser, string label)
        {
            browser.Rows.GetComponentsInChildren<Button>().Single(button => button.interactable &&
                button.GetComponentInChildren<TMP_Text>()?.text.Contains(label) == true).onClick.Invoke();
        }

        IEnumerator ConfirmShared(bool accept)
        {
            yield return WaitFor(() => Shared<UI_ConfirmPanel>() != null, "Shared confirmation opens");
            var panel = Shared<UI_ConfirmPanel>();
            (accept ? panel.ConfirmButton : panel.CancelButton).onClick.Invoke();
            yield return WaitFor(() => Shared<UI_ConfirmPanel>() == null, "Shared confirmation closes");
        }
    }
}
#endif
