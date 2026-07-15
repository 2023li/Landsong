using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.BuildingSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Buildings
{
    [CustomPropertyDrawer(typeof(BuildingModuleBase), true)]
    internal sealed class BuildingModuleReferenceDrawer : ManagedReferenceNameDrawer
    {
        private static readonly Type[] CandidateTypes = TypeCache
            .GetTypesDerivedFrom<BuildingModuleBase>()
            .Where(IsUsableManagedReferenceType)
            .OrderBy(type => GetFriendlyTypeName(type, "BM_"), StringComparer.Ordinal)
            .ToArray();

        protected override GUIContent GetHeader(SerializedProperty property)
        {
            if (property.managedReferenceValue is not BuildingModuleBase module)
            {
                return new GUIContent("未选择模块", "该列表项没有有效的建筑模块类型。");
            }

            var typeName = GetFriendlyTypeName(module.GetType(), "BM_");
            var moduleId = string.IsNullOrWhiteSpace(module.ModuleId)
                ? "缺少 ModuleId"
                : module.ModuleId;
            return new GUIContent(
                $"{typeName}  [{moduleId}]",
                $"类型：{module.GetType().FullName}\n{module.ModuleDescription}");
        }

        protected override string EmptyReferenceLabel => "未选择模块";
        protected override string SelectTypeButtonLabel => "选择模块类型";
        protected override IReadOnlyList<Type> GetCandidateTypes() => CandidateTypes;

        protected override GUIContent GetCandidateLabel(Type type)
        {
            var moduleId = GetModuleId(type);
            return new GUIContent(
                $"{GetFriendlyTypeName(type, "BM_")}  [{moduleId}]",
                type.FullName);
        }

        protected override bool CanAssignType(
            SerializedProperty property,
            Type candidateType,
            out string reason)
        {
            reason = string.Empty;
            var moduleId = GetModuleId(candidateType);
            if (string.IsNullOrWhiteSpace(moduleId))
            {
                reason = "缺少 BuildingModuleId";
                return false;
            }

            var root = property.serializedObject.FindProperty("buildingModules");
            if (root == null || !root.isArray)
            {
                return true;
            }

            for (var i = 0; i < root.arraySize; i++)
            {
                var existing = root.GetArrayElementAtIndex(i).managedReferenceValue
                    as BuildingModuleBase;
                if (existing != null
                    && string.Equals(existing.ModuleId, moduleId, StringComparison.Ordinal))
                {
                    reason = "该 ModuleId 已存在";
                    return false;
                }
            }

            return true;
        }

        protected override void AfterAssign(UnityEngine.Object target)
        {
            if (target is BuildingModuleSetDefinition moduleSet)
            {
                moduleSet.Normalize();
            }
        }

        private static string GetModuleId(Type type)
        {
            return (Attribute.GetCustomAttribute(type, typeof(BuildingModuleIdAttribute))
                    as BuildingModuleIdAttribute)?.Id
                   ?? string.Empty;
        }
    }

    [CustomPropertyDrawer(typeof(BuildingLevelConfigurationBase), true)]
    internal sealed class BuildingLevelConfigurationReferenceDrawer : ManagedReferenceNameDrawer
    {
        private static readonly Dictionary<Type, string> FriendlyNames = new Dictionary<Type, string>
        {
            { typeof(BuildingWorkforceLevelConfiguration), "岗位配置" },
            { typeof(BuildingProductionLevelConfiguration), "资源产出配置" },
            { typeof(BuildingInventoryLevelConfiguration), "库存容量配置" },
            { typeof(BuildingTechnologyPointLevelConfiguration), "科技点配置" },
            { typeof(PlayerHomeLevelConfiguration), "王宫配置" },
            { typeof(ResidentialHousingLevelConfiguration), "居民房配置" },
            { typeof(FishingHutLevelConfiguration), "捕鱼小屋配置" }
        };

        private static readonly Type[] CandidateTypes = TypeCache
            .GetTypesDerivedFrom<BuildingLevelConfigurationBase>()
            .Where(IsUsableManagedReferenceType)
            .OrderBy(GetFriendlyConfigurationName, StringComparer.Ordinal)
            .ToArray();

        protected override GUIContent GetHeader(SerializedProperty property)
        {
            if (property.managedReferenceValue is not BuildingLevelConfigurationBase configuration)
            {
                return new GUIContent("未选择等级配置", "该列表项没有有效的等级配置类型。");
            }

            var type = configuration.GetType();
            var typeName = FriendlyNames.TryGetValue(type, out var friendlyName)
                ? friendlyName
                : GetFriendlyTypeName(type, "Building", "LevelConfiguration");
            return new GUIContent(
                $"{typeName}  [{configuration.ConfigurationId}]",
                $"类型：{type.FullName}\n应用于进入当前运营等级时的模块或家族参数。");
        }

        protected override string EmptyReferenceLabel => "未选择等级配置";
        protected override string SelectTypeButtonLabel => "选择配置类型";
        protected override IReadOnlyList<Type> GetCandidateTypes() => CandidateTypes;

        protected override GUIContent GetCandidateLabel(Type type)
        {
            var configuration = Activator.CreateInstance(type) as BuildingLevelConfigurationBase;
            var configurationId = configuration?.ConfigurationId ?? "缺少 ConfigurationId";
            return new GUIContent(
                $"{GetFriendlyConfigurationName(type)}  [{configurationId}]",
                type.FullName);
        }

        private static string GetFriendlyConfigurationName(Type type)
        {
            return FriendlyNames.TryGetValue(type, out var friendlyName)
                ? friendlyName
                : GetFriendlyTypeName(type, "Building", "LevelConfiguration");
        }
    }

    internal abstract class ManagedReferenceNameDrawer : PropertyDrawer
    {
        private static readonly float LineHeight = EditorGUIUtility.singleLineHeight;
        private static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var height = LineHeight;
            if (!property.isExpanded || property.managedReferenceValue == null)
            {
                return height;
            }

            var children = GetDirectChildren(property);
            for (var i = 0; i < children.Count; i++)
            {
                height += Spacing + EditorGUI.GetPropertyHeight(children[i], true);
            }

            return height;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var headerRect = new Rect(position.x, position.y, position.width, LineHeight);
            if (property.managedReferenceValue == null)
            {
                DrawEmptyReference(headerRect, property);
                EditorGUI.EndProperty();
                return;
            }

            property.isExpanded = EditorGUI.Foldout(
                headerRect,
                property.isExpanded,
                GetHeader(property),
                true);

            if (property.isExpanded && property.managedReferenceValue != null)
            {
                var children = GetDirectChildren(property);
                EditorGUI.indentLevel++;
                var y = headerRect.yMax + Spacing;
                for (var i = 0; i < children.Count; i++)
                {
                    var child = children[i];
                    var childHeight = EditorGUI.GetPropertyHeight(child, true);
                    var childRect = new Rect(position.x, y, position.width, childHeight);
                    EditorGUI.PropertyField(childRect, child, true);
                    y += childHeight + Spacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        protected abstract GUIContent GetHeader(SerializedProperty property);
        protected abstract string EmptyReferenceLabel { get; }
        protected abstract string SelectTypeButtonLabel { get; }
        protected abstract IReadOnlyList<Type> GetCandidateTypes();

        protected abstract GUIContent GetCandidateLabel(Type type);

        protected virtual bool CanAssignType(
            SerializedProperty property,
            Type candidateType,
            out string reason)
        {
            reason = string.Empty;
            return true;
        }

        protected virtual void AfterAssign(UnityEngine.Object target)
        {
        }

        protected static bool IsUsableManagedReferenceType(Type type)
        {
            return type != null
                   && !type.IsAbstract
                   && !type.IsGenericTypeDefinition
                   && type.IsSerializable
                   && type.GetConstructor(Type.EmptyTypes) != null;
        }

        protected static string GetFriendlyTypeName(
            Type type,
            string prefix,
            string suffix = "")
        {
            if (type == null)
            {
                return "未知类型";
            }

            var name = type.Name;
            if (!string.IsNullOrEmpty(prefix)
                && name.StartsWith(prefix, StringComparison.Ordinal))
            {
                name = name.Substring(prefix.Length);
            }

            if (!string.IsNullOrEmpty(suffix)
                && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffix.Length);
            }

            return ObjectNames.NicifyVariableName(name.Replace('_', ' '));
        }

        private void DrawEmptyReference(
            Rect position,
            SerializedProperty property)
        {
            const float buttonWidth = 132f;
            var labelRect = new Rect(
                position.x,
                position.y,
                Mathf.Max(0f, position.width - buttonWidth - 4f),
                position.height);
            var buttonRect = new Rect(
                position.xMax - buttonWidth,
                position.y,
                buttonWidth,
                position.height);
            EditorGUI.LabelField(
                labelRect,
                new GUIContent(EmptyReferenceLabel, "点击右侧按钮选择具体类型。"));
            if (GUI.Button(buttonRect, SelectTypeButtonLabel, EditorStyles.miniButton))
            {
                ShowTypeMenu(property);
            }
        }

        private void ShowTypeMenu(SerializedProperty property)
        {
            var menu = new GenericMenu();
            var candidates = GetCandidateTypes();
            if (candidates == null || candidates.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可用类型"));
                menu.ShowAsContext();
                return;
            }

            var target = property.serializedObject.targetObject;
            var propertyPath = property.propertyPath;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidateType = candidates[i];
                var label = GetCandidateLabel(candidateType);
                if (!CanAssignType(property, candidateType, out var reason))
                {
                    menu.AddDisabledItem(new GUIContent(
                        string.IsNullOrWhiteSpace(reason)
                            ? label.text
                            : $"{label.text}  （{reason}）",
                        label.tooltip));
                    continue;
                }

                menu.AddItem(
                    label,
                    false,
                    () => AssignType(target, propertyPath, candidateType));
            }

            menu.ShowAsContext();
        }

        private void AssignType(
            UnityEngine.Object target,
            string propertyPath,
            Type candidateType)
        {
            if (target == null || candidateType == null)
            {
                return;
            }

            Undo.RecordObject(target, $"选择 {candidateType.Name}");
            var serializedObject = new SerializedObject(target);
            serializedObject.Update();
            var property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ManagedReference)
            {
                Debug.LogError($"无法定位托管引用字段：{propertyPath}", target);
                return;
            }

            property.managedReferenceValue = Activator.CreateInstance(candidateType);
            serializedObject.ApplyModifiedProperties();
            AfterAssign(target);
            EditorUtility.SetDirty(target);
            BuildingAuthoringWindow.RepaintOpenWindow();
        }

        private static List<SerializedProperty> GetDirectChildren(SerializedProperty parent)
        {
            var result = new List<SerializedProperty>();
            var iterator = parent.Copy();
            var end = iterator.GetEndProperty();
            var childDepth = parent.depth + 1;
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren)
                   && !SerializedProperty.EqualContents(iterator, end))
            {
                if (iterator.depth == childDepth)
                {
                    result.Add(iterator.Copy());
                }

                enterChildren = false;
            }

            return result;
        }
    }

    [CustomPropertyDrawer(typeof(BuildingVisualAssetReference))]
    internal sealed class BuildingVisualAssetReferenceDrawer : LabeledChildrenDrawer
    {
        private static readonly ChildField[] ChildFields =
        {
            new ChildField("directPrefab", "直接视图预制体", "直接引用的独立视图预制体。"),
            new ChildField("addressablePrefab", "可寻址视图预制体", "需要按需加载时使用的 Addressables 视图引用。")
        };

        protected override IReadOnlyList<ChildField> Fields => ChildFields;
    }

    [CustomPropertyDrawer(typeof(BuildingStyleDefinition))]
    internal sealed class BuildingStyleDefinitionDrawer : LabeledChildrenDrawer
    {
        private static readonly ChildField[] ChildFields =
        {
            new ChildField("styleId", "样式 ID", "用于表现映射匹配的稳定标识。"),
            new ChildField("displayName", "显示名称", "向玩家或策划显示的样式名称。"),
            new ChildField("icon", "样式图标", "样式选择界面使用的图标。")
        };

        protected override IReadOnlyList<ChildField> Fields => ChildFields;
    }

    [CustomPropertyDrawer(typeof(BuildingViewMapping))]
    internal sealed class BuildingViewMappingDrawer : LabeledChildrenDrawer
    {
        private static readonly ChildField[] ChildFields =
        {
            new ChildField("level", "运营等级", "该映射开始生效的运营等级，必须从 1 开始。"),
            new ChildField("styleId", "样式 ID", "留空表示默认样式；填写后只匹配同 ID 的视觉样式。"),
            new ChildField("view", "视图资源", "该运营等级和样式对应的独立视图资源。")
        };

        protected override IReadOnlyList<ChildField> Fields => ChildFields;
    }

    internal abstract class LabeledChildrenDrawer : PropertyDrawer
    {
        private static readonly float LineHeight = EditorGUIUtility.singleLineHeight;
        private static readonly float Spacing = EditorGUIUtility.standardVerticalSpacing;

        protected abstract IReadOnlyList<ChildField> Fields { get; }

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            var height = LineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            for (var i = 0; i < Fields.Count; i++)
            {
                var field = Fields[i];
                var child = property.FindPropertyRelative(field.PropertyName);
                if (child == null)
                {
                    continue;
                }

                height += Spacing + EditorGUI.GetPropertyHeight(child, field.Label, true);
            }

            return height;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            var headerLabel = LocalizeArrayElementLabel(label);
            EditorGUI.BeginProperty(position, headerLabel, property);
            var headerRect = new Rect(position.x, position.y, position.width, LineHeight);
            property.isExpanded = EditorGUI.Foldout(
                headerRect,
                property.isExpanded,
                headerLabel,
                true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                var y = headerRect.yMax + Spacing;
                for (var i = 0; i < Fields.Count; i++)
                {
                    var field = Fields[i];
                    var child = property.FindPropertyRelative(field.PropertyName);
                    if (child == null)
                    {
                        continue;
                    }

                    var childHeight = EditorGUI.GetPropertyHeight(child, field.Label, true);
                    var childRect = new Rect(position.x, y, position.width, childHeight);
                    EditorGUI.PropertyField(childRect, child, field.Label, true);
                    y += childHeight + Spacing;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static GUIContent LocalizeArrayElementLabel(GUIContent label)
        {
            if (label == null || string.IsNullOrEmpty(label.text))
            {
                return GUIContent.none;
            }

            const string elementPrefix = "Element ";
            if (!label.text.StartsWith(elementPrefix, StringComparison.Ordinal))
            {
                return label;
            }

            return new GUIContent(
                $"元素 {label.text.Substring(elementPrefix.Length)}",
                label.tooltip);
        }

        protected readonly struct ChildField
        {
            public ChildField(
                string propertyName,
                string label,
                string tooltip)
            {
                PropertyName = propertyName;
                Label = new GUIContent(label, tooltip);
            }

            public string PropertyName { get; }
            public GUIContent Label { get; }
        }
    }
}
