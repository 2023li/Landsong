using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.ExpeditionSystem;
using Landsong.GridSystem;
using Landsong.InheritanceSystem;
using Landsong.InventorySystem;
using Landsong.PolicySystem;
using Landsong.TalentSystem;
using Landsong.TechnologySystem;
using UnityEngine;

[Serializable]
public class AppData
{
    public int DataVersion = 1;

    public bool IsFirstLaunch = true;

    public string LastGameGuid = string.Empty;

    public LocalizationSaveData Language = new LocalizationSaveData();

    public AudioSaveData Audio = new AudioSaveData();

    public static AppData CreateDefault()
    {
        return new AppData
        {
            DataVersion = 1,
            IsFirstLaunch = true,
            LastGameGuid = string.Empty,
            Language = LocalizationSaveData.CreateDefault(),
            Audio = AudioSaveData.CreateDefault()
        };
    }

    public void Validate()
    {
        if (DataVersion <= 0)
        {
            DataVersion = 1;
        }

        if (LastGameGuid == null)
        {
            LastGameGuid = string.Empty;
        }

        if (Language == null)
        {
            Language = LocalizationSaveData.CreateDefault();
        }

        if (Audio == null)
        {
            Audio = AudioSaveData.CreateDefault();
        }

        Language.Validate();
        Audio.Validate();
    }
}

[Serializable]
public class GameDataMeta
{
    public string SaveGuid = string.Empty;

    public string SaveName = string.Empty;

    public string PlayerName = "Player";

    public string DynastyName = DynastyService.DefaultDynastyName;

    public string MapId = string.Empty;

    public string MapDisplayName = string.Empty;

    public int RoundCount = 0;

    public string Stage = string.Empty;

    public string CurrentSceneName = string.Empty;

    public long CreatedAtUnixTime;

    public long LastSaveUnixTime;

    public float TotalPlayTimeSeconds;

    public int CurrentTurn = 1;

    public int DataVersion = 1;

    public static GameDataMeta CreateFromGameData(GameData gameData)
    {
        if (gameData == null)
        {
            return null;
        }

        return new GameDataMeta
        {
            SaveGuid = gameData.SaveGuid,
            SaveName = gameData.SaveName,
            PlayerName = gameData.PlayerName,
            DynastyName = gameData.DynastyName,
            MapId = gameData.MapId,
            MapDisplayName = gameData.MapDisplayName,
            RoundCount = Mathf.Max(1, gameData.RoundCount > 0 ? gameData.RoundCount : gameData.CurrentTurn),
            Stage = gameData.Stage,

            CreatedAtUnixTime = gameData.CreatedAtUnixTime,
            LastSaveUnixTime = gameData.LastSaveUnixTime,
            TotalPlayTimeSeconds = gameData.TotalPlayTimeSeconds,
            CurrentTurn = gameData.CurrentTurn,
            DataVersion = gameData.DataVersion
        };
    }

    public void Validate()
    {
        if (SaveGuid == null)
        {
            SaveGuid = string.Empty;
        }

        if (SaveName == null)
        {
            SaveName = string.Empty;
        }

        if (PlayerName == null)
        {
            PlayerName = "Player";
        }

        DynastyName = DynastyService.NormalizeDynastyName(DynastyName);

        MapId = string.IsNullOrWhiteSpace(MapId) ? string.Empty : MapId.Trim();
        MapDisplayName = string.IsNullOrWhiteSpace(MapDisplayName) ? MapId : MapDisplayName.Trim();

        if (Stage == null)
        {
            Stage = string.Empty;
        }

        if (CurrentSceneName == null)
        {
            CurrentSceneName = string.Empty;
        }

        if (DataVersion <= 0)
        {
            DataVersion = 1;
        }

        if (CreatedAtUnixTime <= 0)
        {
            CreatedAtUnixTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        if (LastSaveUnixTime < CreatedAtUnixTime)
        {
            LastSaveUnixTime = CreatedAtUnixTime;
        }

        CurrentTurn = Mathf.Max(1, CurrentTurn);
        RoundCount = Mathf.Max(1, RoundCount > 0 ? RoundCount : CurrentTurn);
        if (string.IsNullOrWhiteSpace(SaveName))
        {
            SaveName = GameData.FormatDefaultSaveName(DynastyName, RoundCount);
        }

        TotalPlayTimeSeconds = Mathf.Max(0f, TotalPlayTimeSeconds);
    }

    public DateTime GetLastSaveLocalTime()
    {
        return DateTimeOffset.FromUnixTimeSeconds(LastSaveUnixTime).LocalDateTime;
    }

    public DateTime GetCreatedLocalTime()
    {
        return DateTimeOffset.FromUnixTimeSeconds(CreatedAtUnixTime).LocalDateTime;
    }
}

[Serializable]
public class GameData
{
    public const string DefaultDynastyName = DynastyService.DefaultDynastyName;

    //data 版本号
    public const int CurrentDataVersion = 10;

    public int DataVersion = CurrentDataVersion;

    public string SaveGuid = string.Empty;

    public string SaveName = string.Empty;

    public string PlayerName = "Player";


    //----------------新增字段------------------
    public string DynastyName = DefaultDynastyName;

    public string MapId = string.Empty;

    public string MapDisplayName = string.Empty;

    public bool RequiresInitialMapSetup = true;

    public int RoundCount = 0;

    public string Stage = string.Empty;

    public int BasePopulation = -1;



    public long CreatedAtUnixTime;

    public long LastSaveUnixTime;

    public float TotalPlayTimeSeconds;

    public int CurrentTurn = 1;

    public int WorldSeed;

    public InventorySaveData InventoryData;

    public TechnologySaveData TechnologyData;

    public PolicySaveData PolicyData;

    public Landsong.QuestSaveData QuestData;

    public ExpeditionSaveData ExpeditionData;

    public TalentSaveData TalentData;

    public RoyalInheritanceSaveData RoyalInheritanceData;

    public List<string> UnlockedTechnologies;

    public List<string> UnlockedBuildingBlueprintIds;

    public List<BuildingInstanceSaveData> BuildingInstances;

    public GameSoftData SoftData;

    public static GameData CreateDefault()
    {
        long now = DateTimeOffset.Now.ToUnixTimeSeconds();

        return new GameData
        {
            DataVersion = CurrentDataVersion,
            SaveGuid = string.Empty,
            SaveName = string.Empty,
            PlayerName = "Player",
            DynastyName = DefaultDynastyName,
            MapId = string.Empty,
            MapDisplayName = string.Empty,
            RequiresInitialMapSetup = true,
            RoundCount = 1,
            Stage = string.Empty,
            BasePopulation = -1,
            CreatedAtUnixTime = now,
            LastSaveUnixTime = now,
            TotalPlayTimeSeconds = 0f,
            CurrentTurn = 1,
            WorldSeed = 0,
            InventoryData = null,
            TechnologyData = null,
            PolicyData = null,
            QuestData = null,
            ExpeditionData = null,
            TalentData = null,
            RoyalInheritanceData = null,
            UnlockedTechnologies = null,
            UnlockedBuildingBlueprintIds = null,
            BuildingInstances = null,
            SoftData = GameSoftData.CreateDefault()
        };
    }

    public void Validate()
    {
        if (SaveGuid == null)
        {
            SaveGuid = string.Empty;
        }

        if (SaveName == null)
        {
            SaveName = string.Empty;
        }

        if (PlayerName == null)
        {
            PlayerName = "Player";
        }

        DynastyName = DynastyService.NormalizeDynastyName(DynastyName);

        MapId = string.IsNullOrWhiteSpace(MapId) ? string.Empty : MapId.Trim();
        MapDisplayName = string.IsNullOrWhiteSpace(MapDisplayName) ? MapId : MapDisplayName.Trim();

        if (Stage == null)
        {
            Stage = string.Empty;
        }

        BasePopulation = Mathf.Max(-1, BasePopulation);

        CurrentTurn = Mathf.Max(1, CurrentTurn);
        RoundCount = Mathf.Max(1, RoundCount > 0 ? RoundCount : CurrentTurn);
        if (string.IsNullOrWhiteSpace(SaveName))
        {
            SaveName = FormatDefaultSaveName(DynastyName, CurrentTurn);
        }

        long now = DateTimeOffset.Now.ToUnixTimeSeconds();

        if (CreatedAtUnixTime <= 0)
        {
            CreatedAtUnixTime = now;
        }

        if (LastSaveUnixTime < CreatedAtUnixTime)
        {
            LastSaveUnixTime = CreatedAtUnixTime;
        }

        TotalPlayTimeSeconds = Mathf.Max(0f, TotalPlayTimeSeconds);
        NormalizeUnlockedTechnologies();
        NormalizeUnlockedBuildingBlueprints();
        NormalizeTechnologyData();
        NormalizePolicyData();
        NormalizeQuestData();
        NormalizeExpeditionData();
        NormalizeTalentData();
        NormalizeRoyalInheritanceData();
        SoftData ??= GameSoftData.CreateDefault();
        SoftData.Validate();

        if (BuildingInstances != null)
        {
            for (var i = BuildingInstances.Count - 1; i >= 0; i--)
            {
                var building = BuildingInstances[i];
                if (building == null)
                {
                    BuildingInstances.RemoveAt(i);
                    continue;
                }

                building.Validate();
            }
        }
    }

    public static string FormatDefaultSaveName(string dynastyName, int turnCount)
    {
        dynastyName = DynastyService.NormalizeDynastyName(dynastyName);
        return $"{dynastyName}-{Mathf.Max(1, turnCount)}";
    }

    private void NormalizeUnlockedTechnologies()
    {
        if (UnlockedTechnologies == null)
        {
            return;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = UnlockedTechnologies.Count - 1; i >= 0; i--)
        {
            var technologyId = string.IsNullOrWhiteSpace(UnlockedTechnologies[i])
                ? string.Empty
                : UnlockedTechnologies[i].Trim();
            if (string.IsNullOrWhiteSpace(technologyId) || !seen.Add(technologyId))
            {
                UnlockedTechnologies.RemoveAt(i);
                continue;
            }

            UnlockedTechnologies[i] = technologyId;
        }
    }

    private void NormalizeUnlockedBuildingBlueprints()
    {
        if (UnlockedBuildingBlueprintIds == null)
        {
            return;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = UnlockedBuildingBlueprintIds.Count - 1; i >= 0; i--)
        {
            var buildingId = string.IsNullOrWhiteSpace(UnlockedBuildingBlueprintIds[i])
                ? string.Empty
                : UnlockedBuildingBlueprintIds[i].Trim();
            if (string.IsNullOrWhiteSpace(buildingId) || !seen.Add(buildingId))
            {
                UnlockedBuildingBlueprintIds.RemoveAt(i);
                continue;
            }

            UnlockedBuildingBlueprintIds[i] = buildingId;
        }
    }

    private void NormalizeTechnologyData()
    {
        if (TechnologyData != null)
        {
            TechnologyData.Validate();
            if (UnlockedTechnologies == null)
            {
                UnlockedTechnologies = new List<string>(TechnologyData.UnlockedTechnologyIds);
            }

            return;
        }

        if (UnlockedTechnologies != null)
        {
            TechnologyData = new TechnologySaveData
            {
                UnlockedTechnologyIds = new List<string>(UnlockedTechnologies)
            };
            TechnologyData.Validate();
        }
    }

    private void NormalizePolicyData()
    {
        PolicyData?.Validate();
    }

    private void NormalizeQuestData()
    {
        QuestData?.Validate();
    }

    private void NormalizeExpeditionData()
    {
        ExpeditionData?.Validate();
    }

    private void NormalizeTalentData()
    {
        TalentData?.Validate();
    }

    private void NormalizeRoyalInheritanceData()
    {
        RoyalInheritanceData?.Validate();
    }
}

[Serializable]
public class GameSoftData
{
    public BuildingSoftReferenceSaveData LastSelectedBuilding = new BuildingSoftReferenceSaveData();

    public static GameSoftData CreateDefault()
    {
        return new GameSoftData
        {
            LastSelectedBuilding = new BuildingSoftReferenceSaveData()
        };
    }

    public void Validate()
    {
        LastSelectedBuilding ??= new BuildingSoftReferenceSaveData();
        LastSelectedBuilding.Validate();
    }
}

[Serializable]
public class BuildingSoftReferenceSaveData
{
    public string BuildingId = string.Empty;

    public bool HasOrigin;

    public int OriginX;

    public int OriginY;

    public bool IsEmpty => string.IsNullOrWhiteSpace(BuildingId);

    public GridPosition Origin => new GridPosition(OriginX, OriginY);

    public static BuildingSoftReferenceSaveData CreateFromBuilding(BuildingBase building)
    {
        if (building == null || !building.HasDefinition)
        {
            return new BuildingSoftReferenceSaveData();
        }

        var saveData = new BuildingSoftReferenceSaveData
        {
            BuildingId = building.Definition.BuildingId,
            HasOrigin = building.HasPlacement,
            OriginX = building.HasPlacement ? building.Origin.X : 0,
            OriginY = building.HasPlacement ? building.Origin.Y : 0
        };
        saveData.Validate();
        return saveData;
    }

    public bool Matches(BuildingBase building)
    {
        if (building == null || !building.HasDefinition)
        {
            return false;
        }

        if (!string.Equals(BuildingId, building.Definition.BuildingId, StringComparison.Ordinal))
        {
            return false;
        }

        return !HasOrigin
               || (building.HasPlacement && building.Origin.X == OriginX && building.Origin.Y == OriginY);
    }

    public void Validate()
    {
        BuildingId = string.IsNullOrWhiteSpace(BuildingId) ? string.Empty : BuildingId.Trim();
        if (string.IsNullOrEmpty(BuildingId))
        {
            HasOrigin = false;
            OriginX = 0;
            OriginY = 0;
        }
    }
}

[Serializable]
public class BuildingInstanceSaveData
{
    public string BuildingId = string.Empty;

    public int OriginX;

    public int OriginY;

    public string BuildingStateTypeId = string.Empty;

    public string BuildingStateJson = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(BuildingId);

    public GridPosition Origin => new GridPosition(OriginX, OriginY);

    public static BuildingInstanceSaveData CreateFromBuilding(BuildingBase building)
    {
        if (building == null || !building.HasDefinition || !building.HasPlacement)
        {
            return null;
        }

        return new BuildingInstanceSaveData
        {
            BuildingId = building.Definition.BuildingId,
            OriginX = building.Origin.X,
            OriginY = building.Origin.Y,
            BuildingStateTypeId = string.Empty,
            BuildingStateJson = string.Empty
        };
    }

    public void Validate()
    {
        BuildingId = string.IsNullOrWhiteSpace(BuildingId) ? string.Empty : BuildingId.Trim();
        BuildingStateTypeId = string.IsNullOrWhiteSpace(BuildingStateTypeId) ? string.Empty : BuildingStateTypeId.Trim();
        BuildingStateJson ??= string.Empty;
    }
}

[Serializable]
public class LocalizationSaveData
{
    public bool UseSystemLanguage = true;

    public string CurrentLanguageCode = string.Empty;

    public LanguageSource Source = LanguageSource.BuiltIn;

    public string ExternalPackId = string.Empty;

    public string ExternalPackFileName = string.Empty;

    public static LocalizationSaveData CreateDefault()
    {
        return new LocalizationSaveData
        {
            UseSystemLanguage = true,
            CurrentLanguageCode = string.Empty,
            Source = LanguageSource.BuiltIn,
            ExternalPackId = string.Empty,
            ExternalPackFileName = string.Empty
        };
    }

    public void Validate()
    {
        if (CurrentLanguageCode == null)
        {
            CurrentLanguageCode = string.Empty;
        }

        if (ExternalPackId == null)
        {
            ExternalPackId = string.Empty;
        }

        if (ExternalPackFileName == null)
        {
            ExternalPackFileName = string.Empty;
        }

        if (Source == LanguageSource.BuiltIn)
        {
            ExternalPackId = string.Empty;
            ExternalPackFileName = string.Empty;
        }
    }
}

[Serializable]
public class AudioSaveData
{
    public const string MasterVolumeGroupKey = "master";
    public const string MusicVolumeGroupKey = "music";
    public const string LegacyBgmVolumeGroupKey = "bgm";
    public const string AmbienceVolumeGroupKey = "ambience";
    public const string SfxVolumeGroupKey = "sfx";

    public float MasterVolume = 1f;

    public float BgmVolume = 1f;

    public float AmbienceVolume = 1f;

    public float SfxVolume = 1f;

    public bool IsMuted;

    public List<AudioKeyVolumeSaveData> VolumeGroups = new List<AudioKeyVolumeSaveData>();

    public List<AudioKeyVolumeSaveData> ChannelVolumes = new List<AudioKeyVolumeSaveData>();

    public static AudioSaveData CreateDefault()
    {
        return new AudioSaveData
        {
            MasterVolume = 1f,
            BgmVolume = 1f,
            AmbienceVolume = 1f,
            SfxVolume = 1f,
            IsMuted = false,
            VolumeGroups = new List<AudioKeyVolumeSaveData>(),
            ChannelVolumes = new List<AudioKeyVolumeSaveData>()
        };
    }

    public void Validate()
    {
        MasterVolume = Mathf.Clamp01(MasterVolume);
        BgmVolume = Mathf.Clamp01(BgmVolume);
        AmbienceVolume = Mathf.Clamp01(AmbienceVolume);
        SfxVolume = Mathf.Clamp01(SfxVolume);
        VolumeGroups ??= new List<AudioKeyVolumeSaveData>();
        ChannelVolumes ??= new List<AudioKeyVolumeSaveData>();
        NormalizeVolumeList(VolumeGroups);
        NormalizeVolumeList(ChannelVolumes);
    }

    public float GetVolumeGroup(string volumeGroupKey)
    {
        string key = NormalizeAudioKey(volumeGroupKey);
        switch (key)
        {
            case MasterVolumeGroupKey:
                return Mathf.Clamp01(MasterVolume);
            case MusicVolumeGroupKey:
            case LegacyBgmVolumeGroupKey:
                return Mathf.Clamp01(BgmVolume);
            case AmbienceVolumeGroupKey:
                return Mathf.Clamp01(AmbienceVolume);
            case SfxVolumeGroupKey:
                return Mathf.Clamp01(SfxVolume);
        }

        return TryGetVolume(VolumeGroups, key, out float volume) ? volume : 1f;
    }

    public void SetVolumeGroup(string volumeGroupKey, float volume)
    {
        string key = NormalizeAudioKey(volumeGroupKey);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        volume = Mathf.Clamp01(volume);
        switch (key)
        {
            case MasterVolumeGroupKey:
                MasterVolume = volume;
                return;
            case MusicVolumeGroupKey:
            case LegacyBgmVolumeGroupKey:
                BgmVolume = volume;
                return;
            case AmbienceVolumeGroupKey:
                AmbienceVolume = volume;
                return;
            case SfxVolumeGroupKey:
                SfxVolume = volume;
                return;
        }

        VolumeGroups ??= new List<AudioKeyVolumeSaveData>();
        SetVolume(VolumeGroups, key, volume);
    }

    public float GetChannelVolume(string channelKey)
    {
        string key = NormalizeAudioKey(channelKey);
        return TryGetVolume(ChannelVolumes, key, out float volume) ? volume : 1f;
    }

    public void SetChannelVolume(string channelKey, float volume)
    {
        string key = NormalizeAudioKey(channelKey);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        ChannelVolumes ??= new List<AudioKeyVolumeSaveData>();
        SetVolume(ChannelVolumes, key, volume);
    }

    public static string NormalizeAudioKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();
    }

    private static bool TryGetVolume(List<AudioKeyVolumeSaveData> volumes, string key, out float volume)
    {
        if (volumes != null)
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                AudioKeyVolumeSaveData item = volumes[i];
                if (item != null && item.Key == key)
                {
                    volume = Mathf.Clamp01(item.Volume);
                    return true;
                }
            }
        }

        volume = 1f;
        return false;
    }

    private static void SetVolume(List<AudioKeyVolumeSaveData> volumes, string key, float volume)
    {
        if (volumes == null)
        {
            return;
        }

        volume = Mathf.Clamp01(volume);
        for (int i = 0; i < volumes.Count; i++)
        {
            AudioKeyVolumeSaveData item = volumes[i];
            if (item != null && item.Key == key)
            {
                item.Volume = volume;
                item.Validate();
                return;
            }
        }

        volumes.Add(new AudioKeyVolumeSaveData
        {
            Key = key,
            Volume = volume
        });
    }

    private static void NormalizeVolumeList(List<AudioKeyVolumeSaveData> volumes)
    {
        if (volumes == null)
        {
            return;
        }

        for (int i = volumes.Count - 1; i >= 0; i--)
        {
            AudioKeyVolumeSaveData item = volumes[i];
            if (item == null)
            {
                volumes.RemoveAt(i);
                continue;
            }

            item.Validate();
            if (string.IsNullOrEmpty(item.Key))
            {
                volumes.RemoveAt(i);
            }
        }
    }
}

[Serializable]
public class AudioKeyVolumeSaveData
{
    public string Key = string.Empty;

    public float Volume = 1f;

    public void Validate()
    {
        Key = AudioSaveData.NormalizeAudioKey(Key);
        Volume = Mathf.Clamp01(Volume);
    }
}
