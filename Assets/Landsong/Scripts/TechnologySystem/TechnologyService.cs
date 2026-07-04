using System;
using System.Collections.Generic;

namespace Landsong.TechnologySystem
{
    public enum TechnologyUnlockFailureReason
    {
        None = 0,
        InvalidTechnology = 1,
        AlreadyUnlocked = 2,
        PrerequisitesLocked = 3,
        InsufficientPoints = 4
    }

    [Serializable]
    public sealed class TechnologySaveData
    {
        public int SciencePoints;
        public List<string> UnlockedTechnologyIds = new List<string>();

        public void Validate()
        {
            SciencePoints = Math.Max(0, SciencePoints);

            if (UnlockedTechnologyIds == null)
            {
                UnlockedTechnologyIds = new List<string>();
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = UnlockedTechnologyIds.Count - 1; i >= 0; i--)
            {
                var technologyId = NormalizeTechnologyId(UnlockedTechnologyIds[i]);
                if (string.IsNullOrEmpty(technologyId) || !seen.Add(technologyId))
                {
                    UnlockedTechnologyIds.RemoveAt(i);
                    continue;
                }

                UnlockedTechnologyIds[i] = technologyId;
            }
        }

        internal static string NormalizeTechnologyId(string technologyId)
        {
            return string.IsNullOrWhiteSpace(technologyId) ? string.Empty : technologyId.Trim();
        }
    }

    public sealed class TechnologyService
    {
        private readonly HashSet<string> unlockedTechnologyIds = new HashSet<string>(StringComparer.Ordinal);
        private TechnologyCatalog catalog;
        private int sciencePoints;

        public TechnologyService(
            TechnologyCatalog catalog,
            IEnumerable<string> startingUnlockedTechnologyIds = null,
            int startingSciencePoints = 0)
        {
            this.catalog = catalog;
            sciencePoints = Math.Max(0, startingSciencePoints);
            AddUnlockedTechnologyIds(startingUnlockedTechnologyIds);
        }

        public event Action<TechnologyService> SciencePointsChanged;
        public event Action<TechnologyService> StateChanged;
        public event Action<TechnologyService, string> TechnologyUnlocked;

        public TechnologyCatalog Catalog => catalog;
        public int SciencePoints => sciencePoints;
        public IReadOnlyCollection<string> UnlockedTechnologyIds => unlockedTechnologyIds;

        public void SetCatalog(TechnologyCatalog newCatalog)
        {
            if (catalog == newCatalog)
            {
                return;
            }

            catalog = newCatalog;
            StateChanged?.Invoke(this);
        }

        public bool IsUnlocked(string technologyId)
        {
            var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
            return !string.IsNullOrEmpty(normalizedId) && unlockedTechnologyIds.Contains(normalizedId);
        }

        public bool CanUnlock(string technologyId, out TechnologyUnlockFailureReason failureReason)
        {
            if (catalog == null || !catalog.TryGetDefinition(technologyId, out var definition))
            {
                failureReason = TechnologyUnlockFailureReason.InvalidTechnology;
                return false;
            }

            return CanUnlock(definition, out failureReason);
        }

        public bool CanUnlock(TechnologyDefinition definition, out TechnologyUnlockFailureReason failureReason)
        {
            if (definition == null || !definition.IsValid)
            {
                failureReason = TechnologyUnlockFailureReason.InvalidTechnology;
                return false;
            }

            if (IsUnlocked(definition.TechnologyId))
            {
                failureReason = TechnologyUnlockFailureReason.AlreadyUnlocked;
                return false;
            }

            var prerequisites = definition.Prerequisites;
            for (var i = 0; i < prerequisites.Count; i++)
            {
                var prerequisite = prerequisites[i];
                if (prerequisite == null || string.IsNullOrWhiteSpace(prerequisite.TechnologyId))
                {
                    continue;
                }

                if (!IsUnlocked(prerequisite.TechnologyId))
                {
                    failureReason = TechnologyUnlockFailureReason.PrerequisitesLocked;
                    return false;
                }
            }

            if (sciencePoints < definition.SciencePointCost)
            {
                failureReason = TechnologyUnlockFailureReason.InsufficientPoints;
                return false;
            }

            failureReason = TechnologyUnlockFailureReason.None;
            return true;
        }

        public bool TryUnlock(string technologyId)
        {
            if (catalog == null || !catalog.TryGetDefinition(technologyId, out var definition))
            {
                return false;
            }

            return TryUnlock(definition);
        }

        public bool TryUnlock(TechnologyDefinition definition)
        {
            if (!CanUnlock(definition, out _))
            {
                return false;
            }

            sciencePoints -= definition.SciencePointCost;
            unlockedTechnologyIds.Add(definition.TechnologyId.Trim());
            SciencePointsChanged?.Invoke(this);
            TechnologyUnlocked?.Invoke(this, definition.TechnologyId);
            StateChanged?.Invoke(this);
            return true;
        }

        public bool UnlockForFree(string technologyId)
        {
            var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
            if (string.IsNullOrEmpty(normalizedId) || !unlockedTechnologyIds.Add(normalizedId))
            {
                return false;
            }

            TechnologyUnlocked?.Invoke(this, normalizedId);
            StateChanged?.Invoke(this);
            return true;
        }

        public void AddSciencePoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            sciencePoints += amount;
            SciencePointsChanged?.Invoke(this);
            StateChanged?.Invoke(this);
        }

        public void SetSciencePoints(int amount)
        {
            var normalizedAmount = Math.Max(0, amount);
            if (sciencePoints == normalizedAmount)
            {
                return;
            }

            sciencePoints = normalizedAmount;
            SciencePointsChanged?.Invoke(this);
            StateChanged?.Invoke(this);
        }

        public TechnologySaveData CaptureSaveData()
        {
            var saveData = new TechnologySaveData
            {
                SciencePoints = sciencePoints,
                UnlockedTechnologyIds = new List<string>(unlockedTechnologyIds)
            };

            saveData.UnlockedTechnologyIds.Sort(StringComparer.Ordinal);
            return saveData;
        }

        public void RestoreSaveData(TechnologySaveData saveData, IEnumerable<string> fallbackUnlockedTechnologyIds = null)
        {
            unlockedTechnologyIds.Clear();

            if (saveData != null)
            {
                saveData.Validate();
                sciencePoints = saveData.SciencePoints;
                AddUnlockedTechnologyIds(saveData.UnlockedTechnologyIds);
            }
            else
            {
                sciencePoints = 0;
                AddUnlockedTechnologyIds(fallbackUnlockedTechnologyIds);
            }

            SciencePointsChanged?.Invoke(this);
            StateChanged?.Invoke(this);
        }

        private void AddUnlockedTechnologyIds(IEnumerable<string> technologyIds)
        {
            if (technologyIds == null)
            {
                return;
            }

            foreach (var technologyId in technologyIds)
            {
                var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
                if (!string.IsNullOrEmpty(normalizedId))
                {
                    unlockedTechnologyIds.Add(normalizedId);
                }
            }
        }
    }
}
