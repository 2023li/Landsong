using Unity.Collections;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Named application boundary. Command remains a compact ECS queue record; callers do not
    // have to remember which generic field carries a quote, confirmation token or slot stamp.
    public static partial class CommandRequests
    {
        public static Command RecruitWorkers(ulong buildingId, int quantity, int expectedUnitCost) => new Command
        { Kind = CommandKind.RecruitWorkerAtQuotedCost, Target = buildingId, Amount = quantity, Argument = expectedUnitCost };
        public static Command ChangeWorkers(ulong buildingId, int delta) => new Command
        { Kind = CommandKind.Workers, Target = buildingId, Amount = delta };
        public static Command SetWorkforceBudget(ulong buildingId, int budget) => new Command
        { Kind = CommandKind.WorkforceBudget, Target = buildingId, Amount = budget, Argument = 0 };
        public static Command AdjustWorkforceBudget(ulong buildingId, int delta) => new Command
        { Kind = CommandKind.WorkforceBudget, Target = buildingId, Amount = delta, Argument = 1 };
        public static Command Harvest(ulong buildingId) => new Command { Kind = CommandKind.Harvest, Target = buildingId };
        public static Command Advance(ulong reviewedLossToken = 0) => new Command
        { Kind = CommandKind.Advance, Argument = reviewedLossToken == 0 ? 0 : 1, Other = reviewedLossToken };
        public static Command Pause(bool paused) => new Command { Kind = CommandKind.Pause, Argument = paused ? 1 : 2 };
        public static Command Build(int definition, float3 position, int rotation) => new Command
        { Kind = CommandKind.Build, Definition = definition, Position = position, Argument = rotation };
        public static Command PlanResearch(int definition, string expectedPlan) => new Command
        { Kind = CommandKind.PlanResearch, Definition = definition, Text = new FixedString128Bytes(expectedPlan ?? "") };
        public static Command CreateSave(string label) => new Command
        { Kind = CommandKind.Save, Argument = 1, Text = new FixedString128Bytes(label ?? "") };
        public static Command QuickSave() => new Command { Kind = CommandKind.Save };
        public static Command OverwriteSave(string slotId, ulong expectedStamp) => new Command
        { Kind = CommandKind.Save, Argument = 2, Text = new FixedString128Bytes(slotId ?? ""), Other = expectedStamp };
        public static Command LoadSave(string slotId, bool backup = false) => new Command
        { Kind = CommandKind.Load, Argument = backup ? 1 : 0, Text = new FixedString128Bytes(slotId ?? "") };
        public static Command AssignSoldier(ulong soldierId, ulong buildingId, int realSlot) => new Command
        { Kind = CommandKind.AssignSoldier, Target = soldierId, Other = buildingId, Argument = realSlot };
    }
}
