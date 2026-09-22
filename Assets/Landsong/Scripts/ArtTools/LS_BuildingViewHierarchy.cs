#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    // Selects and prepares the exported scene hierarchy without owning mesh algorithms or asset storage.
    internal static class LS_BuildingViewHierarchy
    {
        internal static List<MeshRenderer> CollectStaticRenderers(Transform sourceRoot, IReadOnlyList<Transform> dynamicRoots)
        {
            var backedUpRenderers = GetLegacyOriginalRenderers(sourceRoot.gameObject);
            if (backedUpRenderers.Count > 0)
            {
                return backedUpRenderers.OfType<MeshRenderer>().Where(renderer => renderer != null && renderer.GetComponent<MeshFilter>()?.sharedMesh != null && !IsUnderAny(renderer.transform, dynamicRoots)).Distinct().ToList();
            }

            return sourceRoot.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer.enabled && IsActiveWithinSource(renderer.transform, sourceRoot) && renderer.GetComponent<MeshFilter>()?.sharedMesh != null && !IsUnderAny(renderer.transform, dynamicRoots)).ToList();
        }

        internal static List<MeshRenderer> CollectOriginalMeshRenderers(Transform sourceRoot)
        {
            return sourceRoot.GetComponentsInChildren<MeshRenderer>(true).Where(renderer => renderer != null && renderer.GetComponent<MeshFilter>()?.sharedMesh != null).ToList();
        }

        internal static bool HasLegacyRendererBackup(GameObject root)
        {
            return GetLegacyOriginalRenderers(root).Count > 0;
        }

        internal static List<Renderer> GetLegacyOriginalRenderers(GameObject root)
        {
            var result = new List<Renderer>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || !string.Equals(component.GetType().FullName, "UnityMeshSimplifier.LODBackupComponent", StringComparison.Ordinal))
                {
                    continue;
                }

                var property = component.GetType().GetProperty("OriginalRenderers", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetValue(component)is Renderer[] renderers)
                {
                    result.AddRange(renderers.Where(renderer => renderer != null));
                }
            }

            return result;
        }

        internal static void UnpackAllPrefabInstances(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true).OrderBy(transform => GetDepth(transform)).ToArray();
            foreach (var transform in transforms)
            {
                if (transform == null || !PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                {
                    continue;
                }

                PrefabUtility.UnpackPrefabInstance(transform.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }

        internal static void StripExportOnlyComponents(GameObject root, bool removeLodGroups)
        {
            foreach (var nativeHelper in root.GetComponentsInChildren<LODGeneratorHelper>(true))
            {
                if (nativeHelper != null)
                {
                    Object.DestroyImmediate(nativeHelper);
                }
            }

            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                {
                    continue;
                }

                var fullName = component.GetType().FullName;
                if (component is LS_BuildingViewOptimizer || component is EntityVisualAuthoring || (removeLodGroups && component is LODGroup) || string.Equals(fullName, "UnityMeshSimplifier.LODBackupComponent", StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        internal static void PruneEmptyBranches(Transform exportRoot, IReadOnlyList<Transform> protectedRoots)
        {
            var protectedTransforms = new HashSet<Transform>();
            foreach (var protectedRoot in protectedRoots)
            {
                var current = protectedRoot;
                while (current != null)
                {
                    protectedTransforms.Add(current);
                    if (current == exportRoot)
                    {
                        break;
                    }

                    current = current.parent;
                }
            }

            var transforms = exportRoot.GetComponentsInChildren<Transform>(true).OrderByDescending(GetDepth).ToArray();
            foreach (var transform in transforms)
            {
                if (transform == null || transform == exportRoot || protectedTransforms.Contains(transform) || transform.childCount > 0 || transform.GetComponents<Component>().Any(component => component != null && !(component is Transform)))
                {
                    continue;
                }

                Object.DestroyImmediate(transform.gameObject);
            }
        }

        internal static void StripLegacyGeneratedHierarchy(Transform cloneRoot)
        {
            var legacyRoots = cloneRoot.GetComponentsInChildren<Transform>(true).Where(transform => transform != cloneRoot && transform.name == LODGenerator.LODParentGameObjectName).OrderByDescending(GetDepth).ToArray();
            foreach (var legacyRoot in legacyRoots)
            {
                if (legacyRoot != null)
                {
                    Object.DestroyImmediate(legacyRoot.gameObject);
                }
            }
        }

        internal static void StripStaticRenderers(Transform cloneRoot, IReadOnlyList<Transform> dynamicRoots)
        {
            foreach (var renderer in cloneRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsUnderAny(renderer.transform, dynamicRoots))
                {
                    continue;
                }

                if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    Object.DestroyImmediate(renderer);
                    if (filter != null)
                    {
                        Object.DestroyImmediate(filter);
                    }
                }
                else
                {
                    Debug.LogWarning($"[建筑一键优化] 静态层级中的 {renderer.GetType().Name} 不支持合并，已从导出 View 移除：{GetTransformPath(renderer.transform)}");
                    Object.DestroyImmediate(renderer);
                }
            }
        }

        internal static void StripPhysics(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(rigidbody);
            }

            foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody2D>(true))
            {
                Object.DestroyImmediate(rigidbody);
            }
        }

        internal static void ResetRootTransform(Transform root)
        {
            root.SetParent(null, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        internal static int[] GetSiblingPath(Transform root, Transform target)
        {
            var reversed = new List<int>();
            var current = target;
            while (current != null && current != root)
            {
                reversed.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            if (current != root)
            {
                throw new InvalidOperationException($"{target.name} 不属于建筑根对象 {root.name}。");
            }

            reversed.Reverse();
            return reversed.ToArray();
        }

        internal static Transform ResolveBySiblingPath(Transform root, IReadOnlyList<int> siblingPath)
        {
            var current = root;
            foreach (var siblingIndex in siblingPath)
            {
                if (siblingIndex < 0 || siblingIndex >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(siblingIndex);
            }

            return current;
        }

        internal static bool IsUnderAny(Transform transform, IReadOnlyList<Transform> roots)
        {
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null && (transform == roots[i] || transform.IsChildOf(roots[i])))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool IsActiveWithinSource(Transform transform, Transform sourceRoot)
        {
            var current = transform;
            while (current != null && current != sourceRoot)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }

                current = current.parent;
            }

            return current == sourceRoot;
        }

        internal static int GetDepth(Transform transform)
        {
            var depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        internal static string GetTransformPath(Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }
    }
}
#endif
