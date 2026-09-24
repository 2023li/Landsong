using System;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using Unity.Mathematics;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // Common runtime scene shell. SubScene references also declare the entity scenes for player builds.
    public sealed class EcsGameHost : MonoBehaviour
    {
        [LabelText("地图目录"), Required]
        public EcsMapMenuCatalog Catalog;
        [LabelText("地图实体子场景"), Required]
        public SubScene[] Maps;
        [LabelText("世界相机"), Required]
        public Camera Camera;
        [LabelText("场景主光源"), Required]
        public Light Sun;
        [LabelText("昼夜光照")]
        public NightLightingSettings NightLighting = new NightLightingSettings();
        [LabelText("雨天主光比例"), Range(0, 1)]
        public float RainLightRatio = .75f;
        [LabelText("小雨主光比例"), Range(0, 1)]
        public float LightRainLightRatio = .85f;
        [LabelText("大雨主光比例"), Range(0, 1)]
        public float HeavyRainLightRatio = .65f;
        public bool Visible { get; private set; }
        NightLightingController lighting;
        WeatherPresentationController weatherPresentation;
        World lightingWorld;
        Entity lightingRoot;

        public void BindLighting(EntityManager em, Entity root)
        {
            UnbindLighting();
            lightingWorld = em.World;
            lightingRoot = root;
            lighting = new NightLightingController(Sun, NightLighting);
            weatherPresentation = new WeatherPresentationController(Camera);
            UpdateLightningViewport();
            UpdateLighting(0);
        }

        void LateUpdate()
        {
            if (Visible && EcsSceneFlow.GameReady)
            {
                UpdateLightningViewport();
                UpdateLighting(Time.unscaledDeltaTime);
                var em = lightingWorld.EntityManager;
                if (em.Exists(lightingRoot) && em.HasComponent<SeasonWeatherState>(lightingRoot))
                    weatherPresentation?.Tick(em.GetComponentData<SeasonWeatherState>(lightingRoot),
                        em.GetComponentData<SimulationControl>(lightingRoot).Paused != 0,
                        Time.unscaledDeltaTime, em.GetBuffer<LightningVisualEvent>(lightingRoot));
            }
        }

        static float4 Column(Vector4 value) => new float4(value.x, value.y, value.z, value.w);

        void UpdateLightningViewport()
        {
            if (lightingWorld == null || !lightingWorld.IsCreated || Camera == null)
                return;
            var em = lightingWorld.EntityManager;
            if (!em.Exists(lightingRoot) || !em.HasComponent<LightningViewport>(lightingRoot))
                return;
            var matrix = Camera.projectionMatrix * Camera.worldToCameraMatrix;
            var forward = Camera.transform.forward;
            var position = Camera.transform.position;
            em.SetComponentData(lightingRoot, new LightningViewport
            {
                ViewProjection = new float4x4(Column(matrix.GetColumn(0)), Column(matrix.GetColumn(1)), Column(matrix.GetColumn(2)), Column(matrix.GetColumn(3))),
                CameraPosition = new float3(position.x, position.y, position.z),
                Forward = new float3(forward.x, forward.y, forward.z),
                Near = Camera.nearClipPlane,
                Far = Camera.farClipPlane,
                Available = (byte)(Camera.isActiveAndEnabled ? 1 : 0),
            });
        }

        void UpdateLighting(float delta)
        {
            if (lighting == null || lightingWorld == null || !lightingWorld.IsCreated)
                return;
            var em = lightingWorld.EntityManager;
            if (!em.Exists(lightingRoot))
                return;
            var weather = em.GetComponentData<SeasonWeatherState>(lightingRoot).Weather;
            var weatherLightRatio = weather == WeatherKind.LightRain ? LightRainLightRatio
                : weather == WeatherKind.Rain ? RainLightRatio
                : weather == WeatherKind.HeavyRain ? HeavyRainLightRatio : 1f;
            lighting.Tick(em.GetComponentData<Session>(lightingRoot).Phase,
                em.GetComponentData<GameClock>(lightingRoot), em.GetComponentData<NightSettings>(lightingRoot),
                em.GetComponentData<NightRuntimeState>(lightingRoot), em.GetComponentData<SimulationControl>(lightingRoot).Paused != 0, delta,
                weatherLightRatio);
        }

        public void UnbindLighting()
        {
            lighting?.Dispose();
            lighting = null;
            weatherPresentation?.Dispose();
            weatherPresentation = null;
            lightingWorld = null;
            lightingRoot = Entity.Null;
        }

        void OnDisable() => UnbindLighting();

        void Awake()
        {
            if (Catalog == null || Maps == null || Maps.Length == 0 || Camera == null || Sun == null)
                throw new InvalidOperationException("Game 场景宿主检查器引用不完整。");
            SetVisible(false);
        }

        public Entity LoadMap(string id, World world)
        {
            if (Catalog == null || Maps == null || Catalog.Maps.Length != Maps.Length)
                throw new InvalidOperationException("地图目录与 SubScene 配置不一致。");
            for (var i = 0; i < Maps.Length; i++)
            {
                if (Maps[i] == null || Maps[i].AutoLoadScene)
                    throw new InvalidOperationException("Game 的地图 SubScene 必须配置引用并关闭 Auto Load Scene。");
                for (var j = 0; j < i; j++)
                    if (Catalog.Maps[i].Id == Catalog.Maps[j].Id || Maps[i].SceneGUID == Maps[j].SceneGUID)
                        throw new InvalidOperationException("地图 ID 或 SubScene 重复配置。");
            }

            var index = Array.FindIndex(Catalog.Maps, m => m.Id == id);
            if (index < 0 || Maps[index] == null || !Maps[index].SceneGUID.IsValid)
                throw new InvalidOperationException("找不到地图：" + id);
            return SceneSystem.LoadSceneAsync(world.Unmanaged, Maps[index].SceneGUID);
        }

        public void FocusCore(EntityManager em, Entity root)
        {
            using var buildings = WorldQueries.Entities<Building>(em);
            foreach (var entity in buildings)
                if (em.GetComponentData<BuildingHousingStats>(entity).IsCore != 0)
                {
                    Camera.transform.position = (Vector3)EntityState.Position(em, entity) + new Vector3(0, 28, -20);
                    break;
                }
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            if (Camera != null)
                Camera.gameObject.SetActive(visible);
            if (Sun != null)
                Sun.gameObject.SetActive(visible);
        }
    }
}
