using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class BuildingCatalogCard
    {
        public int Definition;
        public BuildingCategory Category;
        public string Name, Tooltip;
        public Sprite Icon;
        public bool Allowed;
    }

    // A presentation-only catalogue. Blueprint ownership and action quotes come from ECS.
    public sealed class BuildingCatalogBar : MonoBehaviour
    {
        public RectTransform Tabs, Cards;
        public ScrollRect CardScroll;
        public Button TabTemplate, CardTemplate, CloseButton, EconomyButton;
        public RectTransform Tooltip;
        public TMP_Text TooltipText, Hint;
        public BuildingCategory SelectedCategory { get; private set; }
        public IReadOnlyList<BuildingCatalogCard> Models => models;
        public int VisibleCardCount => cards.Count;
        readonly List<BuildingCatalogCard> models = new List<BuildingCatalogCard>();
        readonly Dictionary<int, Button> cards = new Dictionary<int, Button>();
        readonly Dictionary<BuildingCategory, Button> tabs = new Dictionary<BuildingCategory, Button>();
        static readonly string[] Categories = { "人口", "农业", "工业", "经济", "科研", "市政", "军事", "交通", "装饰", "奇观" };
        Action<int> choose;
        int hovered = -1;
        public bool HasCategory(BuildingCategory category) => tabs.ContainsKey(category);
        public Button CardButton(int definition) => cards.TryGetValue(definition, out var button) ? button : null;
        public void Bind(List<BuildingCatalogCard> values, Action<int> onChoose, string hint)
        {
            models.Clear(); models.AddRange(values); choose = onChoose; Hint.text = hint;
            var categories = new List<BuildingCategory>();
            if (models.Count > 0) categories.Add(BuildingCategory.None);
            for (var i = 0; i < Categories.Length; i++)
                if (models.Any(m => (m.Category & (BuildingCategory)(1 << i)) != 0)) categories.Add((BuildingCategory)(1 << i));
            if (!categories.Contains(SelectedCategory)) SelectedCategory = BuildingCategory.None;
            foreach (var key in tabs.Keys.Except(categories).ToArray()) { Destroy(tabs[key].gameObject); tabs.Remove(key); }
            foreach (var category in categories)
            {
                if (!tabs.TryGetValue(category, out var button))
                {
                    button = Instantiate(TabTemplate, Tabs); button.gameObject.SetActive(true);
                    var selected = category; button.onClick.AddListener(() => SelectCategory(selected)); tabs.Add(category, button);
                }
                button.GetComponentInChildren<TMP_Text>().text = category == BuildingCategory.None ? "全部" : Categories[Array.IndexOf(Enumerable.Range(0, 10).Select(i => (BuildingCategory)(1 << i)).ToArray(), category)];
                button.image.color = category == SelectedCategory ? new Color(.30f, .48f, .56f) : new Color(.14f, .20f, .27f);
                button.transform.SetAsLastSibling();
            }
            RefreshCards();
        }
        public void SelectCategory(BuildingCategory category)
        {
            if (!tabs.ContainsKey(category)) return;
            SelectedCategory = category; HideTooltip();
            foreach (var pair in tabs) pair.Value.image.color = pair.Key == category ? new Color(.30f, .48f, .56f) : new Color(.14f, .20f, .27f);
            RefreshCards(); CardScroll.horizontalNormalizedPosition = 0;
        }
        void RefreshCards()
        {
            var visible = models.Where(m => SelectedCategory == BuildingCategory.None || (m.Category & SelectedCategory) != 0).ToList();
            foreach (var key in cards.Keys.Where(k => !visible.Any(m => m.Definition == k)).ToArray())
            { if (hovered == key) HideTooltip(); Destroy(cards[key].gameObject); cards.Remove(key); }
            foreach (var model in visible)
            {
                if (!cards.TryGetValue(model.Definition, out var button))
                {
                    button = Instantiate(CardTemplate, Cards); button.gameObject.SetActive(true);
                    var definition = model.Definition; button.onClick.AddListener(() => { HideTooltip(); choose?.Invoke(definition); });
                    var hover = button.gameObject.AddComponent<BuildingCatalogHover>(); hover.Bar = this; hover.Definition = definition;
                    cards.Add(definition, button);
                }
                button.name = "Building · " + model.Name; button.interactable = model.Allowed;
                var icon = button.transform.Find("Icon").GetComponent<Image>(); icon.sprite = model.Icon; icon.enabled = model.Icon != null;
                var fallback = button.transform.Find("Fallback").GetComponent<TMP_Text>(); fallback.text = string.IsNullOrEmpty(model.Name) ? "?" : model.Name.Substring(0, 1); fallback.gameObject.SetActive(model.Icon == null);
                button.transform.Find("Label").GetComponent<TMP_Text>().text = model.Name;
                icon.color = model.Allowed ? Color.white : new Color(.5f, .5f, .5f);
                button.transform.SetAsLastSibling();
            }
            if (hovered >= 0) ShowTooltip(hovered);
        }
        public void ShowTooltip(int definition)
        {
            var model = models.FirstOrDefault(m => m.Definition == definition);
            if (model == null || !cards.ContainsKey(definition) || !isActiveAndEnabled) { HideTooltip(); return; }
            hovered = definition; TooltipText.textWrappingMode = TextWrappingModes.Normal; TooltipText.overflowMode = TextOverflowModes.Ellipsis;
            TooltipText.text = model.Tooltip; Tooltip.gameObject.SetActive(true);
            var parent = (RectTransform)Tooltip.parent;
            var width = Mathf.Min(440, parent.rect.width - 24);
            Tooltip.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            var height = Mathf.Min(parent.rect.height - 24, TooltipText.GetPreferredValues(model.Tooltip, width - 28, float.PositiveInfinity).y + 28);
            Tooltip.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, cards[definition].transform);
            var trayBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parent, transform);
            Tooltip.localPosition = new Vector3(Mathf.Clamp(bounds.center.x, parent.rect.xMin + width / 2 + 12, parent.rect.xMax - width / 2 - 12),
                Mathf.Clamp(trayBounds.max.y + 16, parent.rect.yMin + 12, parent.rect.yMax - height - 12), 0);
            Tooltip.SetAsLastSibling();
        }
        public void HideTooltip() { hovered = -1; if (Tooltip != null) Tooltip.gameObject.SetActive(false); }
        void OnDisable() => HideTooltip();
    }
}
