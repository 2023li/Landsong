using System;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    /// <summary>A validated binding to an ECS world and session root. Contains no view or interaction state.</summary>
    public sealed class GameUiSessionHandle
    {
        internal EntityManager em;
        internal Entity root;
        World boundWorld;
        internal EntityManager Manager => em;
        internal Entity SessionRoot => root;
        public bool IsBound => root != Entity.Null && boundWorld != null && boundWorld.IsCreated && em.Exists(root) && em.HasComponent<SimulationReady>(root);

        public void Bind(EntityManager manager, Entity sessionRoot)
        {
            var world = manager.World;
            if (world == null || !world.IsCreated || sessionRoot == Entity.Null || !manager.Exists(sessionRoot))
                throw new InvalidOperationException("游戏 UI 会话无效。");
            boundWorld = world;
            em = manager;
            root = sessionRoot;
        }

        public void Unbind()
        {
            root = Entity.Null;
            boundWorld = null;
            em = default;
        }
    }
}
