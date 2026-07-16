using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.TechnologySystem
{
    /// <summary>
    /// 科技节点上展示的解锁内容类型。类型只决定 UI 语义；实际生效规则仍归各领域系统所有。
    /// </summary>
    public enum TechnologyUnlockContentKind
    {
        Other = 0,
        ItemReward = 1,
        Building = 2,
        BuildingUpgrade = 3,
        GlobalBuff = 4
    }

    public readonly struct TechnologyUnlockContent
    {
        public TechnologyUnlockContent(
            string stableId,
            Sprite icon,
            string displayName,
            TechnologyUnlockContentKind kind,
            int amount = 1,
            string shortLabel = "")
        {
            StableId = Normalize(stableId);
            Icon = icon;
            DisplayName = Normalize(displayName);
            Kind = kind;
            Amount = Mathf.Max(1, amount);
            ShortLabel = Normalize(shortLabel);
        }

        public string StableId { get; }
        public Sprite Icon { get; }
        public string DisplayName { get; }
        public TechnologyUnlockContentKind Kind { get; }
        public int Amount { get; }
        public string ShortLabel { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(StableId)
                               && (Icon != null || !string.IsNullOrWhiteSpace(DisplayName));

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public readonly struct TechnologyUnlockContentBinding
    {
        public TechnologyUnlockContentBinding(string technologyId, TechnologyUnlockContent content)
        {
            TechnologyId = Normalize(technologyId);
            Content = content;
        }

        public string TechnologyId { get; }
        public TechnologyUnlockContent Content { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(TechnologyId) && Content.IsValid;

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    /// <summary>
    /// 解锁内容生产者在自身数据建立或变化时，主动把完整快照注入中央注册表。
    /// 科技节点只读取注册表，不搜索 Building、Buff 或其他领域资产。
    /// </summary>
    public interface ITechnologyUnlockContentProducer
    {
        string TechnologyUnlockContentSourceId { get; }
        void InjectTechnologyUnlockContents(TechnologyUnlockContentRegistry registry);
    }

    /// <summary>
    /// 科技 UI 唯一读取入口。每个来源使用 ReplaceSource 原子替换自己的快照，
    /// 因而重复注入不会产生重复项，来源删除配置后旧项也会同步移除。
    /// </summary>
    public sealed class TechnologyUnlockContentRegistry
    {
        private readonly Dictionary<string, TechnologyUnlockContentBinding[]> bindingsBySource =
            new Dictionary<string, TechnologyUnlockContentBinding[]>(StringComparer.Ordinal);

        private readonly Dictionary<string, List<TechnologyUnlockContent>> contentsByTechnologyId =
            new Dictionary<string, List<TechnologyUnlockContent>>(StringComparer.Ordinal);

        public event Action<TechnologyUnlockContentRegistry> Changed;
        public int Version { get; private set; }

        public void ReplaceSource(string sourceId, IEnumerable<TechnologyUnlockContentBinding> bindings)
        {
            sourceId = Normalize(sourceId);
            if (string.IsNullOrEmpty(sourceId))
            {
                throw new ArgumentException("科技解锁内容来源 ID 不能为空。", nameof(sourceId));
            }

            var normalized = NormalizeBindings(bindings);
            if (normalized.Length == 0)
            {
                bindingsBySource.Remove(sourceId);
            }
            else
            {
                bindingsBySource[sourceId] = normalized;
            }

            RebuildIndex();
        }

        public void RemoveSource(string sourceId)
        {
            sourceId = Normalize(sourceId);
            if (!string.IsNullOrEmpty(sourceId) && bindingsBySource.Remove(sourceId))
            {
                RebuildIndex();
            }
        }

        public void Clear()
        {
            if (bindingsBySource.Count == 0 && contentsByTechnologyId.Count == 0)
            {
                return;
            }

            bindingsBySource.Clear();
            RebuildIndex();
        }

        public void Collect(TechnologyDefinition technology, List<TechnologyUnlockContent> destination)
        {
            Collect(technology == null ? string.Empty : technology.TechnologyId, destination);
        }

        public void Collect(string technologyId, List<TechnologyUnlockContent> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            technologyId = Normalize(technologyId);
            if (!string.IsNullOrEmpty(technologyId)
                && contentsByTechnologyId.TryGetValue(technologyId, out var contents))
            {
                destination.AddRange(contents);
            }
        }

        public static void InjectCompletionEffects(
            TechnologyCatalog catalog,
            TechnologyUnlockContentRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            var bindings = new List<TechnologyUnlockContentBinding>();
            var definitions = catalog?.Definitions;
            if (definitions != null)
            {
                for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
                {
                    var definition = definitions[definitionIndex];
                    if (definition == null)
                    {
                        continue;
                    }

                    var effects = definition.CompletionEffects;
                    for (var effectIndex = 0; effectIndex < effects.Count; effectIndex++)
                    {
                        var effect = effects[effectIndex];
                        if (effect != null
                            && effect.TryGetPresentation(out var content)
                            && content.IsValid)
                        {
                            bindings.Add(new TechnologyUnlockContentBinding(
                                definition.TechnologyId,
                                content));
                        }
                    }
                }
            }

            registry.ReplaceSource("technology.completion-effects", bindings);
        }

        private void RebuildIndex()
        {
            contentsByTechnologyId.Clear();
            var stableIdsByTechnology = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            var sourceIds = new List<string>(bindingsBySource.Keys);
            sourceIds.Sort(StringComparer.Ordinal);

            for (var sourceIndex = 0; sourceIndex < sourceIds.Count; sourceIndex++)
            {
                var bindings = bindingsBySource[sourceIds[sourceIndex]];
                for (var bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    var binding = bindings[bindingIndex];
                    if (!binding.IsValid)
                    {
                        continue;
                    }

                    if (!stableIdsByTechnology.TryGetValue(binding.TechnologyId, out var stableIds))
                    {
                        stableIds = new HashSet<string>(StringComparer.Ordinal);
                        stableIdsByTechnology.Add(binding.TechnologyId, stableIds);
                    }

                    if (!stableIds.Add(binding.Content.StableId))
                    {
                        continue;
                    }

                    if (!contentsByTechnologyId.TryGetValue(binding.TechnologyId, out var contents))
                    {
                        contents = new List<TechnologyUnlockContent>();
                        contentsByTechnologyId.Add(binding.TechnologyId, contents);
                    }

                    contents.Add(binding.Content);
                }
            }

            unchecked
            {
                Version++;
                if (Version == 0)
                {
                    Version = 1;
                }
            }

            Changed?.Invoke(this);
        }

        private static TechnologyUnlockContentBinding[] NormalizeBindings(
            IEnumerable<TechnologyUnlockContentBinding> bindings)
        {
            if (bindings == null)
            {
                return Array.Empty<TechnologyUnlockContentBinding>();
            }

            var result = new List<TechnologyUnlockContentBinding>();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings)
            {
                if (!binding.IsValid
                    || !keys.Add($"{binding.TechnologyId}\u001f{binding.Content.StableId}"))
                {
                    continue;
                }

                result.Add(binding);
            }

            return result.ToArray();
        }

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
