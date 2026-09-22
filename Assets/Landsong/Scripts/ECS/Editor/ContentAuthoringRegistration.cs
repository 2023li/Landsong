#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class ContentAuthoringRegistration
    {
        public static void Register(ScriptableObject catalog, ScriptableObject definition)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请退出运行模式并等待编译与导入完成后注册内容。");
            if (catalog == null || definition == null) throw new InvalidOperationException("请指定目录和定义。");
            var field = catalog.GetType().GetField("Definitions");
            if (field == null || !field.FieldType.IsArray || field.FieldType.GetElementType() != definition.GetType())
                throw new InvalidOperationException("定义类型与领域目录不一致。");
            using var source = new SerializedObject(definition);
            string id = source.FindProperty("Metadata.Id")?.stringValue;
            if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("注册前请填写稳定 ID。");
            using var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("Definitions");
            bool registered = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var existing = entries.GetArrayElementAtIndex(i).objectReferenceValue;
                if (existing == definition) { registered = true; continue; }
                if (existing == null) throw new InvalidOperationException("目录包含空项，请先修正。");
                using var row = new SerializedObject(existing);
                if (row.FindProperty("Metadata.Id").stringValue == id)
                    throw new InvalidOperationException("稳定 ID 已被注册：" + id);
            }
            if (registered) return;
            if (EditorUtility.IsPersistent(catalog)) Undo.RecordObject(catalog, "注册领域定义");
            entries.arraySize++;
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
    }
}
#endif
