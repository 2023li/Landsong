using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    [Serializable]
    public sealed class NightLightingSettings
    {
        [LabelText("夜间主光强度比例"), Range(0, 1)]
        public float NightIntensityRatio = .12f;
        [LabelText("收尾主光强度比例"), Range(0, 1)]
        public float ClosureIntensityRatio = .3f;
        [LabelText("日落／日出太阳高度角"), Range(0, 45)]
        public float HorizonElevation = 8;
        [LabelText("日落／日出方向偏移"), Range(0, 150)]
        public float HorizonAzimuthOffset = 65;
    }

    // Presentation only: read the authoritative phase clock; never delay night completion.
    public sealed class NightLightingController : IDisposable
    {
        readonly Light sun;
        readonly NightLightingSettings settings;
        readonly float dayIntensity;
        readonly Quaternion dayRotation;
        readonly float dayAzimuth;
        Phase previous;
        bool initialized;
        float phaseStartIntensity;
        Quaternion phaseStartRotation;
        const float EdgeBlendSeconds = .25f;

        public NightLightingController(Light sun, NightLightingSettings settings)
        {
            if (sun == null || sun.type != LightType.Directional)
                throw new ArgumentException("昼夜光照必须绑定场景方向光。", nameof(sun));
            this.sun = sun;
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            dayIntensity = sun.intensity;
            dayRotation = sun.transform.rotation;
            dayAzimuth = sun.transform.eulerAngles.y;
        }

        float NightIntensity => dayIntensity * Mathf.Clamp01(settings.NightIntensityRatio);
        float ClosureIntensity => dayIntensity * Mathf.Clamp(settings.ClosureIntensityRatio, Mathf.Clamp01(settings.NightIntensityRatio), 1);
        Quaternion Horizon(float offset) => Quaternion.Euler(settings.HorizonElevation, dayAzimuth + offset, 0);
        Quaternion Sunset => Horizon(settings.HorizonAzimuthOffset);
        Quaternion Sunrise => Horizon(-settings.HorizonAzimuthOffset);
        static float Smooth(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
        static int Stage(Phase phase) => phase == Phase.Night || phase == Phase.Celebration ? 3 : (int)phase;

        void BeginStage(Phase phase, GameClock clock, NightSettings timing)
        {
            if (initialized)
            {
                // Phase boundaries always start from the value displayed in the previous frame.
                phaseStartIntensity = sun.intensity;
                phaseStartRotation = sun.transform.rotation;
                return;
            }

            // Reconstruct a deterministic source when loading directly into a timed stage.
            if (phase == Phase.Day)
            {
                var skipped = clock.DawnSourceNightTime >= 0;
                var progress = Smooth((clock.DawnSourceNightTime - timing.NightPreparationSeconds) / Mathf.Max(.001f, timing.NightSeconds));
                phaseStartIntensity = skipped ? NightIntensity : ClosureIntensity;
                phaseStartRotation = skipped ? Quaternion.Slerp(Sunset, Sunrise, progress) : Sunrise;
            }
            else if (phase == Phase.Deployment || phase == Phase.Settlement)
            {
                phaseStartIntensity = dayIntensity;
                phaseStartRotation = dayRotation;
            }
            else if (phase == Phase.Retreat)
            {
                phaseStartIntensity = NightIntensity;
                phaseStartRotation = Sunrise;
            }
            else
            {
                phaseStartIntensity = NightIntensity;
                phaseStartRotation = Sunset;
            }
        }

        public void Tick(Phase phase, GameClock clock, NightSettings timing, NightRuntimeState night, bool paused, float delta)
        {
            if (sun == null)
                return;
            bool changed = !initialized || Stage(previous) != Stage(phase);
            if (changed)
                BeginStage(phase, clock, timing);
            previous = phase;
            initialized = true;
            if (phase == Phase.GameOver || phase == Phase.Ended)
                return;
            if (phase == Phase.Day)
            {
                var t = Smooth(timing.DawnSeconds <= 0 ? 1 : 1 - clock.DawnRemaining / timing.DawnSeconds);
                Apply(Mathf.Lerp(phaseStartIntensity, dayIntensity, t), Quaternion.Slerp(phaseStartRotation, dayRotation, t));
            }
            else if (phase == Phase.Settlement)
                Apply(dayIntensity, dayRotation);
            else if (phase == Phase.Report || phase == Phase.Returning)
                Apply(NightIntensity, Sunrise);
            else if (phase == Phase.Deployment)
            {
                var t = Smooth(timing.NightPreparationSeconds <= 0 ? 1 : clock.PhaseTime / timing.NightPreparationSeconds);
                Apply(Mathf.Lerp(phaseStartIntensity, NightIntensity, t), Quaternion.Slerp(phaseStartRotation, Sunset, t));
            }
            else if (phase == Phase.Retreat)
            {
                var closureStart = timing.NightPreparationSeconds + timing.NightSeconds;
                var elapsed = Mathf.Max(0, clock.PhaseTime - closureStart);
                var t = Smooth(timing.NightClosureSeconds <= 0 ? 1 : elapsed / timing.NightClosureSeconds);
                var brighten = Smooth(timing.RetreatDelaySeconds <= 0 ? 1 : elapsed / timing.RetreatDelaySeconds);
                Apply(Mathf.Lerp(phaseStartIntensity, ClosureIntensity, brighten), Quaternion.Slerp(phaseStartRotation, Sunrise, t));
            }
            else
            {
                var elapsed = Mathf.Max(0, clock.PhaseTime - timing.NightPreparationSeconds);
                var t = Smooth(elapsed / Mathf.Max(.001f, timing.NightSeconds));
                var intensity = Mathf.Lerp(phaseStartIntensity, NightIntensity, Smooth(elapsed / EdgeBlendSeconds));
                Apply(intensity, Quaternion.Slerp(phaseStartRotation, Sunrise, t));
            }
        }

        void Apply(float intensity, Quaternion rotation)
        {
            sun.intensity = intensity;
            sun.transform.rotation = rotation;
        }

        public void Dispose()
        {
            if (sun != null)
                Apply(dayIntensity, dayRotation);
        }
    }
}
