using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum SeasonKind : byte { Spring, Summer, Autumn, Winter }
    // Preserve the first three values for v35 snapshots; Rain is the medium tier.
    public enum WeatherKind : byte { Sunny = 0, Rain = 1, Snow = 2, LightRain = 3, HeavyRain = 4 }
    public enum WindKind : byte { Calm, Light, Moderate, Strong }

    public static class WeatherKindOps
    {
        public static bool IsRain(WeatherKind kind)
            => kind == WeatherKind.LightRain || kind == WeatherKind.Rain || kind == WeatherKind.HeavyRain;

        public static string DisplayName(WeatherKind kind)
            => kind == WeatherKind.LightRain ? "小雨" : kind == WeatherKind.Rain ? "中雨"
                : kind == WeatherKind.HeavyRain ? "大雨" : kind == WeatherKind.Snow ? "雪天" : "晴天";
    }

    // The four lanes are spring, summer, autumn and winter, in that order.
    public struct SeasonWeatherSettings : IComponentData
    {
        public int4 RainPercent;
        public int4 BaseTemperature;
        public int4 MinimumTemperature;
        public int4 MaximumTemperature;
        public int4 WarmingPercent;
        // Calm, light, moderate and strong. The last lane receives the remainder.
        public int4 WindPercent;
    }

    // Authoritative daily state. Visual effects only read this data.
    public struct SeasonWeatherState : IComponentData
    {
        public int DayTurn;
        public int Temperature;
        public SeasonKind Season;
        public WeatherKind Weather;
        public WindKind Wind;
        public float WindDegrees;
        public uint RandomState;
        public byte Initialized;
        public byte LightningLimit;
        public byte LightningCount;
        public float DayElapsed;
        public float NextThunderAt;
    }
}
