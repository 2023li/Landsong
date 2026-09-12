using Unity.Entities;

namespace Landsong.ECS
{
    public static class ProgressionCommandHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity target, Command command, out ResultCode result)
        {
            switch (command.Kind)
            {
                case CommandKind.Research: result = ProgressionOps.Research(em, root, command.Definition, false); break;
                case CommandKind.PlanResearch: result = ResearchOps.Plan(em, root, command.Definition, command.Text.ToString()); break;
                case CommandKind.CancelResearch: result = ProgressionOps.Research(em, root, command.Definition, true); break;
                case CommandKind.SelectPolicy: result = ProgressionOps.Policy(em, root, command.Definition); break;
                case CommandKind.AcceptQuest: case CommandKind.RejectQuest: case CommandKind.SubmitQuest:
                case CommandKind.ClaimQuest: case CommandKind.AbandonQuest:
                    result = ProgressionOps.QuestCommand(em, root, command); break;
                case CommandKind.RecruitQuest: result = ProgressionOps.Offer(em, root, target, command.Argument, true); break;
                case CommandKind.StartExpedition: case CommandKind.ClaimExpedition: case CommandKind.AbandonExpedition:
                    result = ProgressionOps.ExpeditionCommand(em, root, command); break;
                case CommandKind.CancelPolicy:
                    var policies = em.GetBuffer<PolicyChoice>(root);
                    for (var i = policies.Length - 1; i >= 0; i--)
                        if (policies[i].Definition == command.Definition) policies.RemoveAt(i);
                    BuildingOps.Changed(em, root); result = ResultCode.Success; break;
                default: result = ResultCode.Unavailable; return false;
            }
            return true;
        }
    }
}
