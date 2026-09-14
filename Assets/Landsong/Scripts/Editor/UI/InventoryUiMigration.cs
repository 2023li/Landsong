#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    /// <summary>Explicit authoring step for the user's inventory layout. Never runs during gameplay.</summary>
    public static class InventoryUiMigration
    {
        public const string Path = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/UI_GamePanel_库存.prefab";
        const string GamePath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab";
        static TMP_FontAsset font;
        static readonly Color Ink = new Color(.91f, .91f, .88f);
        static readonly Color Panel = new Color(.14f, .17f, .19f);

        [MenuItem("Landsong/UI/配置库存双视图与资源详情")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在编辑模式配置库存预制体。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && (stage.assetPath == Path || stage.assetPath == GamePath) && stage.scene.isDirty)
                throw new InvalidOperationException("库存或游戏根预制体有尚未保存的编辑，请先保存。");
            var game = PrefabUtility.LoadPrefabContents(Path);
            try { Configure(game.GetComponent<UI_GamePanel_Inventory>()); PrefabUtility.SaveAsPrefabAsset(game, Path); }
            finally { PrefabUtility.UnloadPrefabContents(game); }
            // The nested prefab keeps its original GUID. Remove only obsolete inventory overrides.
            game = PrefabUtility.LoadPrefabContents(GamePath);
            try
            {
                var view = game.GetComponent<UI_GamePanel>().InventoryWindow;
                var instance = PrefabUtility.GetNearestPrefabInstanceRoot(view);
                var modifications = PrefabUtility.GetPropertyModifications(instance);
                if (modifications != null)
                {
                    var old = new[] { "PrimaryRows", "SecondaryRows", "PrimaryScroll", "RowTemplate", "QuantityTemplate", "StorageGrid", "PendingGrid", "DragRoot", "DragLabel", "DragSpace" };
                    PrefabUtility.SetPropertyModifications(instance, modifications.Where(m => !(m.target is UI_GamePanel_Inventory) || !old.Contains(m.propertyPath)).ToArray());
                }
                view.ValidateConfiguration(); PrefabUtility.SaveAsPrefabAsset(game, GamePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
            AssetDatabase.SaveAssets();
            return "库存双视图、待存区、底栏操作和资源详情已配置。";
        }

        public static void Configure(UI_GamePanel_Inventory view)
        {
            if (view == null) throw new InvalidOperationException("缺少库存面板。");
            var root = view.transform;
            var body = Need(root, "库存内容");
            // Leave room for the authored lists as well as the new fixed operation footer.
            var bodyRect = (RectTransform)body; bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, -220);
            bodyRect.anchoredPosition = new Vector2(bodyRect.anchoredPosition.x, -20);
            font = Need(body, "标题栏/Label").GetComponent<TMP_Text>().font;
            view.ContentRoot = body.gameObject; view.Title = Need(body, "标题栏/Label").GetComponent<TMP_Text>();
            view.CloseButton = Need(body, "标题栏/关闭").GetComponent<Button>();
            view.BuildingsButton = Need(body, "第二行/第二行容器/btn_按建筑显示").GetComponent<Button>();
            view.ResourcesButton = Need(body, "第二行/第二行容器/btn_按资源显示").GetComponent<Button>();
            view.BuildingsScroll = Need(body, "滚动视图父物体/建筑显示滚动视图").GetComponent<ScrollRect>();
            view.ResourcesScroll = Need(body, "滚动视图父物体/资源显示滚动视图").GetComponent<ScrollRect>();
            ConfigureScroll(view.BuildingsScroll); ConfigureScroll(view.ResourcesScroll);
            view.Interaction = Add<UI_GamePanel_InteractionLock>(root);
            var building = Need(view.BuildingsScroll.content, "模板_按建筑显示Item");
            view.BuildingTemplate = Add<UI_GamePanel_InventoryBuilding>(building);
            var b = view.BuildingTemplate;
            b.Information = Need(building, "建筑信息/txt_建筑信息").GetComponent<TMP_Text>();
            b.Locate = Need(building, "建筑信息/btn_定位到建筑").GetComponent<Button>();
            b.Pin = Need(building, "建筑信息/toggle_置顶").GetComponent<Toggle>();
            b.Slots = (RectTransform)Need(building, "格子容器");
            b.SlotTemplate = Slot(Need(b.Slots, "模板_库存格子_简陋库存格"), "txt_数量", "Image_资源图标", view);
            b.Interaction = Lock(building, b.Locate, b.Pin);
            var obsoleteLayout = b.Slots.GetComponent<HorizontalLayoutGroup>(); if (obsoleteLayout != null) Object.DestroyImmediate(obsoleteLayout);
            var fitter = b.Slots.GetComponent<ContentSizeFitter>(); if (fitter != null) Object.DestroyImmediate(fitter);
            b.GridLayout = Add<GridLayoutGroup>(b.Slots); b.GridLayout.cellSize = new Vector2(56, 56); b.GridLayout.spacing = new Vector2(6, 6);
            b.GridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount; b.GridLayout.constraintCount = 8;
            b.Layout = Add<LayoutElement>(building); b.Layout.minHeight = b.Layout.preferredHeight = 112;
            Stretch((RectTransform)building); Stretch(b.Slots); b.Slots.offsetMin = new Vector2(8, 8); b.Slots.offsetMax = new Vector2(-8, -48);
            var information = (RectTransform)Need(building, "建筑信息"); information.anchorMin = new Vector2(0, 1); information.anchorMax = Vector2.one;
            information.pivot = new Vector2(.5f, 1); information.anchoredPosition = Vector2.zero; information.sizeDelta = new Vector2(0, 44);
            var buildingText = (RectTransform)b.Information.transform; Stretch(buildingText); buildingText.offsetMin = new Vector2(10, 2); buildingText.offsetMax = new Vector2(-250, -2);
            b.Information.enableAutoSizing = false; b.Information.fontSize = 17; b.Information.color = Ink; b.Information.alignment = TextAlignmentOptions.MidlineLeft;
            var locateRect = (RectTransform)b.Locate.transform; locateRect.anchorMin = locateRect.anchorMax = new Vector2(1, .5f); locateRect.pivot = new Vector2(1, .5f);
            locateRect.anchoredPosition = new Vector2(-10, 0); locateRect.sizeDelta = new Vector2(124, 32);
            var pinRect = (RectTransform)b.Pin.transform; pinRect.anchorMin = pinRect.anchorMax = new Vector2(1, .5f); pinRect.pivot = new Vector2(1, .5f);
            pinRect.anchoredPosition = new Vector2(-144, 0); pinRect.sizeDelta = new Vector2(90, 32);
            foreach (var label in b.Locate.GetComponentsInChildren<TMP_Text>(true)) { label.fontSize = 16; label.enableAutoSizing = false; label.text = "定位到建筑"; }
            b.Pin.SetIsOnWithoutNotify(false);
            foreach (var label in b.Pin.GetComponentsInChildren<TMP_Text>(true)) { label.fontSize = 16; label.enableAutoSizing = false; }
            b.SlotTemplate.gameObject.SetActive(false); building.gameObject.SetActive(false);

            var resource = Need(view.ResourcesScroll.content, "模板_资源显示Item");
            view.ResourceTemplate = Add<UI_GamePanel_InventoryResource>(resource);
            var r = view.ResourceTemplate;
            r.Information = Need(resource, "txt_资源信息").GetComponent<TMP_Text>();
            r.Icon = Need(resource, "Image_资源图标").GetComponent<Image>(); r.Details = Need(resource, "btn_详情").GetComponent<Button>();
            r.Interaction = Lock(resource, r.Details);
            var resourceLayout = Add<LayoutElement>(resource); resourceLayout.minHeight = resourceLayout.preferredHeight = 88;
            r.Information.enableAutoSizing = false; r.Information.fontSize = 18;
            var infoRect = (RectTransform)r.Information.transform; Stretch(infoRect); infoRect.offsetMin = new Vector2(74, 6); infoRect.offsetMax = new Vector2(-90, -6);
            var ledgerRect = (RectTransform)r.Details.transform; ledgerRect.anchorMin = ledgerRect.anchorMax = new Vector2(1, .5f);
            ledgerRect.pivot = new Vector2(1, .5f); ledgerRect.anchoredPosition = new Vector2(-8, 0); ledgerRect.sizeDelta = new Vector2(74, 36);
            resource.gameObject.SetActive(false);

            view.PendingRoot = (RectTransform)Need(root, "等待入库区");
            view.PendingRoot.sizeDelta = new Vector2(view.PendingRoot.sizeDelta.x, -220);
            view.PendingRoot.anchoredPosition = new Vector2(view.PendingRoot.anchoredPosition.x, -20);
            // Remove the former grid/row adapters with empty references from this fixed area.
            foreach (var old in view.PendingRoot.GetComponents<UI_GamePanel_InventoryGrid>()) Object.DestroyImmediate(old);
            foreach (var old in view.PendingRoot.GetComponents<UI_GamePanel_RowPointerBinding>()) Object.DestroyImmediate(old);
            foreach (var old in view.PendingRoot.GetComponents<UI_GamePanel_InteractionLock>()) Object.DestroyImmediate(old);
            foreach (var old in view.PendingRoot.GetComponents<UI_Common_Click>()) Object.DestroyImmediate(old);
            foreach (var old in view.PendingRoot.GetComponents<Button>()) Object.DestroyImmediate(old);
            Add<UI_GamePanel_InventoryPendingDrop>(view.PendingRoot).Owner = view;
            var pendingScroll = Need(view.PendingRoot, "待存区_滚动视图").GetComponent<ScrollRect>(); ConfigureScroll(pendingScroll);
            view.PendingContent = pendingScroll.content;
            view.PendingTemplate = Slot(Need(view.PendingContent, "模板_待存区Item"), "txt_资源数量", "Image_资源Icon", view);
            var pendingLayout = Add<LayoutElement>(view.PendingTemplate.transform); pendingLayout.minHeight = pendingLayout.preferredHeight = 64;
            var pendingIcon = (RectTransform)view.PendingTemplate.Icon.transform;
            pendingIcon.anchorMin = pendingIcon.anchorMax = new Vector2(0, .5f); pendingIcon.pivot = new Vector2(0, .5f);
            pendingIcon.anchoredPosition = new Vector2(8, 0); pendingIcon.sizeDelta = new Vector2(46, 46);
            var pendingText = (RectTransform)view.PendingTemplate.Label.transform; Stretch(pendingText);
            pendingText.offsetMin = new Vector2(62, 4); pendingText.offsetMax = new Vector2(-8, -4);
            view.PendingTemplate.Label.alignment = TextAlignmentOptions.MidlineLeft; view.PendingTemplate.Label.color = Ink;
            view.PendingTemplate.Pending = true; view.PendingTemplate.gameObject.SetActive(false);
            Need(view.PendingRoot, "Icon").gameObject.SetActive(false);
            view.PendingSummary = Need(view.PendingRoot, "Label").GetComponent<TMP_Text>();
            view.PendingSummary.text = "待存区 · 入夜前清空";
            view.StoreAllButton = Need(view.PendingRoot, "btn_一键入库").GetComponent<Button>();
            ConfigureActions(view, Need(body, "底栏(预留)")); ConfigureResourceDetails(view);
            var drag = Child(root, "拖拽提示"); view.DragRoot = drag; view.DragSpace = (RectTransform)root;
            drag.anchorMin = drag.anchorMax = drag.pivot = new Vector2(.5f, .5f); drag.sizeDelta = new Vector2(230, 40);
            view.DragLabel = Label(drag, "文字", "", 20); Stretch((RectTransform)view.DragLabel.transform);
            Add<CanvasGroup>(drag).blocksRaycasts = false; drag.gameObject.SetActive(false);
            view.Interaction.Bindings = new[] { Bind(view.BuildingsButton, view.Interaction), Bind(view.ResourcesButton, view.Interaction),
                Bind(view.StoreAllButton, view.Interaction), Bind(view.Quantity, view.Interaction), Bind(view.MoveButton, view.Interaction),
                Bind(view.PendingButton, view.Interaction), Bind(view.DiscardButton, view.Interaction), Bind(view.CancelButton, view.Interaction),
                Bind(view.SortButton, view.Interaction), Bind(view.EconomyButton, view.Interaction) };
            view.BuildingsScroll.gameObject.SetActive(false); view.ResourcesScroll.gameObject.SetActive(true); view.ResourceDetailsRoot.SetActive(false);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.GetComponentInParent<TMP_InputField>(true) != null) continue;
                Add<UI_Common_TextBinding>(label.transform).Target = label; label.raycastTarget = false;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true)) Add<UI_Common_Click>(button.transform).Target = button;
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) EditorUtility.SetDirty(component);
            view.ValidateConfiguration();
        }

        static void ConfigureActions(UI_GamePanel_Inventory view, Transform footer)
        {
            var layout = Add<LayoutElement>(footer); layout.minHeight = layout.preferredHeight = 206;
            var group = Add<VerticalLayoutGroup>(footer); group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false; group.spacing = 4; group.padding = new RectOffset(8, 8, 4, 4);
            view.SelectionDetails = Label(footer, "选中详情", "点击物资查看详情", 16);
            Size(view.SelectionDetails.transform, 0, 108, true);
            var first = Child(footer, "物资操作"); Horizontal(first); Size(first, 0, 38, true);
            var quantityLabel = Label(first, "数量标签", "数量", 16); Size(quantityLabel.transform, 40, 36); quantityLabel.transform.SetAsFirstSibling(); quantityLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var inputRoot = Child(first, "数量"); Size(inputRoot, 72, 36);
            Add<Image>(inputRoot).color = new Color(.22f, .26f, .29f);
            view.Quantity = Add<TMP_InputField>(inputRoot);
            var inputText = Label(inputRoot, "输入文本", "1", 18); Stretch((RectTransform)inputText.transform);
            inputText.margin = new Vector4(5, 3, 5, 3);
            view.Quantity.textViewport = (RectTransform)inputText.transform; view.Quantity.textComponent = (TextMeshProUGUI)inputText;
            view.Quantity.contentType = TMP_InputField.ContentType.IntegerNumber; view.Quantity.characterLimit = 10; view.Quantity.text = "1";
            view.Quantity.targetGraphic = inputRoot.GetComponent<Image>(); Add<UI_Common_InputFocusBinding>(inputRoot).Target = view.Quantity;
            view.MoveButton = Button(first, "选择目标格", 104); view.PendingButton = Button(first, "移到待存区", 110);
            view.DiscardButton = Button(first, "丢弃…", 76); view.CancelButton = Button(first, "取消选择", 92);
            var second = Child(footer, "库存操作"); Horizontal(second); Size(second, 0, 38, true);
            view.SortButton = Button(second, "整理库存", 110); view.EconomyButton = Button(second, "btn_账单记录", 110);
        }

        static void ConfigureResourceDetails(UI_GamePanel_Inventory view) => BillUiMigration.ConfigureDetails(view);

        static UI_GamePanel_InventorySlot Slot(Transform root, string text, string icon, UI_GamePanel_Inventory owner)
        {
            var view = Add<UI_GamePanel_InventorySlot>(root); view.Owner = owner;
            view.Label = Need(root, text).GetComponent<TextMeshProUGUI>(); view.Icon = Need(root, icon).GetComponent<Image>();
            view.Background = root.GetComponent<Image>(); view.Select = Add<Button>(root); view.Select.targetGraphic = view.Background;
            if (text == "txt_数量")
            {
                var iconRect = (RectTransform)view.Icon.transform; Stretch(iconRect); iconRect.offsetMin = new Vector2(4, 18); iconRect.offsetMax = new Vector2(-4, -4);
                var countRect = (RectTransform)view.Label.transform; countRect.anchorMin = Vector2.zero; countRect.anchorMax = new Vector2(1, 0); countRect.pivot = new Vector2(.5f, 0);
                countRect.anchoredPosition = Vector2.zero; countRect.sizeDelta = new Vector2(0, 18); view.Label.alignment = TextAlignmentOptions.Center;
            }
            view.InteractionOwner = Lock(root, view.Select); view.Label.enableAutoSizing = false; view.Label.fontSize = 17;
            var select = view.Select.navigation; select.mode = Navigation.Mode.None; view.Select.navigation = select;
            return view;
        }

        static UI_GamePanel_InteractionLock Lock(Transform root, params Selectable[] controls)
        { var state = Add<UI_GamePanel_InteractionLock>(root); state.Bindings = controls.Select(c => Bind(c, state)).ToArray(); return state; }
        static UI_GamePanel_RowPointerBinding Bind(Selectable control, UI_GamePanel_InteractionLock owner)
        { var binding = Add<UI_GamePanel_RowPointerBinding>(control.transform); binding.Target = owner; binding.LockWhileSelected = control is TMP_InputField; return binding; }
        static void ConfigureScroll(ScrollRect scroll)
        {
            scroll.content.anchorMin = new Vector2(0, 1); scroll.content.anchorMax = Vector2.one; scroll.content.pivot = new Vector2(0, 1);
            scroll.content.anchoredPosition = Vector2.zero;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            if (scroll.horizontalScrollbar != null) scroll.horizontalScrollbar.gameObject.SetActive(false);
            var layout = Add<VerticalLayoutGroup>(scroll.content); layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; layout.spacing = 6;
            Add<ContentSizeFitter>(scroll.content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        static Transform Need(Transform root, string path) => root.Find(path) ?? throw new InvalidOperationException(root.name + " 缺少 " + path);
        static T Add<T>(Transform root) where T : Component { var existing = root.GetComponent<T>(); return existing != null ? existing : root.gameObject.AddComponent<T>(); }
        static RectTransform Child(Transform root, string name)
        { var existing = root.Find(name); if (existing != null) return (RectTransform)existing; var child = new GameObject(name, typeof(RectTransform)); child.layer = root.gameObject.layer; child.transform.SetParent(root, false); return (RectTransform)child.transform; }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static TMP_Text Label(Transform root, string name, string text, float size)
        { var rect = Child(root, name); var label = Add<TextMeshProUGUI>(rect); label.font = font; label.fontSize = size; label.color = Ink; label.text = text; label.enableAutoSizing = false; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; return label; }
        static Button Button(Transform root, string name, float width)
        { var rect = Child(root, name); Size(rect, width, 36); var image = Add<Image>(rect); image.color = new Color(.24f, .31f, .35f); var button = Add<Button>(rect); button.targetGraphic = image; var text = Label(rect, "文字", name, 16); text.alignment = TextAlignmentOptions.Center; Stretch((RectTransform)text.transform); return button; }
        static void Size(Transform root, float width, float height, bool flexible = false)
        { var layout = Add<LayoutElement>(root); layout.preferredWidth = width; layout.flexibleWidth = flexible ? 1 : 0; layout.minHeight = layout.preferredHeight = height; ((RectTransform)root).sizeDelta = new Vector2(width, height); }
        static void Horizontal(Transform root)
        { var group = Add<HorizontalLayoutGroup>(root); group.childControlWidth = group.childControlHeight = true; group.childForceExpandWidth = group.childForceExpandHeight = false; group.spacing = 6; }
    }
}
#endif
