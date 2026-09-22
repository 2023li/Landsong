#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    // Test-only Unity object cloning. The generic caller retains each concrete catalog and definition type.
    public static class CatalogFixture
    {
        public static T Clone<T>(T source)
            where T : ScriptableObject
        {
            var scope = new Scope();
            return scope.Clone(source);
        }

        public static T[] CloneDefinitions<T>(T[] source)
            where T : ScriptableObject
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var map = new Dictionary<Object, Object>();
            var copies = source.Select(asset => asset == null ? null : Object.Instantiate(asset)).ToArray();
            for (int i = 0; i < source.Length; i++)
                if (source[i] != null)
                    map[source[i]] = copies[i];
            foreach (var copy in copies)
                if (copy != null)
                    Remap(copy, map);
            return copies;
        }

        public static void Destroy<T>(T catalog)
            where T : ScriptableObject
        {
            if (catalog == null)
                return;
            using var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("Definitions");
            if (definitions != null && definitions.isArray)
                for (int i = 0; i < definitions.arraySize; i++)
                {
                    var asset = definitions.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (asset != null && !AssetDatabase.Contains(asset))
                        Object.DestroyImmediate(asset);
                }

            if (!AssetDatabase.Contains(catalog))
                Object.DestroyImmediate(catalog);
        }

        public sealed class Scope : IDisposable
        {
            readonly Dictionary<Object, Object> copies = new Dictionary<Object, Object>();
            public void CloneCatalogsOn(GameObject composition)
            {
                // Only the explicit content set referenced by this composition participates.
                // This never scans the project or creates a shared definition index.
                var components = composition.GetComponents<MonoBehaviour>().Where(value => value != null).ToArray();
                var contentSource = composition.GetComponent<GameContentSetAuthoring>();
                if (contentSource != null && contentSource.Content != null)
                {
                    var set = Clone(contentSource.Content);
                    using var serialized = new SerializedObject(contentSource.Content);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is ScriptableObject catalog)
                            Clone(catalog);
                    Remap(set, copies);
                }

                foreach (var component in components)
                {
                    using var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference || !(property.objectReferenceValue is ScriptableObject asset))
                            continue;
                        Clone(asset);
                    }
                }

                foreach (var component in components)
                    Remap(component, copies);
            }

            public void RemapReferences(Object owner) => Remap(owner, copies);
            public T Clone<T>(T source)
                where T : ScriptableObject
            {
                if (source == null)
                    throw new ArgumentNullException(nameof(source));
                if (copies.TryGetValue(source, out var existing))
                    return (T)existing;
                var catalog = Object.Instantiate(source);
                copies.Add(source, catalog);
                using (var serialized = new SerializedObject(source))
                {
                    var definitions = serialized.FindProperty("Definitions");
                    for (int i = 0; definitions != null && definitions.isArray && i < definitions.arraySize; i++)
                    {
                        var original = definitions.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (original != null && !copies.ContainsKey(original))
                            copies.Add(original, Object.Instantiate(original));
                    }
                }

                foreach (var owner in copies.Values.ToArray())
                    Remap(owner, copies);
                return catalog;
            }

            public void Dispose()
            {
                foreach (var copy in copies.Values.Distinct())
                    if (copy != null)
                        Object.DestroyImmediate(copy);
                copies.Clear();
            }
        }

        static void Remap(Object owner, Dictionary<Object, Object> replacements)
        {
            using var serialized = new SerializedObject(owner);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue != null && replacements.TryGetValue(property.objectReferenceValue, out var replacement))
                    property.objectReferenceValue = replacement;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
