using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.Persistence
{
    /// <summary>
    /// 管理存档索引的去重、校验和排序。文件访问统一委托给 GameSaveRepository。
    /// </summary>
    public sealed class GameSaveIndexService
    {
        private readonly GameSaveRepository repository;
        private readonly List<GameDataMeta> entries;

        public GameSaveIndexService(GameSaveRepository repository, List<GameDataMeta> entries)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
            this.entries = entries ?? throw new ArgumentNullException(nameof(entries));
        }

        public IReadOnlyList<GameDataMeta> Entries => entries;

        public bool Reload(AppData appData)
        {
            entries.Clear();
            try
            {
                entries.AddRange(repository.LoadIndex());
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"读取存档索引失败，将从 Slots 文件夹重建。\n{exception.Message}");
            }

            var scannedEntries = repository.ScanSlotMetadata();
            for (var i = 0; i < scannedEntries.Count; i++)
            {
                AddOrReplace(scannedEntries[i]);
            }

            var appDataChanged = Validate(appData);
            repository.SaveIndex(entries);
            return appDataChanged;
        }

        public bool Save(AppData appData)
        {
            var appDataChanged = Validate(appData);
            repository.SaveIndex(entries);
            return appDataChanged;
        }

        public void AddOrReplace(GameDataMeta meta)
        {
            if (meta == null || string.IsNullOrWhiteSpace(meta.SaveGuid))
            {
                return;
            }

            meta.Validate();
            var index = entries.FindIndex(candidate => candidate != null && candidate.SaveGuid == meta.SaveGuid);
            if (index >= 0)
            {
                entries[index] = meta;
            }
            else
            {
                entries.Add(meta);
            }
        }

        public bool Remove(string saveGuid)
        {
            return !string.IsNullOrWhiteSpace(saveGuid)
                   && entries.RemoveAll(meta => meta != null && meta.SaveGuid == saveGuid) > 0;
        }

        public GameDataMeta GetLast(AppData appData)
        {
            Validate(appData);
            if (appData != null && !string.IsNullOrWhiteSpace(appData.LastGameGuid))
            {
                var selected = entries.Find(meta => meta != null && meta.SaveGuid == appData.LastGameGuid);
                if (selected != null)
                {
                    return selected;
                }
            }

            return entries.Count == 0 ? null : entries[0];
        }

        private bool Validate(AppData appData)
        {
            var uniqueByGuid = new Dictionary<string, GameDataMeta>(StringComparer.Ordinal);
            for (var i = 0; i < entries.Count; i++)
            {
                var meta = entries[i];
                if (meta == null || string.IsNullOrWhiteSpace(meta.SaveGuid))
                {
                    continue;
                }

                meta.Validate();
                if (!repository.GameDataExists(meta.SaveGuid))
                {
                    continue;
                }

                if (!uniqueByGuid.TryGetValue(meta.SaveGuid, out var existing)
                    || meta.LastSaveUnixTime > existing.LastSaveUnixTime)
                {
                    uniqueByGuid[meta.SaveGuid] = meta;
                }
            }

            entries.Clear();
            entries.AddRange(uniqueByGuid.Values);
            entries.Sort((left, right) => right.LastSaveUnixTime.CompareTo(left.LastSaveUnixTime));

            if (appData == null || string.IsNullOrWhiteSpace(appData.LastGameGuid))
            {
                return false;
            }

            var lastSaveExists = entries.Exists(meta => meta != null && meta.SaveGuid == appData.LastGameGuid);
            if (lastSaveExists)
            {
                return false;
            }

            appData.LastGameGuid = string.Empty;
            return true;
        }
    }
}
