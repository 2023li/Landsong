using Sirenix.OdinInspector;
using TMPro;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_DebugPanel_Common : MonoBehaviour
    {
        [SerializeField, LabelText("一般信息"), Required] private TMP_Text txt_一般信息;
        float nextRefresh;

        void OnEnable()
        {
            if (!Application.isPlaying) return;
            RefreshStatus();
            nextRefresh = Time.unscaledTime + .25f;
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .25f;
            RefreshStatus();
        }

        public void RefreshStatus()
        {
            if (txt_一般信息 == null) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (!EcsSceneFlow.GameReady || world == null || !world.IsCreated)
            {
                txt_一般信息.text = "当前玩家战力：--（未进入游戏）";
                return;
            }

            var em = world.EntityManager;
            var root = WorldQueries.Root(em);
            txt_一般信息.text = root != Entity.Null && em.Exists(root) && em.HasComponent<SimulationReady>(root)
                ? $"当前玩家战力：{MilitaryStrength.Calculate(em, root)}"
                : "当前玩家战力：--（未进入游戏）";
        }
    }
}
