using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.Editor.UI
{
    public enum UIPreviewKind
    {
        [LabelText("主菜单与开局")] Menu,
        [LabelText("设置")] Setting,
        [LabelText("存档浏览")] Save,
        [LabelText("游戏信息")] GameHud,
        [LabelText("科技")] Technology,
        [LabelText("人才")] Talent,
        [LabelText("建筑详情")] BuildingDetails,
        [LabelText("王室人物详情")] RoyalDetails,
        [LabelText("婚姻请求")] Marriage,
        [LabelText("士兵详情")] SoldierDetails
    }

    [Serializable]
    public sealed class UIPreviewTextSample
    {
        [LabelText("语义键")] public string key;
        [LabelText("示例文字"), TextArea] public string value;
        public UIPreviewTextSample(string key, string value) { this.key = key; this.value = value; }
    }

    [Serializable]
    public sealed class UIPreviewRowSample
    {
        [LabelText("各列示例文字")] public string[] cells;
        public UIPreviewRowSample(params string[] cells) { this.cells = cells; }
    }

    [Serializable]
    public sealed class UIPreviewListSample
    {
        [LabelText("语义键")] public string key;
        [LabelText("示例条目")] public UIPreviewRowSample[] rows;
        public UIPreviewListSample(string key, params UIPreviewRowSample[] rows) { this.key = key; this.rows = rows; }
    }

    /// <summary>仅 Editor 程序集可用，不包含真实会话、ECS 状态或文件 IO。</summary>
    [CreateAssetMenu(fileName = "UIPreviewProfile", menuName = "Landsong/UI/编辑预览数据")]
    public sealed class UIPreviewProfile : ScriptableObject
    {
        [SerializeField, LabelText("面板类别")] private UIPreviewKind kind;
        [SerializeField, LabelText("文字示例")] private UIPreviewTextSample[] texts = Array.Empty<UIPreviewTextSample>();
        [SerializeField, LabelText("列表示例")] private UIPreviewListSample[] lists = Array.Empty<UIPreviewListSample>();
        public UIPreviewKind Kind => kind;
        public UIPreviewTextSample[] Texts => texts;
        public UIPreviewListSample[] Lists => lists;

        public string GetText(string key)
        {
            foreach (var sample in texts) if (sample != null && sample.key == key) return sample.value ?? string.Empty;
            throw new InvalidOperationException($"预览配置 {name} 缺少文字键 {key}。");
        }

        public UIPreviewRowSample[] GetRows(string key)
        {
            foreach (var sample in lists)
                if (sample != null && sample.key == key) return sample.rows ?? Array.Empty<UIPreviewRowSample>();
            throw new InvalidOperationException($"预览配置 {name} 缺少列表键 {key}。");
        }

        public void SetDefaults(UIPreviewKind panelKind)
        {
            kind = panelKind;
            switch (panelKind)
            {
                case UIPreviewKind.Menu:
                    texts = new[] { T("title", "Landsong"), T("dynastyName", "晨曦王朝"), T("mapName", "雾林盆地"),
                        T("mapDescription", "河流穿过肥沃的平原，森林与矿脉分布在远处的山麓。向边境拓展时，需要为驻军预留道路与补给。"),
                        T("difficulty", "普通"), T("continue", "继续晨曦王朝 · 第 18 回合") };
                    lists = new[] { L("maps", R("雾林盆地", "河流与森林交错", "普通"), R("北境山谷", "矿脉丰富，耕地有限", "困难")) };
                    break;
                case UIPreviewKind.Setting:
                    texts = new[] { T("title", "设置"), T("resolution", "1920 × 1080"), T("language", "简体中文"),
                        T("displayConfirmation", "保留这些显示设置？将在 15 秒后恢复。"), T("status", "修改将在点击“应用”后保存") };
                    lists = new[] { L("audio", R("主音量", "80%"), R("音乐", "65%"), R("音效", "90%")),
                        L("keys", R("暂停", "空格"), R("旋转建筑", "R"), R("英雄驻守", "H")) };
                    break;
                case UIPreviewKind.Save:
                    texts = new[] { T("title", "存档"), T("selected", "晨曦王朝 · 第 18 回合"),
                        T("description", "雾林盆地 · 白天\n人口 126 · 王朝建立于第 1 回合"), T("empty", "暂无存档。开始一段新的王朝旅程。") };
                    lists = new[] { L("slots", R("第 18 回合 · 黎明", "自动节点", "2026-09-11 09:30", "可加载"),
                        R("北部边境防线完成之后的独立存档", "独立槽", "2026-09-10 21:45", "有备份"),
                        R("旧王朝记录", "记录损坏", "2026-09-08 18:20", "可恢复备份")),
                        L("runs", R("晨曦王朝", "第 18 回合", "雾林盆地"), R("长风王朝", "第 42 回合", "覆灭记录")) };
                    break;
                case UIPreviewKind.GameHud:
                    texts = new[] { T("turn", "第 18 回合 · 白天"), T("population", "人口 126 / 160"), T("resources", "木材 320   石材 180   粮食 640"),
                        T("research", "灌溉技术 · 研究中"), T("researchProgress", "48 / 80"), T("quest", "修建 3 座住宅（2 / 3）"),
                        T("message", "远征队已归来，有待处理的王朝事务。") };
                    lists = new[] { L("quests", R("安居之所", "住宅 2 / 3", "奖励：木材 × 50"), R("准备过冬", "粮食 640 / 800", "奖励：影响力 × 10")),
                        L("heroes", R("林岚", "守卫", "等级 5"), R("谢安", "驻守", "等级 3")) };
                    break;
                case UIPreviewKind.Technology:
                    texts = new[] { T("title", "科技"), T("selected", "灌溉技术"), T("description", "改善农田灌溉，提高粮食产出。研究完成后，将影响符合条件的农田与其后续升级。"),
                        T("progress", "48 / 80"), T("status", "研究中 · 队列第 1 项"), T("cost", "研究点 80 · 前置：基础耕作") };
                    lists = new[] { L("nodes", R("基础耕作", "已完成", "农田可用"), R("灌溉技术", "研究中", "48 / 80"),
                        R("轮作制度", "尚未解锁", "需要灌溉技术")), L("queue", R("灌溉技术", "48 / 80"), R("石工技术", "待研究")) };
                    break;
                case UIPreviewKind.Talent:
                    texts = new[] { T("title", "人才"), T("selected", "沈清和"), T("description", "擅长农事与组织生产，长期关注边境居民的生活。希望参与下一次远征，带回适合本地栽培的新作物。"),
                        T("attributes", "管理 72   农业 86   军事 38"), T("request", "待办：委托未完成（1）") };
                    lists = new[] { L("people", R("沈清和", "农业人才", "好感 68", "有待办"), R("周砚", "建筑人才", "好感 42", "可委任"),
                        R("顾长风", "军事人才", "好感 81", "远征中")) };
                    break;
                case UIPreviewKind.BuildingDetails:
                    texts = new[] { T("title", "东岸农田"), T("level", "等级 2"), T("description", "沿河修建的农田，依赖工人出勤与仓库连接。成熟作物可以收获，新的种植计划将在空闲地块生效。"),
                        T("production", "粮食 +24 / 回合"), T("workers", "工人 4 / 6"), T("experience", "经验 68 / 100"),
                        T("warning", "道路连接不足：运输路径经过未连接地块"), T("position", "坐标 (12, 8) · 行动力 6") };
                    lists = new[] { L("outputs", R("粮食", "+24 / 回合", "预计入库 24")),
                        L("workers", R("自然吸引力", "4 人"), R("岗位预算", "2 人")),
                        L("crops", R("小麦", "成熟度 75%"), R("蔬菜", "成熟度 40%")) };
                    break;
                case UIPreviewKind.RoyalDetails:
                    texts = new[] { T("identity", "沈明远 · 男 · 20 岁 · 储君"),
                        T("description", "父亲：开国君主\n母亲：开国配偶\n配偶：无\n\n国中声望：68.0 / 100\n成长性：1.15\n野心：尚未表现出野心\n\n特性：\n擅长组织生产，关注边境民生。\n\n待办：希望参与下一次远征。") };
                    lists = new[] { L("people", R("开国君主", "42 岁 · 君王", "影响力 82.0"),
                        R("沈明远", "20 岁 · 储君", "影响力 68.0")) };
                    break;
                case UIPreviewKind.Marriage:
                    texts = new[] { T("title", "沈知远希望能和苏清和结婚"), T("hint", "请审阅双方情况，再决定是否赐婚。"),
                        T("person", "沈知远 · 男 · 22 岁\n王室成员\n国中声望：62 / 100\n配偶：无"),
                        T("mate", "苏清和 · 女 · 21 岁\n农业人才\n好感：68\n配偶：无"),
                        T("approve", "同意赐婚"), T("refuse", "不同意"), T("close", "稍后处理") };
                    lists = Array.Empty<UIPreviewListSample>();
                    break;
                case UIPreviewKind.SoldierDetails:
                    texts = new[] { T("name", "守城新兵"), T("age", "年龄：24 岁"), T("stats", "力量：—    知识：—\n敏捷：4    血量：100/100"),
                        T("abilities", "士兵能力\n城防卫士 · Lv.3\n攻击：12\n经验：48\n自动巡逻与攻击\n\n驻地：王宫\n槽位：1") };
                    lists = Array.Empty<UIPreviewListSample>();
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(panelKind));
            }
        }

        private static UIPreviewTextSample T(string key, string value) => new UIPreviewTextSample(key, value);
        private static UIPreviewRowSample R(params string[] values) => new UIPreviewRowSample(values);
        private static UIPreviewListSample L(string key, params UIPreviewRowSample[] rows) => new UIPreviewListSample(key, rows);
    }
}
