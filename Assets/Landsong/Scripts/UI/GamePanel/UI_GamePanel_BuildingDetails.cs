using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // Stable controls preserve input focus while the simulation refreshes their read models.
    public sealed class UI_GamePanel_BuildingDetails : MonoBehaviour
    {
        [LabelText("名称"), Required]
        public TMP_InputField Name;
        [LabelText("图标"), Required]
        public Image Icon;
        [LabelText("经验填充"), Required]
        public Image ExperienceFill;
        [LabelText("等级"), Required]
        public TMP_Text Level;
        [LabelText("经验"), Required]
        public TMP_Text Experience;
        [LabelText("耐久条"), Required]
        public Slider silder_HP条;
        [LabelText("耐久文本"), Required]
        public TMP_Text TMP_Text_HP_文本;
        [LabelText("页脚"), Required]
        public TMP_Text Footer;
        [LabelText("关闭"), Required]
        public Button Close;
        [LabelText("样式"), Required]
        public Button Style;
        [LabelText("升级"), Required]
        public Button Upgrade;
        [LabelText("警告"), Required]
        public Button Warning;
        [LabelText("经验跟踪"), Required]
        public GameObject ExperienceTrack;
        [LabelText("经验悬浮信息"), Required]
        public UI_GamePanel_BuildingDetails_SidebarTrigger ExperienceHover;
        [LabelText("模块滚动视图"), Required]
        public ScrollRect ModulesScroll;
        [LabelText("建筑模块"), Required]
        public List<UI_GamePanel_BuildingDetails_Block> Blocks = new List<UI_GamePanel_BuildingDetails_Block>();
        [FormerlySerializedAs("WorkerSidebarScroll")]
        [LabelText("侧栏滚动视图"), Required]
        public ScrollRect SidebarScroll;
        [FormerlySerializedAs("WorkerSidebarLayout")]
        [LabelText("侧栏布局"), Required]
        public LayoutElement SidebarLayout;
        [LabelText("提示框"), Required]
        public GameObject Tooltip;
        [LabelText("提示框文字"), Required]
        public TMP_Text TooltipText;
        [FormerlySerializedAs("WorkerSidebar")]
        [LabelText("侧栏"), Required]
        public GameObject Sidebar;
        [FormerlySerializedAs("WorkerSidebarText")]
        [LabelText("侧栏文字"), Required]
        public TMP_Text SidebarText;
        Component sidebarOwner;
        Func<string> sidebarContent;
        float sidebarHideAt, sidebarRefreshAt;
        bool sidebarHovered;
        public ulong BuildingId { get; private set; }

        string warningText;

        public T Block<T>() where T : UI_GamePanel_BuildingDetails_Block
        {
            T result = null;
            foreach (var block in Blocks)
                if (block is T typed)
                {
                    if (result != null)
                        throw new InvalidOperationException("建筑详情面板重复配置模块：" + typeof(T).Name);
                    result = typed;
                }
            if (result == null)
                throw new InvalidOperationException("建筑详情面板缺少模块：" + typeof(T).Name);
            return result;
        }

        public static void Span(Image image, float start, float end)
        {
            image.rectTransform.anchorMin = new Vector2(Mathf.Clamp01(start), 0);
            image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(end), 1);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        }

        public void Select(ulong id, string name)
        {
            if (BuildingId != id)
            {
                BuildingId = id;
                ShowWarnings(false);
                HideSidebar();
                ModulesScroll.verticalNormalizedPosition = 1;
            }

            if (!Name.isFocused)
                Name.SetTextWithoutNotify(name);
        }

        public void SetWarnings(string value)
        {
            warningText = value;
            Warning.gameObject.SetActive(value.Length > 0);
            TooltipText.text = value;
            if (value.Length == 0)
                ShowWarnings(false);
        }

        public void ShowWarnings(bool show)
        {
            if (show)
                HideSidebar();
            Tooltip.SetActive(show && warningText.Length > 0);
            if (Tooltip.activeSelf)
                Tooltip.transform.SetAsLastSibling();
        }

        void OnDisable()
        {
            if (Tooltip != null)
                Tooltip.SetActive(false);
            HideSidebar();
        }

        public void ConfigureSidebar(UI_GamePanel_BuildingDetails_SidebarTrigger trigger, Func<string> content)
        {
            trigger.Content = content;
            if (sidebarOwner == trigger && content == null)
                HideSidebar();
        }

        public void ShowSidebar(Component owner, Func<string> content)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (content == null)
                return;
            ShowWarnings(false);
            sidebarOwner = owner;
            sidebarContent = content;
            sidebarHideAt = 0;
            sidebarRefreshAt = 0;
            Sidebar.SetActive(true);
            Sidebar.transform.SetAsLastSibling();
            SidebarScroll.verticalNormalizedPosition = 1;
            RefreshSidebar();
        }

        public void LeaveSidebar(Component owner)
        {
            if (sidebarOwner == owner)
                sidebarHideAt = Time.unscaledTime + .18f;
        }

        public void SetSidebarHovered(bool value)
        {
            sidebarHovered = value;
            if (!value)
                sidebarHideAt = Time.unscaledTime + .18f;
        }

        public void HideSidebar()
        {
            sidebarOwner = null;
            sidebarContent = null;
            sidebarHovered = false;
            sidebarHideAt = 0;
            if (Sidebar != null)
                Sidebar.SetActive(false);
        }

        void LateUpdate()
        {
            if (sidebarOwner == null)
            {
                if (Sidebar.activeSelf)
                    HideSidebar();
                return;
            }

            var content = sidebarOwner is UI_GamePanel_BuildingDetails_SidebarTrigger trigger ? trigger.Content : sidebarContent;
            if (!sidebarOwner.gameObject.activeInHierarchy || content == null || (!sidebarHovered && sidebarHideAt > 0 && Time.unscaledTime >= sidebarHideAt))
            {
                HideSidebar();
                return;
            }

            sidebarContent = content;
            if (Time.unscaledTime >= sidebarRefreshAt)
                RefreshSidebar();
        }

        void RefreshSidebar()
        {
            sidebarRefreshAt = Time.unscaledTime + .3f;
            SidebarText.text = sidebarContent();
            SidebarLayout.preferredHeight = SidebarText.GetPreferredValues(SidebarText.text, Mathf.Max(180, ((RectTransform)Sidebar.transform).rect.width - 44), float.PositiveInfinity).y + 16;
        }

        public static void Bind(Button b, Action action)
        {
            b.onClick.RemoveAllListeners();
            b.interactable = action != null;
            if (action != null)
                b.onClick.AddListener(() => action());
        }
    }
}
