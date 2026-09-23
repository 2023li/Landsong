using System;
using Landsong.ECS.Authoring.Definitions;
using Sirenix.OdinInspector;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    /// <summary>Creates map-independent state on the simulation root.</summary>
    [DisallowMultipleComponent]
    public sealed class GameWorldTemplateAuthoring : MonoBehaviour
    {
        [Serializable]
        public sealed class WeatherParameters
        {
            [LabelText("春季降水概率 %")] public int SpringRain = 45;
            [LabelText("夏季降水概率 %")] public int SummerRain = 25;
            [LabelText("秋季降水概率 %")] public int AutumnRain = 30;
            [LabelText("冬季降水概率 %")] public int WinterRain = 35;
            [LabelText("春季基础/最低/最高温度")] public int3 SpringTemperature = new int3(8, -5, 20);
            [LabelText("夏季基础/最低/最高温度")] public int3 SummerTemperature = new int3(25, 15, 35);
            [LabelText("秋季基础/最低/最高温度")] public int3 AutumnTemperature = new int3(8, -5, 20);
            [LabelText("冬季基础/最低/最高温度")] public int3 WinterTemperature = new int3(-5, -15, 5);
            [LabelText("春/夏/秋/冬升温概率 %")] public int4 WarmingPercent = new int4(50, 65, 50, 35);
            [LabelText("无风/微风/中风/强风概率 %")] public int4 WindPercent = new int4(10, 45, 35, 10);
        }

        [LabelText("季节与天气数值")]
        public WeatherParameters Weather = new WeatherParameters();
        [LabelText("消防员行走速度"), MinValue(.1f)] public float FirefighterSpeed = 1.6f;
        [LabelText("消防站定义"), Required] public BuildingDefinitionAsset FireStation;
        [LabelText("雷神殿定义"), Required] public BuildingDefinitionAsset ThunderTemple;

        public sealed class Baker : Baker<GameWorldTemplateAuthoring>
        {
            public override void Bake(GameWorldTemplateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Session { Phase = Phase.Day });
                AddComponent(entity, new GameClock { Turn = 1 });
                var weather = authoring.Weather ?? new WeatherParameters();
                AddComponent(entity, new SeasonWeatherSettings
                {
                    RainPercent = new int4(weather.SpringRain, weather.SummerRain, weather.AutumnRain, weather.WinterRain),
                    BaseTemperature = new int4(weather.SpringTemperature.x, weather.SummerTemperature.x, weather.AutumnTemperature.x, weather.WinterTemperature.x),
                    MinimumTemperature = new int4(weather.SpringTemperature.y, weather.SummerTemperature.y, weather.AutumnTemperature.y, weather.WinterTemperature.y),
                    MaximumTemperature = new int4(weather.SpringTemperature.z, weather.SummerTemperature.z, weather.AutumnTemperature.z, weather.WinterTemperature.z),
                    WarmingPercent = weather.WarmingPercent,
                    WindPercent = weather.WindPercent,
                });
                AddComponent<SeasonWeatherState>(entity);
                var buildings = GameContentSetAuthoring.Resolve<BuildingCatalogAsset>(authoring);
                DependsOn(buildings);
                DependsOn(authoring.FireStation);
                DependsOn(authoring.ThunderTemple);
                var buildingIndex = new BuildingCatalogIndex(buildings);
                AddComponent(entity, new FireSettings
                {
                    Station = buildingIndex.Resolve(authoring.FireStation),
                    Temple = buildingIndex.Resolve(authoring.ThunderTemple),
                    FirefighterSpeed = authoring.FirefighterSpeed,
                });
                AddComponent<LightningViewport>(entity);
                AddBuffer<LightningVisualEvent>(entity);
                AddComponent<SimulationControl>(entity);
                AddComponent<PublicOpinionState>(entity);
                AddComponent<ResearchState>(entity);
                AddComponent<ExpeditionPenaltyState>(entity);
                AddComponent<NightRuntimeState>(entity);
                AddComponent<DaySettlementState>(entity);
                AddComponent<RetryState>(entity);
                AddComponent<HeroSelection>(entity);
                AddComponent<BellState>(entity);
                AddComponent<IntelligenceModeState>(entity);
                AddComponent<PersistenceGate>(entity);
                AddComponent<IdentitySequence>(entity);

                AddBuffer<QueuedGameplayRequest>(entity);
                AddBuffer<GameEvent>(entity);
                AddBuffer<DamageRequest>(entity);
                AddBuffer<ResearchCompletedEvent>(entity);
                AddBuffer<ItemPickupEvent>(entity);
                AddBuffer<InventorySlot>(entity);
                AddBuffer<PendingItem>(entity);
                AddBuffer<PolicyChoice>(entity);
                AddBuffer<BattleReportEntry>(entity);
                AddBuffer<NightWave>(entity);
                AddComponent<QuestTracking>(entity);
                AddBuffer<SpawnRegion>(entity);
            }
        }
    }
}
