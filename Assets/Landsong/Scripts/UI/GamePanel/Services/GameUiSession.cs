using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Text = TMPro.TextMeshProUGUI;
using System.Text;
using Unity.Transforms;
using UnityEngine.EventSystems;
using InputField = TMPro.TMP_InputField;
using Landsong.ECS.Persistence;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class GameUiSession
    {
        internal EntityManager em;
        World boundWorld;
        internal Entity root;
        internal ulong selected;
        internal ulong request;
        internal readonly GameUiRefreshScheduler Refresh = new GameUiRefreshScheduler();
        internal float nextRefresh { get => Refresh.NextPanel; set => Refresh.NextPanel = value; }
        internal bool intel;
        internal readonly StringBuilder text = new StringBuilder();
        internal string Name(int definition) => Sim.ValidDefinition(em, root, definition) ? Sim.Definition(em, root, definition).Name.ToString() : "—";
        internal string EntityName(ulong id)
        {
            var e = Sim.Find(em, id);
            return e == Entity.Null ? "无驻地" : em.GetComponentData<Identity>(e).Name.ToString();
        }

        internal static string PhaseName(Phase p) => p == Phase.Day ? "白天建造" : p == Phase.Night ? "夜晚" : p == Phase.Deployment ? "出勤" : p == Phase.Retreat ? "敌军撤离" : p == Phase.Celebration ? "战后收尾" : p == Phase.Report ? "今晚战报" : p == Phase.GameOver || p == Phase.Ended ? "王朝终局" : "结算";
        internal static string ResultName(ResultCode r) => r == ResultCode.PreparationFailed ? "准备失败，当前进度已保留；请检查 Console 后重试" : r == ResultCode.ConfirmationRequired ? "请确认结算后的待清空内容" : r == ResultCode.WrongPhase ? "当前阶段不能执行此操作" : r == ResultCode.InsufficientResources ? "资源不足" : r == ResultCode.InsufficientPopulation ? "空闲人口或工人不足" : r == ResultCode.NoCapacity ? "容量不足" : r == ResultCode.InvalidPlacement ? "占地、地形或通行条件不符" : r == ResultCode.MissingResearch ? "需要前置科技" : r == ResultCode.QuestOverflow ? "任务超出可承接数量" : "当前条件不满足（" + r + "）";
        internal void ForDefinitions(ContentKind kind, Action<int, ContentDefinition> action)
        {
            var blob = em.GetComponentData<ContentCatalog>(root).Value;
            for (var i = 0; i < blob.Value.Definitions.Length; i++)
                if (blob.Value.Definitions[i].Kind == kind)
                    action(i, blob.Value.Definitions[i]);
        }

        public static string Direction(int value) => IntelOps.Direction(value);
        internal EntityManager Manager => em;
        internal Entity SessionRoot => root;
        internal float NextRefresh { get => nextRefresh; set => nextRefresh = value; }
        internal ulong Selected { get => selected; set => selected = value; }
        internal ulong RequestSequence { get => request; set => request = value; }
        public bool IsBound => root != Entity.Null && boundWorld != null && boundWorld.IsCreated && em.Exists(root) && em.HasComponent<SimulationReady>(root);

        public void Bind(EntityManager manager, Entity sessionRoot)
        {
            var world = manager.World;
            if (world == null || !world.IsCreated || sessionRoot == Entity.Null || !manager.Exists(sessionRoot))
                throw new InvalidOperationException("游戏 UI 会话无效。");
            boundWorld = world;
            em = manager;
            root = sessionRoot;
            selected = request = 0;
            Refresh.Reset();
            nextRefresh = 0;
            intel = false;
        }

        public void Unbind()
        {
            root = Entity.Null;
            boundWorld = null;
            em = default;
            selected = request = 0;
            Refresh.Reset();
            nextRefresh = 0;
            intel = false;
        }
    }
}
