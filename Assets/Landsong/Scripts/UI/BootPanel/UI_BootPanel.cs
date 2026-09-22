using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_BootPanel : UIPanelBase
    {
        [LabelText("开幕展示秒数"), Min(0)]
        public float SplashSeconds = 2;
    }
}
