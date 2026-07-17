using System;
using System.Collections.Generic;
using Landsong.Localization;

namespace Landsong
{
    public enum GameFeature
    {
        Building = 1,
        Inventory = 2,
        Technology = 3,
        Expedition = 4,
        Inheritance = 5,
        Congress = 6
    }

    /// <summary>
    /// Owns the runtime availability of major player-facing systems.
    /// Claimed quest rewards are replayed after restore, so feature state does not need a parallel save format.
    /// </summary>
    public sealed class GameFeatureUnlockService
    {
        private readonly HashSet<GameFeature> initialFeatures = new HashSet<GameFeature>();
        private readonly HashSet<GameFeature> unlockedFeatures = new HashSet<GameFeature>();

        internal GameFeatureUnlockService(IEnumerable<GameFeature> startingFeatures)
        {
            AddValidFeatures(startingFeatures, initialFeatures);
            ResetToInitialFeatures();
        }

        public event Action<GameFeatureUnlockService> StateChanged;

        public bool IsUnlocked(GameFeature feature)
        {
            return IsValid(feature) && unlockedFeatures.Contains(feature);
        }

        internal bool Unlock(IEnumerable<GameFeature> features, bool notify = true)
        {
            if (features == null)
            {
                return false;
            }

            var changed = false;
            foreach (var feature in features)
            {
                if (IsValid(feature))
                {
                    changed |= unlockedFeatures.Add(feature);
                }
            }

            if (changed && notify)
            {
                StateChanged?.Invoke(this);
            }

            return changed;
        }

        internal void ResetToInitialFeatures(bool notify = true)
        {
            unlockedFeatures.Clear();
            unlockedFeatures.UnionWith(initialFeatures);
            if (notify)
            {
                StateChanged?.Invoke(this);
            }
        }

        public static string GetDisplayName(GameFeature feature)
        {
            var fallback = feature switch
            {
                GameFeature.Building => "建造系统",
                GameFeature.Inventory => "库存系统",
                GameFeature.Technology => "科技系统",
                GameFeature.Expedition => "远征系统",
                GameFeature.Inheritance => "继承系统",
                GameFeature.Congress => "国会系统",
                _ => "未知系统"
            };
            return L10n.Gameplay($"gameplay.feature.{L10n.NormalizeKeyPart(feature.ToString())}", fallback);
        }

        public static bool IsValid(GameFeature feature)
        {
            return feature == GameFeature.Building
                   || feature == GameFeature.Inventory
                   || feature == GameFeature.Technology
                   || feature == GameFeature.Expedition
                   || feature == GameFeature.Inheritance
                   || feature == GameFeature.Congress;
        }

        private static void AddValidFeatures(IEnumerable<GameFeature> source, ISet<GameFeature> target)
        {
            if (source == null || target == null)
            {
                return;
            }

            foreach (var feature in source)
            {
                if (IsValid(feature))
                {
                    target.Add(feature);
                }
            }
        }
    }
}
