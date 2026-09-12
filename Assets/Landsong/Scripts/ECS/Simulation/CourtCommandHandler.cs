using Unity.Entities;

namespace Landsong.ECS
{
    public static class CourtCommandHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity target, Command command, out ResultCode result)
        {
            switch (command.Kind)
            {
                case CommandKind.RecruitTalent: case CommandKind.AssignTalent: case CommandKind.DismissTalent: case CommandKind.RefreshTalents:
                    result = DynastyOps.TalentCommand(em, root, command); break;
                case CommandKind.Abdicate: result = DynastyOps.Abdicate(em, root, target); break;
                case CommandKind.DesignateHeir: result = CourtOps.Designate(em, root, target); break;
                case CommandKind.ExecuteHeir: result = CourtOps.Execute(em, root, target, command.Argument == 1); break;
                case CommandKind.RoyalVisit: result = CourtOps.Visit(em, root, target, command.Argument); break;
                case CommandKind.ResolveMarriage: result = RoyalFamilyOps.Resolve(em, root, target, command.Argument, command.Other, command.Definition); break;
                case CommandKind.PrepareMarriage: result = RoyalFamilyOps.Prepare(em, root, target); break;
                case CommandKind.ArrangeMarriage: result = RoyalFamilyOps.Arrange(em, root, target, Sim.Find(em, command.Other)); break;
                case CommandKind.RefusePersonRequest: result = PersonRequestOps.Refuse(em, root, target, command.Definition); break;
                case CommandKind.CustomizePortrait: result = PortraitOps.Customize(em, root, target, command.Text, (uint)command.Other); break;
                case CommandKind.GiftPerson: case CommandKind.CompleteSocialTask: case CommandKind.ProposeMarriage:
                    result = SocialOps.Command(em, root, command); break;
                default: result = ResultCode.Unavailable; return false;
            }
            return true;
        }
    }
}
