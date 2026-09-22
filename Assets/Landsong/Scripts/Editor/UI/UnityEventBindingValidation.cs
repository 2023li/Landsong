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
    public static class UnityEventBindingValidation
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

    }
}
#endif
