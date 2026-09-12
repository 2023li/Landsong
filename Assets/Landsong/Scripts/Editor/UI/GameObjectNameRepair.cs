#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    /// <summary>Repairs only unnamed objects in the migrated Game UI prefab family.</summary>
    public static class GameObjectNameRepair
    {
        const string Folder = "Assets/Landsong/Objects/Prefabs/UI/GamePanel";
        const string RootPath = Folder + "/UI_GamePanel.prefab";

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式。");
            var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path == RootPath ? 2 : path.Contains("/Views/") ? 1 : 0)
                .ThenBy(path => path, StringComparer.Ordinal).ToArray();
            var report = new StringBuilder();
            var total = 0;
            foreach (var path in paths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var candidates = CollectNames(root);
                    var changed = false;
                    foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!string.IsNullOrWhiteSpace(node.name)) continue;
                        var oldPath = HierarchyPath(node);
                        var suggested = node == root.transform ? Path.GetFileNameWithoutExtension(path) : RoleName(node);
                        if (candidates.TryGetValue(node.gameObject, out var names) && names.Count > 0)
                            suggested = names.Min;
                        node.name = UniqueName(node, suggested);
                        EditorUtility.SetDirty(node.gameObject);
                        if (PrefabUtility.IsPartOfPrefabInstance(node.gameObject))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
                        report.AppendLine(path + " | " + oldPath + " -> " + node.name);
                        changed = true;
                        total++;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                    if (root.GetComponentsInChildren<Transform>(true).Any(node => string.IsNullOrWhiteSpace(node.name)))
                        throw new InvalidOperationException("仍有未命名的游戏界面对象：" + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            var summary = "Game UI 空对象名称补校完成：" + total + " 个对象，" + paths.Length + " 个预制体。";
            report.Insert(0, summary + Environment.NewLine);
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText("Library/LandsongEcs/game-object-name-repair.txt", report.ToString(), Encoding.UTF8);
            return summary;
        }

        static Dictionary<GameObject, SortedSet<string>> CollectNames(GameObject root)
        {
            var result = new Dictionary<GameObject, SortedSet<string>>();
            foreach (var owner in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (owner == null || owner.GetType().Namespace == null || !owner.GetType().Namespace.StartsWith("Landsong", StringComparison.Ordinal)) continue;
                var serialized = new SerializedObject(owner);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference || property.propertyPath.StartsWith("m_", StringComparison.Ordinal)) continue;
                    var reference = property.objectReferenceValue;
                    var target = reference as GameObject;
                    if (reference is Component component) target = component.gameObject;
                    if (target == null || !string.IsNullOrWhiteSpace(target.name) || !target.transform.IsChildOf(root.transform)) continue;
                    var fieldPath = property.propertyPath;
                    if (fieldPath == "Target" || fieldPath == "target" || fieldPath.StartsWith("childViews", StringComparison.Ordinal) || fieldPath.StartsWith("previewBindings", StringComparison.Ordinal)) continue;
                    var candidate = Regex.Replace(fieldPath, @"\.Array\.data\[(\d+)\]", match => "_" + (int.Parse(match.Groups[1].Value) + 1).ToString("00"));
                    candidate = Regex.Replace(candidate.Replace('.', '_'), @"[^\p{L}\p{N}_]", "");
                    if (candidate.Length == 0) continue;
                    if (!result.TryGetValue(target, out var names))
                    {
                        names = new SortedSet<string>(StringComparer.Ordinal);
                        result.Add(target, names);
                    }
                    names.Add(candidate);
                }
            }
            return result;
        }

        static string RoleName(Transform node)
        {
            if (node.GetComponent<TMP_InputField>() != null) return "InputField";
            if (node.GetComponent<Button>() != null) return "Button";
            if (node.GetComponent<Toggle>() != null) return "Toggle";
            if (node.GetComponent<Slider>() != null) return "Slider";
            if (node.GetComponent<ScrollRect>() != null) return "ScrollView";
            if (node.GetComponent<TMP_Text>() != null) return "Text";
            if (node.GetComponent<Image>() != null) return "Image";
            return node is RectTransform ? "Container" : "Object";
        }

        static string UniqueName(Transform node, string candidate)
        {
            if (node.parent == null) return candidate;
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < node.parent.childCount; index++)
            {
                var sibling = node.parent.GetChild(index);
                if (sibling != node) occupied.Add(sibling.name);
            }
            if (!occupied.Contains(candidate)) return candidate;
            var suffix = 2;
            while (occupied.Contains(candidate + "_" + suffix.ToString("00"))) suffix++;
            return candidate + "_" + suffix.ToString("00");
        }

        static string HierarchyPath(Transform node)
        {
            var segments = new Stack<string>();
            for (var current = node; current != null; current = current.parent)
                segments.Push(string.IsNullOrWhiteSpace(current.name) ? "<空名称>#" + current.GetSiblingIndex() : current.name);
            return string.Join("/", segments);
        }
    }
}
#endif
