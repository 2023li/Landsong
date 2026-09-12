using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong.ECS.Presentation
{
    // Application preferences, never part of a dynasty or the simulation authority.
    [Serializable] public sealed class InterfacePreferences
    {
        public float Master = 1, Music = 1, Effects = 1, Ambient = 1, UiScale = 1, CameraSpeed = 18, ZoomSpeed = 1, Smoothing = 0;
        public int Width, Height, Quality = -1, Fullscreen = -1;
        public bool Muted, ReducedMotion, HighContrast, GeneralMessages = true, EconomyMessages = true;
        public string Language="zh-Hans";
        public Key Forward = Key.W, Back = Key.S, Left = Key.A, Right = Key.D, RotateLeft = Key.Q, RotateRight = Key.E, Pause = Key.Space;
        public InterfacePreferences Copy() => (InterfacePreferences)MemberwiseClone();
        public void Validate()
        {
            Master = Clamp(Master, 0, 1, 1); Music = Clamp(Music, 0, 1, 1); Effects = Clamp(Effects, 0, 1, 1); Ambient = Clamp(Ambient, 0, 1, 1);
            UiScale = Clamp(UiScale, .8f, 1.4f, 1); CameraSpeed = Clamp(CameraSpeed, 5, 60, 18); ZoomSpeed = Clamp(ZoomSpeed, .25f, 3, 1); Smoothing = Clamp(Smoothing, 0, .3f, 0);
            if (Width < 800 || Width > 16384 || Height < 600 || Height > 16384) Width = Height = 0;
            Fullscreen = Mathf.Clamp(Fullscreen, -1, 1); Quality = Mathf.Clamp(Quality, -1, Mathf.Max(0, QualitySettings.names.Length - 1));
            if(string.IsNullOrWhiteSpace(Language)||Language.Length>64||!System.Text.RegularExpressions.Regex.IsMatch(Language,@"^[a-zA-Z0-9_.-]+$"))Language="zh-Hans";
            var keys = new[] { Forward, Back, Left, Right, RotateLeft, RotateRight, Pause };
            if (keys.Any(k => !AllowedKey(k)) || keys.Distinct().Count() != keys.Length)
            { Forward = Key.W; Back = Key.S; Left = Key.A; Right = Key.D; RotateLeft = Key.Q; RotateRight = Key.E; Pause = Key.Space; }
        }
        static float Clamp(float value, float low, float high, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, low, high);
        public static bool AllowedKey(Key key) => Enum.IsDefined(typeof(Key), key) && key != Key.None && key != Key.Escape && key != Key.R && key != Key.H && key != Key.Backquote && key != Key.Enter && key != Key.Tab && !(key >= Key.Digit1 && key <= Key.Digit0);
    }
    public static class InterfaceSettings
    {
        const string KeyName = "Landsong.UI.Preferences.v1";
        public static InterfacePreferences Current { get; private set; } = new InterfacePreferences();
        public static int Revision { get; private set; }
        public static Action<string> PersistenceOverride; // Owned test scope only; production uses PlayerPrefs.
        public static InterfacePreferences Decode(string json)
        { try { var data = JsonUtility.FromJson<InterfacePreferences>(json) ?? new InterfacePreferences(); data.Validate(); return data; } catch { return new InterfacePreferences(); } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Load()
        {
            Current = PlayerPrefs.HasKey(KeyName) ? Decode(PlayerPrefs.GetString(KeyName)) : new InterfacePreferences { Master = PlayerPrefs.GetFloat("Landsong.UI.MasterVolume", 1), Fullscreen = PlayerPrefs.GetInt("Landsong.UI.Fullscreen", -1) };
            Apply(Current, false);
        }
        public static void Apply(InterfacePreferences data, bool persist = true, bool display = true)
        {
            data = data.Copy(); data.Validate(); Current = data; Revision++;
            AudioListener.volume = data.Muted?0:data.Master;
            if (display)
            {
                if (data.Quality >= 0 && QualitySettings.GetQualityLevel() != data.Quality) QualitySettings.SetQualityLevel(data.Quality);
                if (data.Width > 0 && (Screen.width != data.Width || Screen.height != data.Height)) Screen.SetResolution(data.Width, data.Height, data.Fullscreen < 0 ? Screen.fullScreen : data.Fullscreen != 0);
                else if (data.Fullscreen >= 0 && Screen.fullScreen != (data.Fullscreen != 0)) Screen.fullScreen = data.Fullscreen != 0;
            }
            if (persist) {var json=JsonUtility.ToJson(data);if(PersistenceOverride!=null)PersistenceOverride(json);else{PlayerPrefs.SetString(KeyName,json);PlayerPrefs.Save();}}
        }
    }
}
