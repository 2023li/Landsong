using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct GameClock : IComponentData
    {
        public int Turn;
        public float Time;
        public float PhaseTime;
        // Dawn is an interactive part of the new day, independent of the night clock.
        public float DawnRemaining;
        // -1 means normal closure; otherwise stores the skipped night time for reproducible sunrise.
        public float DawnSourceNightTime;
    }
}
