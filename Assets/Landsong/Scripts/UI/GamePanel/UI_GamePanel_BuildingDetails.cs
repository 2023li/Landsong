using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Stable controls preserve input focus while the simulation refreshes their read models.
    public sealed class UI_GamePanel_BuildingDetails : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("名称")]
        public TMP_InputField Name;
        [Sirenix.OdinInspector.LabelText("图标")]
        public Image Icon;
        [Sirenix.OdinInspector.LabelText("经验填充")]
        public Image ExperienceFill;
        [Sirenix.OdinInspector.LabelText("等级")]
        public TMP_Text Level;
        [Sirenix.OdinInspector.LabelText("经验")]
        public TMP_Text Experience;
        [Sirenix.OdinInspector.LabelText("耐久条")]
        public Slider silder_HP条;
        [Sirenix.OdinInspector.LabelText("耐久文本")]
        public TMP_Text TMP_Text_HP_文本;
        [Sirenix.OdinInspector.LabelText("页脚")]
        public TMP_Text Footer;
        [Sirenix.OdinInspector.LabelText("关闭")]
        public Button Close;
        [Sirenix.OdinInspector.LabelText("样式")]
        public Button Style;
        [Sirenix.OdinInspector.LabelText("升级")]
        public Button Upgrade;
        [Sirenix.OdinInspector.LabelText("警告")]
        public Button Warning;
        [Sirenix.OdinInspector.LabelText("经验跟踪")]
        public GameObject ExperienceTrack;
        [Sirenix.OdinInspector.LabelText("经验悬浮信息")]
        public UI_GamePanel_BuildingDetails_SidebarTrigger ExperienceHover;
        [Sirenix.OdinInspector.LabelText("模块滚动视图")]
        public ScrollRect ModulesScroll;
        [Sirenix.OdinInspector.LabelText("建筑模块")]
        public List<UI_GamePanel_BuildingDetails_Block> Blocks = new List<UI_GamePanel_BuildingDetails_Block>();
        [FormerlySerializedAs("WorkerSidebarScroll")]
        [Sirenix.OdinInspector.LabelText("侧栏滚动视图")]
        public ScrollRect SidebarScroll;
        [FormerlySerializedAs("WorkerSidebarLayout")]
        [Sirenix.OdinInspector.LabelText("侧栏布局")]
        public LayoutElement SidebarLayout;
        [Sirenix.OdinInspector.LabelText("提示框")]
        public GameObject Tooltip;
        [Sirenix.OdinInspector.LabelText("提示框文字")]
        public TMP_Text TooltipText;
        [FormerlySerializedAs("WorkerSidebar")]
        [Sirenix.OdinInspector.LabelText("侧栏")]
        public GameObject Sidebar;
        [FormerlySerializedAs("WorkerSidebarText")]
        [Sirenix.OdinInspector.LabelText("侧栏文字")]
        public TMP_Text SidebarText;
        Component sidebarOwner;
        Func<string> sidebarContent;
        float sidebarHideAt, sidebarRefreshAt;
        bool sidebarHovered;
        public ulong BuildingId { get; private set; }

        string warningText;
        public void ValidateConfiguration()
        {
            var missing = new List<string>();
            void Need(UnityEngine.Object value, string name)
            {
                if (value == null)
                    missing.Add(name);
            }

            Need(Name, nameof(Name));
            Need(Icon, nameof(Icon));
            Need(ExperienceFill, nameof(ExperienceFill));
            Need(Level, nameof(Level));
            Need(Experience, nameof(Experience));
            Need(silder_HP条, nameof(silder_HP条));
            Need(TMP_Text_HP_文本, nameof(TMP_Text_HP_文本));
            Need(Footer, nameof(Footer));
            Need(Close, nameof(Close));
            Need(Style, nameof(Style));
            Need(Upgrade, nameof(Upgrade));
            Need(Warning, nameof(Warning));
            Need(ExperienceTrack, nameof(ExperienceTrack));
            Need(ExperienceHover, nameof(ExperienceHover));
            Need(ModulesScroll, nameof(ModulesScroll));
            Need(SidebarScroll, nameof(SidebarScroll));
            Need(SidebarLayout, nameof(SidebarLayout));
            Need(Tooltip, nameof(Tooltip));
            Need(TooltipText, nameof(TooltipText));
            Need(Sidebar, nameof(Sidebar));
            Need(SidebarText, nameof(SidebarText));
            if (missing.Count > 0)
                throw new InvalidOperationException("建筑详情面板检查器引用不完整：" + string.Join("、", missing));

            if (Blocks == null || Blocks.Count == 0)
                throw new InvalidOperationException("建筑详情面板未配置建筑模块。");
            if (ModulesScroll.content == null || ModulesScroll.content.childCount != Blocks.Count)
                throw new InvalidOperationException("建筑详情的每个 Content 直接子模块都必须对应一个 Block。");
            var moduleObjects = new HashSet<GameObject>();
            foreach (var block in Blocks)
            {
                if (block == null)
                    throw new InvalidOperationException("建筑详情面板包含空 Block 引用。");
                if (block.View != this || block.transform.parent != ModulesScroll.content || !moduleObjects.Add(block.gameObject))
                    throw new InvalidOperationException(block.name + " 未作为唯一 Block 正确绑定到建筑模块 Content。");
                block.ValidateConfiguration();
            }
            ExperienceHover.ValidateConfiguration();
            if (ExperienceHover.View != this)
                throw new InvalidOperationException("建筑详情经验悬浮目标错误。");
        }

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
            if (trigger == null || trigger.View != this)
                throw new InvalidOperationException("建筑详情侧栏触发器配置错误。");
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
