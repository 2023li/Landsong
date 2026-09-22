#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Landsong.VisualSystem
{
    // Preserves authored moving parts when cloning and building static LOD geometry.
    internal static class LS_BuildingViewDynamicParts
    {
        internal sealed class Pair
        {
            public LS_BuildingViewOptimizer.DynamicPartSettings Settings;
            public Transform SourceRoot;
            public Transform CloneRoot;
        }

        internal static List<Transform> ValidateAndCollectDynamicRoots(LS_BuildingViewOptimizer optimizer, Transform sourceRoot)
        {
            var result = new List<Transform>();
            foreach (var settings in optimizer.DynamicParts)
            {
                if (settings?.Root == null)
                {
                    continue;
                }

                if (settings.Root == sourceRoot || !settings.Root.IsChildOf(sourceRoot))
                {
                    throw new InvalidOperationException($"动态部件“{settings.Root.name}”必须是建筑根对象的子节点，不能是建筑根对象本身。");
                }

                if (result.Any(existing => settings.Root.IsChildOf(existing) || existing.IsChildOf(settings.Root)))
                {
                    throw new InvalidOperationException($"动态部件“{settings.Root.name}”与另一个动态部件存在父子嵌套，请只配置最外层旋转根节点。");
                }

                result.Add(settings.Root);
            }

            return result;
        }

        internal static List<Pair> ResolveDynamicPairs(LS_BuildingViewOptimizer optimizer, Transform sourceRoot, Transform cloneRoot)
        {
            var result = new List<Pair>();
            foreach (var settings in optimizer.DynamicParts)
            {
                if (settings?.Root == null)
                {
                    continue;
                }

                var cloneTransform = LS_BuildingViewHierarchy.ResolveBySiblingPath(cloneRoot, LS_BuildingViewHierarchy.GetSiblingPath(sourceRoot, settings.Root));
                if (cloneTransform == null)
                {
                    throw new InvalidOperationException($"无法在导出副本中找到动态部件“{settings.Root.name}”。");
                }

                result.Add(new Pair { Settings = settings, SourceRoot = settings.Root, CloneRoot = cloneTransform });
            }

            return result;
        }

        internal static void ConfigureDynamicParts(IEnumerable<Pair> dynamicParts, LS_BuildingViewExportStats stats)
        {
            foreach (var pair in dynamicParts)
            {
                if (pair.Settings.AddLocalAxisRotation)
                {
                    var rotator = pair.CloneRoot.GetComponent<LS_LocalAxisRotator>() ?? pair.CloneRoot.gameObject.AddComponent<LS_LocalAxisRotator>();
                    rotator.Configure(pair.Settings.LocalAxis, pair.Settings.DegreesPerSecond, pair.Settings.UseUnscaledTime);
                }

                if (pair.CloneRoot.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    stats.Warnings.Add($"动态部件没有 Renderer：{LS_BuildingViewHierarchy.GetTransformPath(pair.SourceRoot)}");
                }
            }
        }

        internal static Renderer[] CollectDynamicRenderers(IEnumerable<Pair> dynamicParts)
        {
            var renderers = dynamicParts.SelectMany(pair => pair.CloneRoot.GetComponentsInChildren<Renderer>(true)).Where(renderer => renderer != null).Distinct().ToArray();
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
            }

            return renderers;
        }
    }
}
#endif
