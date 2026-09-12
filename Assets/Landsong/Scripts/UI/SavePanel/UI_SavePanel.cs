using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Landsong.ECS.Persistence;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_SavePanel : UIPanelBase
    {
        [LabelText("返回按钮"), Required]
        public Button BackButton;
        [LabelText("存档内容容器"), Required]
        public RectTransform Rows;
        [LabelText("条目模板"), Required]
        public UI_SavePanel_ArchiveRow RowTemplate;
        [LabelText("存档名称输入框"), Required]
        public TMP_InputField RenameInput;
        [LabelText("缩略图容器"), Required]
        public GameObject PreviewRoot;
        [LabelText("缩略图"), Required]
        public RawImage PreviewImage;
        [LabelText("列表滚动区"), Required]
        public ScrollRect Scroll;
        readonly Dictionary<string, UI_SavePanel_ArchiveRow> rows = new();
        readonly HashSet<string> used = new();
        ArchiveApplicationService service;
        ArchiveOpenRequest context;
        string run;
        int page;
        bool ended;
        ArchiveListing detail;
        Texture2D preview;
        float nextRefresh;
        protected override void ValidateLocalConfiguration()
        {
            base.ValidateLocalConfiguration();
            if (BackButton == null || Rows == null || RowTemplate == null || RowTemplate.Select == null || RowTemplate.Label == null || RowTemplate.Layout == null || RenameInput == null || PreviewRoot == null || PreviewImage == null || Scroll == null)
                throw new InvalidOperationException("存档面板检查器引用不完整。");
        }

        public override Task OnCreateAsync()
        {
            ValidateConfiguration();
            BackButton.onClick.AddListener(Back);
            return base.OnCreateAsync();
        }

        public override Task OnOpenAsync(object args)
        {
            context = args as ArchiveOpenRequest ?? throw new InvalidOperationException("存档面板缺少打开上下文。");
            if (context.Navigation == null)
                throw new InvalidOperationException("存档面板未注入导航。");
            service = new ArchiveApplicationService(context);
            run = context.Mode == ArchiveOpenMode.Save ? context.CurrentRun : null;
            page = 0;
            ended = false;
            detail = null;
            RenameInput.SetTextWithoutNotify("");
            Refresh();
            return Task.CompletedTask;
        }

        public override Task OnCloseAsync()
        {
            ClearPreview();
            foreach (var row in rows.Values)
            {
                row.Select.onClick.RemoveAllListeners();
                row.gameObject.SetActive(false);
            }

            context = null;
            service = null;
            detail = null;
            return Task.CompletedTask;
        }

        public override Task OnReleaseAsync()
        {
            ClearPreview();
            foreach (var row in rows.Values)
                if (row != null)
                    Destroy(row.gameObject);
            rows.Clear();
            return base.OnReleaseAsync();
        }

        public override Task<bool> TryHandleBackAsync()
        {
            if (detail != null)
            {
                detail = null;
                Refresh();
                return Task.FromResult(true);
            }

            if (run != null && context.Mode != ArchiveOpenMode.Save)
            {
                run = null;
                page = 0;
                Refresh();
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public async void Back()
        {
            try
            {
                if (!await TryHandleBackAsync())
                    await Manager.CloseAsync<UI_SavePanel>();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        void Update()
        {
            if (service == null || detail != null || context.Mode != ArchiveOpenMode.Save || Time.unscaledTime < nextRefresh || UiInputState.TextFocused || UiInputState.GlobalModalOpen && Manager.TopFocusedPanel != this)
                return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasReleasedThisFrame))
                return;
            Refresh();
        }

        void ClearPreview()
        {
            PreviewImage.texture = null;
            if (preview != null)
                Destroy(preview);
            preview = null;
            PreviewRoot.SetActive(false);
        }

        void Add(string key, string label, Action action = null, float height = 48)
        {
            if (!rows.TryGetValue(key, out var row))
            {
                row = Instantiate(RowTemplate, Rows);
                rows.Add(key, row);
            }

            used.Add(key);
            row.gameObject.SetActive(true);
            row.transform.SetAsLastSibling();
            row.Label.text = label;
            row.Layout.minHeight = row.Layout.preferredHeight = height;
            row.Select.onClick.RemoveAllListeners();
            row.Select.interactable = action != null;
            if (action != null)
                row.Select.onClick.AddListener(() => Safe(action));
        }

        void Safe(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                Add("error", "操作未完成：" + error.Message, null, 95);
            }
        }

        async void LoadAndClose(ArchiveListing info, bool backup)
        {
            try
            {
                service.Load(info, backup);
                await Manager.CloseAsync<UI_SavePanel>();
            }
            catch (Exception error)
            {
                if (service != null)
                    Add("error", "载入未完成：" + error.Message, null, 95);
                else
                    Debug.LogException(error);
            }
        }

        void Confirm(string text, Action action, ArchiveListing target = null)
        {
            var source = service;
            context.Navigation.Confirm("存档操作", text, () => Safe(action), () => source.IsValid && (target == null || source.Matches(target)));
        }

        public void Refresh()
        {
            if (service == null)
                return;
            nextRefresh = Time.unscaledTime + 1;
            var position = Scroll.verticalNormalizedPosition;
            used.Clear();
            ClearPreview();
            RenameInput.gameObject.SetActive(false);
            Add("heading", context.Mode == ArchiveOpenMode.Save ? "当前王朝存档 · 自动节点和独立槽分别保留" : "存档管理 · 自动节点与独立槽分开", null, 62);
            Add("back", "关闭 / 返回", Back);
            if (!service.IsValid)
                Add("expired", "游戏会话已结束，请关闭后重新打开。");
            else if (detail != null)
                RenderDetail(detail);
            else if (ended)
            {
                Add("history-mode", "查看在世王朝", () =>
                {
                    ended = false;
                    page = 0;
                    Refresh();
                });
                var history = service.Histories();
                foreach (var id in history.Skip(page * 15).Take(15))
                    Add("history:" + id, service.History(id), null, 85);
                Pages(history.Length);
            }
            else if (run == null)
            {
                Add("history-mode", "查看覆灭记录", () =>
                {
                    ended = true;
                    page = 0;
                    Refresh();
                });
                var runs = service.Runs();
                foreach (var id in runs.Skip(page * 15).Take(15))
                {
                    var info = service.Describe(id);
                    Add("run:" + id, (info.Valid ? info.Dynasty + " · " + info.Map + " · 回合 " + info.Turn + (info.Backup ? "（备份）" : "") : "王朝记录不可读") + "\n" + id.Substring(0, 8), () =>
                    {
                        run = id;
                        page = 0;
                        Refresh();
                    }, 75);
                }

                if (runs.Length == 0)
                    Add("empty", "暂无在世王朝存档。");
                Pages(runs.Length);
            }
            else
            {
                if (context.Mode == ArchiveOpenMode.Save)
                {
                    RenameInput.gameObject.SetActive(true);
                    Add("create", service.CanWrite ? "创建独立存档" : "当前阶段不能保存", service.CanWrite ? () =>
                    {
                        service.Create(RenameInput.text);
                        nextRefresh = 0;
                    } : null);
                }
                else
                    Add("runs", "返回王朝列表", () =>
                    {
                        run = null;
                        page = 0;
                        Refresh();
                    });
                RenderListing(service.Describe(run));
                var slots = service.Slots(run);
                foreach (var slot in slots.Skip(page * 15).Take(15))
                    RenderListing(service.Describe(run, slot));
                if (slots.Length == 0)
                    Add("empty-slots", "尚无独立存档。");
                Pages(slots.Length);
            }

            foreach (var pair in rows)
                if (!used.Contains(pair.Key))
                    pair.Value.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            Scroll.verticalNormalizedPosition = position;
        }

        void RenderListing(ArchiveListing info)
        {
            Add("slot:" + info.Run + ":" + info.Slot, info.Name + " · " + (info.Valid ? info.Dynasty + " / " + info.Map + " / 回合 " + info.Turn + " / " + info.Stage + (info.Backup ? " / 备份可恢复" : "") : info.Error) + "\n" + info.Saved.ToString("yyyy-MM-dd HH:mm:ss"), () =>
            {
                detail = info;
                RenameInput.SetTextWithoutNotify(info.Name);
                Refresh();
            }, 92);
        }

        void Pages(int count)
        {
            if (page > 0)
                Add("prev", "上一页", () =>
                {
                    page--;
                    Refresh();
                });
            if ((page + 1) * 15 < count)
                Add("next", "下一页", () =>
                {
                    page++;
                    Refresh();
                });
        }

        void RenderDetail(ArchiveListing info)
        {
            bool ready = context.Mode != ArchiveOpenMode.Save || service.CanWrite;
            Add("detail-title", info.Name + " · " + info.Dynasty + " · " + info.Map + " · 回合 " + info.Turn, null, 65);
            if (info.Valid)
                Add("load", info.Backup ? "载入（恢复有效备份）" : "载入此记录", ready ? () => Confirm("载入“" + info.Name + "”？当前未保存进度会丢失，文件原件保留。", () => LoadAndClose(info, false), info) : null);
            else
                Add("invalid", "无法载入：" + info.Error, null, 90);
            if (service.HasBackup(info))
                Add("backup", "载入上一版备份", ready ? () => Confirm("从上一版备份重新开始？文件原件保留。", () => LoadAndClose(info, true), info) : null);
            if (info.Slot != null)
            {
                RenameInput.gameObject.SetActive(true);
                Add("rename", "重命名", ready ? () =>
                {
                    var name = RenameInput.text;
                    Confirm("将此槽重命名为“" + RunArchiveStore.DisplayName(name) + "”？", () =>
                    {
                        service.Rename(info, name);
                        detail = null;
                        Refresh();
                    }, info);
                } : null);
                if (context.Mode == ArchiveOpenMode.Save)
                    Add("overwrite", "覆盖此槽", ready ? () => Confirm("覆盖“" + info.Name + "”？原版本成为备份，其他槽保留。", () =>
                    {
                        service.Overwrite(info);
                        detail = null;
                        nextRefresh = 0;
                    }, info) : null);
                Add("delete", "永久删除此槽及其备份", ready ? () => Confirm("只删除“" + info.Name + "”及其槽备份/缩略图，不删除王朝自动节点或其他槽。无法撤销。", () =>
                {
                    service.Delete(info);
                    detail = null;
                    Refresh();
                }, info) : null);
                var bytes = service.Preview(info);
                if (bytes != null)
                {
                    preview = new Texture2D(2, 2);
                    if (preview.LoadImage(bytes))
                    {
                        PreviewImage.texture = preview;
                        PreviewRoot.SetActive(true);
                    }
                }
            }

            Add("list", "返回列表", () =>
            {
                detail = null;
                Refresh();
            });
        }
    }
}
