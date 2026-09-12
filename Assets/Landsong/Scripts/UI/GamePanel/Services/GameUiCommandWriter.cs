using Unity.Collections;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class GameUiCommandWriter
    {
        internal IGameUiNavigation navigation;
        internal GameUiSession sessionController;
        public void Send(CommandKind kind, ulong target = 0, ulong other = 0, int definition = -1, int amount = 0, int argument = 0, string text = null, Vector3 position = default)
        {
            var command = new Command
            {
                Kind = kind,
                Target = target,
                Other = other,
                Definition = definition,
                Amount = amount,
                Argument = argument,
                Position = position,
                Text = new FixedString128Bytes(kind == CommandKind.Rename || kind == CommandKind.RenameSoldier ? BuildingOps.SanitizeName(text) : text ?? "")
            };
            TryQueue(command);
        }

        public bool TryQueue(Command command)
        {
            if (!navigation.InputPolicy.Capture().CanQueue(command.Kind))
                return false;
            command.RequestId = ++sessionController.request;
            sessionController.em.GetBuffer<Command>(sessionController.root).Add(command);
            sessionController.nextRefresh = 0;
            return true;
        }
    }
}
