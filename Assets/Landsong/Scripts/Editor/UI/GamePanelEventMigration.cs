#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    // Persistent calls are authored configuration. Resolve moved methods once in the
    // editor; no runtime component search or legacy forwarding method is required.
    public static class GamePanelEventMigration
    {
        internal static Type[] ArgumentTypes(SerializedProperty call)
        {
            switch (call.FindPropertyRelative("m_Mode").intValue)
            {
                case 0: // Button.onClick has no dynamic arguments.
                case 1: return Type.EmptyTypes;
                case 2:
                    string name = call.FindPropertyRelative("m_Arguments.m_ObjectArgumentAssemblyTypeName").stringValue;
                    var type = Type.GetType(name, false);
                    if (type == null || !typeof(UnityEngine.Object).IsAssignableFrom(type))
                        throw new InvalidOperationException("按钮对象参数类型缺失或无效：" + name);
                    return new[] { type };
                case 3: return new[] { typeof(int) };
                case 4: return new[] { typeof(float) };
                case 5: return new[] { typeof(string) };
                case 6: return new[] { typeof(bool) };
                default: throw new InvalidOperationException("未知按钮事件参数模式。");
            }
        }

        internal static bool HasValidMethod(UnityEngine.Object target, SerializedProperty call)
        {
            if (target == null) return false;
            string method = call.FindPropertyRelative("m_MethodName").stringValue;
            if (string.IsNullOrEmpty(method)) return false;
            var found = UnityEventBase.GetValidMethodInfo(target, method, ArgumentTypes(call));
            return found != null && found.IsPublic && !found.IsStatic && found.ReturnType == typeof(void);
        }

        internal static bool IsValidCall(SerializedProperty call)
        {
            var target = call.FindPropertyRelative("m_Target").objectReferenceValue;
            if (!HasValidMethod(target, call)) return false;
            string storedType = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
            return Type.GetType(storedType, false) == target.GetType();
        }

        [MenuItem("Landsong/ECS/Migration/Repair game persistent event targets")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("请退出运行并等待编译完成后迁移按钮事件。");
            string path = ApplicationUiMigration.GamePath;
            var game = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var root = game.GetComponent<UI_GamePanel>();
                if (root == null) throw new InvalidOperationException("游戏根面板配置缺失。");
                var components = game.GetComponentsInChildren<MonoBehaviour>(true);
                var changes = new List<(Button Button, int Index, UnityEngine.Object Target)>();
                int inspected = 0;
                foreach (var button in game.GetComponentsInChildren<Button>(true))
                {
                    var calls = new SerializedObject(button).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
                    for (int i = 0; i < calls.arraySize; i++)
                    {
                        inspected++;
                        var call = calls.GetArrayElementAtIndex(i);
                        var target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                        if (!HasValidMethod(target, call))
                        {
                            // A former root method may now belong to exactly one authored
                            // controller. Missing targets and ambiguous methods are errors.
                            if (target != root)
                                throw new InvalidOperationException("按钮事件目标或方法无效：" + button.name + "." + call.FindPropertyRelative("m_MethodName").stringValue);
                            var candidates = components.Where(component => component != root && HasValidMethod(component, call)).ToArray();
                            if (candidates.Length != 1)
                                throw new InvalidOperationException("旧根按钮事件无法唯一定位到配置组件：" + button.name + "." + call.FindPropertyRelative("m_MethodName").stringValue + "，候选数量 " + candidates.Length);
                            target = candidates[0];
                        }
                        if (target != call.FindPropertyRelative("m_Target").objectReferenceValue || !IsValidCall(call))
                            changes.Add((button, i, target));
                    }
                }
                // All calls pass preflight before the loaded prefab is changed.
                if (changes.Count > 0)
                {
                    const string backup = "Library/LandsongEcs/NavigationMigration/BeforeEventTargets.prefab";
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    if (!File.Exists(backup)) File.Copy(path, backup);
                    foreach (var change in changes)
                    {
                        var serialized = new SerializedObject(change.Button);
                        var call = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").GetArrayElementAtIndex(change.Index);
                        call.FindPropertyRelative("m_Target").objectReferenceValue = change.Target;
                        var type = change.Target.GetType();
                        call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = type.FullName + ", " + type.Assembly.GetName().Name;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(game, path);
                }
                return $"已检查 {inspected} 个有效根实例静态按钮事件，修复 {changes.Count} 个目标或类型配置。";
            }
            finally { PrefabUtility.UnloadPrefabContents(game); }
        }
    }
}
#endif
