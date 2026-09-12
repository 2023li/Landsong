#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator MainMenuArchivesUi(UI_StartPanel menu, RunArchive archive)
        {
            var originalStore = menu.Store;
            var temporary = Path.GetFullPath(Path.Combine("Library/LandsongEcs/VerificationRuns", "MainMenuArchives-" + Guid.NewGuid().ToString("N")));
            var store = new RunArchiveStore(temporary);
            menu.Store = store;
            try
            {
                store.Write(archive);
                store.Write(archive); // Owned backup for the damaged-primary case.
                menu.RefreshContinue();
                Require(menu.ContinueButton.gameObject.activeSelf && menu.ContinueButton.IsInteractable(), "Readable recent archive exposes quick continue");
                File.WriteAllText(store.RunPath(archive.RunId), "damaged fixture");
                menu.RefreshContinue();
                Require(menu.ContinueButton.gameObject.activeSelf, "Valid backup still permits quick continue");
                File.WriteAllText(store.RunPath(archive.RunId) + ".bak", "damaged backup fixture");
                menu.RefreshContinue();
                Require(!menu.ContinueButton.gameObject.activeSelf, "Unreadable primary and backup hide quick continue");
                store.Write(archive);
                var first = store.SaveSlot(archive, true);
                var second = store.SaveSlot(archive, true);
                store.RenameSlot(archive.RunId, first, "菜单测试槽一", store.SlotStamp(archive.RunId, first));
                store.RenameSlot(archive.RunId, second, "菜单测试槽二", store.SlotStamp(archive.RunId, second));
                menu.RefreshContinue();
                yield return null;
                if (Application.isEditor) ScreenCapture.CaptureScreenshot("Library/LandsongEcs/main-menu-with-save.png");
                yield return null;
                menu.LoadButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_SavePanel>() != null, "Shared archive browser opens from Start");
                ObserveSharedArchives("returned Start archive browser");
                var browser = Shared<UI_SavePanel>();
                string loadedRun = null, loadedSlot = null;
                var archiveManager = browser.Manager;
                var request = new ArchiveOpenRequest { Navigation = FindFirstObjectByType<ApplicationUiRoot>(), Mode = ArchiveOpenMode.Load, Store = store, Load = (run, slot, backup) => { loadedRun = run; loadedSlot = slot; } };
                void Click(string text) => browser.GetComponentsInChildren<Button>()
                    .Single(b => b.gameObject.activeInHierarchy && b.GetComponentInChildren<TextMeshProUGUI>().text.Contains(text)).onClick.Invoke();
                foreach (var pair in new[] { (first, "菜单测试槽一"), (second, "菜单测试槽二") })
                {
                    yield return WaitTask(archiveManager.OpenAsync<UI_SavePanel>(request)); browser = Shared<UI_SavePanel>();
                    Click(archive.RunId.Substring(0, 8));
                    Click(pair.Item2);
                    Click("载入此记录");
                    yield return ConfirmShared(true);
                    Require(loadedRun == archive.RunId && loadedSlot == pair.Item1, "Load panel dispatches the explicitly chosen slot: " + pair.Item2);
                    yield return WaitFor(() => Shared<UI_SavePanel>() == null, "Accepted load closes the shared archive browser");
                }
                yield return CloseShared<UI_SavePanel>();
                menu.CloseManagement();
            }
            finally
            {
                menu.CloseManagement();
                menu.Store = originalStore;
                menu.RefreshContinue();
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
        }

        IEnumerator MainMenuUi(UI_StartPanel menu)
        {
            var originalStore = menu.Store;
            var temporary = Path.GetFullPath(Path.Combine("Library/LandsongEcs/VerificationRuns", "MainMenu-" + Guid.NewGuid().ToString("N")));
            var store = new RunArchiveStore(temporary);
            menu.Store = store;
            try
            {
                menu.RefreshContinue();
                Require(!menu.ContinueButton.gameObject.activeSelf, "No archive hides continue entirely");
                Directory.CreateDirectory(temporary);
                File.WriteAllText(Path.Combine(temporary, "active-run.txt"), Guid.NewGuid().ToString("N"));
                menu.RefreshContinue();
                Require(!menu.ContinueButton.gameObject.activeSelf, "Stale archive pointer does not expose continue");
                Require(!menu.MapSelection.gameObject.activeInHierarchy && !menu.NewDynastyPanel.activeSelf, "Home does not expose map configuration");
                var labels = menu.StartButton.transform.parent.GetComponentsInChildren<Button>()
                    .Select(b => b.GetComponentInChildren<TextMeshProUGUI>().text).ToArray();
                Require(labels.SequenceEqual(new[] { "开始新王朝", "加载游戏", "设置", "退出到桌面" }), "Home actions appear in requested order without saves");
                yield return null;
                if (Application.isEditor) ScreenCapture.CaptureScreenshot("Library/LandsongEcs/main-menu.png");
                yield return null;
                menu.StartButton.onClick.Invoke();
                yield return null;
                Require(menu.NewDynastyPanel.activeSelf && !EcsSceneFlow.Busy && Released(), "New dynasty opens configuration without starting gameplay");
                Require(menu.MapSelection.IsInteractable() && !menu.StartButton.IsInteractable(), "Modal accepts configuration and blocks home actions");
                var input = menu.NewDynastyPanel.GetComponentInChildren<TMP_InputField>();
                input.text = "配置草稿";
                menu.MapSelection.value = menu.Catalog.Maps.Length - 1;
                menu.DifficultySelection.value = 2;
                var placeholder = menu.NewDynastyPanel.GetComponentsInChildren<Button>().Single(b => b.GetComponentInChildren<TextMeshProUGUI>().text == "不刷新小偷（暂未开放）");
                Require(!placeholder.interactable, "Special rules are explicitly unavailable");
                if (Application.isEditor) ScreenCapture.CaptureScreenshot("Library/LandsongEcs/new-dynasty.png");
                yield return null;
                menu.CloseManagement();
                menu.StartButton.onClick.Invoke();
                Require(input.text == "配置草稿" && menu.DifficultySelection.value == 2 && menu.MapSelection.value == menu.Catalog.Maps.Length - 1, "Returning to configuration preserves this menu session draft");
                menu.DifficultySelection.Show();
                yield return null;
                Require(menu.DifficultySelection.IsExpanded && menu.DifficultySelection.options.Count == 3, "Difficulty dropdown opens with three choices");
                menu.DifficultySelection.Hide();
                yield return new WaitForSecondsRealtime(.2f);
                menu.CloseManagement();
                var catalog = menu.Catalog;
                try
                {
                    menu.NewDynasty.Catalog = null;
                    menu.StartButton.onClick.Invoke();
                    Require(!menu.CreateDynastyButton.interactable && !menu.MapSelection.interactable, "Missing catalog disables creation inside configuration");
                }
                finally { menu.NewDynasty.Catalog = catalog; menu.CloseManagement(); }
                menu.LoadButton.onClick.Invoke();
                yield return null;
                yield return WaitFor(() => Shared<UI_SavePanel>() != null, "Load game opens shared browser");
                ObserveSharedArchives("initial Start archive browser");
                var archive = Shared<UI_SavePanel>();
                Require(archive.Rows.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text.Contains("暂无在世王朝存档")), "Shared browser reads isolated empty store");
                yield return CloseShared<UI_SavePanel>();
                menu.SettingsButton.onClick.Invoke();
                yield return null;
                yield return WaitFor(() => Shared<UI_SettingPanel>() != null, "Home opens shared settings");
                ObserveSharedSettings("initial Start settings");
                yield return CloseShared<UI_SettingPanel>();
                menu.CloseManagement();
                menu.QuitButton.onClick.Invoke();
                yield return null;
                yield return WaitFor(() => Shared<UI_ConfirmPanel>() != null, "Quit opens shared confirmation");
                Require(Shared<UI_ConfirmPanel>().CancelButton.IsInteractable() && !EcsSceneFlow.Busy, "Quit waits for an interactive confirmation");
                yield return ConfirmShared(false);
                Require(menu.StartButton.IsInteractable() && !EcsSceneFlow.Busy, "Cancelling quit restores home");
            }
            finally
            {
                menu.CloseManagement();
                menu.Store = originalStore;
                menu.RefreshContinue();
                // Exact fixture-owned GUID directory only.
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
        }
    }
}
#endif
