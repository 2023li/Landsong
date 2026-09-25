#if UNITY_EDITOR
using System;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class RoyalFoundingUiAuthoring
    {
        const string ModalPath = "Assets/Landsong/UI/Prefabs/GamePanel/Views/UI_GamePanel_RoyalFounding.prefab";
        const string GamePath = "Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab";
        const string FontPath = "Assets/Landsong/Art/Fonts/Kingnammm-Maiyuan-II-Regular-2 SDF.asset";

        [MenuItem("Landsong/UI/Build royal founding dialog")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出运行模式后制作王室拥立弹窗。");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new InvalidOperationException("王室拥立弹窗缺少中文字体资源。");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModalPath) == null)
                CreateModal(font);
            BindPresentation();
            AttachToGame();
            AssetDatabase.SaveAssets();
        }

        static void BindPresentation()
        {
            var contents = PrefabUtility.LoadPrefabContents(ModalPath);
            try
            {
                ApplicationUiAuthoring.BindPresentation(contents);
                var group = contents.GetComponent<CanvasGroup>();
                if (group == null)
                    group = contents.AddComponent<CanvasGroup>();
                group.interactable = true;
                group.blocksRaycasts = true;
                group.ignoreParentGroups = true;
                var modal = contents.GetComponent<UI_GamePanel_RoyalFounding>();
                modal.ModalGroup = group;
                modal.NameInput.characterLimit = 24;
                PrefabUtility.SaveAsPrefabAsset(contents, ModalPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static void CreateModal(TMP_FontAsset font)
        {
            var root = new GameObject("王室拥立弹窗", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(UI_GamePanel_RoyalFounding));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                root.GetComponent<Image>().color = new Color(.015f, .025f, .04f, .82f);
                root.GetComponent<CanvasGroup>().ignoreParentGroups = true;
                var dialog = Image("拥立内容", rect, new Vector2(650, 430), new Vector2(0, 0), new Color(.08f, .12f, .17f, 1f));
                var modal = root.GetComponent<UI_GamePanel_RoyalFounding>();
                modal.ModalGroup = root.GetComponent<CanvasGroup>();
                Text("标题", dialog, font, "拥立新王", 32, new Vector2(570, 48), new Vector2(0, 170), TextAlignmentOptions.Center, new Color(1f, .86f, .54f));
                modal.Message = Text("说明", dialog, font, "人们认为你是一名优秀的领导者，拥护你成为这片土地的领导者", 23, new Vector2(570, 74), new Vector2(0, 92), TextAlignmentOptions.Center, Color.white);
                Text("姓名标签", dialog, font, "你的名字：", 23, new Vector2(155, 42), new Vector2(-228, 12), TextAlignmentOptions.MidlineLeft, Color.white);
                modal.NameInput = Input(dialog, font);
                Text("性别标签", dialog, font, "性别：", 23, new Vector2(155, 42), new Vector2(-228, -57), TextAlignmentOptions.MidlineLeft, Color.white);
                modal.MaleButton = Button("男性按钮", dialog, font, "男", new Vector2(150, 46), new Vector2(-53, -57), out var male);
                modal.FemaleButton = Button("女性按钮", dialog, font, "女", new Vector2(150, 46), new Vector2(119, -57), out var female);
                modal.MaleLabel = male;
                modal.FemaleLabel = female;
                modal.Feedback = Text("提示", dialog, font, "", 18, new Vector2(570, 30), new Vector2(0, -119), TextAlignmentOptions.Center, new Color(1f, .67f, .5f));
                modal.ConfirmButton = Button("确认按钮", dialog, font, "确定", new Vector2(220, 50), new Vector2(0, -170), out _);
                ApplicationUiAuthoring.BindPresentation(root);
                root.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(root, ModalPath) == null)
                    throw new InvalidOperationException("无法保存王室拥立弹窗预制体。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void AttachToGame()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModalPath);
            var contents = PrefabUtility.LoadPrefabContents(GamePath);
            try
            {
                var game = contents.GetComponent<UI_GamePanel>();
                if (game == null || game.ModalRoot == null)
                    throw new InvalidOperationException("游戏主预制体缺少弹窗根对象。");
                if (game.RoyalFounding == null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, contents.scene);
                    instance.transform.SetParent(game.ModalRoot, false);
                    instance.SetActive(false);
                    var serialized = new SerializedObject(game);
                    serialized.FindProperty("royalFounding").objectReferenceValue = instance.GetComponent<UI_GamePanel_RoyalFounding>();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (PrefabUtility.SaveAsPrefabAsset(contents, GamePath) == null)
                    throw new InvalidOperationException("无法保存游戏主预制体中的拥立弹窗引用。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        static RectTransform Image(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            obj.GetComponent<Image>().color = color;
            return rect;
        }

        static TextMeshProUGUI Text(string name, RectTransform parent, TMP_FontAsset font, string content,
            float size, Vector2 bounds, Vector2 position, TextAlignmentOptions alignment, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = bounds;
            rect.anchoredPosition = position;
            var text = obj.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        static TMP_InputField Input(RectTransform parent, TMP_FontAsset font)
        {
            var frame = Image("姓名输入框", parent, new Vector2(390, 48), new Vector2(58, 12), new Color(.16f, .22f, .27f, 1f));
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.SetParent(frame, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(12, 4);
            viewportRect.offsetMax = new Vector2(-12, -4);
            var value = Text("输入内容", viewportRect, font, "", 23, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft, Color.white);
            value.rectTransform.anchorMin = Vector2.zero;
            value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
            var placeholder = Text("占位文字", viewportRect, font, "请输入姓名", 22, Vector2.zero, Vector2.zero, TextAlignmentOptions.MidlineLeft, new Color(.65f, .69f, .72f));
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = placeholder.rectTransform.offsetMax = Vector2.zero;
            var input = frame.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = frame.GetComponent<Image>();
            input.textViewport = viewportRect;
            input.textComponent = value;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 24;
            return input;
        }

        static Button Button(string name, RectTransform parent, TMP_FontAsset font, string label,
            Vector2 size, Vector2 position, out TextMeshProUGUI text)
        {
            var frame = Image(name, parent, size, position, new Color(.31f, .39f, .42f, 1f));
            var button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame.GetComponent<Image>();
            text = Text("标签", frame, font, label, 23, size, Vector2.zero, TextAlignmentOptions.Center, Color.white);
            return button;
        }
    }
}
#endif
