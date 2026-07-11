using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.Persistence
{
    /// <summary>
    /// 只负责存档文件、ES3 Key 和备份文件的读写，不持有游戏运行时状态。
    /// </summary>
    public sealed class GameSaveRepository
    {
        private const string AppDataKey = "AppData";
        private const string GameDataIndexKey = "GameDataIndex";
        private const string GameDataKey = "GameData";
        private const string GameDataMetaKey = "GameDataMeta";

        private IOManager IO => IOManager.Instance;

        public string SaveRootPath => IO.SaveRootPath;

        public void Initialize()
        {
            IO.Initialize();
            IO.EnsureSaveFolders();
        }

        public AppData LoadAppData()
        {
            IO.EnsureSaveFolders();
            return IO.ES3KeyExists(AppDataKey, IO.AppDataFilePath)
                ? IO.LoadES3<AppData>(AppDataKey, IO.AppDataFilePath)
                : null;
        }

        public void SaveAppData(AppData data)
        {
            IO.EnsureSaveFolders();
            IO.SaveES3(AppDataKey, data, IO.AppDataFilePath);
        }

        public List<GameDataMeta> LoadIndex()
        {
            IO.EnsureSaveFolders();
            if (!IO.ES3KeyExists(GameDataIndexKey, IO.GameDataIndexFilePath))
            {
                return new List<GameDataMeta>();
            }

            return IO.LoadES3<List<GameDataMeta>>(GameDataIndexKey, IO.GameDataIndexFilePath)
                   ?? new List<GameDataMeta>();
        }

        public void SaveIndex(IReadOnlyList<GameDataMeta> entries)
        {
            IO.EnsureSaveFolders();
            var copy = entries == null
                ? new List<GameDataMeta>()
                : new List<GameDataMeta>(entries);
            IO.SaveES3(GameDataIndexKey, copy, IO.GameDataIndexFilePath);
        }

        public bool GameDataExists(string saveGuid)
        {
            return !string.IsNullOrWhiteSpace(saveGuid)
                   && IO.FileExists(IO.GetGameDataFilePath(saveGuid));
        }

        public List<GameDataMeta> ScanSlotMetadata()
        {
            var result = new List<GameDataMeta>();
            if (!IO.DirectoryExists(IO.SlotsFolderPath))
            {
                return result;
            }

            var slotFolders = IO.GetSlotFolderPaths();
            for (var i = 0; i < slotFolders.Length; i++)
            {
                var slotFolder = slotFolders[i];
                var saveGuid = IO.GetFolderName(slotFolder);
                if (string.IsNullOrWhiteSpace(saveGuid))
                {
                    continue;
                }

                var gameDataPath = IO.GetGameDataFilePath(saveGuid);
                if (!IO.FileExists(gameDataPath))
                {
                    continue;
                }

                try
                {
                    GameDataMeta meta = null;
                    if (IO.ES3KeyExists(GameDataKey, gameDataPath))
                    {
                        var gameData = IO.LoadES3<GameData>(GameDataKey, gameDataPath);
                        if (gameData != null)
                        {
                            if (string.IsNullOrWhiteSpace(gameData.SaveGuid))
                            {
                                gameData.SaveGuid = saveGuid;
                            }

                            gameData.Validate();
                            meta = GameDataMeta.CreateFromGameData(gameData);
                        }
                    }
                    else if (IO.ES3KeyExists(GameDataMetaKey, gameDataPath))
                    {
                        meta = IO.LoadES3<GameDataMeta>(GameDataMetaKey, gameDataPath);
                    }

                    if (meta == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(meta.SaveGuid))
                    {
                        meta.SaveGuid = saveGuid;
                    }

                    meta.Validate();
                    result.Add(meta);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"重建存档索引失败：{gameDataPath}\n{exception.Message}");
                }
            }

            return result;
        }

        public void SaveGame(GameData gameData, GameDataMeta meta)
        {
            if (gameData == null || meta == null || string.IsNullOrWhiteSpace(gameData.SaveGuid))
            {
                throw new ArgumentException("GameData and metadata must contain a valid SaveGuid.");
            }

            var saveFolderPath = IO.GetGameSaveFolderPath(gameData.SaveGuid);
            var gameDataPath = IO.GetGameDataFilePath(gameData.SaveGuid);
            IO.EnsureDirectory(saveFolderPath);
            IO.SaveES3(GameDataKey, gameData, gameDataPath);
            IO.SaveES3(GameDataMetaKey, meta, gameDataPath);
        }

        public GameData LoadGame(string saveGuid)
        {
            if (string.IsNullOrWhiteSpace(saveGuid))
            {
                return null;
            }

            var gameDataPath = IO.GetGameDataFilePath(saveGuid);
            if (!IO.ES3KeyExists(GameDataKey, gameDataPath))
            {
                return null;
            }

            return IO.LoadES3<GameData>(GameDataKey, gameDataPath);
        }

        public bool DeleteGame(string saveGuid)
        {
            if (string.IsNullOrWhiteSpace(saveGuid))
            {
                return false;
            }

            var saveFolderPath = IO.GetGameSaveFolderPath(saveGuid);
            if (IO.DirectoryExists(saveFolderPath))
            {
                IO.DeleteDirectory(saveFolderPath, true);
            }

            return true;
        }

        public bool CreateBackup(string saveGuid)
        {
            if (string.IsNullOrWhiteSpace(saveGuid))
            {
                return false;
            }

            var sourcePath = IO.GetGameDataFilePath(saveGuid);
            if (!IO.FileExists(sourcePath))
            {
                return false;
            }

            var backupFolder = IO.GetBackupFolderPath(saveGuid);
            IO.EnsureDirectory(backupFolder);
            var backupFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{IO.GetBackupGameDataFileName()}";
            var targetPath = IO.CombinePath(backupFolder, backupFileName);
            IO.CopyFile(sourcePath, targetPath, false);
            return true;
        }
    }
}
