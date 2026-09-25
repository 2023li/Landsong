using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public enum AudioBus
    {
        [LabelText("背景音乐")]
        Music,
        [LabelText("环境声音")]
        Ambient,
        [LabelText("世界音效")]
        Effects,
        [LabelText("界面音效")]
        UI
    }

    public enum PresentationCue
    {
        [LabelText("点击")]
        Click,
        [LabelText("返回")]
        Back,
        [LabelText("操作拒绝")]
        Denied,
        [LabelText("建造")]
        Build,
        [LabelText("完工")]
        Complete,
        [LabelText("修复")]
        Repair,
        [LabelText("采集")]
        Harvest,
        [LabelText("受击")]
        Hit,
        [LabelText("死亡")]
        Death,
        [LabelText("荒废")]
        Ruin,
        [LabelText("敲钟")]
        Bell,
        [LabelText("英雄唤醒")]
        HeroWake,
        [LabelText("访客到达")]
        Visitor,
        [LabelText("捕获")]
        Capture,
        [LabelText("获得掉落")]
        Loot,
        [LabelText("庆祝")]
        Celebration,
        [LabelText("预警")]
        Warning,
        [LabelText("战报")]
        Report,
        [LabelText("雷声")]
        Thunder,
        [LabelText("敌人刷新")]
        EnemySpawn
    }
}
