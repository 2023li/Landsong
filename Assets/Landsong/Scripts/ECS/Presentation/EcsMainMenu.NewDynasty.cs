using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsMainMenu
    {
        public GameObject NewDynastyPanel { get; private set; }
        public TMP_Dropdown DifficultySelection { get; private set; }
        public Button CreateDynastyButton { get; private set; }
        TMP_InputField dynastyName;
        TextMeshProUGUI mapInfo;
        Image mapPreview;

        void InitializeNewDynasty(Transform canvas)
        {
            NewDynastyPanel = InterfaceWidgets.Modal("New dynasty configuration", canvas, 500, out var card);
            InterfaceWidgets.Text("开始新王朝", InterfaceWidgets.Rect("Title", card, new Vector2(.04f, .88f), new Vector2(.96f, 1)), Status.font, 28);
            var mapRows = InterfaceWidgets.Scroll(card, "Map options", new Vector2(.04f, .16f), new Vector2(.50f, .88f));
            var rows = InterfaceWidgets.Scroll(card, "New dynasty options", new Vector2(.52f, .16f), new Vector2(.96f, .88f));
            mapRows.GetComponentInParent<ScrollRect>().verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            rows.GetComponentInParent<ScrollRect>().verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            AddCaption(rows, "王朝名称", 38);
            dynastyName = InterfaceWidgets.Input("新王朝名称（可留空）", rows, Status.font);
            AddCaption(mapRows, "选择地图", 38);
            MapSelection.transform.SetParent(mapRows, false);
            var layout = MapSelection.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 48;
            MapSelection.interactable = HasMaps;
            mapInfo = AddCaption(mapRows, "", 160);
            var preview = InterfaceWidgets.Rect("Map thumbnail", mapRows, Vector2.zero, Vector2.one);
            preview.gameObject.AddComponent<LayoutElement>().preferredHeight = 160;
            mapPreview = preview.gameObject.AddComponent<Image>();
            mapPreview.preserveAspect = true;
            mapPreview.raycastTarget = false;

            AddCaption(rows, "难度", 38);
            // Reuse the scene-authored TMP dropdown template and font.
            DifficultySelection = Instantiate(MapSelection, rows);
            DifficultySelection.name = "Difficulty selection";
            DifficultySelection.onValueChanged = new TMP_Dropdown.DropdownEvent();
            DifficultySelection.ClearOptions();
            DifficultySelection.AddOptions(new List<string> { "简单", "普通", "困难" });
            DifficultySelection.SetValueWithoutNotify(1);
            DifficultySelection.interactable = true;
            AddCaption(rows, "难度数值效果待策划定义，当前选择暂不改变游戏规则。", 68);
            AddCaption(rows, "特殊规则", 38);
            InterfaceWidgets.Button("不刷新小偷（暂未开放）", rows, Status.font, null, 48);
            AddCaption(rows, "特殊规则待策划完成后开放配置。", 55);

            var actions = InterfaceWidgets.Rect("New dynasty actions", card, new Vector2(.04f, .02f), new Vector2(.96f, .14f));
            var group = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 12;
            group.childForceExpandWidth = true;
            CreateDynastyButton = InterfaceWidgets.Button("建立王朝", actions, Status.font, () =>
            {
                if (!HasMaps) { mapInfo.text = "未配置地图，请先添加可用地图。"; return; }
                TryStart(() => EcsSceneFlow.NewGame(Catalog.Maps[Mathf.Clamp(MapSelection.value, 0, Catalog.Maps.Length - 1)].Id, dynastyName.text));
            });
            InterfaceWidgets.Button("返回主菜单", actions, Status.font, CloseManagement);
            foreach (var button in actions.GetComponentsInChildren<Button>())
                button.GetComponentInChildren<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
            MapSelection.onValueChanged.AddListener(_ => UpdateMapDescription());
            UpdateMapDescription();
            NewDynastyPanel.SetActive(false);
        }

        TextMeshProUGUI AddCaption(Transform parent, string text, float height, int size = 18)
        {
            var row = InterfaceWidgets.Rect("Caption", parent, Vector2.zero, Vector2.one);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            return InterfaceWidgets.Text(text, row, Status.font, size);
        }

        void OpenNewDynasty()
        {
            if (EcsSceneFlow.Busy) return;
            CloseManagement();
            returnSelection = StartButton.gameObject;
            managementModal = NewDynastyPanel;
            menuGroup.interactable = false;
            NewDynastyPanel.SetActive(true);
            UpdateMapDescription();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(dynastyName.gameObject);
        }

        void UpdateMapDescription()
        {
            CreateDynastyButton.interactable = MapSelection.interactable = HasMaps;
            if (!HasMaps)
            {
                mapInfo.text = "未配置地图，请先添加可用地图。";
                mapPreview.gameObject.SetActive(false);
                return;
            }
            var map = Catalog.Maps[Mathf.Clamp(MapSelection.value, 0, Catalog.Maps.Length - 1)];
            mapInfo.text = map.DisplayName + "\n" + (string.IsNullOrWhiteSpace(map.Description)
                ? "在此地图建立新王朝。白天建造，夜晚巡逻/作战。" : map.Description);
            mapPreview.sprite = map.Thumbnail;
            mapPreview.gameObject.SetActive(map.Thumbnail != null);
        }
    }
}
