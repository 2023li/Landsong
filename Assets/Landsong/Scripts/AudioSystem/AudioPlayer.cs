using System;
using System.Collections;
using System.Collections.Generic;
using Moyo.Unity;
using UnityEngine;

namespace Landsong.AudioSystem
{
    public enum AudioChannelMode
    {
        LoopSingle = 0,
        OneShotPool = 1
    }

    [Serializable]
    public sealed class AudioChannelDefinition
    {
        [SerializeField] private string key = AudioPlayer.DefaultSfxChannelKey;
        [SerializeField] private AudioChannelMode mode = AudioChannelMode.OneShotPool;
        [SerializeField] private string volumeGroupKey = AudioPlayer.SfxVolumeGroupKey;
        [SerializeField, Min(1)] private int initialSourceCount = 4;
        [SerializeField, Min(1)] private int maxSourceCount = 16;
        [SerializeField, Min(0f)] private float defaultFadeDuration = 0.5f;

        public string Key => AudioPlayer.NormalizeKey(key, AudioPlayer.DefaultSfxChannelKey);
        public AudioChannelMode Mode => mode;
        public string VolumeGroupKey => AudioPlayer.NormalizeKey(volumeGroupKey, AudioPlayer.SfxVolumeGroupKey);
        public int InitialSourceCount => Mathf.Max(1, initialSourceCount);
        public int MaxSourceCount => Mathf.Max(InitialSourceCount, maxSourceCount);
        public float DefaultFadeDuration => Mathf.Max(0f, defaultFadeDuration);

        public AudioChannelDefinition()
        {
        }

        public AudioChannelDefinition(
            string key,
            AudioChannelMode mode,
            string volumeGroupKey,
            int initialSourceCount,
            int maxSourceCount,
            float defaultFadeDuration)
        {
            this.key = key;
            this.mode = mode;
            this.volumeGroupKey = volumeGroupKey;
            this.initialSourceCount = Mathf.Max(1, initialSourceCount);
            this.maxSourceCount = Mathf.Max(this.initialSourceCount, maxSourceCount);
            this.defaultFadeDuration = Mathf.Max(0f, defaultFadeDuration);
        }
    }

    [Serializable]
    public sealed class AudioCueDefinition
    {
        [SerializeField] private string key;
        [SerializeField] private AudioClip clip;
        [SerializeField] private AudioClip[] variants;
        [SerializeField, Range(0f, 1f)] private float volumeScale = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend;
        [SerializeField] private Vector2 pitchRange = Vector2.one;

        public string Key => AudioPlayer.NormalizeKey(key, string.Empty);
        public float VolumeScale => Mathf.Clamp01(volumeScale);
        public float SpatialBlend => Mathf.Clamp01(spatialBlend);

        public bool TryPickClip(out AudioClip pickedClip)
        {
            if (variants != null && variants.Length > 0)
            {
                int validCount = 0;
                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i] != null)
                    {
                        validCount++;
                    }
                }

                if (validCount > 0)
                {
                    int targetIndex = UnityEngine.Random.Range(0, validCount);
                    for (int i = 0; i < variants.Length; i++)
                    {
                        if (variants[i] == null)
                        {
                            continue;
                        }

                        if (targetIndex == 0)
                        {
                            pickedClip = variants[i];
                            return true;
                        }

                        targetIndex--;
                    }
                }
            }

            pickedClip = clip;
            return pickedClip != null;
        }

        public float PickPitch()
        {
            float minPitch = Mathf.Max(0.01f, Mathf.Min(pitchRange.x, pitchRange.y));
            float maxPitch = Mathf.Max(minPitch, Mathf.Max(pitchRange.x, pitchRange.y));
            return Mathf.Approximately(minPitch, maxPitch)
                ? minPitch
                : UnityEngine.Random.Range(minPitch, maxPitch);
        }
    }

    [DisallowMultipleComponent]
    public sealed class AudioPlayer : MonoSingleton<AudioPlayer>
    {
        public const string MasterVolumeGroupKey = "master";
        public const string MusicVolumeGroupKey = "music";
        public const string AmbienceVolumeGroupKey = "ambience";
        public const string SfxVolumeGroupKey = "sfx";

        public const string DefaultBgmChannelKey = "bgm.main";
        public const string DefaultAmbienceChannelKey = "ambience.environment";
        public const string DefaultSfxChannelKey = "sfx.default";
        public const string DefaultUiSfxChannelKey = "sfx.ui";

        [Header("Legacy Defaults")]
        [SerializeField] private AudioClip defaultBgmClip;
        [SerializeField] private bool playDefaultBgmOnInitialize = true;
        [SerializeField, Min(0f)] private float defaultBgmFadeDuration = 0.5f;
        [SerializeField] private AudioClip defaultUiClickSound;
        [SerializeField] private AudioClip defaultUiBackSound;

        [Header("Channels")]
        [SerializeField] private List<AudioChannelDefinition> channels = new List<AudioChannelDefinition>();

        [Header("Cues")]
        [SerializeField] private List<AudioCueDefinition> cues = new List<AudioCueDefinition>();

        [Header("Fade")]
        [SerializeField] private bool useUnscaledTimeForFades = true;

        [Header("Debug")]
        [SerializeField] private bool warnMissingKeys = true;

        private readonly Dictionary<string, AudioChannelRuntime> channelRuntimes =
            new Dictionary<string, AudioChannelRuntime>(StringComparer.Ordinal);

        private readonly Dictionary<string, AudioCueDefinition> cueLookup =
            new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);

        private DataManager subscribedDataManager;
        private AudioSaveData audioData = AudioSaveData.CreateDefault();

        public event Action<float, float, bool> SettingsChanged;
        public event Action<float, float, float, bool> ChannelSettingsChanged;
        public event Action<AudioSaveData> AudioSettingsChanged;

        public AudioClip CurrentBgmClip => GetCurrentClip(DefaultBgmChannelKey);
        public AudioClip CurrentAmbienceClip => GetCurrentClip(DefaultAmbienceChannelKey);
        public AudioClip DefaultUiClickSound => defaultUiClickSound;
        public AudioClip DefaultUiBackSound => defaultUiBackSound;
        public bool IsBgmPlaying => IsPlaying(DefaultBgmChannelKey);
        public bool IsAmbiencePlaying => IsPlaying(DefaultAmbienceChannelKey);
        public float MasterVolume => audioData.MasterVolume;
        public float BgmVolume => audioData.BgmVolume;
        public float AmbienceVolume => audioData.AmbienceVolume;
        public float SfxVolume => audioData.SfxVolume;
        public bool IsMuted => audioData.IsMuted;

        protected override void Init()
        {
            Initialize();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (Application.isPlaying)
            {
                _ = Instance;
            }
        }

        public void Initialize()
        {
            EnsureDefaultDefinitions();
            RebuildCueLookup();
            EnsureChannels();
            SubscribeDataManager();
            SyncFromDataManager();

            if (playDefaultBgmOnInitialize && defaultBgmClip != null && CurrentBgmClip == null)
            {
                PlayBgm(defaultBgmClip, false, defaultBgmFadeDuration);
            }
        }

        public void SetMasterVolume(float volume)
        {
            DataManager.Instance.SetAudioMasterVolume(volume);
        }

        public void SetBgmVolume(float volume)
        {
            DataManager.Instance.SetBgmVolume(volume);
        }

        public void SetAmbienceVolume(float volume)
        {
            DataManager.Instance.SetAmbienceVolume(volume);
        }

        public void SetSfxVolume(float volume)
        {
            DataManager.Instance.SetSfxVolume(volume);
        }

        public void SetVolumeGroup(string volumeGroupKey, float volume)
        {
            DataManager.Instance.SetAudioVolumeGroup(volumeGroupKey, volume);
        }

        public void SetChannelVolume(string channelKey, float volume)
        {
            DataManager.Instance.SetAudioChannelVolume(channelKey, volume);
        }

        public void SetMuted(bool muted)
        {
            DataManager.Instance.SetMuted(muted);
        }

        public bool Play(string channelKey, string cueKey, bool restart = false, float fadeDuration = -1f)
        {
            if (!TryGetCue(cueKey, out AudioCueDefinition cue) || !cue.TryPickClip(out AudioClip clip))
            {
                WarnMissingCue(cueKey);
                return false;
            }

            return PlayClip(channelKey, clip, restart, fadeDuration, cue.VolumeScale, cue.SpatialBlend, cue.PickPitch());
        }

        public bool PlayClip(
            string channelKey,
            AudioClip clip,
            bool restart = false,
            float fadeDuration = -1f,
            float volumeScale = 1f,
            float spatialBlend = 0f,
            float pitch = 1f)
        {
            EnsureChannels();

            if (clip == null)
            {
                Stop(channelKey, fadeDuration);
                return false;
            }

            if (!TryGetRuntime(channelKey, out AudioChannelRuntime runtime))
            {
                WarnMissingChannel(channelKey);
                return false;
            }

            if (runtime.Definition.Mode == AudioChannelMode.OneShotPool)
            {
                return PlayOneShotClip(channelKey, clip, volumeScale, transform.position, spatialBlend, pitch);
            }

            if (!restart && runtime.SingleSource != null && runtime.SingleSource.clip == clip && runtime.SingleSource.isPlaying)
            {
                runtime.CurrentVolumeScale = Mathf.Clamp01(volumeScale);
                runtime.CurrentSpatialBlend = Mathf.Clamp01(spatialBlend);
                runtime.CurrentPitch = Mathf.Max(0.01f, pitch);
                ApplyVolumeToRuntime(runtime);
                return true;
            }

            float resolvedFadeDuration = ResolveFadeDuration(runtime, fadeDuration);
            StopFadeRoutine(runtime);

            if (resolvedFadeDuration > 0f && isActiveAndEnabled)
            {
                runtime.FadeRoutine = StartCoroutine(SwitchSingleRoutine(
                    runtime,
                    clip,
                    Mathf.Clamp01(volumeScale),
                    Mathf.Clamp01(spatialBlend),
                    Mathf.Max(0.01f, pitch),
                    resolvedFadeDuration));
                return true;
            }

            StartSingleImmediate(
                runtime,
                clip,
                Mathf.Clamp01(volumeScale),
                Mathf.Clamp01(spatialBlend),
                Mathf.Max(0.01f, pitch));
            return true;
        }

        public bool PlayOneShot(string channelKey, string cueKey, float volumeScale = 1f)
        {
            return PlayOneShotAtPoint(channelKey, cueKey, transform.position, volumeScale);
        }

        public bool PlayOneShotAtPoint(string channelKey, string cueKey, Vector3 position, float volumeScale = 1f)
        {
            if (!TryGetCue(cueKey, out AudioCueDefinition cue) || !cue.TryPickClip(out AudioClip clip))
            {
                WarnMissingCue(cueKey);
                return false;
            }

            return PlayOneShotClip(
                channelKey,
                clip,
                Mathf.Clamp01(volumeScale) * cue.VolumeScale,
                position,
                cue.SpatialBlend,
                cue.PickPitch());
        }

        public bool PlayOneShotClip(
            string channelKey,
            AudioClip clip,
            float volumeScale = 1f,
            Vector3 position = default,
            float spatialBlend = 0f,
            float pitch = 1f)
        {
            EnsureChannels();

            if (clip == null || audioData.IsMuted)
            {
                return false;
            }

            if (!TryGetRuntime(channelKey, out AudioChannelRuntime runtime))
            {
                WarnMissingChannel(channelKey);
                return false;
            }

            AudioSource source = runtime.GetAvailablePooledSource();
            if (source == null)
            {
                return false;
            }

            ConfigureBaseSource(source);
            source.transform.position = position;
            source.loop = false;
            source.clip = null;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.pitch = Mathf.Max(0.01f, pitch);
            source.volume = GetEffectiveVolume(runtime, Mathf.Clamp01(volumeScale));
            source.mute = audioData.IsMuted;

            if (source.volume <= 0f)
            {
                return false;
            }

            source.PlayOneShot(clip, 1f);
            return true;
        }

        public void Stop(string channelKey, float fadeDuration = -1f)
        {
            EnsureChannels();

            if (!TryGetRuntime(channelKey, out AudioChannelRuntime runtime))
            {
                WarnMissingChannel(channelKey);
                return;
            }

            if (runtime.Definition.Mode == AudioChannelMode.OneShotPool)
            {
                runtime.StopPooledSources();
                return;
            }

            StopFadeRoutine(runtime);
            if (runtime.SingleSource == null)
            {
                return;
            }

            float resolvedFadeDuration = ResolveFadeDuration(runtime, fadeDuration);
            if (resolvedFadeDuration > 0f && isActiveAndEnabled && runtime.SingleSource.isPlaying)
            {
                runtime.FadeRoutine = StartCoroutine(StopSingleRoutine(runtime, resolvedFadeDuration));
                return;
            }

            runtime.SingleSource.Stop();
            runtime.SingleSource.clip = null;
            ApplyVolumeToRuntime(runtime);
        }

        public void Pause(string channelKey)
        {
            if (TryGetRuntime(channelKey, out AudioChannelRuntime runtime) && runtime.SingleSource != null)
            {
                runtime.SingleSource.Pause();
            }
        }

        public void Resume(string channelKey)
        {
            if (TryGetRuntime(channelKey, out AudioChannelRuntime runtime)
                && runtime.SingleSource != null
                && runtime.SingleSource.clip != null
                && !runtime.SingleSource.isPlaying)
            {
                runtime.SingleSource.UnPause();
            }
        }

        public bool IsPlaying(string channelKey)
        {
            return TryGetRuntime(channelKey, out AudioChannelRuntime runtime)
                   && runtime.SingleSource != null
                   && runtime.SingleSource.isPlaying;
        }

        public AudioClip GetCurrentClip(string channelKey)
        {
            return TryGetRuntime(channelKey, out AudioChannelRuntime runtime) && runtime.SingleSource != null
                ? runtime.SingleSource.clip
                : null;
        }

        public void PlayDefaultBgm(bool restart = false)
        {
            PlayBgm(defaultBgmClip, restart, defaultBgmFadeDuration);
        }

        public void PlayBgm(AudioClip clip, bool restart = false, float fadeDuration = -1f)
        {
            PlayClip(DefaultBgmChannelKey, clip, restart, fadeDuration);
        }

        public bool PlayBgm(string cueKey, bool restart = false, float fadeDuration = -1f)
        {
            return Play(DefaultBgmChannelKey, cueKey, restart, fadeDuration);
        }

        public void StopBgm(float fadeDuration = -1f)
        {
            Stop(DefaultBgmChannelKey, fadeDuration);
        }

        public void PauseBgm()
        {
            Pause(DefaultBgmChannelKey);
        }

        public void ResumeBgm()
        {
            Resume(DefaultBgmChannelKey);
        }

        public void PlayAmbience(AudioClip clip, bool restart = false, float fadeDuration = -1f)
        {
            PlayClip(DefaultAmbienceChannelKey, clip, restart, fadeDuration);
        }

        public bool PlayAmbience(string cueKey, bool restart = false, float fadeDuration = -1f)
        {
            return Play(DefaultAmbienceChannelKey, cueKey, restart, fadeDuration);
        }

        public void StopAmbience(float fadeDuration = -1f)
        {
            Stop(DefaultAmbienceChannelKey, fadeDuration);
        }

        public bool PlayUiClick(float volumeScale = 1f)
        {
            return PlayOneShotClip(DefaultUiSfxChannelKey, defaultUiClickSound, volumeScale);
        }

        public bool PlayUiBack(float volumeScale = 1f)
        {
            AudioClip clip = defaultUiBackSound != null ? defaultUiBackSound : defaultUiClickSound;
            return PlayOneShotClip(DefaultUiSfxChannelKey, clip, volumeScale);
        }

        public bool PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            return PlayOneShotClip(DefaultSfxChannelKey, clip, volumeScale);
        }

        public bool PlaySfx(string cueKey, float volumeScale = 1f)
        {
            return PlayOneShot(DefaultSfxChannelKey, cueKey, volumeScale);
        }

        public bool PlaySfxAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1f, float spatialBlend = 0f)
        {
            return PlayOneShotClip(DefaultSfxChannelKey, clip, volumeScale, position, spatialBlend);
        }

        public bool PlaySfxAtPoint(string cueKey, Vector3 position, float volumeScale = 1f)
        {
            return PlayOneShotAtPoint(DefaultSfxChannelKey, cueKey, position, volumeScale);
        }

        public float GetVolumeGroup(string volumeGroupKey)
        {
            return audioData.GetVolumeGroup(volumeGroupKey);
        }

        public float GetChannelVolume(string channelKey)
        {
            return audioData.GetChannelVolume(channelKey);
        }

        internal static string NormalizeKey(string key, string fallback)
        {
            return string.IsNullOrWhiteSpace(key) ? fallback : key.Trim().ToLowerInvariant();
        }

        private void EnsureDefaultDefinitions()
        {
            channels ??= new List<AudioChannelDefinition>();

            EnsureChannelDefinition(
                DefaultBgmChannelKey,
                AudioChannelMode.LoopSingle,
                MusicVolumeGroupKey,
                1,
                1,
                defaultBgmFadeDuration);
            EnsureChannelDefinition(
                DefaultAmbienceChannelKey,
                AudioChannelMode.LoopSingle,
                AmbienceVolumeGroupKey,
                1,
                1,
                1f);
            EnsureChannelDefinition(
                DefaultSfxChannelKey,
                AudioChannelMode.OneShotPool,
                SfxVolumeGroupKey,
                4,
                16,
                0f);
            EnsureChannelDefinition(
                DefaultUiSfxChannelKey,
                AudioChannelMode.OneShotPool,
                SfxVolumeGroupKey,
                4,
                16,
                0f);
        }

        private void EnsureChannelDefinition(
            string channelKey,
            AudioChannelMode mode,
            string volumeGroupKey,
            int initialSourceCount,
            int maxSourceCount,
            float fadeDuration)
        {
            string normalizedKey = NormalizeKey(channelKey, string.Empty);
            for (int i = 0; i < channels.Count; i++)
            {
                if (channels[i] != null && channels[i].Key == normalizedKey)
                {
                    return;
                }
            }

            channels.Add(new AudioChannelDefinition(
                channelKey,
                mode,
                volumeGroupKey,
                initialSourceCount,
                maxSourceCount,
                fadeDuration));
        }

        private void RebuildCueLookup()
        {
            cueLookup.Clear();
            if (cues == null)
            {
                return;
            }

            for (int i = 0; i < cues.Count; i++)
            {
                AudioCueDefinition cue = cues[i];
                if (cue == null || string.IsNullOrEmpty(cue.Key))
                {
                    continue;
                }

                cueLookup[cue.Key] = cue;
            }
        }

        private void EnsureChannels()
        {
            EnsureDefaultDefinitions();

            for (int i = 0; i < channels.Count; i++)
            {
                AudioChannelDefinition definition = channels[i];
                if (definition == null || string.IsNullOrEmpty(definition.Key))
                {
                    continue;
                }

                if (!channelRuntimes.TryGetValue(definition.Key, out AudioChannelRuntime runtime))
                {
                    runtime = new AudioChannelRuntime(this, definition);
                    channelRuntimes.Add(definition.Key, runtime);
                }

                runtime.EnsureSources();
                ApplyVolumeToRuntime(runtime);
            }
        }

        private bool TryGetRuntime(string channelKey, out AudioChannelRuntime runtime)
        {
            string normalizedKey = NormalizeKey(channelKey, string.Empty);
            return channelRuntimes.TryGetValue(normalizedKey, out runtime);
        }

        private bool TryGetCue(string cueKey, out AudioCueDefinition cue)
        {
            string normalizedKey = NormalizeKey(cueKey, string.Empty);
            return cueLookup.TryGetValue(normalizedKey, out cue);
        }

        private void SubscribeDataManager()
        {
            DataManager dataManager = DataManager.Instance;
            if (subscribedDataManager == dataManager)
            {
                return;
            }

            if (subscribedDataManager != null)
            {
                subscribedDataManager.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
            }

            subscribedDataManager = dataManager;
            subscribedDataManager.OnAudioSettingsChanged += HandleAudioSettingsChanged;
        }

        private void SyncFromDataManager()
        {
            DataManager dataManager = DataManager.Instance;
            dataManager.EnsureAppDataLoaded();
            HandleAudioSettingsChanged(dataManager.AppData != null ? dataManager.AppData.Audio : null);
        }

        private void HandleAudioSettingsChanged(AudioSaveData changedAudioData)
        {
            audioData = changedAudioData ?? AudioSaveData.CreateDefault();
            audioData.Validate();

            foreach (AudioChannelRuntime runtime in channelRuntimes.Values)
            {
                ApplyVolumeToRuntime(runtime);
            }

            SettingsChanged?.Invoke(audioData.BgmVolume, audioData.SfxVolume, audioData.IsMuted);
            ChannelSettingsChanged?.Invoke(
                audioData.BgmVolume,
                audioData.AmbienceVolume,
                audioData.SfxVolume,
                audioData.IsMuted);
            AudioSettingsChanged?.Invoke(audioData);
        }

        private void ApplyVolumeToRuntime(AudioChannelRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            if (runtime.SingleSource != null)
            {
                runtime.SingleSource.mute = audioData.IsMuted;
                if (runtime.FadeRoutine == null || audioData.IsMuted)
                {
                    runtime.SingleSource.volume = audioData.IsMuted
                        ? 0f
                        : GetEffectiveVolume(runtime, runtime.CurrentVolumeScale);
                }
            }

            runtime.ApplyPoolVolume(GetEffectiveVolume(runtime, 1f), audioData.IsMuted);
        }

        private float GetEffectiveVolume(AudioChannelRuntime runtime, float volumeScale)
        {
            if (runtime == null || audioData == null || audioData.IsMuted)
            {
                return 0f;
            }

            string volumeGroupKey = runtime.Definition.VolumeGroupKey;
            float master = audioData.GetVolumeGroup(MasterVolumeGroupKey);
            float group = volumeGroupKey == MasterVolumeGroupKey ? 1f : audioData.GetVolumeGroup(volumeGroupKey);
            float channel = audioData.GetChannelVolume(runtime.Definition.Key);
            return Mathf.Clamp01(master * group * channel * Mathf.Clamp01(volumeScale));
        }

        private void StartSingleImmediate(
            AudioChannelRuntime runtime,
            AudioClip clip,
            float volumeScale,
            float spatialBlend,
            float pitch)
        {
            runtime.EnsureSingleSource();
            runtime.CurrentVolumeScale = volumeScale;
            runtime.CurrentSpatialBlend = spatialBlend;
            runtime.CurrentPitch = pitch;
            runtime.SingleSource.clip = clip;
            runtime.SingleSource.loop = true;
            runtime.SingleSource.spatialBlend = spatialBlend;
            runtime.SingleSource.pitch = pitch;
            ApplyVolumeToRuntime(runtime);
            runtime.SingleSource.Play();
        }

        private IEnumerator SwitchSingleRoutine(
            AudioChannelRuntime runtime,
            AudioClip clip,
            float volumeScale,
            float spatialBlend,
            float pitch,
            float fadeDuration)
        {
            runtime.EnsureSingleSource();
            if (runtime.SingleSource.isPlaying)
            {
                yield return FadeSourceVolume(runtime.SingleSource, runtime.SingleSource.volume, 0f, fadeDuration);
            }

            runtime.CurrentVolumeScale = volumeScale;
            runtime.CurrentSpatialBlend = spatialBlend;
            runtime.CurrentPitch = pitch;
            runtime.SingleSource.clip = clip;
            runtime.SingleSource.loop = true;
            runtime.SingleSource.spatialBlend = spatialBlend;
            runtime.SingleSource.pitch = pitch;
            runtime.SingleSource.volume = 0f;
            runtime.SingleSource.mute = audioData.IsMuted;
            runtime.SingleSource.Play();

            yield return FadeSourceVolume(runtime.SingleSource, 0f, GetEffectiveVolume(runtime, volumeScale), fadeDuration);

            runtime.FadeRoutine = null;
            ApplyVolumeToRuntime(runtime);
        }

        private IEnumerator StopSingleRoutine(AudioChannelRuntime runtime, float fadeDuration)
        {
            runtime.EnsureSingleSource();
            yield return FadeSourceVolume(runtime.SingleSource, runtime.SingleSource.volume, 0f, fadeDuration);
            runtime.SingleSource.Stop();
            runtime.SingleSource.clip = null;

            runtime.FadeRoutine = null;
            ApplyVolumeToRuntime(runtime);
        }

        private IEnumerator FadeSourceVolume(AudioSource source, float from, float to, float duration)
        {
            if (source == null)
            {
                yield break;
            }

            if (duration <= 0f)
            {
                source.volume = to;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += useUnscaledTimeForFades ? Time.unscaledDeltaTime : Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                source.volume = Mathf.Lerp(from, to, t);
                yield return null;
            }

            source.volume = to;
        }

        private void StopFadeRoutine(AudioChannelRuntime runtime)
        {
            if (runtime == null || runtime.FadeRoutine == null)
            {
                return;
            }

            StopCoroutine(runtime.FadeRoutine);
            runtime.FadeRoutine = null;
        }

        private float ResolveFadeDuration(AudioChannelRuntime runtime, float fadeDuration)
        {
            if (fadeDuration >= 0f)
            {
                return fadeDuration;
            }

            return runtime == null ? 0f : runtime.Definition.DefaultFadeDuration;
        }

        private void ConfigureBaseSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }

        private void WarnMissingChannel(string channelKey)
        {
            if (warnMissingKeys)
            {
                Debug.LogWarning($"音频通道不存在：{channelKey}", this);
            }
        }

        private void WarnMissingCue(string cueKey)
        {
            if (warnMissingKeys)
            {
                Debug.LogWarning($"音频 Cue 不存在或未绑定 AudioClip：{cueKey}", this);
            }
        }

        private void OnDestroy()
        {
            if (subscribedDataManager != null)
            {
                subscribedDataManager.OnAudioSettingsChanged -= HandleAudioSettingsChanged;
                subscribedDataManager = null;
            }
        }

        private sealed class AudioChannelRuntime
        {
            private readonly AudioPlayer owner;
            private readonly List<AudioSource> pooledSources = new List<AudioSource>();
            private Transform root;

            public AudioChannelRuntime(AudioPlayer owner, AudioChannelDefinition definition)
            {
                this.owner = owner;
                Definition = definition;
            }

            public AudioChannelDefinition Definition { get; }
            public AudioSource SingleSource { get; private set; }
            public Coroutine FadeRoutine { get; set; }
            public float CurrentVolumeScale { get; set; } = 1f;
            public float CurrentSpatialBlend { get; set; }
            public float CurrentPitch { get; set; } = 1f;

            public void EnsureSources()
            {
                EnsureRoot();
                if (Definition.Mode == AudioChannelMode.LoopSingle)
                {
                    EnsureSingleSource();
                    return;
                }

                while (pooledSources.Count < Definition.InitialSourceCount)
                {
                    pooledSources.Add(CreatePooledSource(pooledSources.Count));
                }
            }

            public void EnsureSingleSource()
            {
                EnsureRoot();
                if (SingleSource != null)
                {
                    owner.ConfigureBaseSource(SingleSource);
                    return;
                }

                Transform existing = root.Find("Source");
                if (existing != null && existing.TryGetComponent(out AudioSource existingSource))
                {
                    SingleSource = existingSource;
                }
                else
                {
                    GameObject sourceObject = new GameObject("Source");
                    sourceObject.transform.SetParent(root, false);
                    SingleSource = sourceObject.AddComponent<AudioSource>();
                }

                owner.ConfigureBaseSource(SingleSource);
            }

            public AudioSource GetAvailablePooledSource()
            {
                EnsureSources();

                for (int i = pooledSources.Count - 1; i >= 0; i--)
                {
                    if (pooledSources[i] == null)
                    {
                        pooledSources.RemoveAt(i);
                    }
                }

                for (int i = 0; i < pooledSources.Count; i++)
                {
                    if (!pooledSources[i].isPlaying)
                    {
                        return pooledSources[i];
                    }
                }

                if (pooledSources.Count < Definition.MaxSourceCount)
                {
                    AudioSource source = CreatePooledSource(pooledSources.Count);
                    pooledSources.Add(source);
                    return source;
                }

                return null;
            }

            public void ApplyPoolVolume(float volume, bool muted)
            {
                for (int i = 0; i < pooledSources.Count; i++)
                {
                    AudioSource source = pooledSources[i];
                    if (source == null)
                    {
                        continue;
                    }

                    source.volume = muted ? 0f : volume;
                    source.mute = muted;
                }
            }

            public void StopPooledSources()
            {
                for (int i = 0; i < pooledSources.Count; i++)
                {
                    if (pooledSources[i] != null)
                    {
                        pooledSources[i].Stop();
                    }
                }
            }

            private void EnsureRoot()
            {
                if (root != null)
                {
                    return;
                }

                string objectName = $"Audio Channel - {Definition.Key}";
                Transform existing = owner.transform.Find(objectName);
                if (existing != null)
                {
                    root = existing;
                    return;
                }

                GameObject rootObject = new GameObject(objectName);
                rootObject.transform.SetParent(owner.transform, false);
                root = rootObject.transform;
            }

            private AudioSource CreatePooledSource(int index)
            {
                GameObject sourceObject = new GameObject($"Source {index + 1}");
                sourceObject.transform.SetParent(root, false);
                AudioSource source = sourceObject.AddComponent<AudioSource>();
                owner.ConfigureBaseSource(source);
                source.loop = false;
                return source;
            }
        }
    }
}
