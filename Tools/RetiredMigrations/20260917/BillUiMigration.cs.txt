#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class BillUiMigration
    {
        public const string GamePath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/UI_GamePanel.prefab";
        public const string BillPath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/UI_GamePanel_账单.prefab";
        static TMP_FontAsset font;
        [MenuItem("Landsong/UI/配置回合账单与资源详情")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play。");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isDirty) throw new InvalidOperationException("请先保存正在编辑的预制体。");
            const string old = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/UI_GamePanel_Economy.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(old) != null)
            { var error = AssetDatabase.MoveAsset(old, BillPath); if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error); }
            Edit(InventoryUiMigration.Path, go => ConfigureDetails(go.GetComponent<UI_GamePanel_Inventory>()));
            Edit(BillPath, go => ConfigureBill(go.GetComponent<UI_GamePanel_Economy>()));
            Edit(GamePath, go =>
            {
                var game = go.GetComponent<UI_GamePanel>();
                ConfigureDetails(game.InventoryWindow); ConfigureBill(game.EconomyWindow);
                game.EconomyWindow.name = "账单面板";
            });
            AssetDatabase.SaveAssets();
            return "回合账单五列表格、资源预测详情双滚动视图已配置。";
        }
        static void Edit(string path, Action<GameObject> action)
        {
            var go = PrefabUtility.LoadPrefabContents(path);
            try { action(go); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        public static void ConfigureDetails(UI_GamePanel_Inventory view)
        {
            font = view.Title.font;
            view.BuildingTemplate.gameObject.SetActive(false); view.ResourceTemplate.gameObject.SetActive(false); view.PendingTemplate.gameObject.SetActive(false);
            view.ResourceTemplate.Details.name = "btn_详情";
            var icon = view.ResourceTemplate.Icon.rectTransform;
            icon.anchorMin = icon.anchorMax = new Vector2(0, .5f); icon.pivot = new Vector2(0, .5f);
            icon.anchoredPosition = new Vector2(12, 0); icon.sizeDelta = new Vector2(56, 56);
            var information = view.ResourceTemplate.Information;
            Anchor((RectTransform)information.transform, Vector2.zero, Vector2.one, new Vector2(84, 8), new Vector2(-100, -8));
            information.alignment = TextAlignmentOptions.MidlineLeft;
            foreach (var label in view.ResourceTemplate.Details.GetComponentsInChildren<TMP_Text>(true)) label.text = "详情";
            view.EconomyButton.name = "btn_账单记录";
            foreach (var label in view.EconomyButton.GetComponentsInChildren<TMP_Text>(true)) label.text = "账单记录";
            view.ResourceDetailsRoot.name = "资源详情子面板";
            var panel = view.ResourceDetailsTitle.transform.parent; panel.name = "详情";
            view.ResourceDetailsTitle.text = "资源详情";
            view.IncomeScroll.name = "产出滚动视图";
            if (view.ExpenseScroll == null)
            {
                var existing = panel.Find("消耗滚动视图");
                view.ExpenseScroll = existing != null ? existing.GetComponent<ScrollRect>() : UnityEngine.Object.Instantiate(view.IncomeScroll, panel);
            }
            view.ExpenseScroll.name = "消耗滚动视图";
            view.ExpenseBody = view.ExpenseScroll.content.GetComponent<TMP_Text>();
            view.ForecastStatus = Label(panel, "预测状态", "本回合预计收支 · 随当前条件更新", 17);
            Anchor((RectTransform)view.ForecastStatus.transform, new Vector2(0, 1), Vector2.one, new Vector2(20, -100), new Vector2(-20, -56));
            ConfigureColumn(view.IncomeScroll, view.IncomeBody, 0, .5f);
            ConfigureColumn(view.ExpenseScroll, view.ExpenseBody, .5f, 1);
            var income = Label(panel, "产出标题", "本回合预计产出", 24);
            var expense = Label(panel, "消耗标题", "本回合预计消耗", 24);
            Anchor((RectTransform)income.transform, new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(20, -142), new Vector2(-12, -104));
            Anchor((RectTransform)expense.transform, new Vector2(.5f, 1), Vector2.one, new Vector2(12, -142), new Vector2(-20, -104));
            view.ResourceDetailsRoot.SetActive(false);
            BindCommon(view.gameObject); view.ValidateConfiguration();
        }
        static void ConfigureColumn(ScrollRect scroll, TMP_Text body, float left, float right)
        {
            Anchor((RectTransform)scroll.transform, new Vector2(left, 0), new Vector2(right, 1), new Vector2(left == 0 ? 20 : 12, 20), new Vector2(right == 1 ? -20 : -12, -148));
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            body.text = "正在计算…"; body.fontSize = 21; body.textWrappingMode = TextWrappingModes.Normal;
            body.margin = new Vector4(12, 12, 12, 12);
            Add<Image>(scroll.viewport).color = new Color(.22f, .24f, .25f, .7f);
            scroll.content.anchoredPosition = Vector2.zero;
        }
        public static void ConfigureBill(UI_GamePanel_Economy view)
        {
            font = view.Title.font; view.Title.text = "账单";
            if (view.name == "UI_GamePanel_Economy") view.name = "UI_GamePanel_账单";
            view.ContentRoot.name = "账单内容";
            var body = (RectTransform)view.ContentRoot.transform;
            Anchor(body, new Vector2(.14f, .12f), new Vector2(.86f, .88f), Vector2.zero, Vector2.zero);
            view.BillScroll = body.GetComponent<ScrollRect>();
            var scroll = view.BillScroll; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            Anchor(scroll.viewport, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -104));
            var content = scroll.content; content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
            Vertical(content, 14, 0); Add<ContentSizeFitter>(content).verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (Transform child in content) if (child.name != "模板_回合账单" && child.name != "暂无账单") child.gameObject.SetActive(false);
            view.EmptyState = Label(content, "暂无账单", "暂无账单记录。完成回合结算后显示。", 22); Height(view.EmptyState.transform, 72);
            view.ScopeLabel = Label(body, "账单范围", "全城 · 已结算回合；库存为当回合结算后已入库数量。", 18);
            Anchor((RectTransform)view.ScopeLabel.transform, new Vector2(0, 1), Vector2.one, new Vector2(20, -100), new Vector2(-140, -54));
            view.AllSources = Button(body, "查看全城");
            Anchor((RectTransform)view.AllSources.transform, Vector2.one, Vector2.one, new Vector2(-132, -96), new Vector2(-20, -56));
            view.AllSources.gameObject.SetActive(false);
            var template = Child(content, "模板_回合账单"); view.TurnTemplate = Add<UI_GamePanel_BillTurn>(template);
            Add<Image>(template).color = new Color(.36f, .36f, .36f, .92f);
            Vertical(template, 4, 12); Add<LayoutElement>(template).minHeight = 190;
            var turn = view.TurnTemplate; turn.TurnLabel = Label(template, "回合", "351回合", 26); Height(turn.TurnLabel.transform, 34);
            var header = Child(template, "表头"); Height(header, 34); Columns(header);
            foreach (var name in new[] { "资源名", "产出", "消耗", "净量", "库存" }) Cell(header, name, name);
            turn.Rows = Child(template, "资源行"); Vertical(turn.Rows, 0, 0);
            var row = Child(turn.Rows, "模板_资源账单"); Height(row, 36); Columns(row);
            turn.RowTemplate = Add<UI_GamePanel_BillRow>(row);
            var r = turn.RowTemplate; r.Resource = Cell(row, "资源名", "石头"); r.Income = Cell(row, "产出", "20"); r.Expense = Cell(row, "消耗", "25"); r.Net = Cell(row, "净量", "-5"); r.Stored = Cell(row, "库存", "500");
            row.gameObject.SetActive(false); template.gameObject.SetActive(false);
            BindCommon(view.gameObject); view.ValidateConfiguration();
        }
        static void BindCommon(GameObject root)
        {
            foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                if (label.GetComponentInParent<TMP_InputField>(true) == null) Add<UI_Common_TextBinding>(label.transform).Target = label;
            foreach (var button in root.GetComponentsInChildren<Button>(true)) Add<UI_Common_Click>(button.transform).Target = button;
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) EditorUtility.SetDirty(component);
        }
        static T Add<T>(Transform t) where T : Component { var c = t.GetComponent<T>(); return c != null ? c : t.gameObject.AddComponent<T>(); }
        static RectTransform Child(Transform parent, string name)
        { var t = parent.Find(name); if (t != null) return (RectTransform)t; var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer; go.transform.SetParent(parent, false); return (RectTransform)go.transform; }
        static void Anchor(RectTransform t, Vector2 min, Vector2 max, Vector2 low, Vector2 high) { t.anchorMin = min; t.anchorMax = max; t.offsetMin = low; t.offsetMax = high; }
        static TMP_Text Label(Transform parent, string name, string text, float size)
        { var label = Add<TextMeshProUGUI>(Child(parent, name)); label.font = font; label.fontSize = size; label.color = Color.white; label.text = text; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal; return label; }
        static void Height(Transform t, float value) { var l = Add<LayoutElement>(t); l.minHeight = l.preferredHeight = value; }
        static void Vertical(Transform t, float gap, int padding)
        { var l = Add<VerticalLayoutGroup>(t); l.childControlWidth = l.childControlHeight = true; l.childForceExpandWidth = true; l.childForceExpandHeight = false; l.spacing = gap; l.padding = new RectOffset(padding, padding, padding, padding); }
        static void Columns(Transform t)
        { var l = Add<HorizontalLayoutGroup>(t); l.childControlWidth = l.childControlHeight = true; l.childForceExpandWidth = true; l.childForceExpandHeight = true; l.spacing = 8; }
        static TMP_Text Cell(Transform parent, string name, string text)
        { var label = Label(parent, name, text, 24); label.alignment = name == "资源名" ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center; var l = Add<LayoutElement>(label.transform); l.minWidth = 0; l.preferredWidth = 0; l.flexibleWidth = name == "资源名" ? 1.4f : 1; return label; }
        static Button Button(Transform parent, string name)
        { var t = Child(parent, name); Add<Image>(t).color = new Color(.24f, .31f, .35f); var b = Add<Button>(t); b.targetGraphic = t.GetComponent<Image>(); var label = Label(t, "文字", name, 18); label.alignment = TextAlignmentOptions.Center; Anchor((RectTransform)label.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); return b; }
        public static string Inspect()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            var text = new StringBuilder("stage=" + stage?.assetPath + " dirty=" + stage?.scene.isDirty + "\n");
            var go = PrefabUtility.LoadPrefabContents(GamePath);
            try
            {
                foreach (var view in go.GetComponentsInChildren<UI_GamePanel_View>(true).Where(v => v.PanelId == GamePanelId.Inventory || v.PanelId == GamePanelId.Economy))
                    foreach (var t in view.GetComponentsInChildren<Transform>(true))
                        text.AppendLine(AnimationUtility.CalculateTransformPath(t, go.transform) + " | " + string.Join(",", t.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().Name)));
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
            File.WriteAllText("Library/LandsongEcs/bill-ui-hierarchy.txt", text.ToString());
            return text.ToString();
        }
    }
}
#endif
