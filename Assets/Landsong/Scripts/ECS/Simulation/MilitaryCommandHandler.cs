using Unity.Entities;

namespace Landsong.ECS
{
    public static class MilitaryCommandHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Command command, out ResultCode result)
        {
            switch (command.Kind)
            {
                case CommandKind.RecruitSoldier: result = MilitaryOps.Recruit(em, root, command, false); break;
                case CommandKind.RecruitHero: result = MilitaryOps.Recruit(em, root, command, true); break;
                case CommandKind.AssignSoldier: result = MilitaryOps.Assign(em, root, command); break;
                case CommandKind.UnassignSoldier: command.Other = 0; result = MilitaryOps.Assign(em, root, command); break;
                case CommandKind.SwapSoldiers: result = MilitaryOps.Swap(em, command); break;
                case CommandKind.RenameSoldier: case CommandKind.DismissSoldier:
                    result = MilitaryOps.SoldierCommand(em, root, command); break;
                case CommandKind.FillGarrison: result = MilitaryOps.Fill(em, root, command.Target); break;
                default: result = ResultCode.Unavailable; return false;
            }
            return true;
        }
    }
}
