using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public enum ApplicationSceneRole
    {
        [LabelText("开幕")] Boot,
        [LabelText("主菜单")] Menu,
        [LabelText("加载过渡")] Loading,
        [LabelText("游戏")] Game
    }
    [DefaultExecutionOrder(-10000)]
    public sealed class ApplicationSceneEntry : MonoBehaviour
    {
        [LabelText("应用界面根预制体"), Required] public ApplicationUiRoot RootTemplate;
        [LabelText("场景职责")] public ApplicationSceneRole Role;
        [LabelText("游戏场景宿主"), ShowIf("IsGame")] public EcsGameHost GameHost;
        bool IsGame => Role == ApplicationSceneRole.Game;
        ApplicationUiRoot owner;
        void Awake()
        {
            if (RootTemplate == null || IsGame && GameHost == null) throw new System.InvalidOperationException("场景启动配置不完整：" + gameObject.scene.path);
            owner = ApplicationUiRoot.Install(RootTemplate);
            owner.Flow.Register(this);
        }
        void OnDestroy() { if (owner != null) owner.Flow.Unregister(this); }
    }
}
