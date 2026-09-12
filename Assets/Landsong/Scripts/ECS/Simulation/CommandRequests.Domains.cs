using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // A displayed inventory source is immutable while selecting a destination or confirming loss.
    // It is a transient application request, never a persisted ECS state component.
    public readonly struct InventorySelection
    {
        public bool Pending { get; }
        public ulong Provider { get; }
        public int Slot { get; }
        public int Item { get; }
        public int Quantity { get; }
        public string ExpectedInventory { get; }
        public InventorySelection(bool pending, ulong provider, int slot, int item, int quantity, string expectedInventory)
        { Pending = pending; Provider = provider; Slot = slot; Item = item; Quantity = quantity; ExpectedInventory = expectedInventory; }
    }

    public static partial class CommandRequests
    {
        static FixedString128Bytes Text(string value) => new FixedString128Bytes(value ?? "");
        public static Command BuildRoad(int definition, float3 start, float3 end) => new Command
        { Kind = CommandKind.BuildRoad, Definition = definition, Position = start, EndPosition = end };
        public static Command MoveBuilding(ulong buildingId, int definition, float3 position, int rotation) => new Command
        { Kind = CommandKind.MoveBuilding, Target = buildingId, Definition = definition, Position = position, Argument = rotation };
        public static Command ConfirmBuildingChange(CommandKind kind, ulong buildingId)
        {
            if (kind != CommandKind.Upgrade && kind != CommandKind.Repair && kind != CommandKind.Demolish)
                throw new ArgumentOutOfRangeException(nameof(kind), "Only reviewed upgrade, repair and demolition changes use this request.");
            return new Command { Kind = kind, Target = buildingId, Argument = 1 };
        }
        public static Command RenameBuilding(ulong buildingId, string name) => new Command
        { Kind = CommandKind.Rename, Target = buildingId, Text = Text(BuildingOps.SanitizeName(name)) };
        public static Command ChangeBuildingSkin(ulong buildingId, string skinId) => new Command
        { Kind = CommandKind.ChangeBuildingSkin, Target = buildingId, Text = Text(skinId) };
        public static Command PlantCrop(ulong buildingId, int cropDefinition) => new Command
        { Kind = CommandKind.Plant, Target = buildingId, Definition = cropDefinition };
        public static Command SetAutoHarvest(ulong buildingId, bool enabled) => new Command
        { Kind = CommandKind.AutoHarvest, Target = buildingId, Amount = enabled ? 1 : 0 };
        public static Command SetOffering(ulong buildingId, bool enabled) => new Command
        { Kind = CommandKind.Offering, Target = buildingId, Amount = enabled ? 1 : 0 };
        public static Command SetWorkforceTarget(ulong buildingId, int workforce) => new Command
        { Kind = CommandKind.WorkforceTarget, Target = buildingId, Amount = workforce };

        public static Command SortInventory(string expectedInventory) => new Command
        { Kind = CommandKind.SortInventory, Text = Text(expectedInventory) };
        public static Command StorePending(string expectedInventory) => new Command
        { Kind = CommandKind.StorePending, Text = Text(expectedInventory) };
        public static Command TransferInventory(InventorySelection source, ulong destinationProvider, int destinationSlot) => new Command
        {
            Kind = source.Pending ? CommandKind.StorePendingSlot : CommandKind.MoveInventory,
            Target = source.Provider, SourceSlot = source.Slot, Other = destinationProvider, DestinationSlot = destinationSlot,
            Definition = source.Item, Amount = source.Quantity, Text = Text(source.ExpectedInventory)
        };
        public static Command DiscardInventory(InventorySelection source, int quantity) => new Command
        {
            Kind = source.Pending ? CommandKind.DiscardPending : CommandKind.DiscardSlot,
            Target = source.Provider, SourceSlot = source.Slot, Definition = source.Item, Amount = quantity, Text = Text(source.ExpectedInventory)
        };

        public static Command SubmitQuest(ulong questId, int itemDefinition, int quantity, string requirementKey, string expectedQuote) => new Command
        { Kind = CommandKind.SubmitQuest, Target = questId, Definition = itemDefinition, Amount = quantity, Text = Text(requirementKey + "|" + expectedQuote) };
        public static Command RecruitQuest(ulong sourceId, int realOfferSlot) => new Command
        { Kind = CommandKind.RecruitQuest, Target = sourceId, Argument = realOfferSlot };
        public static Command StartExpedition(ulong sourceId, ulong captainId, int destination, ExpeditionQuote reviewedQuote) => new Command
        { Kind = CommandKind.StartExpedition, Target = sourceId, Other = captainId, Definition = destination, Amount = reviewedQuote.Crew, Text = Text(ExpeditionOps.Payload(reviewedQuote)) };
        public static Command TrackQuest(ulong questId) => new Command { Kind = CommandKind.TrackQuest, Target = questId, Argument = 1 };
        public static Command StopQuestTracking(ulong expectedQuestId = 0) => new Command { Kind = CommandKind.TrackQuest, Target = expectedQuestId, Argument = 2 };
        public static Command ResumeAutoQuestTracking() => new Command { Kind = CommandKind.TrackQuest };
        public static Command QueueResearch(int definition) => new Command { Kind = CommandKind.Research, Definition = definition };
        public static Command CancelResearch(int definition) => new Command { Kind = CommandKind.CancelResearch, Definition = definition };
        public static Command SelectPolicy(int definition, bool selected) => new Command
        { Kind = selected ? CommandKind.SelectPolicy : CommandKind.CancelPolicy, Definition = definition };

        public static Command MoveHero(float3 destination) => new Command { Kind = CommandKind.MoveHero, Position = destination };
        public static Command RecruitSoldiers(ulong buildingId, int soldierDefinition, int quantity) => new Command
        { Kind = CommandKind.RecruitSoldier, Target = buildingId, Definition = soldierDefinition, Amount = quantity, Argument = 1 };
        public static Command RecallGarrison(ulong buildingId, bool cancel = false) => new Command
        { Kind = CommandKind.RecallGarrison, Target = buildingId, Argument = cancel ? 1 : 0 };
        public static Command DismissSoldier(ulong soldierId) => new Command { Kind = CommandKind.DismissSoldier, Target = soldierId, Argument = 1 };
        public static Command SetSoldierAttention(ulong soldierId, bool watched) => new Command
        { Kind = CommandKind.SetSoldierAttention, Target = soldierId, Argument = watched ? 1 : 0 };
        public static Command RenameSoldier(ulong soldierId, string name) => new Command
        { Kind = CommandKind.RenameSoldier, Target = soldierId, Text = Text(BuildingOps.SanitizeName(name)) };
        public static Command ResolveMarriage(ulong personId, ulong mateId, int expectedRequestTurn, int decision) => new Command
        { Kind = CommandKind.ResolveMarriage, Target = personId, Other = mateId, Definition = expectedRequestTurn, Argument = decision };
        public static Command CustomizePortrait(ulong personId, PortraitDNA draft) => new Command
        { Kind = CommandKind.CustomizePortrait, Target = personId, Other = draft.Seed, Text = Text(PortraitOps.Payload(draft)) };
        public static Command RefusePersonRequest(ulong personId, int requestTurn) => new Command
        { Kind = CommandKind.RefusePersonRequest, Target = personId, Definition = requestTurn };
        public static Command AssignTalent(ulong personId, int slotDefinition) => new Command
        { Kind = CommandKind.AssignTalent, Target = personId, Definition = slotDefinition };
        public static Command RoyalVisit(ulong personId, bool guarded) => new Command
        { Kind = CommandKind.RoyalVisit, Target = personId, Argument = guarded ? 1 : 2 };
        public static Command RefuseRoyalVisit() => new Command { Kind = CommandKind.RoyalVisit };
        public static Command ExecuteHeir(ulong personId) => new Command { Kind = CommandKind.ExecuteHeir, Target = personId, Argument = 1 };
        public static Command ReadIntelligence(ulong expectedFingerprint) => new Command { Kind = CommandKind.ReadIntelligence, Other = expectedFingerprint };
        public static Command SetIntelligenceMode(bool enabled) => new Command { Kind = CommandKind.IntelligenceMode, Argument = enabled ? 1 : 0 };
        public static Command SetNightSpeed(int speed) => new Command { Kind = CommandKind.NightSpeed, Amount = speed };
    }
}
