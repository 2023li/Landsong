using System.Linq;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public TextMeshProUGUI ResearchHudName { get; private set; }
        public TextMeshProUGUI ResearchHudEffects { get; private set; }
        public Image ResearchHudProgress { get; private set; }
        public int CurrentResearchDefinition { get; private set; } = -1;
        Image researchHudIcon;
        TextMeshProUGUI researchHudStatus, researchHudIconFallback;
        float nextResearchHudRefresh;
        bool focusResearchHud;

        void InitializeResearchHud()
        {
            var canvas = GetComponentInParent<Canvas>().transform;
            if (TechnologyButton == null) TechnologyButton = InterfaceWidgets.Button("Research HUD", canvas, Status.font, null);
            CompactFunctionToolbar();
            // Preserve the scene reference, replacing the old toolbar entry with one clickable HUD card.
            foreach (Transform child in TechnologyButton.transform) child.gameObject.SetActive(false);
            TechnologyButton.transform.SetParent(canvas, false);
            TechnologyButton.name = "Research HUD";
            var rect = (RectTransform)TechnologyButton.transform;
            rect.anchorMin = new Vector2(.77f, .23f);
            rect.anchorMax = new Vector2(.99f, .43f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TechnologyButton.targetGraphic.color = new Color(.045f, .065f, .10f, .98f);
            TechnologyButton.onClick = new Button.ButtonClickedEvent();
            TechnologyButton.onClick.AddListener(() =>
            {
                if (PauseMenu != null && PauseMenu.IsOpen || BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf) return;
                var queue = ResearchOps.Queue(em, root);
                selectedTechnology = queue.Count > 0 ? queue[0].Definition : -1;
                focusResearchHud = true;
                OpenPanel("科技");
            });
            var icon = InterfaceWidgets.Rect("Technology icon", rect, new Vector2(.03f, .46f), new Vector2(.25f, .94f));
            researchHudIcon = icon.gameObject.AddComponent<Image>();
            researchHudIcon.preserveAspect = true; researchHudIcon.raycastTarget = false;
            researchHudIconFallback = InterfaceWidgets.Text("研", icon, Status.font, 30);
            researchHudIconFallback.alignment = TextAlignmentOptions.Center;
            ResearchHudName = InterfaceWidgets.Text("", InterfaceWidgets.Rect("Technology name", rect, new Vector2(.28f, .68f), new Vector2(.98f, .97f)), Status.font, 20);
            ResearchHudName.enableAutoSizing = true; ResearchHudName.fontSizeMin = 14; ResearchHudName.fontSizeMax = 20;
            var track = InterfaceWidgets.Rect("Research progress", rect, new Vector2(.30f, .54f), new Vector2(.96f, .63f));
            track.gameObject.AddComponent<Image>().color = new Color(.65f, .67f, .72f, 1);
            ResearchHudProgress = InterfaceWidgets.Rect("Fill", track, Vector2.zero, Vector2.one).gameObject.AddComponent<Image>();
            ResearchHudProgress.color = new Color(.48f, .42f, .93f, 1);
            ResearchHudProgress.raycastTarget = false;
            researchHudStatus = InterfaceWidgets.Text("", InterfaceWidgets.Rect("Research status", rect, new Vector2(.28f, .36f), new Vector2(.98f, .53f)), Status.font, 13);
            ResearchHudEffects = InterfaceWidgets.Text("", InterfaceWidgets.Rect("Technology effects", rect, new Vector2(.02f, .02f), new Vector2(.98f, .36f)), Status.font, 14);
            ResearchHudEffects.overflowMode = TextOverflowModes.Ellipsis;
            TechnologyButton.gameObject.SetActive(false);
        }

        void RefreshResearchHud()
        {
            if (TechnologyButton == null || ResearchHudName == null || Time.unscaledTime < nextResearchHudRefresh) return;
            nextResearchHudRefresh = Time.unscaledTime + .2f;
            bool unlocked = ResearchOps.Unlocked(em, root);
            TechnologyButton.gameObject.SetActive(unlocked);
            if (!unlocked) { CurrentResearchDefinition = -1; return; }
            TechnologyButton.interactable = true;
            var queue = ResearchOps.Queue(em, root);
            CurrentResearchDefinition = queue.Count > 0 ? queue[0].Definition : -1;
            if (CurrentResearchDefinition < 0)
            {
                ResearchHudName.text = "尚未选择科技";
                researchHudStatus.text = "点击选择研究项目";
                ResearchHudEffects.text = "选择科技，规划王朝的发展方向。";
                SetResearchHudProgress(0);
                researchHudIcon.sprite = null;
                researchHudIcon.color = new Color(.18f, .22f, .30f, 1);
                researchHudIconFallback.gameObject.SetActive(true);
                return;
            }
            int definition = CurrentResearchDefinition;
            var quote = ResearchOps.Quote(em, root, definition);
            var source = BuildingSource(definition);
            ResearchHudName.text = Name(definition);
            researchHudStatus.text = TechnologyTreeView.StatusName(quote.Status) + "  " + quote.Progress + " / " + quote.Cost;
            SetResearchHudProgress(quote.Cost <= 0 ? 1 : Mathf.Clamp01((float)quote.Progress / quote.Cost));
            researchHudIcon.sprite = source?.Icon;
            researchHudIcon.color = researchHudIcon.sprite != null ? Color.white : new Color(.18f, .22f, .30f, 1);
            researchHudIconFallback.gameObject.SetActive(researchHudIcon.sprite == null);
            ResearchHudEffects.text = quote.Rewards.Count > 0 ? string.Join("；", quote.Rewards.Select(reward =>
                    (reward.Kind == RuleKind.RewardBlueprint ? "解锁建筑：" : reward.Kind == RuleKind.RewardFeature ? "解锁功能：" : reward.Kind == RuleKind.RewardBuff ? "获得增益：" : "获得物品：")
                    + Name(reward.Target) + (reward.Kind == RuleKind.RewardItem ? " ×" + reward.Amount : "")))
                : !string.IsNullOrWhiteSpace(source?.Description) ? source.Description : "点击查看科技详情。";
        }

        void CompactFunctionToolbar()
        {
            var original = (RectTransform)TechnologyButton.transform;
            var siblings = original.parent.Cast<Transform>().Select(t => t.GetComponent<Button>())
                .Where(b => b != null && b != TechnologyButton)
                .Select(b => (RectTransform)b.transform)
                .Where(r => Mathf.Approximately(r.anchorMin.y, original.anchorMin.y) && Mathf.Approximately(r.anchorMax.y, original.anchorMax.y))
                .OrderBy(r => r.anchorMin.x).ToArray();
            if (siblings.Length == 0) return;
            float left = Mathf.Min(original.anchorMin.x, siblings[0].anchorMin.x);
            float right = Mathf.Max(original.anchorMax.x, siblings[siblings.Length - 1].anchorMax.x);
            float width = (right - left) / siblings.Length;
            for (int i = 0; i < siblings.Length; i++)
            {
                siblings[i].anchorMin = new Vector2(left + width * i, original.anchorMin.y);
                siblings[i].anchorMax = new Vector2(left + width * (i + 1) - .002f, original.anchorMax.y);
            }
        }

        void SetResearchHudProgress(float value)
        {
            // A plain Image needs no additional sprite asset to render a determinate progress bar.
            ResearchHudProgress.rectTransform.anchorMax = new Vector2(value, 1);
            ResearchHudProgress.rectTransform.offsetMin = ResearchHudProgress.rectTransform.offsetMax = Vector2.zero;
        }
    }
}
