using System;
using System.Collections.Generic;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 建筑蓝图解锁状态的唯一运行时服务。
    /// </summary>
    public sealed class BuildingBlueprintService
    {
        private readonly HashSet<string> unlockedIds = new HashSet<string>(StringComparer.Ordinal);

        public BuildingBlueprintService(IEnumerable<string> startingUnlockedIds = null)
        {
            RestoreSaveData(startingUnlockedIds);
        }

        public event Action<BuildingBlueprintService> StateChanged;

        public IReadOnlyCollection<string> UnlockedIds => unlockedIds;

        public bool IsUnlocked(string buildingId)
        {
            buildingId = NormalizeId(buildingId);
            return !string.IsNullOrEmpty(buildingId) && unlockedIds.Contains(buildingId);
        }

        public bool Unlock(string buildingId)
        {
            buildingId = NormalizeId(buildingId);
            if (string.IsNullOrEmpty(buildingId) || !unlockedIds.Add(buildingId))
            {
                return false;
            }

            StateChanged?.Invoke(this);
            return true;
        }

        public List<string> CaptureSaveData()
        {
            var result = new List<string>(unlockedIds);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public void RestoreSaveData(IEnumerable<string> buildingIds)
        {
            unlockedIds.Clear();
            if (buildingIds != null)
            {
                foreach (var buildingId in buildingIds)
                {
                    var normalizedId = NormalizeId(buildingId);
                    if (!string.IsNullOrEmpty(normalizedId))
                    {
                        unlockedIds.Add(normalizedId);
                    }
                }
            }

            StateChanged?.Invoke(this);
        }

        public static string NormalizeId(string buildingId)
        {
            return string.IsNullOrWhiteSpace(buildingId) ? string.Empty : buildingId.Trim();
        }
    }
}
