using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    internal static class SessionRequestActions
    {
        internal static ResultCode Pause(EntityManager em, Entity root, PauseDecision decision)
        {
            if (decision > PauseDecision.Resume)
                return ResultCode.InvalidTarget;
            var control = em.GetComponentData<SimulationControl>(root);
            control.Paused = decision == PauseDecision.Pause ? (byte)1 : decision == PauseDecision.Resume ? (byte)0 : (byte)(control.Paused == 0 ? 1 : 0);
            em.SetComponentData(root, control);
            return ResultCode.Success;
        }

        internal static ResultCode Intelligence(EntityManager em, Entity root, bool enabled)
        {
            if (enabled && em.GetComponentData<Session>(root).Phase == Phase.GameOver)
                return ResultCode.WrongPhase;
            em.SetComponentData(root, new IntelligenceModeState { Enabled = (byte)(enabled ? 1 : 0) });
            return ResultCode.Success;
        }

        internal static ResultCode Advance(EntityManager em, Entity root, AdvanceRequest request)
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase == Phase.Day)
            {
                try
                {
                    return NightOps.Begin(em, root, request.ReviewedLossToken != 0, request.ReviewedLossToken);
                }
                catch (System.Exception error)
                {
                    UnityEngine.Debug.LogException(error);
                    return ResultCode.PreparationFailed;
                }
            }

            if (phase == Phase.Report)
            {
                NightOps.Dawn(em, root);
                return ResultCode.Success;
            }

            return NightOps.EndEarly(em, root);
        }

        internal static ResultCode Retry(EntityManager em, Entity root, bool dusk, bool endDynasty)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.GameOver)
                return ResultCode.WrongPhase;
            if (!endDynasty && (CourtOps.State(em, root).Extinction != 0 || em.HasComponent<RecoveryState>(root) && em.GetComponentData<RecoveryState>(root).Extinction != 0))
                return ResultCode.Unavailable;
            em.SetComponentData(root, new PersistenceGate { CheckpointPending = 1 });
            SimulationEvents.Emit(em, root, endDynasty ? EventKind.EndDynasty : EventKind.Retry, "", amount: dusk ? 1 : 0);
            return ResultCode.Success;
        }

        internal static ResultCode Save(EntityManager em, Entity root, FixedString128Bytes labelOrSlot, ulong stamp, int mode)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return ResultCode.WrongPhase;
            em.SetComponentData(root, new PersistenceGate { CheckpointPending = 1 });
            SimulationEvents.Emit(em, root, EventKind.Save, labelOrSlot, target: stamp, amount: mode);
            return ResultCode.Success;
        }

        internal static ResultCode Load(EntityManager em, Entity root, LoadSaveRequest request)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return ResultCode.WrongPhase;
            em.SetComponentData(root, new PersistenceGate { CheckpointPending = 1 });
            SimulationEvents.Emit(em, root, EventKind.Load, request.SlotId, amount: request.Backup ? 1 : 0);
            return ResultCode.Success;
        }

        internal static ResultCode NightSpeed(EntityManager em, Entity root, int speed)
        {
            var night = em.GetComponentData<NightRuntimeState>(root);
            if (em.GetComponentData<Session>(root).Phase != Phase.Night || night.Kind != NightKind.Peaceful || speed != 1 && speed != 2)
                return ResultCode.WrongPhase;
            night.Speed = (byte)speed;
            em.SetComponentData(root, night);
            return ResultCode.Success;
        }
    }
}
