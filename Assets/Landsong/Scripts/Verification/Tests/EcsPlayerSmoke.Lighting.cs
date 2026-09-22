#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator NightLightingUi(EntityManager em, Entity root)
        {
            var host = FindFirstObjectByType<EcsGameHost>();
            Require(host != null && host.Visible && host.Sun.isActiveAndEnabled, "Runtime host owns an active directional light");
            var sun = host.Sun;
            var dayIntensity = sun.intensity;
            var dayRotation = sun.transform.rotation;
            var control = em.GetComponentData<SimulationControl>(root);
            control.Paused = 1;
            em.SetComponentData(root, control);
            var timing = em.GetComponentData<NightSettings>(root);
            var night = em.GetComponentData<NightRuntimeState>(root);
            night.Duration = timing.TotalNightSeconds;
            night.Kind = NightKind.Peaceful;
            em.SetComponentData(root, night);
            void Set(Phase phase, float time, float dawn = 0)
            {
                var state = em.GetComponentData<Session>(root);
                var clock = em.GetComponentData<GameClock>(root);
                state.Phase = phase;
                clock.PhaseTime = time;
                clock.DawnRemaining = dawn;
                clock.DawnSourceNightTime = -1;
                em.SetComponentData(root, state);
                em.SetComponentData(root, clock);
            }
            IEnumerator Frame(string name)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                if (Application.isEditor && name != null)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/lighting-" + name + ".png");
                    yield return new WaitForEndOfFrame();
                }
            }
            yield return Frame("day");
            Set(Phase.Deployment, timing.NightPreparationSeconds / 2);
            yield return Frame("sunset");
            Require(sun.intensity < dayIntensity && sun.intensity > dayIntensity * host.NightLighting.NightIntensityRatio && Quaternion.Angle(dayRotation, sun.transform.rotation) > 1, "Live Game sun dims and rotates during preparation");
            var pausedIntensity = sun.intensity;
            var pausedRotation = sun.transform.rotation;
            yield return new WaitForSecondsRealtime(.2f);
            Require(Mathf.Abs(sun.intensity - pausedIntensity) < .001f && Quaternion.Angle(pausedRotation, sun.transform.rotation) < .01f, "Live pause holds the sunset");
            Set(Phase.Night, timing.NightPreparationSeconds + 1);
            yield return Frame("night");
            Require(Mathf.Abs(sun.intensity - dayIntensity * host.NightLighting.NightIntensityRatio) < .001f, "Live night uses configured intensity ratio");
            Set(Phase.Retreat, timing.NightPreparationSeconds + timing.NightSeconds + timing.RetreatDelaySeconds);
            yield return Frame("closure");
            Require(Mathf.Abs(sun.intensity - dayIntensity * host.NightLighting.ClosureIntensityRatio) < .001f, "Live closure uses its brighter intensity");
            Set(Phase.Day, 0, timing.DawnSeconds / 2);
            yield return Frame("sunrise");
            Require(sun.intensity > dayIntensity * host.NightLighting.NightIntensityRatio && sun.intensity < dayIntensity, "Live interactive dawn raises the light gradually");
            Set(Phase.Day, 0);
            yield return Frame(null);
            Require(Mathf.Abs(sun.intensity - dayIntensity) < .001f && Quaternion.Angle(dayRotation, sun.transform.rotation) < .01f, "Live dawn restores the day baseline");
            Set(Phase.Retreat, night.Duration);
            // Exercise the actual dawn commit with a deployed soldier well away from the exit.
            Entity soldier = Entity.Null;
            using (var units = WorldQueries.Entities<Soldier>(em))
                foreach (var unit in units)
                    if (EntityState.Alive(em, unit) && em.GetComponentData<Soldier>(unit).Garrison != 0) { soldier = unit; break; }
            Require(soldier != Entity.Null, "Daytime return fixture has a garrison soldier");
            var actor = em.GetComponentData<Combatant>(soldier);
            var home = WorldQueries.Find(em, em.GetComponentData<Soldier>(soldier).Garrison);
            var placement = em.GetComponentData<BuildingPlacementState>(home);
            var grid = em.GetComponentData<GridData>(root);
            var probe = GridOps.Position(grid, placement.Cell + new Unity.Mathematics.int2(placement.Size.x, 0), new Unity.Mathematics.int2(1));
            Require(NavigationOps.TryNearestOpenOnSurface(em, root, probe, 12, placement.Surface, placement.Elevation, out var exit), "Daytime return fixture has a legal home exit");
            bool found = false;
            var start = exit;
            using (var reach = new NightSpatialOps.Reach(em, root, exit, actor.Profile.BodyRadius))
                foreach (var node in em.GetBuffer<SurfaceNavNode>(root))
                    if (node.Open != 0 && Unity.Mathematics.math.distance(exit, node.Position) > 10 && Unity.Mathematics.math.distance(exit, node.Position) < 14 && reach.Point(node.Position))
                    { start = node.Position; found = true; break; }
            Require(found, "Daytime return starts away from the home");
            actor.Deployed = 1;
            em.SetComponentData(soldier, actor);
            var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(soldier);
            transform.Position = start;
            em.SetComponentData(soldier, transform);
            em.SetComponentData(soldier, new VisualState { Visible = 1 });
            Set(Phase.Retreat, timing.NightPreparationSeconds + timing.NightSeconds + timing.ClosureCelebrationAt);
            em.SetComponentData(soldier, new VisualState { Visible = 1, Celebrating = (byte)NightEndPose.Celebrate });
            control.Paused = 0;
            em.SetComponentData(root, control);
            yield return new WaitForSecondsRealtime(.5f);
            control.Paused = 1;
            em.SetComponentData(root, control);
            yield return Frame("celebration");
            var animationView = em.GetComponentData<Landsong.Animation.SoldierAnimationState>(soldier).View;
            var animationBinding = em.GetComponentData<Landsong.Animation.SoldierAnimationBinding>(animationView);
            Require(em.GetBuffer<Rukhanka.AnimatorControllerLayerComponent>(animationBinding.Rig)[animationBinding.CelebrationLayer].weight == 1, "Live Rukhanka arms layer plays Victory during closure");
            var victoryLayer = em.GetBuffer<Rukhanka.AnimatorControllerLayerComponent>(animationBinding.Rig)[animationBinding.CelebrationLayer];
            Require(victoryLayer.rtd.srcState.id >= 0 && victoryLayer.rtd.srcState.id != victoryLayer.controller.Value.layers[victoryLayer.layerIndex].defaultStateIndex && victoryLayer.rtd.srcState.normalizedDuration > 0, "Live Victory state advances the actual clip");
            NightOps.Dawn(em, root);
            Require(em.GetComponentData<Session>(root).Phase == Phase.Day && em.HasComponent<DayReturnState>(soldier) && em.GetComponentData<Combatant>(soldier).Deployed != 0, "Dawn enters interactive day while soldiers remain outside");
            var hud = FindFirstObjectByType<UI_GamePanel_Hud>();
            yield return WaitFor(() => hud.Status.text.Contains("黎明") && !hud.MoonProgress.gameObject.activeSelf && !hud.Advance.gameObject.activeSelf, "Dawn HUD belongs to day and excludes the night progress");
            yield return WaitFor(() => em.GetBuffer<Rukhanka.AnimatorControllerLayerComponent>(animationBinding.Rig)[animationBinding.CelebrationLayer].weight == 0, "Dawn releases the celebration arms for return locomotion");
            var remaining = em.GetComponentData<DayReturnState>(soldier).Remaining;
            Require(remaining >= 15 && remaining <= 30, "Runtime return timeout is authored as 15 to 30 seconds");
            yield return new WaitForSecondsRealtime(.2f);
            Require(em.GetComponentData<DayReturnState>(soldier).Remaining == remaining, "Pause freezes the daytime timeout");
            control.Paused = 0;
            em.SetComponentData(root, control);
            yield return WaitFor(() => Unity.Mathematics.math.distance(start, EntityState.Position(em, soldier)) > .2f && em.GetComponentData<Combatant>(soldier).Deployed != 0, "DBP and navigation move the returning soldier during daytime");
            Require(em.GetComponentData<Session>(root).Phase == Phase.Day && em.GetComponentData<GameClock>(root).DawnRemaining > 0 && sun.intensity < dayIntensity, "Physical return runs during the interactive sunrise");
            yield return WaitFor(() => !em.HasComponent<DayReturnState>(soldier), "Background return completes on arrival or individual timeout", timeoutSeconds: 35);
            Require(em.GetComponentData<Combatant>(soldier).Deployed == 0 && em.GetComponentData<VisualState>(soldier).Visible == 0, "Completed return hides the visual but retains soldier identity");
            yield return WaitFor(() => em.GetComponentData<GameClock>(root).DawnRemaining == 0, "Live dawn independently completes", timeoutSeconds: 35);
            Require(Mathf.Abs(sun.intensity - dayIntensity) < .001f, "Completed dawn restores daylight regardless of soldier return");
            yield return WaitFor(() => hud.Advance.gameObject.activeSelf && hud.Advance.interactable && !hud.Status.text.Contains("黎明"), "Ordinary day reopens next-night advancement after sunrise");

        }
    }
}
#endif
