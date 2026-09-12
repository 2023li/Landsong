using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Persistence
{
    public static class InvitationStateValidation
    {
        public static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot snapshot)
            => Validate(em, root, snapshot, false);
        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot snapshot, bool deferQuestContainerReconciliation)
        {
            if (snapshot.Session.ExpeditionPenaltyStacks < 0 || snapshot.Session.ExpeditionPenaltyUntil < 0) throw new InvalidDataException("Invalid expedition penalty");
            var containers = new HashSet<(ulong,int)>();
            var travelling = new HashSet<ulong>(); var offered = new HashSet<(ulong,int)>();
            foreach (var r in snapshot.Records)
            {
                if (r.Supplies == null || r.ExpeditionHistory == null) throw new InvalidDataException("Missing expedition buffers");
                var history = new HashSet<int>(); foreach (var entry in r.ExpeditionHistory) if ((r.Mask & 2) == 0 || !history.Add(entry.Definition) || !Sim.ValidDefinition(em, root, entry.Definition) || Sim.Definition(em, root, entry.Definition).Kind != ContentKind.Expedition) throw new InvalidDataException("Invalid per-site expedition history");
                if ((r.Mask & 2) != 0)
                {
                    if (r.Building.MarketLifetimeValue < 0) throw new InvalidDataException("Invalid market lifetime value");
                    var keys = new HashSet<(int,int)>(); foreach (var slot in r.Offers) if (slot.Type < 0 || slot.Type > 3 || slot.Index < 0 || slot.NextTurn < 0 || !keys.Add((slot.Type, slot.Index))) throw new InvalidDataException("Invalid quest source slots");
                }
                if ((r.Mask & 16) != 0 && r.Quest.Status == QuestStatus.Offered && !offered.Add((r.Quest.Source, r.Quest.Slot))) throw new InvalidDataException("Duplicate offered quest in source slot");
                if ((r.Mask & 16) != 0)
                {
                    var q=r.Quest; var bound=q.Status==QuestStatus.Active || q.Status==QuestStatus.Completed;
                    if(bound)
                    {
                        var provider=System.Array.Find(snapshot.Records,b=>(b.Mask&2)!=0 && b.Identity.Id==q.Container);
                        // Duplicate authority is never a repairable capacity change. A disappeared
                        // provider or invalid slot can only be deferred inside night-entry staging,
                        // where ReconcileQuestContainers removes it before strict revalidation.
                        if(!containers.Add((q.Container,q.ContainerSlot)))throw new InvalidDataException("Duplicate quest container");
                        if(provider==null || q.ContainerSlot<0)
                        {
                            if(!deferQuestContainerReconciliation)throw new InvalidDataException("Invalid quest container");
                        }
                        else
                        {
                            var capacity=0;var definition=Sim.Definition(em,root,provider.Identity.Definition);
                            for(var i=0;i<definition.RuleCount;i++){var rule=Sim.GetRule(em,root,definition.RuleStart+i);if(rule.Kind==RuleKind.QuestCapacity && (rule.Level==0 || rule.Level==provider.Building.Level))capacity+=rule.Amount;}
                            if(q.ContainerSlot>=capacity && !deferQuestContainerReconciliation)throw new InvalidDataException("Quest container slot exceeds configured capacity");
                        }
                    }
                    else if(q.Container!=0 || q.ContainerSlot!=0)throw new InvalidDataException("Unexpected quest container");
                }
                if ((r.Mask & 32) == 0) continue;
                var e = r.Expedition;
                if (!Sim.ValidDefinition(em, root, r.Identity.Definition) || Sim.Definition(em, root, r.Identity.Definition).Kind != ContentKind.Expedition || (byte)e.Status > (byte)ExpeditionStatus.Failure || e.Crew < 1 || e.SourceLevel < 1 || e.Departure < 1 || e.Arrival <= e.Departure || e.Casualties < 0 || e.Casualties > e.Crew || e.SubsidyRequired < 0 || e.SubsidyPaid < 0 || e.SubsidyPaid > e.SubsidyRequired || e.PenaltyStacks < 0 || !math.isfinite(e.SuccessChance) || e.SuccessChance < 0 || e.SuccessChance > 1 || !math.isfinite(e.RewardBonus) || e.RewardBonus < 0) throw new InvalidDataException("Invalid expedition departure/result snapshot");
                if (e.Status == ExpeditionStatus.Travelling)
                {
                    var exists = false; foreach (var b in snapshot.Records) if ((b.Mask & 2) != 0 && b.Identity.Id == e.Site) exists = true;
                    if (!exists || !travelling.Add(e.Site) || e.Casualties != 0 || e.SubsidyPaid != 0) throw new InvalidDataException("Invalid travelling expedition source");
                }
                if (r.Supplies == null) throw new InvalidDataException("Missing expedition supplies");
                var items = new HashSet<int>(); foreach (var supply in r.Supplies) if (supply.Amount < 0 || !items.Add(supply.Item) || !Sim.ValidDefinition(em, root, supply.Item) || Sim.Definition(em, root, supply.Item).Kind != ContentKind.Item) throw new InvalidDataException("Invalid expedition supply snapshot");
            }
        }
    }
}
