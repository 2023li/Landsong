#if UNITY_EDITOR
using System.IO;
using Landsong.UISystem;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class GenerateMinimalGamePanelQuestItem
{
    private const string OutputFolder = "Assets/_Test/Generated";
    private const string PrefabPath = OutputFolder + "/GamePanel_QuestItem_Minimal.prefab";

    [MenuItem("Tools/Landsong/Test/生成最小可用任务Item")]
    public static void Generate()
    {
        EnsureFolder(OutputFolder);

        var root = CreateRectObject("GamePanel_QuestItem_Minimal", null);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(640f, 220f);

        var background = root.AddComponent<Image>();
        background.color = new Color(0.12f, 0.11f, 0.09f, 0.92f);

        var button = root.AddComponent<Button>();
        button.targetGraphic = background;

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var layoutElement = root.AddComponent<LayoutElement>();
        layoutElement.minHeight = 128f;
        layoutElement.flexibleWidth = 1f;

        var questItem = root.AddComponent<GamePanel_QuestItem>();

        var header = CreateRectObject("Header", root.transform);
        AddHorizontalLayout(header, 8f);

        var icon = CreateImage("Icon", header.transform, new Color(0.78f, 0.66f, 0.42f, 1f));
        var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = 44f;
        iconLayout.preferredHeight = 44f;
        iconLayout.flexibleWidth = 0f;

        var titleStack = CreateRectObject("TitleStack", header.transform);
        var titleStackLayout = titleStack.AddComponent<VerticalLayoutGroup>();
        titleStackLayout.spacing = 2f;
        titleStackLayout.childControlWidth = true;
        titleStackLayout.childControlHeight = true;
        titleStackLayout.childForceExpandWidth = true;
        titleStackLayout.childForceExpandHeight = false;
        titleStack.AddComponent<LayoutElement>().flexibleWidth = 1f;

        var titleLabel = CreateText("Title", titleStack.transform, "任务名称", 22f, FontStyles.Bold);
        var progressLabel = CreateText("Progress", titleStack.transform, "进度 0/1", 16f, FontStyles.Normal);

        var stateStack = CreateRectObject("StateStack", header.transform);
        var stateStackLayout = stateStack.AddComponent<VerticalLayoutGroup>();
        stateStackLayout.spacing = 2f;
        stateStackLayout.childControlWidth = true;
        stateStackLayout.childControlHeight = true;
        stateStackLayout.childForceExpandWidth = false;
        stateStackLayout.childForceExpandHeight = false;
        var stateLayout = stateStack.AddComponent<LayoutElement>();
        stateLayout.preferredWidth = 140f;
        stateLayout.flexibleWidth = 0f;

        var statusLabel = CreateText("Status", stateStack.transform, "进行中", 16f, FontStyles.Bold);
        statusLabel.alignment = TextAlignmentOptions.Right;
        var deadlineLabel = CreateText("Deadline", stateStack.transform, "剩余 1 回合", 15f, FontStyles.Normal);
        deadlineLabel.alignment = TextAlignmentOptions.Right;

        var slider = CreateSlider(root.transform);

        var detailsRoot = CreateRectObject("Details", root.transform);
        var detailsLayout = detailsRoot.AddComponent<VerticalLayoutGroup>();
        detailsLayout.spacing = 6f;
        detailsLayout.childControlWidth = true;
        detailsLayout.childControlHeight = true;
        detailsLayout.childForceExpandWidth = true;
        detailsLayout.childForceExpandHeight = false;

        var descriptionLabel = CreateText("Description", detailsRoot.transform, "任务描述", 16f, FontStyles.Normal);
        descriptionLabel.textWrappingMode = TextWrappingModes.Normal;

        var resourceDetailsLabel = CreateText("ResourceDetails", detailsRoot.transform, "资源：0/1", 16f, FontStyles.Normal);
        resourceDetailsLabel.textWrappingMode = TextWrappingModes.Normal;
        ResourceRichTextFormatter.ApplySpriteAsset(resourceDetailsLabel);

        var submitButton = CreateButton("SubmitButton", detailsRoot.transform, "提交");

        var selectedRoot = CreateStateMarker("SelectedRoot", root.transform, "已选中", new Color(0.25f, 0.48f, 0.9f, 0.75f));
        var completedRoot = CreateStateMarker("CompletedRoot", root.transform, "已完成", new Color(0.25f, 0.62f, 0.34f, 0.75f));
        var failedRoot = CreateStateMarker("FailedRoot", root.transform, "已失败", new Color(0.7f, 0.2f, 0.18f, 0.75f));
        selectedRoot.SetActive(false);
        completedRoot.SetActive(false);
        failedRoot.SetActive(false);

        BindQuestItem(
            questItem,
            button,
            submitButton.GetComponent<Button>(),
            submitButton.GetComponentInChildren<TMP_Text>(true),
            icon,
            titleLabel,
            descriptionLabel,
            statusLabel,
            deadlineLabel,
            progressLabel,
            resourceDetailsLabel,
            slider,
            detailsRoot,
            selectedRoot,
            completedRoot,
            failedRoot);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"已生成最小可用任务 Item prefab：{PrefabPath}");
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }

        return go;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float fontSize, FontStyles style)
    {
        var go = CreateRectObject(name, parent);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = Color.white;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.alignment = TextAlignmentOptions.Left;
        return label;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        var go = CreateRectObject(name, parent);
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static GameObject CreateButton(string name, Transform parent, string text)
    {
        var go = CreateRectObject(name, parent);
        var image = go.AddComponent<Image>();
        image.color = new Color(0.45f, 0.32f, 0.16f, 0.95f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = 36f;

        var label = CreateText("Label", go.transform, text, 18f, FontStyles.Bold);
        label.alignment = TextAlignmentOptions.Center;
        Stretch(label.rectTransform);

        return go;
    }

    private static Slider CreateSlider(Transform parent)
    {
        var sliderGo = CreateRectObject("ProgressSlider", parent);
        var sliderLayout = sliderGo.AddComponent<LayoutElement>();
        sliderLayout.preferredHeight = 12f;

        var background = CreateImage("Background", sliderGo.transform, new Color(0.08f, 0.07f, 0.06f, 0.8f));
        Stretch(background.rectTransform);

        var fillArea = CreateRectObject("Fill Area", sliderGo.transform);
        Stretch(fillArea.GetComponent<RectTransform>());

        var fill = CreateImage("Fill", fillArea.transform, new Color(0.78f, 0.58f, 0.25f, 1f));
        Stretch(fill.rectTransform);

        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.35f;
        slider.interactable = false;
        slider.targetGraphic = fill;
        slider.fillRect = fill.rectTransform;
        return slider;
    }

    private static GameObject CreateStateMarker(string name, Transform parent, string text, Color color)
    {
        var go = CreateButton(name, parent, text);
        var image = go.GetComponent<Image>();
        image.color = color;
        return go;
    }

    private static void AddHorizontalLayout(GameObject target, float spacing)
    {
        var layout = target.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void BindQuestItem(
        GamePanel_QuestItem questItem,
        Button selectButton,
        Button submitButton,
        TMP_Text submitButtonLabel,
        Image icon,
        TMP_Text titleLabel,
        TMP_Text descriptionLabel,
        TMP_Text statusLabel,
        TMP_Text deadlineLabel,
        TMP_Text progressLabel,
        TMP_Text resourceDetailsLabel,
        Slider progressSlider,
        GameObject detailsRoot,
        GameObject selectedRoot,
        GameObject completedRoot,
        GameObject failedRoot)
    {
        var serializedObject = new SerializedObject(questItem);
        SetObject(serializedObject, "selectButton", selectButton);
        SetObject(serializedObject, "submitButton", submitButton);
        SetObject(serializedObject, "submitButtonLabel", submitButtonLabel);
        SetObject(serializedObject, "icon", icon);
        SetObject(serializedObject, "titleLabel", titleLabel);
        SetObject(serializedObject, "descriptionLabel", descriptionLabel);
        SetObject(serializedObject, "statusLabel", statusLabel);
        SetObject(serializedObject, "deadlineLabel", deadlineLabel);
        SetObject(serializedObject, "progressLabel", progressLabel);
        SetObject(serializedObject, "resourceDetailsLabel", resourceDetailsLabel);
        SetObject(serializedObject, "progressSlider", progressSlider);
        SetObject(serializedObject, "detailsRoot", detailsRoot);
        SetObject(serializedObject, "selectedRoot", selectedRoot);
        SetObject(serializedObject, "completedRoot", completedRoot);
        SetObject(serializedObject, "failedRoot", failedRoot);
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        var folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrWhiteSpace(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
#endif
