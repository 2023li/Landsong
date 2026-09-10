#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public sealed class PortraitImportWindow : EditorWindow
    {
        [SerializeField] PortraitImportDraft draft = new PortraitImportDraft();
        Vector2 scroll; string error = "", result = ""; bool validate = true; int resolution, nextLayer;
        [SerializeField] bool imported;
        PortraitConfig config;
        readonly Dictionary<string, Texture2D> previews = new Dictionary<string, Texture2D>();
        static readonly string[] Types = { "脸型", "耳朵", "眼睛", "眉毛", "鼻子", "嘴型", "发型", "胡须", "身体", "服装", "头饰", "面饰", "饰品" };
        static readonly string[] Tints = { "固定颜色", "肤色", "发色", "眼睛颜色" };
        public PortraitImportDraft Draft => draft;
        public string ValidationError => error;
        public string ImportResult => result;
        public bool CanImport => !imported && config != null && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && PortraitPartImporter.Validate(config, draft).Length == 0;

        [MenuItem("Landsong/ECS/Portraits/Import parts")]
        public static void Open() => GetWindow<PortraitImportWindow>("肖像部件导入");
        void OnEnable() { minSize = new Vector2(780, 620); if (draft == null) draft = new PortraitImportDraft(); if (draft.Layers == null) draft.ResetLayers(); ReloadConfig(); }
        void OnDisable() => ClearPreviews();
        void OnProjectChange() { ReloadConfig(); ClearPreviews(); validate = true; Repaint(); }
        void ReloadConfig() => config = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset")?.Portraits;
        void ClearPreviews() { foreach (var preview in previews.Values) if (preview != null) DestroyImmediate(preview); previews.Clear(); }
        public void Recheck() { ReloadConfig(); error = imported ? "" : PortraitPartImporter.Validate(config, draft); validate = false; Repaint(); }
        public void ImportCurrent()
        {
            Recheck(); if (imported || error.Length != 0) return;
            try { result = "已导入并登记：" + PortraitPartImporter.Import(config, draft) + "\n下次进入游戏时生效。"; imported = true; }
            catch (Exception e) { error = e.Message; result = ""; }
            validate = true; Repaint();
        }
        public static string LayerName(PortraitLayer layer) => layer switch
        {
            PortraitLayer.HairBack => "后发", PortraitLayer.HairFront => "前发", PortraitLayer.FaceBase => "脸部底图",
            PortraitLayer.Ear => "耳朵", PortraitLayer.Eyes => "眼睛底图", PortraitLayer.EyeOverlay => "瞳孔／眼睛覆盖层",
            PortraitLayer.Eyebrows => "眉毛", PortraitLayer.Nose => "鼻子", PortraitLayer.Mouth => "嘴型",
            PortraitLayer.FacialHair => "胡须", PortraitLayer.Body => "身体", PortraitLayer.NeckBack => "颈部后层", PortraitLayer.Neck => "颈部",
            PortraitLayer.ClothesFront => "服装前层", PortraitLayer.ClothesBack => "服装后层", PortraitLayer.HeadAccessoryFront => "头饰前层", PortraitLayer.HeadAccessoryBack => "头饰后层",
            PortraitLayer.FaceAccessory => "面饰", PortraitLayer.FrontAccessory => "饰品前层", PortraitLayer.BackAccessory => "饰品后层", _ => layer.ToString()
        };
        void OnGUI()
        {
            if (config == null) ReloadConfig();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("肖像部件导入", EditorStyles.boldLabel); GUILayout.FlexibleSpace();
                if (GUILayout.Button("定位配置", EditorStyles.toolbarButton)) { Selection.activeObject = config; EditorGUIUtility.PingObject(config); }
                if (GUILayout.Button("打开素材目录", EditorStyles.toolbarButton)) { var path = Path.GetFullPath(PortraitPartImporter.OutputRoot); if (Directory.Exists(path)) EditorUtility.RevealInFinder(path); else result = "首次成功导入时会自动创建：" + PortraitPartImporter.OutputRoot; }
                if (GUILayout.Button("校验当前肖像库", EditorStyles.toolbarButton))
                { try { using var library = PortraitLibraryBuilder.Build(config); result = "当前肖像库校验通过。"; } catch (Exception e) { result = "当前肖像库校验失败：" + e.Message; } }
            }
            if (config == null) { EditorGUILayout.HelpBox("GameCatalog 尚未引用 PortraitConfig，请先配置项目肖像资源。", MessageType.Error); return; }
            if (resolution != config.Resolution) { resolution = config.Resolution; validate = true; ClearPreviews(); }
            EditorGUILayout.LabelField($"项目尺寸：{resolution} × {resolution} 像素", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("原始 PNG 必须与项目尺寸完全相同，不能用缩放、裁切或 Sprite 切片凑尺寸。窗口会自动设置中心 Pivot、Point、可读、无压缩和无 Mipmap。\n素材复制到 " + PortraitPartImporter.OutputRoot + "/类别/标识/，源文件保留。", MessageType.Info);
            EditorGUILayout.LabelField(config.Placeholders ? "当前：已导入美术与程序占位共同加载，可逐步补齐。" : "当前：仅加载正式美术，必须备齐男女必需部件。", EditorStyles.wordWrappedLabel);
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;
            if (locked) EditorGUILayout.HelpBox("Play／编译期间禁用导入，请退出 Play 并等待编译完成。", MessageType.Warning);
            using (var area = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = area.scrollPosition;
                using (new EditorGUI.DisabledScope(locked))
                {
                    EditorGUI.BeginChangeCheck();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(420)))
                        {
                            int type = EditorGUILayout.Popup("部件类别", (int)draft.Type, Types);
                            if (type != (int)draft.Type) { draft.Type = (PortraitPartType)type; draft.ResetLayers(); nextLayer = 0; }
                            draft.Id = EditorGUILayout.TextField(new GUIContent("部件标识", "例如 hair.short_01。标识会写入存档，导入后不要随意更改。"), draft.Id);
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.PrefixLabel("适用性别（可复选）");
                                draft.Male = GUILayout.Toggle(draft.Male, "男用", GUILayout.Width(70)); draft.Female = GUILayout.Toggle(draft.Female, "女用", GUILayout.Width(70));
                            }
                            draft.Weight = EditorGUILayout.FloatField(new GUIContent("随机权重", "非负数。0 表示不参与随机，但仍可在丽质捏脸时选择。"), draft.Weight);
                            EditorGUILayout.HelpBox("按实际素材添加图层，各程序层均可省略，部件至少提供一张 PNG。一个逻辑部件拆成多层时，可继续添加其他图层，性别与权重按整个部件设置。", MessageType.Info);
                            if (draft.Type == PortraitPartType.Hair) EditorGUILayout.LabelField("发型可只含前发、只含后发，或同时包含两层。", EditorStyles.wordWrappedLabel);
                            var available = PortraitPartRules.Allowed(draft.Type).Where(layer => draft.Layers.All(item => item.Layer != layer)).ToArray();
                            if (available.Length > 0)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    nextLayer = EditorGUILayout.Popup("可添加图层", Mathf.Clamp(nextLayer, 0, available.Length - 1), available.Select(LayerName).ToArray());
                                    if (GUILayout.Button("添加图层", GUILayout.Width(85))) { draft.AddLayer(available[nextLayer]); nextLayer = 0; GUI.changed = true; }
                                }
                            }
                            else EditorGUILayout.LabelField("该类别的所有可用图层均已添加，可移除不需要的图层。", EditorStyles.wordWrappedMiniLabel);
                            int removeLayer = -1;
                            for (int layerIndex = 0; layerIndex < draft.Layers.Count; layerIndex++)
                            {
                                var layer = draft.Layers[layerIndex];
                                EditorGUILayout.Space(5);
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    EditorGUILayout.LabelField(LayerName(layer.Layer), EditorStyles.boldLabel);
                                    if (GUILayout.Button("移除图层", GUILayout.Width(85))) removeLayer = layerIndex;
                                }
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    layer.File = EditorGUILayout.TextField(layer.File);
                                    var drop = GUILayoutUtility.GetLastRect(); var evt = Event.current;
                                    if (drop.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
                                    {
                                        string path = DragAndDrop.paths.FirstOrDefault() ?? AssetDatabase.GetAssetPath(DragAndDrop.objectReferences.FirstOrDefault());
                                        if (!string.IsNullOrEmpty(path)) { DragAndDrop.visualMode = DragAndDropVisualMode.Copy; if (evt.type == EventType.DragPerform) { DragAndDrop.AcceptDrag(); layer.File = path; GUI.changed = true; } evt.Use(); }
                                    }
                                    if (GUILayout.Button("选择 PNG", GUILayout.Width(85))) { string file = EditorUtility.OpenFilePanel("选择" + LayerName(layer.Layer), "", "png"); if (!string.IsNullOrEmpty(file)) { layer.File = file; GUI.changed = true; } }
                                }
                                using (new EditorGUI.DisabledScope(draft.Type == PortraitPartType.Hair)) layer.Tint = (PortraitTint)EditorGUILayout.Popup("染色通道", (int)layer.Tint, Tints);
                            }
                            if (removeLayer >= 0) { draft.Layers.RemoveAt(removeLayer); nextLayer = 0; GUI.changed = true; }
                        }
                        using (new EditorGUILayout.VerticalScope(GUILayout.Width(210)))
                        {
                            EditorGUILayout.LabelField("部件叠层预览", EditorStyles.boldLabel);
                            var rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(false)); EditorGUI.DrawRect(rect, new Color(.24f,.24f,.24f));
                            for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) if ((x + y) % 2 == 0) EditorGUI.DrawRect(new Rect(rect.x+x*20,rect.y+y*20,20,20), new Color(.31f,.31f,.31f));
                            foreach (var layer in draft.Layers.OrderBy(l => l.Layer)) { var preview = Preview(layer.File); if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit, true); }
                            EditorGUILayout.LabelField("透明棋盘背景。预览展示原图叠层，游戏中的肤色、发色与眼睛颜色由人物决定。", EditorStyles.wordWrappedMiniLabel);
                            foreach (var layer in draft.Layers) if (!string.IsNullOrWhiteSpace(layer.File)) EditorGUILayout.LabelField(LayerName(layer.Layer) + "：" + Path.GetFileName(layer.File), EditorStyles.wordWrappedMiniLabel);
                        }
                    }
                    if (EditorGUI.EndChangeCheck()) { ClearPreviews(); validate = true; imported = false; result = ""; }
                }
            }
            if (validate) Recheck();
            if (result.Length > 0) EditorGUILayout.HelpBox(result, MessageType.Info);
            if (error.Length > 0) EditorGUILayout.HelpBox(error, MessageType.Error);
            else if (!imported) EditorGUILayout.HelpBox("校验通过：" + DestinationText(), MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重新校验", GUILayout.Height(32))) { ClearPreviews(); Recheck(); }
                if (GUILayout.Button("填写下一个部件", GUILayout.Height(32))) { draft = new PortraitImportDraft { Type = draft.Type }; nextLayer = 0; imported = false; validate = true; result = ""; ClearPreviews(); }
                using (new EditorGUI.DisabledScope(locked || imported || error.Length != 0)) if (GUILayout.Button(imported ? "已导入" : "导入并登记部件", GUILayout.Height(32))) ImportCurrent();
            }
        }
        string DestinationText() => PortraitPartImporter.Destination(draft);
        Texture2D Preview(string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            if (previews.TryGetValue(file, out var existing)) return existing;
            Texture2D texture = null;
            try { var bytes = PortraitPartImporter.ReadPng(file, resolution); texture = new Texture2D(2,2,TextureFormat.RGBA32,false); ImageConversion.LoadImage(texture, bytes); texture.filterMode = FilterMode.Point; }
            catch { if (texture != null) DestroyImmediate(texture); texture = null; }
            previews[file] = texture; return texture;
        }
    }
}
#endif
