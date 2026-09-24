using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class SeasonWeatherOps
    {
        public static SeasonKind Season(int turn)
        {
            var day = (math.max(1, turn) - 1) % 100;
            return day < 30 ? SeasonKind.Spring : day < 50 ? SeasonKind.Summer : day < 80 ? SeasonKind.Autumn : SeasonKind.Winter;
        }

        public static int DayOfSeason(int turn)
        {
            var day = (math.max(1, turn) - 1) % 100;
            return day < 30 ? day + 1 : day < 50 ? day - 29 : day < 80 ? day - 49 : day - 79;
        }

        public static void Initialize(EntityManager em, Entity root)
        {
            var state = em.GetComponentData<SeasonWeatherState>(root);
            if (state.Initialized != 0)
                return;
            var seed = em.GetComponentData<SimulationRandomState>(root).State;
            state.RandomState = math.max(1u, math.hash(new uint2(math.max(1u, seed), 0xA47B37D3u)));
            var turn = em.GetComponentData<GameClock>(root).Turn;
            var settings = em.GetComponentData<SeasonWeatherSettings>(root);
            state.Temperature = settings.BaseTemperature[(int)Season(turn)];
            GenerateDay(ref state, settings, turn, firstDay: true);
            em.SetComponentData(root, state);
        }

        public static void Dawn(EntityManager em, Entity root)
        {
            var state = em.GetComponentData<SeasonWeatherState>(root);
            if (state.Initialized == 0)
            {
                Initialize(em, root);
                return;
            }
            var turn = em.GetComponentData<GameClock>(root).Turn;
            if (state.DayTurn >= turn)
                return;
            var settings = em.GetComponentData<SeasonWeatherSettings>(root);
            var snowBefore = state.Weather == WeatherKind.Snow;
            for (var day = state.DayTurn + 1; day <= turn; day++)
                GenerateDay(ref state, settings, day, firstDay: false);
            em.SetComponentData(root, state);
            if (snowBefore != (state.Weather == WeatherKind.Snow) && em.HasComponent<GridData>(root))
            {
                var grid = em.GetComponentData<GridData>(root);
                grid.Revision++;
                em.SetComponentData(root, grid);
                SurfaceNavigationGraph.Invalidate(em, root);
            }
        }

        static void GenerateDay(ref SeasonWeatherState state, SeasonWeatherSettings settings, int turn, bool firstDay)
        {
            var season = Season(turn);
            var index = (int)season;
            var random = new Random(math.max(1u, state.RandomState));
            if (!firstDay)
            {
                var previous = math.clamp(state.Temperature, settings.MinimumTemperature[index], settings.MaximumTemperature[index]);
                var warming = random.NextInt(100) < settings.WarmingPercent[index];
                // Weights for magnitudes 2..10 are 9,8,...,1 (sum 45).
                var pick = random.NextInt(45);
                var magnitude = 2;
                for (var weight = 9; weight > 1 && pick >= weight; weight--, magnitude++)
                    pick -= weight;
                state.Temperature = math.clamp(previous + (warming ? magnitude : -magnitude), settings.MinimumTemperature[index], settings.MaximumTemperature[index]);
            }
            state.Season = season;
            if (random.NextInt(100) >= settings.RainPercent[index])
                state.Weather = WeatherKind.Sunny;
            else if (state.Temperature < 0)
                state.Weather = WeatherKind.Snow;
            else
                state.Weather = random.NextInt(3) switch
                {
                    0 => WeatherKind.LightRain,
                    1 => WeatherKind.Rain,
                    _ => WeatherKind.HeavyRain,
                };
            var wind = random.NextInt(100);
            state.Wind = wind < settings.WindPercent.x ? WindKind.Calm
                : wind < settings.WindPercent.x + settings.WindPercent.y ? WindKind.Light
                : wind < settings.WindPercent.x + settings.WindPercent.y + settings.WindPercent.z ? WindKind.Moderate
                : WindKind.Strong;
            state.WindDegrees = random.NextFloat(0, 360);
            state.LightningLimit = WeatherKindOps.IsRain(state.Weather) ? (byte)random.NextInt(1, 4) : (byte)0;
            state.LightningCount = 0;
            state.DayElapsed = 0;
            state.NextThunderAt = WeatherKindOps.IsRain(state.Weather) ? random.NextFloat(60, 120) : 0;
            state.DayTurn = turn;
            state.Initialized = 1;
            state.RandomState = random.state;
        }
    }
}
