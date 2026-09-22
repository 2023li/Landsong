namespace Landsong.ECS.Presentation
{
    public sealed class GameUiCommandWriter
    {
        internal GameUiInputContext inputContext;
        internal GameUiSessionHandle sessionController;
        internal GameUiRefreshScheduler refresh;
        ulong requestSequence;
        public void ResetSequence() => requestSequence = 0;
        public bool TryQueue<T>(T request)
            where T : unmanaged, IGameRequest
        {
            if (!inputContext.Policy.Capture().CanQueue(request.Kind))
                return false;
            GameplayRequests.Enqueue(sessionController.em, sessionController.root, request, ++requestSequence);
            refresh.NextPanel = 0;
            return true;
        }
    }
}
