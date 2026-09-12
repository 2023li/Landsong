using System;
using Moyo.Unity;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Landsong.Editor.UI
{
    /// <summary>不访问真实场景、配置资产或玩家数据的预览制作回归。</summary>
    public static class UIPreviewVerification
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("预览制作验证需在非 Play 状态执行。");
            var scene = EditorSceneManager.NewPreviewScene();
            var profile = ScriptableObject.CreateInstance<UIPreviewProfile>();
            GameObject root = null;
            try
            {
                profile.SetDefaults(UIPreviewKind.Save);
                root = NewObject("验证视图", scene, null);
                root.SetActive(false);
                var owner = root.AddComponent<UIPanelBase>();
                owner.ConfigurePanel(root.AddComponent<CanvasGroup>());
                var heading = NewObject("标题", scene, root.transform).AddComponent<TextMeshProUGUI>();
                var container = (RectTransform)NewObject("列表", scene, root.transform).transform;
                var template = (RectTransform)NewObject("模板", scene, container).transform;
                template.gameObject.SetActive(false);
                template.anchoredPosition = new Vector2(17, -23);
                var originalTemplateSize = template.sizeDelta;
                var originalContainerSize = container.sizeDelta;
                // 两个同名子对象，验证映射依据显式模板引用及序号，而非对象名称。
                var title = NewObject("文字", scene, template).AddComponent<TextMeshProUGUI>();
                var detail = NewObject("文字", scene, template).AddComponent<TextMeshProUGUI>();
                var texts = new[] { new UIPreviewTextBinding("title", heading, "运行时初始标题") };
                var lists = new[] { new UIPreviewListBinding("slots", container, template,
                    new TMP_Text[] { title, detail }, new[] { 0, 1 }) };
                var marker = UIPreviewBuilder.Apply(owner, profile, texts, lists);
                Require(heading.text == "存档" && owner.PreviewBindings.Count == 1 && root.activeSelf,
                    "正式预制体字段显示可读示例，并配置运行清理引用");
                Require(marker.SampleObjects.Length == 3 && !template.gameObject.activeSelf,
                    "从显式模板生成代表性条目，不启用原始模板");
                var first = marker.SampleObjects[0];
                Require(first.CompareTag("EditorOnly") && first.activeSelf
                    && first.transform.GetChild(0).GetComponent<TMP_Text>().text == "第 18 回合 · 黎明"
                    && first.transform.GetChild(1).GetComponent<TMP_Text>().text == "自动节点",
                    "同名文字仍正确映射，样例条目标记为 EditorOnly");
                Require(((RectTransform)first.transform).anchoredPosition == template.anchoredPosition
                    && ((RectTransform)first.transform).sizeDelta == originalTemplateSize,
                    "未配置网格时保持现有模板布局行为");
                owner.ValidateConfiguration();
                marker.PrepareRuntime();
                Require(heading.text == "运行时初始标题" && !first.activeSelf,
                    "运行准备恢复真实初值，样例不进入交互");
                marker = UIPreviewBuilder.Apply(owner, profile, texts, lists);
                Require(marker.SampleObjects.Length == 3 && container.childCount == 4,
                    "反复刷新样例不累积重复条目");
                var authoredLayout = UIPreviewListLayout.GraphGrid();
                authoredLayout.columnCount = 2;
                authoredLayout.padding = new Vector2(10, 15);
                authoredLayout.spacing = new Vector2(20, 10);
                var restoredLayout = JsonUtility.FromJson<UIPreviewListLayout>(JsonUtility.ToJson(authoredLayout));
                Require(restoredLayout.mode == UIPreviewLayoutMode.Grid && restoredLayout.columnCount == 2
                    && restoredLayout.padding == authoredLayout.padding && restoredLayout.spacing == authoredLayout.spacing,
                    "网格配方布局参数可序列化还原");
                lists[0].layout = restoredLayout;
                marker = UIPreviewBuilder.Apply(owner, profile, texts, lists);
                Require(((RectTransform)marker.SampleObjects[0].transform).anchoredPosition == new Vector2(10, -15)
                    && ((RectTransform)marker.SampleObjects[1].transform).anchoredPosition == new Vector2(250, -15)
                    && ((RectTransform)marker.SampleObjects[2].transform).anchoredPosition == new Vector2(10, -155),
                    "显式网格依列数、尺寸、间距与留白排列样例，不重叠");
                marker = UIPreviewBuilder.Apply(owner, profile, texts, lists);
                Require(marker.SampleObjects.Length == 3 && container.childCount == 4
                    && ((RectTransform)marker.SampleObjects[2].transform).anchoredPosition == new Vector2(10, -155),
                    "重复刷新配方保留网格排列，样例数量稳定");
                Require(template.anchoredPosition == new Vector2(17, -23) && template.sizeDelta == originalTemplateSize
                    && container.sizeDelta == originalContainerSize, "网格仅改变示例副本，不污染正式模板或容器布局");
                bool invalidLayoutRejected = false;
                lists[0].layout.columnCount = 0;
                try { UIPreviewBuilder.Apply(owner, profile, texts, lists); }
                catch (InvalidOperationException) { invalidLayoutRejected = true; }
                Require(invalidLayoutRejected && marker.SampleObjects.Length == 3,
                    "非法布局作为配置错误报告，校验失败不清空已有示例");
                lists[0].layout = null;
                lists[0].textFormats = new[] { "{0} · {1}", "" };
                marker = UIPreviewBuilder.Apply(owner, profile, texts, lists);
                Require(marker.SampleObjects[0].transform.GetChild(0).GetComponent<TMP_Text>().text == "第 18 回合 · 黎明 · 自动节点"
                    && marker.SampleObjects[0].transform.GetChild(1).GetComponent<TMP_Text>().text == "自动节点",
                    "单个文字控件可组合多列，空格式保留原列绑定");
                lists[0] = new UIPreviewListBinding("固定详情", container, template,
                    new TMP_Text[] { title }, new[] { 0 }, textKeys: new[] { "title", "title" });
                marker = UIPreviewBuilder.Apply(owner, profile, texts, lists);
                Require(marker.SampleObjects.Length == 2
                    && marker.SampleObjects[1].transform.GetChild(0).GetComponent<TMP_Text>().text == "存档",
                    "固定语义文字可生成配方条目，并替换旧列表样例");
                lists[0].textFormats = new[] { "{5}" };
                bool invalidFormatRejected = false;
                try { UIPreviewBuilder.Apply(owner, profile, texts, lists); }
                catch (InvalidOperationException) { invalidFormatRejected = true; }
                Require(invalidFormatRejected && marker.SampleObjects.Length == 2,
                    "无效列格式在制作前报告并保留已有示例");
                UIPreviewBuilder.ClearSamples(marker);
                Require(marker.SampleObjects.Length == 0 && container.childCount == 1
                    && heading.text == "运行时初始标题", "样例清理完整保留原模板及初始文字");
                return "UI 预览制作验证通过：15 项；未读取 ECS、玩家存档或改写正式资产。";
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(profile);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static GameObject NewObject(string name, Scene scene, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(value, scene);
            if (parent != null) value.transform.SetParent(parent, false);
            return value;
        }
        private static void Require(bool condition, string invariant)
        { if (!condition) throw new InvalidOperationException("预览制作验证失败：" + invariant); }
    }
}
