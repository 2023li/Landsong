#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Landsong.ECS.Persistence;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator CombatUi(EcsGameView view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root); var cameraPosition = view.Camera.transform.position; var cameraSize = view.Camera.orthographicSize;
            var grid = em.GetComponentData<GridData>(root); var occupied = em.GetBuffer<Occupancy>(root); int2 center = default; bool found = false;
            for (int y = grid.Value.Value.Min.y + 10; y < grid.Value.Value.Min.y + grid.Value.Value.Size.y - 10 && !found; y++) for (int x = grid.Value.Value.Min.x + 10; x < grid.Value.Value.Min.x + grid.Value.Value.Size.x - 10; x++)
            { bool open = true; for (int dy = -7; dy <= 7 && open; dy++) for (int dx = -7; dx <= 7; dx++) if (!GridOps.Traversable(grid, occupied, new int2(x + dx, y + dy))) { open = false; break; } if (open) { center = new int2(x, y); found = true; break; } }
            Require(found, "11C actual battle arena exists"); var point = GridOps.Position(grid, center, new int2(1)) + new float3(0, .5f, 0);
            Entity core = Entity.Null; using (var sites = Sim.Entities<Building>(em)) foreach (var site in sites) if (em.GetComponentData<BuildingStats>(site).IsCore != 0) core = site;
            ulong home = em.GetComponentData<Identity>(core).Id;
            var friendly = Sim.Spawn(em, root, Sim.FirstDefinition(em, root, ContentKind.Soldier), point, false); MilitaryOps.ConfigureCombatant(em, root, friendly, 0, false, true, home, point);
            var enemy = Sim.Spawn(em, root, Sim.FindDefinition(em, root, "boss"), point + new float3(4, 0, 0), false); MilitaryOps.ConfigureCombatant(em, root, enemy, 1, false, true, home, point + new float3(4, 0, 0));
            em.SetComponentData(friendly, new Health { Maximum = 10000, Current = 10000 }); em.SetComponentData(enemy, new Health { Maximum = 10000, Current = 10000 });
            var s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; s.NightKind = NightKind.Invasion; s.NightDuration = 120; s.PhaseTime = 0; s.CheckpointPending = 0; em.SetComponentData(root, s); em.GetBuffer<NightWave>(root).Clear();
            var ray = new Ray(view.Camera.transform.position, view.Camera.transform.forward); var plane = new Plane(Vector3.up, (Vector3)point); if (plane.Raycast(ray, out float distance)) view.Camera.transform.position += (Vector3)point - ray.GetPoint(distance);
            view.Camera.orthographicSize = 10; view.OpenPanel("建筑"); Entity WarningShot() { using var shots = Sim.Entities<Projectile>(em); foreach (var e in shots) { var p = em.GetComponentData<Projectile>(e); if (p.Mode == ProjectileMode.Ground && p.Warning > .2f) return e; } return Entity.Null; }
            yield return WaitFor(() => WarningShot() != Entity.Null, "Actual DBP acquires unit and launches telegraphed boss projectile");
            view.Send(CommandKind.Pause); yield return WaitFor(() => em.GetComponentData<Session>(root).Paused != 0, "11C pause command accepted");
            var shot = WarningShot(); Require(shot != Entity.Null, "Ground projectile warning still visible on pause"); float warning = em.GetComponentData<Projectile>(shot).Warning;
            yield return new WaitForSecondsRealtime(.2f); Require(em.Exists(shot) && em.GetComponentData<Projectile>(shot).Warning == warning, "Real player loop freezes projectile warning");
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/combat-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            view.Send(CommandKind.Pause); yield return WaitFor(() => em.GetComponentData<Session>(root).Paused == 0, "11C resume command accepted");
            yield return WaitFor(() => em.GetComponentData<Health>(friendly).Current < 10000, "Actual telegraphed projectile applies damage via ECS impact chain");
            Require(em.GetComponentData<Combatant>(friendly).Participated != 0, "Impact feeds persistent military participation");
            yield return WaitFor(()=>view.GetComponent<WorldPresentationView>().EffectCount>0,"15 actual projectile damage emits and consumes spatial Hit cue");
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); view.Camera.transform.position = cameraPosition; view.Camera.orthographicSize = cameraSize; view.OpenPanel("建筑"); yield return null;
        }
    }
}
#endif
