using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.Editor.UI
{
    public enum UIPreviewLayoutMode
    {
        [LabelText("沿用模板布局")] Template,
        [LabelText("按网格排列样例")] Grid
    }

    [Serializable]
    public sealed class UIPreviewListLayout
    {
        [LabelText("样例布局方式")] public UIPreviewLayoutMode mode;
        [LabelText("每行列数"), Min(1)] public int columnCount = 3;
        [LabelText("样例节点尺寸")] public Vector2 cellSize = new Vector2(220, 130);
        [LabelText("样例节点间距")] public Vector2 spacing = new Vector2(20, 20);
        [LabelText("左侧与顶部留白")] public Vector2 padding = new Vector2(24, 24);

        public static UIPreviewListLayout GraphGrid() => new UIPreviewListLayout { mode = UIPreviewLayoutMode.Grid };
        public UIPreviewListLayout Copy() => new UIPreviewListLayout
        { mode = mode, columnCount = columnCount, cellSize = cellSize, spacing = spacing, padding = padding };

        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(UIPreviewLayoutMode), mode)) throw new InvalidOperationException("样例布局方式无效。");
            if (mode == UIPreviewLayoutMode.Template) return;
            static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
            if (columnCount < 1 || !Finite(cellSize) || !Finite(spacing) || !Finite(padding)
                || cellSize.x <= 0 || cellSize.y <= 0 || spacing.x < 0 || spacing.y < 0 || padding.x < 0 || padding.y < 0)
                throw new InvalidOperationException("样例网格必须配置正数列数与节点尺寸，间距和留白不能为负数。");
        }

        internal void Apply(RectTransform sample, int index)
        {
            if (mode == UIPreviewLayoutMode.Template) return;
            sample.anchorMin = sample.anchorMax = sample.pivot = new Vector2(0, 1);
            sample.sizeDelta = cellSize;
            sample.anchoredPosition = new Vector2(padding.x + index % columnCount * (cellSize.x + spacing.x),
                -padding.y - index / columnCount * (cellSize.y + spacing.y));
        }
    }

    [Serializable]
    public sealed class UIPreviewTextBinding
    {
        [LabelText("样例语义键")] public string key;
        [LabelText("文字控件"), Required] public TMP_Text target;
        [LabelText("运行时初始内容")] public string runtimeText = string.Empty;
        public UIPreviewTextBinding(string key, TMP_Text target, string runtimeText = "")
        { this.key = key; this.target = target; this.runtimeText = runtimeText; }
    }

    [Serializable]
    public sealed class UIPreviewListBinding
    {
        [LabelText("样例语义键")] public string key;
        [LabelText("列表容器"), Required] public RectTransform container;
        [LabelText("已配置条目模板"), Required] public RectTransform template;
        [LabelText("模板中各列文字")] public TMP_Text[] textTargets;
        [LabelText("样例列索引")] public int[] columns;
        [LabelText("样例排列配置")] public UIPreviewListLayout layout = new UIPreviewListLayout();
        [LabelText("每个文字控件的列格式")] public string[] textFormats = Array.Empty<string>();
        [LabelText("改用固定文字语义键生成条目")] public string[] textKeys = Array.Empty<string>();
        public UIPreviewListBinding(string key, RectTransform container, RectTransform template,
            TMP_Text[] textTargets, int[] columns = null, UIPreviewListLayout layout = null, string[] textFormats = null, string[] textKeys = null)
        {
            this.key = key; this.container = container; this.template = template; this.textTargets = textTargets;
            this.columns = columns ?? Enumerable.Range(0, textTargets?.Length ?? 0).ToArray();
            this.layout = layout?.Copy() ?? new UIPreviewListLayout();
            this.textFormats = textFormats == null ? Array.Empty<string>() : (string[])textFormats.Clone();
            this.textKeys = textKeys == null ? Array.Empty<string>() : (string[])textKeys.Clone();
        }
    }

    /// <summary>资源迁移器与美术工具共用的预览制作入口；只有编辑器能调用。</summary>
    public static class UIPreviewBuilder
    {
        public static UIPreviewProfile EnsureDefaultProfile(string assetPath, UIPreviewKind kind)
        {
            assetPath = assetPath.Replace('\\', '/');
            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) || !assetPath.Contains("/Editor/"))
                throw new ArgumentException("预览配置必须保存在 Assets 内的 Editor 目录，避免进入正式内容构建。", nameof(assetPath));
            var existing = AssetDatabase.LoadAssetAtPath<UIPreviewProfile>(assetPath);
            if (existing != null) return existing; // 保留美术已经编辑的样例。
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null || File.Exists(assetPath))
                throw new InvalidOperationException($"{assetPath} 已被其他资产使用。");
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath) ?? throw new InvalidOperationException("预览配置目录无效。"));
            AssetDatabase.Refresh();
            var profile = ScriptableObject.CreateInstance<UIPreviewProfile>();
            profile.SetDefaults(kind);
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssetIfDirty(profile);
            return profile;
        }

        public static UIPreviewOnly Apply(UIViewBase owner, UIPreviewProfile profile,
            UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists, bool recordUndo = false)
        {
            if (Application.isPlaying) throw new InvalidOperationException("编辑预览不能在真实运行会话中应用。");
            if (owner == null || profile == null) throw new ArgumentException("必须提供所属视图和预览数据。");
            texts ??= Array.Empty<UIPreviewTextBinding>();
            lists ??= Array.Empty<UIPreviewListBinding>();
            Validate(owner, profile, texts, lists);
            using var rootLayout = UiPanelLayoutAuthoring.Preserve(owner.transform as RectTransform);
            // 此查询仅发生在 Editor 资源制作阶段；运行时使用下面写入的 previewBindings。
            var marker = owner.GetComponent<UIPreviewOnly>();
            if (marker == null)
                marker = recordUndo ? Undo.AddComponent<UIPreviewOnly>(owner.gameObject) : owner.gameObject.AddComponent<UIPreviewOnly>();
            else ClearSamples(marker, recordUndo);
            if (recordUndo) { Undo.RecordObject(owner, "配置界面预览"); Undo.RecordObject(marker, "配置界面预览"); }
            var sampleObjects = new List<GameObject>();
            try
            {
                foreach (var binding in texts)
                {
                    if (recordUndo) Undo.RecordObject(binding.target, "设置界面示例文字");
                    binding.target.text = profile.GetText(binding.key);
                    EditorUtility.SetDirty(binding.target);
                }
                foreach (var binding in lists)
                {
                    var rows = ResolveRows(profile, binding);
                    for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                    {
                        var clone = Object.Instantiate(binding.template, binding.container, false);
                        sampleObjects.Add(clone.gameObject);
                        if (recordUndo) Undo.RegisterCreatedObjectUndo(clone.gameObject, "创建界面预览条目");
                        clone.name = $"PreviewOnly_{binding.key}_{rowIndex + 1:00}";
                        clone.gameObject.tag = "EditorOnly";
                        binding.layout?.Apply(clone, rowIndex);
                        for (var textIndex = 0; textIndex < binding.textTargets.Length; textIndex++)
                        {
                            var target = MapComponent(binding.textTargets[textIndex], binding.template, clone);
                            target.text = FormatCell(binding, rows[rowIndex], textIndex);
                        }
                        clone.gameObject.SetActive(true);
                    }
                }
                marker.Configure(sampleObjects.ToArray(), texts.Select(x => x.target).ToArray(), texts.Select(x => x.runtimeText).ToArray());
                var markers = owner.PreviewBindings.Where(x => x != null).ToList();
                if (!markers.Contains(marker)) markers.Add(marker);
                owner.ConfigurePreview(markers.ToArray());
                if (recordUndo) Undo.RecordObject(owner.gameObject, "显示界面预览");
                owner.gameObject.SetActive(true);
                EditorUtility.SetDirty(marker);
                EditorUtility.SetDirty(owner);
                return marker;
            }
            catch
            {
                foreach (var sample in sampleObjects) if (sample != null) DestroySample(sample, recordUndo);
                foreach (var binding in texts) if (binding.target != null) binding.target.text = binding.runtimeText ?? string.Empty;
                marker.Configure(Array.Empty<GameObject>(), Array.Empty<TMP_Text>(), Array.Empty<string>());
                throw;
            }
        }

        public static void ClearSamples(UIPreviewOnly marker, bool recordUndo = false)
        {
            if (marker == null) return;
            marker.ValidateConfiguration();
            foreach (var sample in marker.SampleObjects) if (sample != null) DestroySample(sample, recordUndo);
            for (var i = 0; i < marker.SampleTextTargets.Length; i++)
            {
                var target = marker.SampleTextTargets[i];
                if (recordUndo) Undo.RecordObject(target, "清除界面示例文字");
                target.text = marker.RuntimeTexts[i] ?? string.Empty;
            }
            if (recordUndo) Undo.RecordObject(marker, "清除界面预览配置");
            marker.Configure(Array.Empty<GameObject>(), Array.Empty<TMP_Text>(), Array.Empty<string>());
        }

        private static void Validate(UIViewBase owner, UIPreviewProfile profile,
            UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists)
        {
            var targets = new HashSet<TMP_Text>();
            foreach (var binding in texts)
            {
                if (binding == null || binding.target == null || !BelongsTo(binding.target.transform, owner.transform))
                    throw new InvalidOperationException("预览文字必须显式绑定所属视图内的控件。");
                if (!targets.Add(binding.target)) throw new InvalidOperationException($"文字 {binding.target.name} 重复绑定预览。");
                profile.GetText(binding.key);
            }
            foreach (var binding in lists)
            {
                if (binding == null || binding.container == null || binding.template == null
                    || !BelongsTo(binding.container, owner.transform) || !BelongsTo(binding.template, owner.transform)
                    || binding.textTargets == null || binding.columns == null || binding.textTargets.Length != binding.columns.Length)
                    throw new InvalidOperationException("预览列表的容器、模板、列控件必须完整配置且属于该视图。");
                var rows = ResolveRows(profile, binding);
                if (binding.textFormats != null && binding.textFormats.Length != 0 && binding.textFormats.Length != binding.textTargets.Length)
                    throw new InvalidOperationException("预览列格式必须为空或与文字控件数量一致。");
                binding.layout?.Validate(); // 旧配方没有此可选配置时，保持原模板布局。
                for (var i = 0; i < binding.textTargets.Length; i++)
                {
                    if (binding.textTargets[i] == null || !BelongsTo(binding.textTargets[i].transform, binding.template))
                        throw new InvalidOperationException("预览列表列控件必须属于指定模板。");
                    foreach (var row in rows)
                    {
                        if (row?.cells == null || binding.columns[i] < 0 || binding.columns[i] >= row.cells.Length)
                            throw new InvalidOperationException($"预览列表 {binding.key} 缺少配置的第 {binding.columns[i]} 列。");
                        FormatCell(binding, row, i);
                    }
                }
            }
        }

        static UIPreviewRowSample[] ResolveRows(UIPreviewProfile profile, UIPreviewListBinding binding)
            => binding.textKeys != null && binding.textKeys.Length > 0
                ? binding.textKeys.Select(key => new UIPreviewRowSample(profile.GetText(key))).ToArray()
                : profile.GetRows(binding.key);

        static string FormatCell(UIPreviewListBinding binding, UIPreviewRowSample row, int index)
        {
            if (binding.textFormats == null || binding.textFormats.Length == 0 || string.IsNullOrEmpty(binding.textFormats[index]))
                return row.cells[binding.columns[index]] ?? string.Empty;
            try { return string.Format(binding.textFormats[index], row.cells.Cast<object>().ToArray()); }
            catch (FormatException error) { throw new InvalidOperationException("预览列格式无效：" + binding.key, error); }
        }

        internal static T MapComponent<T>(T original, Transform sourceRoot, Transform targetRoot) where T : Component
        {
            if (original == null) return null;
            var indices = new Stack<int>();
            var cursor = original.transform;
            while (cursor != sourceRoot)
            {
                if (cursor == null) throw new InvalidOperationException("预览引用不属于配置的源层级。");
                indices.Push(cursor.GetSiblingIndex());
                cursor = cursor.parent;
            }
            var mapped = targetRoot;
            while (indices.Count > 0) mapped = mapped.GetChild(indices.Pop());
            var type = original.GetType();
            var sourceComponents = original.transform.GetComponents(type);
            var ordinal = Array.IndexOf(sourceComponents, original);
            var targetComponents = mapped.GetComponents(type);
            if (ordinal < 0 || ordinal >= targetComponents.Length) throw new InvalidOperationException("预览模板组件映射不完整。");
            return (T)targetComponents[ordinal];
        }

        private static bool BelongsTo(Transform value, Transform root) => value == root || value.IsChildOf(root);
        private static void DestroySample(GameObject sample, bool recordUndo)
        { if (recordUndo) Undo.DestroyObjectImmediate(sample); else Object.DestroyImmediate(sample); }
    }
}
