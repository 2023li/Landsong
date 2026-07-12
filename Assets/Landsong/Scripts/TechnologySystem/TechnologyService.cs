using System;
using System.Collections.Generic;

namespace Landsong.TechnologySystem
{
    public enum TechnologyResearchFailureReason
    {
        None = 0,
        InvalidTechnology = 1,
        AlreadyUnlocked = 2,
        PrerequisitesLocked = 3
    }

    public readonly struct TechnologyResearchAppliedResult
    {
        public TechnologyResearchAppliedResult(
            TechnologyDefinition technology,
            int appliedPoints,
            int progressBefore,
            int progressAfter,
            int requiredPoints,
            bool completed,
            bool unlockedForFirstTime = false)
        {
            Technology = technology;
            TechnologyId = technology == null ? string.Empty : technology.TechnologyId;
            AppliedPoints = Math.Max(0, appliedPoints);
            ProgressBefore = Math.Max(0, progressBefore);
            ProgressAfter = Math.Max(0, progressAfter);
            RequiredPoints = Math.Max(0, requiredPoints);
            Completed = completed;
            UnlockedForFirstTime = unlockedForFirstTime;
        }

        public TechnologyDefinition Technology { get; }
        public string TechnologyId { get; }
        public int AppliedPoints { get; }
        public int ProgressBefore { get; }
        public int ProgressAfter { get; }
        public int RequiredPoints { get; }
        public bool Completed { get; }
        public bool UnlockedForFirstTime { get; }
        public bool HasResearch => Technology != null && !string.IsNullOrWhiteSpace(TechnologyId);
    }

    [Serializable]
    public sealed class TechnologyResearchProgressSaveData
    {
        public string TechnologyId = string.Empty;
        public int Progress;

        public void Validate()
        {
            TechnologyId = TechnologySaveData.NormalizeTechnologyId(TechnologyId);
            Progress = Math.Max(0, Progress);
        }
    }

    [Serializable]
    public sealed class TechnologySaveData
    {
        public string CurrentResearchTechnologyId = string.Empty;
        public int CurrentResearchProgress;
        public int LastTurnResearchPoints;
        public List<TechnologyResearchProgressSaveData> ResearchProgresses =
            new List<TechnologyResearchProgressSaveData>();
        public List<string> UnlockedTechnologyIds = new List<string>();
        public List<string> ResearchQueueTechnologyIds = new List<string>();

        public void Validate()
        {
            CurrentResearchTechnologyId = NormalizeTechnologyId(CurrentResearchTechnologyId);
            CurrentResearchProgress = Math.Max(0, CurrentResearchProgress);
            LastTurnResearchPoints = Math.Max(0, LastTurnResearchPoints);
            NormalizeUnlockedTechnologies();
            NormalizeResearchQueue();
            NormalizeResearchProgresses();
        }

        internal static string NormalizeTechnologyId(string technologyId)
        {
            return string.IsNullOrWhiteSpace(technologyId) ? string.Empty : technologyId.Trim();
        }

        private void NormalizeUnlockedTechnologies()
        {
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

        private void NormalizeResearchProgresses()
        {
            ResearchProgresses ??= new List<TechnologyResearchProgressSaveData>();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = ResearchProgresses.Count - 1; i >= 0; i--)
            {
                var progress = ResearchProgresses[i];
                if (progress == null)
                {
                    ResearchProgresses.RemoveAt(i);
                    continue;
                }

                progress.Validate();
                if (string.IsNullOrWhiteSpace(progress.TechnologyId)
                    || progress.Progress <= 0
                    || !seen.Add(progress.TechnologyId))
                {
                    ResearchProgresses.RemoveAt(i);
                }
            }

            if (!string.IsNullOrWhiteSpace(CurrentResearchTechnologyId) && CurrentResearchProgress > 0)
            {
                AddOrReplaceProgress(CurrentResearchTechnologyId, CurrentResearchProgress);
            }
        }

        private void AddOrReplaceProgress(string technologyId, int progress)
        {
            var normalizedId = NormalizeTechnologyId(technologyId);
            if (string.IsNullOrWhiteSpace(normalizedId) || progress <= 0)
            {
                return;
            }

            for (var i = 0; i < ResearchProgresses.Count; i++)
            {
                if (ResearchProgresses[i].TechnologyId != normalizedId)
                {
                    continue;
                }

                ResearchProgresses[i].Progress = Math.Max(0, progress);
                return;
            }

            ResearchProgresses.Add(
                new TechnologyResearchProgressSaveData
                {
                    TechnologyId = normalizedId,
                    Progress = Math.Max(0, progress)
                });
        }

        private void NormalizeResearchQueue()
        {
            if (ResearchQueueTechnologyIds == null)
            {
                ResearchQueueTechnologyIds = new List<string>();
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = ResearchQueueTechnologyIds.Count - 1; i >= 0; i--)
            {
                var technologyId = NormalizeTechnologyId(ResearchQueueTechnologyIds[i]);
                if (string.IsNullOrEmpty(technologyId) || !seen.Add(technologyId))
                {
                    ResearchQueueTechnologyIds.RemoveAt(i);
                    continue;
                }

                ResearchQueueTechnologyIds[i] = technologyId;
            }
        }
    }

    public sealed class TechnologyService
    {
        private readonly HashSet<string> unlockedTechnologyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> researchProgressByTechnologyId =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> researchQueueTechnologyIds = new List<string>();

        private TechnologyCatalog catalog;
        private string currentResearchTechnologyId = string.Empty;
        private int lastTurnResearchPoints;

        public TechnologyService(
            TechnologyCatalog catalog,
            IEnumerable<string> startingUnlockedTechnologyIds = null,
            string startingResearchTechnologyId = null,
            int startingResearchProgress = 0)
        {
            this.catalog = catalog;
            AddUnlockedTechnologyIds(startingUnlockedTechnologyIds);
            RestoreCurrentResearch(startingResearchTechnologyId, startingResearchProgress);
        }

        public event Action<TechnologyService> StateChanged;
        public event Action<TechnologyService> CurrentResearchChanged;
        public event Action<TechnologyService, TechnologyResearchAppliedResult> ResearchProgressApplied;
        public event Action<TechnologyService, string> TechnologyUnlocked;

        public TechnologyCatalog Catalog => catalog;
        public IReadOnlyCollection<string> UnlockedTechnologyIds => unlockedTechnologyIds;
        public IReadOnlyList<string> ResearchQueueTechnologyIds => researchQueueTechnologyIds;
        public string CurrentResearchTechnologyId => currentResearchTechnologyId;
        public TechnologyDefinition CurrentResearchDefinition => GetDefinition(currentResearchTechnologyId);
        public int CurrentResearchProgress => GetResearchProgress(currentResearchTechnologyId);
        public int CurrentResearchRequiredPoints => GetRequiredResearchPoints(CurrentResearchDefinition);
        public int LastTurnResearchPoints => lastTurnResearchPoints;
        public bool HasCurrentResearch => CurrentResearchDefinition != null;
        public bool HasResearchQueue => researchQueueTechnologyIds.Count > 0;
        public bool HasResearchPlan => HasCurrentResearch || HasResearchQueue;
        public float CurrentResearchProgress01 =>
            CurrentResearchRequiredPoints <= 0
                ? 0f
                : Math.Min(1f, CurrentResearchProgress / (float)CurrentResearchRequiredPoints);

        public void SetCatalog(TechnologyCatalog newCatalog)
        {
            if (catalog == newCatalog)
            {
                return;
            }

            catalog = newCatalog;
            if (!string.IsNullOrWhiteSpace(currentResearchTechnologyId)
                && !CanKeepCurrentResearch(currentResearchTechnologyId))
            {
                currentResearchTechnologyId = string.Empty;
            }

            RemoveInvalidQueuedResearch();
            StateChanged?.Invoke(this);
        }

        public bool IsUnlocked(string technologyId)
        {
            var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
            return !string.IsNullOrEmpty(normalizedId) && unlockedTechnologyIds.Contains(normalizedId);
        }

        public bool IsCurrentResearch(TechnologyDefinition definition)
        {
            return definition != null
                   && !string.IsNullOrWhiteSpace(currentResearchTechnologyId)
                   && string.Equals(definition.TechnologyId, currentResearchTechnologyId, StringComparison.Ordinal);
        }

        public bool IsQueuedResearch(TechnologyDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            var technologyId = TechnologySaveData.NormalizeTechnologyId(definition.TechnologyId);
            if (string.IsNullOrEmpty(technologyId))
            {
                return false;
            }

            for (var i = 0; i < researchQueueTechnologyIds.Count; i++)
            {
                if (string.Equals(researchQueueTechnologyIds[i], technologyId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanStartResearch(string technologyId, out TechnologyResearchFailureReason failureReason)
        {
            if (catalog == null || !catalog.TryGetDefinition(technologyId, out var definition))
            {
                failureReason = TechnologyResearchFailureReason.InvalidTechnology;
                return false;
            }

            return CanStartResearch(definition, out failureReason);
        }

        public bool CanStartResearch(TechnologyDefinition definition, out TechnologyResearchFailureReason failureReason)
        {
            if (definition == null || !definition.IsValid)
            {
                failureReason = TechnologyResearchFailureReason.InvalidTechnology;
                return false;
            }

            if (IsUnlocked(definition.TechnologyId) && !definition.AllowRepeatResearch)
            {
                failureReason = TechnologyResearchFailureReason.AlreadyUnlocked;
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
                    failureReason = TechnologyResearchFailureReason.PrerequisitesLocked;
                    return false;
                }
            }

            failureReason = TechnologyResearchFailureReason.None;
            return true;
        }

        public bool TryStartResearch(string technologyId)
        {
            if (catalog == null || !catalog.TryGetDefinition(technologyId, out var definition))
            {
                return false;
            }

            return TryStartResearch(definition);
        }

        public bool TryStartResearch(TechnologyDefinition definition)
        {
            researchQueueTechnologyIds.Clear();
            return TryStartResearchInternal(definition, true);
        }

        public bool TryQueueResearchPath(TechnologyDefinition targetDefinition)
        {
            if (targetDefinition == null || !targetDefinition.IsValid)
            {
                return false;
            }

            var researchPath = new List<TechnologyDefinition>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            CollectResearchPath(targetDefinition, researchPath, visited, true);
            researchPath.Sort(CompareTechnologyDefinitionsByNodeId);
            if (researchPath.Count == 0)
            {
                return false;
            }

            var currentResearchInPath = false;
            researchQueueTechnologyIds.Clear();
            for (var i = 0; i < researchPath.Count; i++)
            {
                var definition = researchPath[i];
                var technologyId = TechnologySaveData.NormalizeTechnologyId(definition.TechnologyId);
                if (string.IsNullOrEmpty(technologyId))
                {
                    continue;
                }

                if (string.Equals(currentResearchTechnologyId, technologyId, StringComparison.Ordinal))
                {
                    currentResearchInPath = true;
                    continue;
                }

                if (!ContainsQueuedTechnology(technologyId))
                {
                    researchQueueTechnologyIds.Add(technologyId);
                }
            }

            if (researchQueueTechnologyIds.Count == 0)
            {
                StateChanged?.Invoke(this);
                return currentResearchInPath || IsCurrentResearch(targetDefinition);
            }

            if (!currentResearchInPath)
            {
                TryStartNextQueuedResearch();
            }
            else
            {
                StateChanged?.Invoke(this);
            }

            return true;
        }

        private bool TryStartResearchInternal(TechnologyDefinition definition, bool notifyWhenAlreadyCurrent)
        {
            if (!CanStartResearch(definition, out _))
            {
                return false;
            }

            var technologyId = TechnologySaveData.NormalizeTechnologyId(definition.TechnologyId);
            if (string.Equals(currentResearchTechnologyId, technologyId, StringComparison.Ordinal))
            {
                if (notifyWhenAlreadyCurrent)
                {
                    StateChanged?.Invoke(this);
                }

                return true;
            }

            if (GetRequiredResearchPoints(definition) <= 0)
            {
                return UnlockForFree(technologyId);
            }

            currentResearchTechnologyId = technologyId;
            EnsureResearchProgress(technologyId);
            CurrentResearchChanged?.Invoke(this);
            StateChanged?.Invoke(this);
            return true;
        }

        private bool TryStartNextQueuedResearch()
        {
            while (researchQueueTechnologyIds.Count > 0)
            {
                var technologyId = researchQueueTechnologyIds[0];
                researchQueueTechnologyIds.RemoveAt(0);

                var definition = GetDefinition(technologyId);
                if (definition == null)
                {
                    continue;
                }

                if (IsUnlocked(technologyId) && !definition.AllowRepeatResearch)
                {
                    continue;
                }

                if (!CanStartResearch(definition, out _))
                {
                    researchQueueTechnologyIds.Insert(0, technologyId);
                    StateChanged?.Invoke(this);
                    return false;
                }

                if (GetRequiredResearchPoints(definition) <= 0)
                {
                    UnlockForFree(technologyId);
                    continue;
                }

                return TryStartResearchInternal(definition, false);
            }

            StateChanged?.Invoke(this);
            return false;
        }

        private void CollectResearchPath(
            TechnologyDefinition definition,
            List<TechnologyDefinition> researchPath,
            HashSet<string> visited,
            bool isTarget)
        {
            if (definition == null || !definition.IsValid)
            {
                return;
            }

            var technologyId = TechnologySaveData.NormalizeTechnologyId(definition.TechnologyId);
            if (string.IsNullOrEmpty(technologyId) || !visited.Add(technologyId))
            {
                return;
            }

            var prerequisites = definition.Prerequisites;
            for (var i = 0; i < prerequisites.Count; i++)
            {
                CollectResearchPath(prerequisites[i], researchPath, visited, false);
            }

            if (IsUnlocked(technologyId) && (!definition.AllowRepeatResearch || !isTarget))
            {
                return;
            }

            researchPath.Add(definition);
        }

        private bool ContainsQueuedTechnology(string technologyId)
        {
            for (var i = 0; i < researchQueueTechnologyIds.Count; i++)
            {
                if (string.Equals(researchQueueTechnologyIds[i], technologyId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public TechnologyResearchAppliedResult ApplyResearchPoints(int amount)
        {
            lastTurnResearchPoints = Math.Max(0, amount);
            var definition = CurrentResearchDefinition;
            if (definition == null || lastTurnResearchPoints <= 0)
            {
                var emptyResult = new TechnologyResearchAppliedResult(
                    definition,
                    lastTurnResearchPoints,
                    CurrentResearchProgress,
                    CurrentResearchProgress,
                    CurrentResearchRequiredPoints,
                    false);
                ResearchProgressApplied?.Invoke(this, emptyResult);
                StateChanged?.Invoke(this);
                return emptyResult;
            }

            var technologyId = definition.TechnologyId.Trim();
            var requiredPoints = GetRequiredResearchPoints(definition);
            var progressBefore = GetResearchProgress(technologyId);
            var progressAfter = Math.Min(requiredPoints, progressBefore + lastTurnResearchPoints);
            var completed = progressAfter >= requiredPoints;

            if (completed)
            {
                researchProgressByTechnologyId.Remove(technologyId);
                var unlockedForFirstTime = unlockedTechnologyIds.Add(technologyId);

                var completedResult = new TechnologyResearchAppliedResult(
                    definition,
                    lastTurnResearchPoints,
                    progressBefore,
                    progressAfter,
                    requiredPoints,
                    true,
                    unlockedForFirstTime);

                ResearchProgressApplied?.Invoke(this, completedResult);
                if (unlockedForFirstTime)
                {
                    TechnologyUnlocked?.Invoke(this, technologyId);
                }

                if (definition.AllowRepeatResearch && researchQueueTechnologyIds.Count == 0)
                {
                    currentResearchTechnologyId = technologyId;
                    EnsureResearchProgress(technologyId);
                }
                else
                {
                    currentResearchTechnologyId = string.Empty;
                    TryStartNextQueuedResearch();
                }

                CurrentResearchChanged?.Invoke(this);
                StateChanged?.Invoke(this);
                return completedResult;
            }
            else
            {
                researchProgressByTechnologyId[technologyId] = progressAfter;
            }

            var result = new TechnologyResearchAppliedResult(
                definition,
                lastTurnResearchPoints,
                progressBefore,
                progressAfter,
                requiredPoints,
                completed);

            ResearchProgressApplied?.Invoke(this, result);
            StateChanged?.Invoke(this);
            return result;
        }

        public bool UnlockForFree(string technologyId)
        {
            var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
            if (string.IsNullOrEmpty(normalizedId) || !unlockedTechnologyIds.Add(normalizedId))
            {
                return false;
            }

            researchProgressByTechnologyId.Remove(normalizedId);
            if (string.Equals(currentResearchTechnologyId, normalizedId, StringComparison.Ordinal))
            {
                currentResearchTechnologyId = string.Empty;
                CurrentResearchChanged?.Invoke(this);
            }

            TechnologyUnlocked?.Invoke(this, normalizedId);
            StateChanged?.Invoke(this);
            return true;
        }

        public int GetResearchProgress(TechnologyDefinition definition)
        {
            return definition == null ? 0 : GetResearchProgress(definition.TechnologyId);
        }

        public int GetResearchProgress(string technologyId)
        {
            var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
            return !string.IsNullOrEmpty(normalizedId)
                   && researchProgressByTechnologyId.TryGetValue(normalizedId, out var progress)
                ? Math.Max(0, progress)
                : 0;
        }

        public TechnologySaveData CaptureSaveData()
        {
            var saveData = new TechnologySaveData
            {
                CurrentResearchTechnologyId = currentResearchTechnologyId,
                CurrentResearchProgress = CurrentResearchProgress,
                LastTurnResearchPoints = lastTurnResearchPoints,
                UnlockedTechnologyIds = new List<string>(unlockedTechnologyIds),
                ResearchQueueTechnologyIds = new List<string>(researchQueueTechnologyIds),
                ResearchProgresses = new List<TechnologyResearchProgressSaveData>()
            };

            saveData.UnlockedTechnologyIds.Sort(StringComparer.Ordinal);
            foreach (var pair in researchProgressByTechnologyId)
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                saveData.ResearchProgresses.Add(
                    new TechnologyResearchProgressSaveData
                    {
                        TechnologyId = pair.Key,
                        Progress = pair.Value
                    });
            }

            saveData.Validate();
            return saveData;
        }

        public void RestoreSaveData(TechnologySaveData saveData, IEnumerable<string> fallbackUnlockedTechnologyIds = null)
        {
            unlockedTechnologyIds.Clear();
            researchProgressByTechnologyId.Clear();
            researchQueueTechnologyIds.Clear();
            currentResearchTechnologyId = string.Empty;
            lastTurnResearchPoints = 0;

            if (saveData != null)
            {
                saveData.Validate();
                lastTurnResearchPoints = saveData.LastTurnResearchPoints;
                AddUnlockedTechnologyIds(saveData.UnlockedTechnologyIds);
                RestoreResearchProgresses(saveData.ResearchProgresses);
                RestoreCurrentResearch(saveData.CurrentResearchTechnologyId, saveData.CurrentResearchProgress);
                RestoreResearchQueue(saveData.ResearchQueueTechnologyIds);
            }
            else
            {
                AddUnlockedTechnologyIds(fallbackUnlockedTechnologyIds);
            }

            if (!HasCurrentResearch)
            {
                TryStartNextQueuedResearch();
            }

            CurrentResearchChanged?.Invoke(this);
            StateChanged?.Invoke(this);
        }

        private void RestoreResearchProgresses(IEnumerable<TechnologyResearchProgressSaveData> progressData)
        {
            if (progressData == null)
            {
                return;
            }

            foreach (var progress in progressData)
            {
                if (progress == null)
                {
                    continue;
                }

                progress.Validate();
                if (!string.IsNullOrWhiteSpace(progress.TechnologyId) && progress.Progress > 0)
                {
                    researchProgressByTechnologyId[progress.TechnologyId] = progress.Progress;
                }
            }
        }

        private void RestoreCurrentResearch(string technologyId, int progress)
        {
            var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
            if (string.IsNullOrEmpty(normalizedId)
                || IsUnlocked(normalizedId) && !IsRepeatableTechnology(normalizedId))
            {
                currentResearchTechnologyId = string.Empty;
                return;
            }

            currentResearchTechnologyId = normalizedId;
            if (progress > 0)
            {
                researchProgressByTechnologyId[normalizedId] = Math.Max(0, progress);
            }
            else
            {
                EnsureResearchProgress(normalizedId);
            }
        }

        private void RestoreResearchQueue(IEnumerable<string> queueTechnologyIds)
        {
            if (queueTechnologyIds == null)
            {
                return;
            }

            foreach (var technologyId in queueTechnologyIds)
            {
                var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
                if (string.IsNullOrEmpty(normalizedId)
                    || ContainsQueuedTechnology(normalizedId)
                    || string.Equals(normalizedId, currentResearchTechnologyId, StringComparison.Ordinal))
                {
                    continue;
                }

                var definition = GetDefinition(normalizedId);
                if (definition == null || IsUnlocked(normalizedId) && !definition.AllowRepeatResearch)
                {
                    continue;
                }

                researchQueueTechnologyIds.Add(normalizedId);
            }
        }

        private void EnsureResearchProgress(string technologyId)
        {
            var normalizedId = TechnologySaveData.NormalizeTechnologyId(technologyId);
            if (!string.IsNullOrEmpty(normalizedId)
                && !researchProgressByTechnologyId.ContainsKey(normalizedId))
            {
                researchProgressByTechnologyId.Add(normalizedId, 0);
            }
        }

        private TechnologyDefinition GetDefinition(string technologyId)
        {
            return catalog != null && catalog.TryGetDefinition(technologyId, out var definition)
                ? definition
                : null;
        }

        private bool CanKeepCurrentResearch(string technologyId)
        {
            var definition = GetDefinition(technologyId);
            return definition != null
                   && (!IsUnlocked(technologyId) || definition.AllowRepeatResearch);
        }

        private bool IsRepeatableTechnology(string technologyId)
        {
            var definition = GetDefinition(technologyId);
            return definition != null && definition.AllowRepeatResearch;
        }

        private void RemoveInvalidQueuedResearch()
        {
            for (var i = researchQueueTechnologyIds.Count - 1; i >= 0; i--)
            {
                var technologyId = researchQueueTechnologyIds[i];
                var definition = GetDefinition(technologyId);
                if (definition == null || IsUnlocked(technologyId) && !definition.AllowRepeatResearch)
                {
                    researchQueueTechnologyIds.RemoveAt(i);
                }
            }
        }

        private static int CompareTechnologyDefinitionsByNodeId(
            TechnologyDefinition left,
            TechnologyDefinition right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var leftId = TechnologySaveData.NormalizeTechnologyId(left.TechnologyId);
            var rightId = TechnologySaveData.NormalizeTechnologyId(right.TechnologyId);
            var leftHasPosition = TechnologyNodeId.TryParse(leftId, out var leftRow, out var leftColumn);
            var rightHasPosition = TechnologyNodeId.TryParse(rightId, out var rightRow, out var rightColumn);
            if (leftHasPosition && rightHasPosition)
            {
                var rowComparison = leftRow.CompareTo(rightRow);
                if (rowComparison != 0)
                {
                    return rowComparison;
                }

                var columnComparison = leftColumn.CompareTo(rightColumn);
                if (columnComparison != 0)
                {
                    return columnComparison;
                }
            }
            else if (leftHasPosition)
            {
                return -1;
            }
            else if (rightHasPosition)
            {
                return 1;
            }

            return string.Compare(leftId, rightId, StringComparison.Ordinal);
        }

        private static int GetRequiredResearchPoints(TechnologyDefinition definition)
        {
            return definition == null ? 0 : Math.Max(0, definition.SciencePointCost);
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
