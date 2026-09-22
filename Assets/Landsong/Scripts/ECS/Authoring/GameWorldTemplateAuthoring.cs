using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    /// <summary>Creates map-independent state on the simulation root.</summary>
    [DisallowMultipleComponent]
    public sealed class GameWorldTemplateAuthoring : MonoBehaviour
    {
        public sealed class Baker : Baker<GameWorldTemplateAuthoring>
        {
            public override void Bake(GameWorldTemplateAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Session { Phase = Phase.Day });
                AddComponent(entity, new GameClock { Turn = 1 });
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
