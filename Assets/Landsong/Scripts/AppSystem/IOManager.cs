using System;
using System.IO;
using System.Text;
using Moyo.Unity;
using UnityEngine;

/// <summary>
/// 统一管理本地文件路径、文件夹、ES3读写、基础文件操作。
/// DataManager 只负责数据业务，GameLocalizationManager 只负责本地化业务。
/// </summary>
public class IOManager : MonoSingleton<IOManager>
{
    private const string SaveRootFolderName = "Landsong_Save";

    private const string SystemFolderName = "System";
    private const string SlotsFolderName = "Slots";
    private const string BackupsFolderName = "Backups";
    private const string ExternalLanguagePacksFolderName = "ExternalLanguagePacks";

    private const string AppDataFileName = "app.es3";
    private const string SaveIndexFileName = "save_index.es3";
    private const string GameDataFileName = "game.es3";

    public string SaveRootPath => Path.Combine(Application.persistentDataPath, SaveRootFolderName);

    public string SystemFolderPath => Path.Combine(SaveRootPath, SystemFolderName);

    public string SlotsFolderPath => Path.Combine(SaveRootPath, SlotsFolderName);

    public string BackupsFolderPath => Path.Combine(SaveRootPath, BackupsFolderName);

    public string ExternalLanguagePacksFolderPath => Path.Combine(SaveRootPath, ExternalLanguagePacksFolderName);

    public string AppDataFilePath => Path.Combine(SystemFolderPath, AppDataFileName);

    public string GameDataIndexFilePath => Path.Combine(SystemFolderPath, SaveIndexFileName);

    protected override void Init()
    {
        Initialize();
    }

    public void Initialize()
    {
        EnsureSaveFolders();
        EnsureExternalLanguagePackFolder();
    }

    public void EnsureSaveFolders()
    {
        EnsureDirectory(SaveRootPath);
        EnsureDirectory(SystemFolderPath);
        EnsureDirectory(SlotsFolderPath);
        EnsureDirectory(BackupsFolderPath);
    }

    public void EnsureExternalLanguagePackFolder()
    {
        EnsureSaveFolders();
        EnsureDirectory(ExternalLanguagePacksFolderPath);
    }

    public void EnsureDirectory(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        Directory.CreateDirectory(folderPath);
    }

    public string GetGameSaveFolderPath(string saveGuid)
    {
        return Path.Combine(SlotsFolderPath, saveGuid ?? string.Empty);
    }

    public string GetGameDataFilePath(string saveGuid)
    {
        return Path.Combine(GetGameSaveFolderPath(saveGuid), GameDataFileName);
    }

    public string GetBackupFolderPath(string saveGuid)
    {
        return Path.Combine(BackupsFolderPath, saveGuid ?? string.Empty);
    }

    public string GetBackupGameDataFileName()
    {
        return GameDataFileName;
    }

    public string CombinePath(string left, string right)
    {
        return Path.Combine(left ?? string.Empty, right ?? string.Empty);
    }

    public string GetExternalLanguagePackFullPath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return Path.Combine(ExternalLanguagePacksFolderPath, Path.GetFileName(fileName));
    }

    public string GetSafeFileName(string fileNameOrPath)
    {
        if (string.IsNullOrWhiteSpace(fileNameOrPath))
        {
            return string.Empty;
        }

        return Path.GetFileName(fileNameOrPath);
    }

    public string GetFileNameWithoutExtension(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return string.Empty;
        }

        return Path.GetFileNameWithoutExtension(filePath);
    }

    public string GetFolderName(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return string.Empty;
        }

        string trimmedPath = folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFileName(trimmedPath);
    }

    public bool FileExists(string filePath)
    {
        return !string.IsNullOrEmpty(filePath) && File.Exists(filePath);
    }

    public bool DirectoryExists(string folderPath)
    {
        return !string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath);
    }

    public string[] GetSlotFolderPaths()
    {
        EnsureSaveFolders();

        if (!DirectoryExists(SlotsFolderPath))
        {
            return Array.Empty<string>();
        }

        return Directory.GetDirectories(SlotsFolderPath);
    }

    public string[] GetExternalLanguagePackCsvFiles()
    {
        EnsureExternalLanguagePackFolder();

        if (!DirectoryExists(ExternalLanguagePacksFolderPath))
        {
            return Array.Empty<string>();
        }

        return Directory.GetFiles(ExternalLanguagePacksFolderPath, "*.csv", SearchOption.TopDirectoryOnly);
    }

    public string[] ReadAllLines(string filePath)
    {
        if (!FileExists(filePath))
        {
            return Array.Empty<string>();
        }

        return File.ReadAllLines(filePath, Encoding.UTF8);
    }

    public bool TryReadAllLines(string filePath, out string[] lines, out Exception exception)
    {
        lines = Array.Empty<string>();
        exception = null;

        try
        {
            lines = ReadAllLines(filePath);
            return true;
        }
        catch (Exception e)
        {
            exception = e;
            return false;
        }
    }

    public bool DeleteDirectory(string folderPath, bool recursive)
    {
        if (!DirectoryExists(folderPath))
        {
            return true;
        }

        Directory.Delete(folderPath, recursive);
        return true;
    }

    public void CopyFile(string sourcePath, string targetPath, bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetPath))
        {
            return;
        }

        string targetFolder = Path.GetDirectoryName(targetPath);
        EnsureDirectory(targetFolder);

        File.Copy(sourcePath, targetPath, overwrite);
    }

    public bool ES3KeyExists(string key, string filePath)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        return ES3.KeyExists(key, filePath);
    }

    public T LoadES3<T>(string key, string filePath)
    {
        return ES3.Load<T>(key, filePath);
    }

    public void SaveES3<T>(string key, T value, string filePath)
    {
        string folderPath = Path.GetDirectoryName(filePath);
        EnsureDirectory(folderPath);

        ES3.Save(key, value, filePath);
    }
}
