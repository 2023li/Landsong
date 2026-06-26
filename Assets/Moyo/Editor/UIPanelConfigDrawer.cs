#if UNITY_EDITOR

using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Moyo.Unity
{
    [CustomPropertyDrawer(typeof(UIPanelConfig))]
    public class UIPanelConfigDrawer : PropertyDrawer
    {
        private const float LineHeight = 18f;
        private const float LineSpacing = 4f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Foldout 一行。
            if (!property.isExpanded)
            {
                return LineHeight;
            }

            // 展开后显示：
            // 1. Panel Type
            // 2. Addressable Key
            // 3. Layer
            // 4. Cache Policy
            // 5. Can Close By Back
            // 6. Blur Previous On Open
            // 7. Hide Same Layer Panels
            return LineHeight + (LineHeight + LineSpacing) * 7 + 6f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var foldoutRect = new Rect(position.x, position.y, position.width, LineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GetFoldoutLabel(property), true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;

            var panelIdProp = property.FindPropertyRelative("panelId");
            var addressableKeyProp = property.FindPropertyRelative("addressableKey");
            var layerProp = property.FindPropertyRelative("layer");
            var cachePolicyProp = property.FindPropertyRelative("cachePolicy");
            var canCloseByBackProp = property.FindPropertyRelative("canCloseByBack");
            var blurPreviousOnOpenProp = property.FindPropertyRelative("blurPreviousOnOpen");
            var hideSameLayerPanelsProp = property.FindPropertyRelative("hideSameLayerPanels");

            var y = position.y + LineHeight + LineSpacing;

            DrawPanelTypePopup(position, ref y, panelIdProp);

            DrawProperty(position, ref y, addressableKeyProp, "Addressable Key");
            DrawProperty(position, ref y, layerProp, "Layer");
            DrawProperty(position, ref y, cachePolicyProp, "Cache Policy");
            DrawProperty(position, ref y, canCloseByBackProp, "Can Close By Back");
            DrawProperty(position, ref y, blurPreviousOnOpenProp, "Blur Previous On Open");
            DrawProperty(position, ref y, hideSameLayerPanelsProp, "Hide Same Layer Panels");

            EditorGUI.indentLevel--;

            EditorGUI.EndProperty();
        }

        private static GUIContent GetFoldoutLabel(SerializedProperty property)
        {
            var panelIdProp = property.FindPropertyRelative("panelId");

            if (panelIdProp != null && !string.IsNullOrEmpty(panelIdProp.stringValue))
            {
                return new GUIContent(panelIdProp.stringValue);
            }

            return new GUIContent("UIPanel Config");
        }

        private static void DrawPanelTypePopup(Rect position, ref float y, SerializedProperty panelIdProp)
        {
            var panelTypes = TypeCache.GetTypesDerivedFrom<UIPanelBase>()
                .Where(type =>
                    type != null &&
                    !type.IsAbstract &&
                    !type.IsGenericType &&
                    typeof(UIPanelBase).IsAssignableFrom(type))
                .OrderBy(type => type.Name)
                .ToArray();

            var names = panelTypes
                .Select(type => type.Name)
                .ToArray();

            var rect = new Rect(position.x, y, position.width, LineHeight);

            if (names.Length == 0)
            {
                EditorGUI.HelpBox(rect, "没有找到任何 UIPanelBase 子类。", MessageType.Warning);
                y += LineHeight + LineSpacing;
                return;
            }

            var currentIndex = Array.IndexOf(names, panelIdProp.stringValue);

            if (currentIndex < 0)
            {
                currentIndex = 0;
                panelIdProp.stringValue = names[0];
            }

            var newIndex = EditorGUI.Popup(rect, "Panel Type", currentIndex, names);

            if (newIndex >= 0 && newIndex < names.Length)
            {
                panelIdProp.stringValue = names[newIndex];
            }

            y += LineHeight + LineSpacing;
        }

        private static void DrawProperty(Rect position, ref float y, SerializedProperty property, string label)
        {
            var rect = new Rect(position.x, y, position.width, LineHeight);
            EditorGUI.PropertyField(rect, property, new GUIContent(label));
            y += LineHeight + LineSpacing;
        }
    }
}

#endif
