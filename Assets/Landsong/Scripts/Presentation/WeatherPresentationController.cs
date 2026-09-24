using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    // Runtime placeholders; gameplay never reads these objects or their timing.
    public sealed class WeatherPresentationController : IDisposable
    {
        readonly Camera camera;
        readonly UniversalAdditionalCameraData cameraData;
        readonly bool originalPostProcessing;
        readonly LayerMask originalVolumeMask;
        readonly VolumeProfile[] seasons = new VolumeProfile[4];
        readonly GameObject root;
        readonly Volume volume;
        readonly ParticleSystem rain;
        readonly ParticleSystem snow;
        readonly Material particleMaterial;
        readonly Material boltMaterial;
        readonly RawImage flash;
        GameObject boltObject;
        LineRenderer bolt;
        ParticleSystem boltTrail;
        ParticleSystem impactSparks;
        Light impactLight;
        Vector3[] boltPath;
        float flashAlpha;
        float flashHold;
        float boltElapsed;
        bool impactPlayed;
        SeasonKind currentSeason = (SeasonKind)byte.MaxValue;

        public WeatherPresentationController(Camera camera)
        {
            this.camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            cameraData = camera.GetUniversalAdditionalCameraData();
            originalPostProcessing = cameraData.renderPostProcessing;
            originalVolumeMask = cameraData.volumeLayerMask;
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask |= 1 << camera.gameObject.layer;
            root = new GameObject("Weather Placeholders");
            // Bind placeholders to the camera's scene. During game startup the active scene
            // is still the loading scene, which is unloaded after BindLighting returns.
            var cameraScene = camera.gameObject.scene;
            if (cameraScene.IsValid() && cameraScene.isLoaded && root.scene != cameraScene)
                SceneManager.MoveGameObjectToScene(root, cameraScene);
            root.layer = camera.gameObject.layer;
            volume = root.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100;
            volume.weight = 1;
            seasons[0] = Profile("Spring", new Color(.93f, 1f, .92f), 8, 4);
            seasons[1] = Profile("Summer", new Color(1f, .97f, .88f), 12, 8);
            seasons[2] = Profile("Autumn", new Color(1f, .9f, .79f), 6, 2);
            seasons[3] = Profile("Winter", new Color(.84f, .91f, 1f), -12, 8);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            particleMaterial = shader == null ? null : new Material(shader) { name = "Weather Particle Placeholder" };
            boltMaterial = shader == null ? null : new Material(shader) { name = "Lightning Placeholder" };
            rain = Particles("Rain", true);
            snow = Particles("Snow", false);
            var canvas = new GameObject("Lightning Flash", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.transform.SetParent(root.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            flash = new GameObject("Flash Image", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            flash.transform.SetParent(canvas.transform, false);
            flash.texture = Texture2D.whiteTexture;
            flash.raycastTarget = false;
            flash.color = new Color(1, 1, 1, 0);
            var rect = flash.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static VolumeProfile Profile(string name, Color tint, float saturation, float contrast)
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = name + " Placeholder Filter";
            var color = profile.Add<ColorAdjustments>(true);
            color.colorFilter.Override(tint);
            color.saturation.Override(saturation);
            color.contrast.Override(contrast);
            return profile;
        }

        ParticleSystem Particles(string name, bool isRain)
        {
            var obj = new GameObject(name, typeof(ParticleSystem));
            obj.transform.SetParent(root.transform, false);
            var system = obj.GetComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            // Keep precipitation in camera space so panning and zooming cannot expose the emitter edge.
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = isRain ? 1.5f : 5f;
            main.prewarm = true;
            main.startSpeed = 0;
            main.startSize = isRain ? .04f : .09f;
            main.startColor = isRain ? new Color(.7f, .8f, 1f, .65f) : new Color(1f, 1f, 1f, .9f);
            main.maxParticles = isRain ? 3000 : 1000;
            var emission = system.emission;
            emission.rateOverTime = isRain ? 450 : 75;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40, 40, .1f);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = isRain ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = isRain ? 7 : 1;
            if (particleMaterial != null)
                renderer.sharedMaterial = particleMaterial;
            obj.SetActive(false);
            return system;
        }

        public void Tick(SeasonWeatherState state, bool paused, float delta, DynamicBuffer<LightningVisualEvent> visuals)
        {
            if (state.Season != currentSeason)
            {
                currentSeason = state.Season;
                volume.sharedProfile = seasons[(int)state.Season];
            }
            PositionParticles(rain, state, true);
            PositionParticles(snow, state, false);
            var rainEmission = rain.emission;
            var rainRate = state.Weather == WeatherKind.LightRain ? 180f
                : state.Weather == WeatherKind.HeavyRain ? 900f : 450f;
            if (!Mathf.Approximately(rainEmission.rateOverTime.constant, rainRate))
                rainEmission.rateOverTime = rainRate;
            SetParticles(rain, WeatherKindOps.IsRain(state.Weather), paused);
            SetParticles(snow, state.Weather == WeatherKind.Snow, paused);
            foreach (var visual in visuals)
            {
                flashAlpha = .82f;
                flashHold = .12f;
                AudioRuntime.Instance?.Play(PresentationCue.Thunder);
                if (visual.Kind != LightningVisualKind.Flash)
                    ShowBolt((Vector3)visual.Position);
            }
            visuals.Clear();
            // The host passes unscaled time, so a debug strike remains visible while simulation is paused.
            var step = Mathf.Max(0, delta);
            if (flashHold > 0)
                flashHold = Mathf.Max(0, flashHold - step);
            else
                flashAlpha = Mathf.MoveTowards(flashAlpha, 0, step * 2.6f);
            flash.color = new Color(.91f, .96f, 1f, flashAlpha);
            AdvanceBolt(step);
        }

        void PositionParticles(ParticleSystem system, SeasonWeatherState state, bool isRain)
        {
            // This is a camera-facing particle layer, rendered in front of the world and behind the UI.
            // Its world-space dimensions follow the current projection, while particle count stays fixed.
            var depth = Mathf.Max(camera.nearClipPlane + 1f, 2f);
            var halfHeight = camera.orthographic ? camera.orthographicSize
                : Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * depth;
            var halfWidth = halfHeight * camera.aspect;
            var overscan = halfHeight * .8f;
            system.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * depth,
                camera.transform.rotation);
            var shape = system.shape;
            var area = new Vector3(halfWidth * 2 + overscan * 2, halfHeight * 2 + overscan * 2, .1f);
            if ((shape.scale - area).sqrMagnitude > .0001f)
                shape.scale = area;
            var main = system.main;
            var rainSize = state.Weather == WeatherKind.LightRain ? .0032f
                : state.Weather == WeatherKind.HeavyRain ? .005f : .004f;
            var size = halfHeight * (isRain ? rainSize : .009f);
            if (!Mathf.Approximately(main.startSize.constant, size))
                main.startSize = size;

            // WindDegrees is a direction on the world XZ plane. Project it into camera-local axes.
            var worldWind = Quaternion.Euler(0, state.WindDegrees, 0) * Vector3.forward;
            var windSpeed = state.Wind == WindKind.Calm ? 0f
                : state.Wind == WindKind.Light ? .03f
                : state.Wind == WindKind.Moderate ? .07f : .12f;
            var drift = windSpeed * halfHeight;
            var velocity = system.velocityOverLifetime;
            var horizontal = Vector3.Dot(worldWind, camera.transform.right) * drift;
            var vertical = (isRain ? -1.1f : -.25f) * halfHeight
                + Vector3.Dot(worldWind, camera.transform.up) * drift;
            if (!Mathf.Approximately(velocity.x.constant, horizontal))
                velocity.x = new ParticleSystem.MinMaxCurve(horizontal);
            if (!Mathf.Approximately(velocity.y.constant, vertical))
                velocity.y = new ParticleSystem.MinMaxCurve(vertical);
        }

        static void SetParticles(ParticleSystem system, bool active, bool paused)
        {
            if (system.gameObject.activeSelf != active)
                system.gameObject.SetActive(active);
            if (!active)
                return;
            if (paused)
                system.Pause();
            else if (!system.isPlaying)
                system.Play();
        }

        void ShowBolt(Vector3 ground)
        {
            if (boltObject != null)
                Release(boltObject);
            var targetViewport = camera.WorldToViewportPoint(ground);
            var depth = Mathf.Clamp(targetViewport.z - .25f, camera.nearClipPlane + .5f, camera.farClipPlane - .5f);
            var sourceViewport = new Vector3(Mathf.Clamp(targetViewport.x + .12f, .08f, .92f), 1.12f, depth);
            boltPath = new Vector3[9];
            for (var i = 0; i < boltPath.Length; i++)
            {
                var t = i / (float)(boltPath.Length - 1);
                var view = Vector3.Lerp(sourceViewport, new Vector3(targetViewport.x, targetViewport.y, depth), t);
                if (i > 0 && i < boltPath.Length - 1)
                    view.x += (i % 2 == 0 ? -.018f : .018f) * (1f - t * .45f);
                boltPath[i] = camera.ViewportToWorldPoint(view);
            }
            var halfHeight = camera.orthographic ? camera.orthographicSize
                : Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * depth;
            var width = Mathf.Max(.025f, halfHeight * .012f);
            boltObject = new GameObject("Falling Lightning Placeholder", typeof(LineRenderer));
            boltObject.transform.SetParent(root.transform, false);
            boltObject.transform.position = boltPath[boltPath.Length - 1];
            bolt = boltObject.GetComponent<LineRenderer>();
            bolt.positionCount = 2;
            bolt.useWorldSpace = true;
            bolt.widthMultiplier = width;
            bolt.numCornerVertices = 3;
            bolt.numCapVertices = 3;
            bolt.startColor = new Color(1f, 1f, 1f, 1f);
            bolt.endColor = new Color(.48f, .84f, 1f, 1f);
            if (boltMaterial != null)
                bolt.sharedMaterial = boltMaterial;
            bolt.SetPosition(0, boltPath[0]);
            bolt.SetPosition(1, boltPath[0]);
            boltTrail = CreateBoltParticles("Arc Trail Particles", width * 1.8f, .18f, 160);
            impactSparks = CreateBoltParticles("Impact Spark Particles", width * 1.2f, .36f, 48);
            impactLight = boltObject.AddComponent<Light>();
            impactLight.type = LightType.Point;
            impactLight.color = new Color(.66f, .86f, 1f);
            impactLight.range = Mathf.Max(2f, halfHeight * .45f);
            impactLight.intensity = 0;
            boltElapsed = 0;
            impactPlayed = false;
        }

        ParticleSystem CreateBoltParticles(string name, float size, float lifetime, int maximum)
        {
            var obj = new GameObject(name, typeof(ParticleSystem));
            obj.transform.SetParent(boltObject.transform, false);
            var system = obj.GetComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0;
            main.startLifetime = lifetime;
            main.startSize = size;
            main.startColor = new Color(.78f, .94f, 1f);
            main.maxParticles = maximum;
            var emission = system.emission;
            emission.enabled = false;
            var renderer = obj.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (particleMaterial != null)
                renderer.sharedMaterial = particleMaterial;
            system.Play();
            return system;
        }

        void AdvanceBolt(float delta)
        {
            if (bolt == null || boltPath == null)
                return;
            boltElapsed += delta;
            var head = Mathf.Clamp01(boltElapsed / .16f) * (boltPath.Length - 1);
            var whole = Mathf.Min(Mathf.FloorToInt(head), boltPath.Length - 2);
            var tip = Vector3.Lerp(boltPath[whole], boltPath[whole + 1], head - whole);
            bolt.positionCount = whole + 2;
            for (var i = 0; i <= whole; i++)
                bolt.SetPosition(i, boltPath[i]);
            bolt.SetPosition(whole + 1, tip);
            if (boltElapsed <= .16f)
            {
                var trail = new ParticleSystem.EmitParams { position = tip, startColor = new Color(.8f, .96f, 1f) };
                boltTrail.Emit(trail, 3);
            }
            if (boltElapsed >= .16f && !impactPlayed)
            {
                impactPlayed = true;
                var target = boltPath[boltPath.Length - 1];
                var radius = camera.orthographic ? camera.orthographicSize :
                    Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) * camera.WorldToViewportPoint(target).z;
                for (var i = 0; i < 24; i++)
                {
                    var angle = i * Mathf.PI * 2f / 24f;
                    var direction = camera.transform.right * Mathf.Cos(angle) + camera.transform.up * Mathf.Sin(angle);
                    var spark = new ParticleSystem.EmitParams
                    {
                        position = target,
                        velocity = direction * (radius * (.18f + (i % 3) * .035f)),
                        startColor = new Color(.8f, .96f, 1f),
                    };
                    impactSparks.Emit(spark, 1);
                }
            }
            var fade = Mathf.Clamp01((.48f - boltElapsed) / .26f);
            bolt.startColor = new Color(1f, 1f, 1f, fade);
            bolt.endColor = new Color(.48f, .84f, 1f, fade);
            impactLight.intensity = impactPlayed ? fade * 2.5f : 0;
            if (boltElapsed < .52f)
                return;
            Release(boltObject);
            boltObject = null;
            bolt = null;
            boltTrail = null;
            impactSparks = null;
            impactLight = null;
            boltPath = null;
        }

        public void Dispose()
        {
            cameraData.renderPostProcessing = originalPostProcessing;
            cameraData.volumeLayerMask = originalVolumeMask;
            Release(root);
            foreach (var profile in seasons)
                Release(profile);
            Release(particleMaterial);
            Release(boltMaterial);
        }

        static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(value);
            else
#endif
                UnityEngine.Object.Destroy(value);
        }
    }
}
