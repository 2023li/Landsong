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
        readonly Image flash;
        LineRenderer bolt;
        float flashAlpha;
        float boltRemaining;
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
            flash = new GameObject("Flash Image", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            flash.transform.SetParent(canvas.transform, false);
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
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = isRain ? 1.5f : 5f;
            main.startSpeed = 0;
            main.startSize = isRain ? .04f : .09f;
            main.startColor = isRain ? new Color(.7f, .8f, 1f, .65f) : new Color(1f, 1f, 1f, .9f);
            main.maxParticles = isRain ? 3000 : 1000;
            var emission = system.emission;
            emission.rateOverTime = isRain ? 450 : 75;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(40, 1, 40);
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(isRain ? -21f : -2f);
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
            SetParticles(rain, state.Weather == WeatherKind.Rain, paused);
            SetParticles(snow, state.Weather == WeatherKind.Snow, paused);
            var rainPosition = camera.transform.position + camera.transform.forward * 20 + Vector3.up * 12;
            rain.transform.position = snow.transform.position = rainPosition;
            foreach (var visual in visuals)
            {
                flashAlpha = .72f;
                AudioRuntime.Instance?.Play(PresentationCue.Thunder);
                if (visual.Kind != LightningVisualKind.Flash)
                    ShowBolt((Vector3)visual.Position);
            }
            var step = paused ? 0 : Mathf.Max(0, delta);
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0, step * 3.5f);
            flash.color = new Color(1, 1, 1, flashAlpha);
            boltRemaining -= step;
            if (bolt != null && boltRemaining <= 0)
            {
                Release(bolt.gameObject);
                bolt = null;
            }
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
            if (bolt != null)
                Release(bolt.gameObject);
            var obj = new GameObject("Lightning Bolt Placeholder", typeof(LineRenderer));
            obj.transform.SetParent(root.transform, false);
            bolt = obj.GetComponent<LineRenderer>();
            bolt.positionCount = 4;
            bolt.useWorldSpace = true;
            bolt.widthMultiplier = .12f;
            bolt.startColor = bolt.endColor = new Color(.9f, .95f, 1f, 1f);
            if (boltMaterial != null)
                bolt.sharedMaterial = boltMaterial;
            bolt.SetPosition(0, ground + new Vector3(-.3f, 15, .2f));
            bolt.SetPosition(1, ground + new Vector3(.45f, 10, -.2f));
            bolt.SetPosition(2, ground + new Vector3(-.25f, 5, .2f));
            bolt.SetPosition(3, ground + Vector3.up * .5f);
            boltRemaining = .35f;
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
