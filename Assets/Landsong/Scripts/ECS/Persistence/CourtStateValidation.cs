using System.Linq;
using Landsong.ECS.Definitions;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Persistence
{
    public static class CourtStateValidation
    {
        public static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot s)
        {
            var c = s.Court;
            if (s.Policies == null || s.CourtLog == null || s.CourtLog.Length > 64 || c.Extinction > 1 || c.LegacySeverity > 2 || c.LastSettledTurn < 0 || c.LastSettledTurn > s.Clock.Turn || !math.isfinite(c.Disorder) || c.Disorder < 0 || c.Disorder > 1)
                throw new InvalidDataException("Invalid court state");
            foreach (var value in new[]
            {
                c.TemporaryAttack,
                c.TemporaryProduction,
                c.LegacyAttack,
                c.LegacyProduction
            }

            )
                if (!math.isfinite(value) || value < -1 || value > 1)
                    throw new InvalidDataException("Invalid political effect");
            var people = new Dictionary<ulong, PersonSnapshot>();
            var occupied = new HashSet<TalentSlotId>();
            int monarchs = 0;
            ulong monarchId = 0;
            foreach (var record in s.Records.OfType<PersonSnapshot>())
            {
                var p = record.Royal;
                people.Add(record.Identity.Id, record);
                if (p.Gender != PersonGender.Male && p.Gender != PersonGender.Female)
                    throw new InvalidDataException("Invalid persistent person gender");
                if (p.EverMonarch > 1 || p.MarriageCooldownUntil < 0 || p.MarriageRequestTurn < 0 || p.MarriageRequestTurn > s.Clock.Turn || (p.RequestedSpouse == 0) != (p.MarriageRequestMonarch == 0) || (p.RequestedSpouse == 0) != (p.MarriageRequestTurn == 0))
                    throw new InvalidDataException("Invalid marriage request or monarch history");
                if (p.Role > 4 || p.Alive > 1 || p.Retired > 1 || p.FateUsed > 1 || p.TaskClaimed > 1 || p.Evidence > 1 || p.Age < 0 || p.Generation < 0 || p.LastGiftTurn > s.Clock.Turn || p.Affection < 0 || p.Affection > 100 || p.FateUntil < 0 || p.VisitUntil < 0 || p.FateUntil > 0 && p.FateUsed == 0 || !math.isfinite(p.Influence) || p.Influence < 0 || p.Influence > 100 || !math.isfinite(p.Growth) || p.Growth < 0 || !math.isfinite(p.Ambition) || p.Ambition < 0 || p.Ambition > 1 || !math.isfinite(p.Grievance) || p.Grievance < 0 || p.Grievance > 1)
                    throw new InvalidDataException("Invalid person state");
                if (p.Alive != 0 && p.Role == 0)
                {
                    monarchs++;
                    monarchId = record.Identity.Id;
                }

                if (record.Talent.HasValue && (record.Talent.Value.AssignedTurns < 0 || record.Talent.Value.WageTurn < 0 || record.Talent.Value.WageTurn > s.Clock.Turn || record.Talent.Value.LastBenefitTurn < 0 || record.Talent.Value.LastBenefitTurn > s.Clock.Turn || record.Talent.Value.Slot.IsValid && record.Talent.Value.Recruited == 0 || !TalentDefinitions.IsValid(em, root, record.TalentDefinition)))
                    throw new InvalidDataException("Invalid talent identity/payment turn");
                if (record.Talent.HasValue)
                {
                    var t = record.Talent.Value;
                    if (t.Paid > 1 || t.Recruited > 1 || t.Level < 1 || t.Experience < 0 || (p.Role == 0 || p.Role == 1 || p.Role == 3 || p.Alive == 0 || p.Retired != 0) && (t.Paid != 0 || t.Slot.IsValid))
                        throw new InvalidDataException("Ineligible talent assignment");
                    if (t.Slot.IsValid && (!occupied.Add(t.Slot) || !TalentSlotDefinitions.IsValid(em, root, t.Slot)))
                        throw new InvalidDataException("Invalid talent slot");
                }

                var traits = new HashSet<RoyalTraitId>();
                foreach (var t in record.Traits)
                    if (!traits.Add(t.Definition) || !RoyalTraitDefinitions.IsValid(em, root, t.Definition) || t.Revealed > 1 || t.Active > 1 || t.Active > t.Revealed)
                        throw new InvalidDataException("Invalid trait");
            }

            if (monarchs > 1 || c.Extinction != 0 && monarchs != 0)
                throw new InvalidDataException("Invalid reigning monarch count");
            foreach (var r in people.Values)
            {
                var tokens = new HashSet<int>();
                bool activeRequest = false;
                foreach (var request in r.PersonRequests)
                {
                    bool active = request.Status == PersonRequestStatus.Pending || request.Status == PersonRequestStatus.Travelling;
                    if (request.Kind != PersonRequestKind.Expedition || request.Status > PersonRequestStatus.Cancelled || request.CreatedTurn < 1 || request.CreatedTurn > s.Clock.Turn || !tokens.Add(request.CreatedTurn) || active && activeRequest || active && r.Royal.Alive == 0 || (active ? request.ResolvedTurn != 0 : request.ResolvedTurn < request.CreatedTurn || request.ResolvedTurn > s.Clock.Turn))
                        throw new InvalidDataException("Invalid person request state");
                    activeRequest |= active;
                    if (request.Status == PersonRequestStatus.Travelling)
                    {
                        var journey = s.Records.OfType<ExpeditionSnapshot>().FirstOrDefault(x => x.Identity.Id == request.Journey);
                        if (request.Journey == 0 || journey == null || journey.Expedition.Status != ExpeditionStatus.Travelling || journey.Expedition.Captain != r.Identity.Id)
                            throw new InvalidDataException("Missing or mismatched person request expedition");
                    }
                    else if (request.Journey != 0)
                        throw new InvalidDataException("Unexpected person request expedition");
                }

                var p = r.Royal;
                foreach (var parent in new[]
                {
                    p.Parent,
                    p.SecondParent
                }

                )
                    if (parent != 0 && (!people.TryGetValue(parent, out var ancestor) || ancestor.Royal.Generation >= p.Generation))
                        throw new InvalidDataException("Missing/cyclic parent");
                if (p.RequestedSpouse != 0 && (p.RequestedSpouse == r.Identity.Id || !people.ContainsKey(p.RequestedSpouse) || !people.ContainsKey(p.MarriageRequestMonarch)))
                    throw new InvalidDataException("Missing marriage request participant");
                if (p.Spouse != 0 && (!people.TryGetValue(p.Spouse, out var spouse) || spouse.Royal.Spouse != r.Identity.Id || p.Spouse == r.Identity.Id))
                    throw new InvalidDataException("Invalid spouse link");
            }

            if (c.Crown != 0 && (!people.TryGetValue(c.Crown, out var crown) || crown.Royal.Alive == 0 || crown.Royal.Retired != 0))
                throw new InvalidDataException("Invalid crown prince");
            bool Descendant(ulong id, ulong ancestor)
            {
                if (id == 0 || id == ancestor || !people.TryGetValue(id, out var record))
                    return false;
                var p = record.Royal;
                return p.Parent == ancestor || p.SecondParent == ancestor || Descendant(p.Parent, ancestor) || Descendant(p.SecondParent, ancestor);
            }

            if (c.Crown != 0 && (monarchId == 0 || !Descendant(c.Crown, monarchId)))
                throw new InvalidDataException("Crown is not a direct descendant");
            if (c.LegacyFounder != 0 && !people.ContainsKey(c.LegacyFounder))
                throw new InvalidDataException("Missing legacy founder");
            var policyLayers = new HashSet<(PolicyGroupId, int)>();
            foreach (var policy in s.Policies)
            {
                if (!PolicyDefinitions.IsValid(em, root, policy.Definition))
                    throw new InvalidDataException("Unknown policy");
                ref var d = ref PolicyDefinitions.Get(em, root, policy.Definition);
                if (!policyLayers.Add((d.PolicyGroup, d.PolicyTier)))
                    throw new InvalidDataException("Invalid/duplicate policy layer");
            }

            var captains = new HashSet<ulong>();
            foreach (var r in s.Records.OfType<ExpeditionSnapshot>())
                if (r.Expedition.Captain != 0)
                {
                    var id = r.Expedition.Captain;
                    if (!people.ContainsKey(id) || r.Expedition.Status == ExpeditionStatus.Travelling && (!captains.Add(id) || people[id].Royal.VisitUntil > 0))
                        throw new InvalidDataException("Invalid royal captain");
                }
        }
    }
}
