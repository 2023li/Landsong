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
    public sealed class UI_GamePanel_Hud : Moyo.Unity.UIViewBase, IGameUiFeedback
    {
        internal void RefreshHeader(Session s)
        {
            var population = Sim.Population(sessionController.em, sessionController.root);
            Status.text = PresentationText.Get("Gameplay/gameplay.ecs.turn_status", "{0}　白天 {1} / 夜晚 {1}　{2}　人口 {3}（空闲 {4}）", s.DynastyName.ToString(), s.Turn, PresentationText.Source(GameUiSession.PhaseName(s.Phase)), population, math.max(0, population - Sim.Employed(sessionController.em)));
            AdvanceLabel.text = s.Phase == Phase.Report ? "今晚战报" : "下一阶段";
            Advance.interactable = !sessionController.intel && s.Paused == 0 && s.CheckpointPending == 0 && (s.Phase == Phase.Day || s.Phase == Phase.Report);
            MoonProgress.gameObject.SetActive(s.Phase != Phase.Day);
            MoonProgress.SetValueWithoutNotify(NightOps.Progress(sessionController.em, sessionController.root));
            Moon.text = s.Paused != 0 ? "已暂停" : s.Phase == Phase.Night ? "月亮进度" : GameUiSession.PhaseName(s.Phase);
        }

        internal UI_GamePanel_Building buildingController;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_History historyController;
        internal UI_GamePanel_Quest questController;
        internal IGameUiNavigation navigation;
        internal GameUiSession sessionController;
        internal UI_GamePanel_WorldInteraction worldController;
        [Sirenix.OdinInspector.LabelText("状态")]
        public Text Status;
        [Sirenix.OdinInspector.LabelText("选中信息")]
        public Text Selection;
        [Sirenix.OdinInspector.LabelText("消息")]
        public Text Message;
        [Sirenix.OdinInspector.LabelText("月亮")]
        public Text Moon;
        [Sirenix.OdinInspector.LabelText("推进文字")]
        public Text AdvanceLabel;
        [Sirenix.OdinInspector.LabelText("消息按钮")]
        public Button MessageButton;
        [Sirenix.OdinInspector.LabelText("月亮进度")]
        public Slider MoonProgress;
        [Sirenix.OdinInspector.LabelText("推进")]
        public Button Advance;
        internal void RequestAdvance()
        {
            if (sessionController.root == Entity.Null || !sessionController.em.Exists(sessionController.root))
                return;
            if (sessionController.intel)
                return;
            if (worldController.HasBuildingPlacement)
                worldController.EndBuildingPlacement();
            if (sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Report)
            {
                navigation.OpenPanel(GamePanelId.BattleReport);
                return;
            }

            commandsController.TryQueue(CommandRequests.Advance());
        }

        internal void ConsumeInterfaceEvents(Session s)
        {
            var events = sessionController.em.GetBuffer<GameEvent>(sessionController.root);
            for (int i = 0; i < events.Length; i++)
            {
                var e = events[i];
                if (worldController.worldPresentation != null)
                    worldController.worldPresentation.Consume(sessionController.em, sessionController.root, e);
                if (e.Kind == EventKind.Reward && s.Phase != Phase.GameOver && s.Phase != Phase.Ended)
                    RewardFlight(e);
                if (e.Kind == EventKind.Message || e.Kind == EventKind.Ruin)
                {
                    questController.BindInvitationMessage(e);
                    var category = e.Category;
                    if (category == HistoryCategory.Important || category == HistoryCategory.Economy && InterfaceSettings.Current.EconomyMessages || category == HistoryCategory.General && InterfaceSettings.Current.GeneralMessages)
                        Message.text = e.Message.ToString() == "研究完成" ? "研究完成：" + sessionController.Name(e.Definition) + "（奖励见科技详情）" : e.Message.ToString();
                }
                else if (e.Kind == EventKind.CommandResult && e.Result != ResultCode.Success)
                {
                    Message.text = GameUiSession.ResultName(e.Result);
                    if (e.Result == ResultCode.ConfirmationRequired)
                        navigation.OpenPanel(GamePanelId.NightConfirmation);
                    if (e.Result == ResultCode.QuestOverflow)
                        navigation.OpenPanel(GamePanelId.Quest);
                }
            }

            // IO requests remain owned by CheckpointSystem; all presentation events are consumed even while a text field is focused.
            for (int i = events.Length - 1; i >= 0; i--)
            {
                var kind = events[i].Kind;
                if (kind != EventKind.Save && kind != EventKind.Load && kind != EventKind.Retry && kind != EventKind.EndDynasty && kind != EventKind.DayCheckpoint && kind != EventKind.DuskCheckpoint)
                    events.RemoveAt(i);
            }
        }

        internal void RefreshFeedbackVisibility()
        {
            if (Message != null)
                Message.gameObject.SetActive(!string.IsNullOrWhiteSpace(Message.text));
        }

        [Sirenix.OdinInspector.LabelText("战斗信息栏")]
        public UI_GamePanel_BattleHud BattleHud;
        internal RectTransform heroHud;
        internal RectTransform heroCards;
        internal Text defenseStatus;
        internal Button defenseFocus;
        internal readonly Dictionary<ulong, UI_GamePanel_HeroHudItem> heroButtons = new Dictionary<ulong, UI_GamePanel_HeroHudItem>();
        internal readonly List<ulong> heroIds = new List<ulong>();
        internal void HeroHotkeys(Keyboard keyboard)
        {
            var s = sessionController.em.GetComponentData<Session>(sessionController.root);
            if (s.Phase != Phase.Night && s.Phase != Phase.Retreat)
                return;
            for (int i = 0; i < math.min(9, heroIds.Count); i++)
                if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                    commandsController.Send(CommandKind.SelectHero, heroIds[i]);
            if (keyboard.backquoteKey.wasPressedThisFrame)
                commandsController.Send(CommandKind.SelectHero);
            if (keyboard.hKey.wasPressedThisFrame && Sim.Alive(sessionController.em, s.SelectedHero))
                commandsController.TryQueue(CommandRequests.MoveHero(Sim.Position(sessionController.em, s.SelectedHero)));
        }

        internal void RefreshHeroHud()
        {
            if (heroHud == null)
            {
                if (BattleHud == null)
                    throw new System.InvalidOperationException("战斗 HUD 检查器引用缺失。");
                BattleHud.ValidateConfiguration();
                heroHud = (RectTransform)BattleHud.transform;
                heroCards = BattleHud.HeroCards;
                defenseStatus = BattleHud.DefenseStatus;
                defenseFocus = BattleHud.DefenseFocus;
            }

            var s = sessionController.em.GetComponentData<Session>(sessionController.root);
            bool visible = s.Phase == Phase.Night || s.Phase == Phase.Retreat || s.Phase == Phase.Celebration;
            visible &= !sessionController.intel && navigation.Panel != GamePanelId.Technology && navigation.Panel != GamePanelId.Quest && (buildingController.BuildingConfirmPanel == null || !buildingController.BuildingConfirmPanel.activeSelf);
            heroHud.gameObject.SetActive(visible);
            if (!visible)
            {
                heroIds.Clear();
                return;
            }

            int deployed = 0, returning = 0, housed = 0, dead = 0, enemies = 0;
            bool danger = false;
            using (var actors = Sim.Entities<Combatant>(sessionController.em))
                foreach (var e in actors)
                {
                    var a = sessionController.em.GetComponentData<Combatant>(e);
                    if (a.Faction == 0)
                    {
                        if (!Sim.Alive(sessionController.em, e))
                        {
                            if (!sessionController.em.HasComponent<Hero>(e) || sessionController.em.GetComponentData<Hero>(e).DeathPending != 0)
                                dead++;
                        }
                        else if (a.Deployed != 0)
                            deployed++;
                        if (sessionController.em.HasComponent<Soldier>(e))
                        {
                            var soldier = sessionController.em.GetComponentData<Soldier>(e);
                            if (soldier.RecallState == 1)
                                returning++;
                            if (soldier.RecallState == 2)
                                housed++;
                        }
                    }
                    else if (Sim.Alive(sessionController.em, e) && a.Deployed != 0 && sessionController.em.HasComponent<VisualState>(e) && sessionController.em.GetComponentData<VisualState>(e).Visible != 0)
                    {
                        enemies++;
                        if (sessionController.em.Exists(a.Target) && sessionController.em.HasComponent<BuildingStats>(a.Target) && sessionController.em.GetComponentData<BuildingStats>(a.Target).IsCore != 0 && CombatOps.Distance(sessionController.em, e, a.Target) <= a.Range + 1)
                            danger = true;
                    }
                }

            defenseStatus.text = (danger ? "核心遭到攻击！\n" : "") + $"出场 {deployed} · 召回中 {returning} · 归营 {housed} · 阵亡 {dead} · 已现身敌军 {enemies}\n" + (s.ActiveBell != 0 ? "警铃集结：" + sessionController.EntityName(s.ActiveBell) : "警铃未激活") + (s.Phase == Phase.Retreat ? " · 敌军撤退中" : s.Phase == Phase.Celebration ? " · 战斗结束，十秒收尾" : "");
            defenseStatus.color = danger ? new Color(1, .4f, .35f) : Color.white;
            ulong attention = 0;
            int priority = int.MaxValue;
            string attentionText = "";
            using (var all = Sim.OrderedEntities<Identity>(sessionController.em))
                foreach (var e in all)
                {
                    if (!sessionController.em.HasComponent<VisualState>(e))
                        continue;
                    int candidate = int.MaxValue;
                    string label = "";
                    if (sessionController.em.HasComponent<Hero>(e) && sessionController.em.GetComponentData<Hero>(e).DeathPending != 0)
                    {
                        candidate = 1;
                        label = "英雄阵亡";
                    }
                    else if (sessionController.em.GetComponentData<VisualState>(e).Visible != 0 && sessionController.em.HasComponent<Combatant>(e) && sessionController.em.GetComponentData<Combatant>(e).Faction == 1 && Sim.Alive(sessionController.em, e))
                    {
                        var actor = sessionController.em.GetComponentData<Combatant>(e);
                        candidate = danger && sessionController.em.Exists(actor.Target) && sessionController.em.HasComponent<BuildingStats>(actor.Target) && sessionController.em.GetComponentData<BuildingStats>(actor.Target).IsCore != 0 ? 0 : 2;
                        label = candidate == 0 ? "核心受袭" : "敌军";
                    }
                    else if (sessionController.em.GetComponentData<VisualState>(e).Visible != 0 && sessionController.em.HasComponent<Opportunity>(e))
                    {
                        candidate = 3;
                        label = "可交互机会";
                    }

                    if (candidate >= priority || worldController.Camera == null)
                        continue;
                    var screen = worldController.Camera.WorldToViewportPoint(Sim.Position(sessionController.em, e));
                    if (screen.z > 0 && screen.x >= 0 && screen.x <= 1 && screen.y >= 0 && screen.y <= 1)
                        continue;
                    priority = candidate;
                    attention = sessionController.em.GetComponentData<Identity>(e).Id;
                    attentionText = (screen.x < 0 ? "左" : screen.x > 1 ? "右" : screen.y > 1 ? "上" : "下") + "侧画面外 · " + label;
                }

            if (!BattleHud.FocusInteraction.IsPinned)
            {
                defenseFocus.onClick.RemoveAllListeners();
                defenseFocus.interactable = attention != 0;
                if (attention != 0)
                {
                    ulong focus = attention;
                    defenseStatus.text += "\n" + attentionText + "（点击定位）";
                    defenseFocus.onClick.AddListener(() => navigation.LocateGarrison(focus));
                }
            }

            heroIds.Clear();
            using var heroes = Sim.OrderedEntities<Hero>(sessionController.em);
            int count = 0;
            var seenHeroes = new HashSet<ulong>();
            bool heroOrderPinned = heroButtons.Values.Any(item => item.Interaction.IsPinned);
            foreach (var e in heroes)
            {
                var id = sessionController.em.GetComponentData<Identity>(e);
                var h = sessionController.em.GetComponentData<Hero>(e);
                var a = sessionController.em.GetComponentData<Combatant>(e);
                var hp = sessionController.em.GetComponentData<Health>(e);
                bool active = h.Recruited != 0 && hp.Current > 0 && a.Deployed != 0;
                heroIds.Add(id.Id);
                Button button;
                seenHeroes.Add(id.Id);
                if (!heroButtons.TryGetValue(id.Id, out var itemView))
                {
                    itemView = Instantiate(BattleHud.HeroTemplate, heroCards);
                    itemView.gameObject.SetActive(true);
                    heroButtons.Add(id.Id, itemView);
                }

                if (itemView.Interaction.IsPinned) { count++; continue; }
                if (!heroOrderPinned) itemView.transform.SetSiblingIndex(count);
                button = itemView.Select;
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();
                ulong key = id.Id;
                button.interactable = s.Paused == 0 && s.Phase != Phase.Celebration;
                ulong sanctum = h.Sanctum;
                button.onClick.AddListener(() =>
                {
                    if (active)
                        commandsController.Send(CommandKind.SelectHero, key);
                    else
                    {
                        navigation.LocateGarrison(sanctum);
                        buildingController.showBuildingDetails = true;
                        navigation.OpenPanel(GamePanelId.Building);
                    }
                });
                var label = itemView.Label;
                var portraitImage = itemView.Portrait;
                portraitImage.sprite = buildingController.BuildingSource(id.Definition)?.Icon;
                portraitImage.color = portraitImage.sprite != null ? Color.white : new Color(.3f, .48f, .65f);
                portraitImage.raycastTarget = false;
                itemView.PortraitBinding.Bind(sessionController.em, sessionController.root, id.Id);
                label.rectTransform.offsetMin = new Vector2(48, 2);
                string state = h.DeathPending != 0 ? "阵亡 · 黎明开始冷却" : h.Recruited == 0 ? "冷却 " + math.max(0, h.CooldownUntil - s.Turn) + " 回合" : !active ? "待神殿唤醒" : $"生命 {hp.Current:0}/{hp.Maximum:0}";
                label.text = (s.SelectedHero == e ? "▶ " : "") + (count < 9 ? (count + 1) + " " : "") + id.Name + " Lv." + MilitaryOps.HeroLevel(sessionController.em, sessionController.root, e) + "\n" + state + "\n经验 " + h.Experience + " · 本夜 +" + MilitaryOps.HeroBattleExperience(sessionController.em, sessionController.root, e);
                count++;
            }

            foreach (var key in new List<ulong>(heroButtons.Keys))
                if (!seenHeroes.Contains(key) && !heroButtons[key].Interaction.IsPinned)
                { Destroy(heroButtons[key].gameObject); heroButtons.Remove(key); }
        }

        internal void RefreshInterfaceBarrier()
        {
            var input = navigation.InputPolicy.Capture();
            if (historyController.historyTools != null)
                historyController.historyTools.gameObject.SetActive(navigation.IsPanelOpen && navigation.Panel == GamePanelId.History && !input.HasOwner(GameUiInputOwner.Pause) && !input.Intelligence);
            Selection.gameObject.SetActive(!navigation.IsPanelOpen || navigation.Panel != GamePanelId.History);
            navigation.InterfaceGroup.interactable = input.CanInteractWithBackgroundGroup;
            if (!navigation.InterfaceGroup.interactable)
            {
                worldController.cameraVelocity = Vector3.zero;
                worldController.cameraDragging = false;
            }
        }

        [Sirenix.OdinInspector.LabelText("情报按钮文字")]
        public TMP_Text IntelligenceButtonLabel;
        internal TMP_Text intelligenceButtonLabel;
        internal IntelView intelligenceView => navigation.IntelligenceWindow.View;
        public bool InIntelligenceMode => sessionController.intel;

        internal void InitializeIntelligence()
        {
            if (IntelligenceButtonLabel == null)
                throw new InvalidOperationException("情报按钮文字检查器引用缺失。");
            intelligenceButtonLabel = IntelligenceButtonLabel;
        }

        internal void RefreshIntelligenceBadge()
        {
            if (intelligenceButtonLabel == null || !sessionController.em.HasComponent<RecoveryState>(sessionController.root))
                return;
            var r = sessionController.em.GetComponentData<RecoveryState>(sessionController.root);
            intelligenceButtonLabel.text = r.IntelFingerprint != r.IntelReadFingerprint ? "情报 •" : "情报";
        }

        internal void DrawIntelligence(Action<float3, Vector3, Color> draw)
        {
            if (!sessionController.intel || intelligenceView == null || intelligenceView.Tier < 2)
                return;
            foreach (var area in intelligenceView.Areas)
            {
                var color = area.Target ? new Color(1, .12f, .15f, .20f) : new Color(1, .57f, .06f, area.Secondary ? .07f : .20f);
                var grid = sessionController.em.GetComponentData<GridData>(sessionController.root);
                var p = area.Center;
                p.y = GridOps.Position(grid, GridOps.Cell(grid, p), new int2(1)).y + .15f;
                draw(p, new Vector3(area.Size.x, .06f, area.Size.z), color);
                color.a = area.Secondary ? .25f : .85f;
                // Broken outline versus central cross remains distinguishable without red/orange color perception.
                float width = Mathf.Clamp(worldController.Camera.orthographicSize * .018f, .12f, .8f);
                if (area.Target)
                {
                    draw(p, new Vector3(area.Size.x * .7f, .08f, width), color);
                    draw(p, new Vector3(width, .08f, area.Size.z * .7f), color);
                }
                else
                    for (int i = 0; i < 8; i++)
                    {
                        float t = (i + .5f) / 8 - .5f;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            draw(p + new float3(t * area.Size.x, 0, side * area.Size.z * .5f), new Vector3(area.Size.x / 16, .08f, width), color);
                            draw(p + new float3(side * area.Size.x * .5f, 0, t * area.Size.z), new Vector3(width, .08f, area.Size.z / 16), color);
                        }
                    }
            }
        }

        [Sirenix.OdinInspector.LabelText("夜晚信息栏")]
        public UI_GamePanel_NightHud NightHud;
        internal RectTransform nightMarkers;
        internal readonly Dictionary<ulong, UI_GamePanel_NightMarker> nightMarkerButtons = new Dictionary<ulong, UI_GamePanel_NightMarker>();
        internal sealed class Flight
        {
            [Sirenix.OdinInspector.LabelText("文字")]
            public Text Text;
            [Sirenix.OdinInspector.LabelText("来源")]
            public Vector2 From;
            [Sirenix.OdinInspector.LabelText("年龄")]
            public float Age;
        }

        internal readonly List<Flight> rewardFlights = new List<Flight>();
        internal float priorNightTime;
        internal void RewardFlight(GameEvent reward)
        {
            if (worldController.Camera == null || reward.Amount <= 0)
                return;
            if (rewardFlights.Count >= 8)
            {
                Destroy(rewardFlights[0].Text.gameObject);
                rewardFlights.RemoveAt(0);
            }

            if (NightHud == null)
                throw new InvalidOperationException("夜间 HUD 检查器引用缺失。");
            var canvas = NightHud.RewardSpace;
            var text = Instantiate(NightHud.RewardTemplate, NightHud.transform);
            text.gameObject.SetActive(true);
            var rect = text.rectTransform;
            text.text = "+ " + sessionController.Name(reward.Definition) + " × " + reward.Amount + "（待结算）";
            var screen = worldController.Camera.WorldToScreenPoint(reward.Position);
            screen.x = math.clamp(screen.x, 120, Screen.width - 120);
            screen.y = math.clamp(screen.y, 70, Screen.height - 70);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen, null, out var local);
            rect.anchoredPosition = local;
            rewardFlights.Add(new Flight { Text = text, From = local });
        }

        internal void TickRewardFlights(Session s)
        {
            bool clear = s.Phase == Phase.GameOver || s.Phase == Phase.Ended || s.Time < priorNightTime;
            priorNightTime = s.Time;
            for (int i = rewardFlights.Count - 1; i >= 0; i--)
            {
                var f = rewardFlights[i];
                if (clear || f.Text == null || f.Age >= 1.2f)
                {
                    if (f.Text != null)
                        Destroy(f.Text.gameObject);
                    rewardFlights.RemoveAt(i);
                    continue;
                }

                f.Text.gameObject.SetActive(!sessionController.intel && (navigation.PauseMenu == null || !navigation.PauseMenu.IsOpen));
                if (s.Paused == 0)
                    f.Age += Time.unscaledDeltaTime;
                var canvas = NightHud.RewardSpace;
                f.Text.rectTransform.anchoredPosition = InterfaceSettings.Current.ReducedMotion ? new Vector2(0, canvas.rect.height * .42f) : Vector2.Lerp(f.From, new Vector2(0, canvas.rect.height * .42f), Mathf.SmoothStep(0, 1, f.Age / 1.2f));
                f.Text.color = new Color(1, .85f, .2f, 1 - math.saturate((f.Age - .8f) / .4f));
            }
        }

        internal void DrawNightResults(Action<float3, Vector3, Color> draw)
        {
            var s = sessionController.em.GetComponentData<Session>(sessionController.root);
            TickRewardFlights(s);
            if (NightHud == null)
                throw new InvalidOperationException("夜间 HUD 检查器引用缺失。");
            NightHud.ValidateConfiguration();
            nightMarkers = NightHud.MarkerRoot;
            bool visible = !sessionController.intel && (s.Phase == Phase.Night || s.Phase == Phase.Retreat || s.Phase == Phase.Celebration) && (navigation.PauseMenu == null || !navigation.PauseMenu.IsOpen) && (buildingController.BuildingConfirmPanel == null || !buildingController.BuildingConfirmPanel.activeSelf);
            nightMarkers.gameObject.SetActive(visible);
            if (!visible)
                return;
            var seenMarkers = new HashSet<ulong>();
            using var all = Sim.OrderedEntities<Identity>(sessionController.em);
            foreach (var e in all)
            {
                if (!sessionController.em.HasComponent<VisualState>(e) || sessionController.em.GetComponentData<VisualState>(e).Visible == 0)
                    continue;
                var position = Sim.Position(sessionController.em, e);
                var visual = sessionController.em.GetComponentData<VisualState>(e);
                if (visual.Celebrating != 0 && sessionController.em.HasComponent<Combatant>(e))
                {
                    // Semantic placeholder cues, keyed to simulation time, so pause freezes them as well.
                    float motion = InterfaceSettings.Current.ReducedMotion ? 0 : math.sin(s.Time * 5 + (float)sessionController.em.GetComponentData<Identity>(e).Id);
                    if (visual.Celebrating == (byte)NightEndPose.Celebrate)
                    {
                        draw(position + new float3(-.4f, 1 + motion * .18f, 0), new Vector3(.14f, .55f, .14f), Color.yellow);
                        draw(position + new float3(.4f, 1 + motion * .18f, 0), new Vector3(.14f, .55f, .14f), Color.yellow);
                    }
                    else if (visual.Celebrating == (byte)NightEndPose.Aid)
                    {
                        draw(position + new float3(0, 1.2f, 0), new Vector3(.6f, .15f, .15f), Color.cyan);
                        draw(position + new float3(0, 1.2f, 0), new Vector3(.15f, .6f, .15f), Color.cyan);
                    }
                    else
                        draw(position + new float3(0, 1.2f, 0), new Vector3(.15f, .5f, .15f), new Color(1, .65f, .2f));
                }

                bool loot = sessionController.em.HasComponent<Loot>(e), visitor = sessionController.em.HasComponent<Opportunity>(e);
                if (!loot && !visitor)
                    continue;
                Color color;
                string label;
                var id = sessionController.em.GetComponentData<Identity>(e);
                bool responding = false;
                if (loot)
                {
                    var drop = sessionController.em.GetComponentData<Loot>(e);
                    color = drop.Rarity >= 3 ? new Color(1, .65f, .15f) : drop.Rarity == 2 ? new Color(.7f, .4f, 1) : Color.cyan;
                    label = "特殊战利品 · " + sessionController.Name(drop.Item) + " × " + drop.Count;
                    draw(position + new float3(0, 1.4f, 0), new Vector3(.2f, 2.8f, .2f), color);
                }
                else
                {
                    var o = sessionController.em.GetComponentData<Opportunity>(e);
                    responding = sessionController.em.Exists(o.Responder);
                    color = o.Thief != 0 ? new Color(1, .4f, .2f) : new Color(.3f, 1, .7f);
                    label = (o.Thief != 0 ? "小偷" : "小精灵") + " · " + math.max(0, o.Expires - s.Time).ToString("0.0") + "秒";
                    label += responding ? "\n" + sessionController.em.GetComponentData<Identity>(o.Responder).Name + "接近中" : "\n点击派人拦截";
                    if (responding)
                        draw(Sim.Position(sessionController.em, o.Responder) + new float3(0, 1, 0), new Vector3(.2f, .6f, .2f), color);
                    draw(position + new float3(0, .65f + math.sin(s.Time * 3) * .15f, 0), new Vector3(.3f, .4f, .3f), color);
                }

                if (worldController.Camera == null)
                    continue;
                seenMarkers.Add(id.Id);
                if (!nightMarkerButtons.TryGetValue(id.Id, out var itemView))
                {
                    itemView = Instantiate(NightHud.MarkerTemplate, nightMarkers);
                    itemView.name = "Night marker " + id.Id;
                    itemView.gameObject.SetActive(true);
                    nightMarkerButtons.Add(id.Id, itemView);
                }

                if (itemView.Interaction.IsPinned) continue;
                var button = itemView.Select;
                button.gameObject.SetActive(true);
                var screen = worldController.Camera.WorldToScreenPoint((Vector3)position + Vector3.up * 1.6f);
                bool outside = screen.z <= 0 || screen.x < 110 || screen.x > Screen.width - 110 || screen.y < 75 || screen.y > Screen.height - 75;
                if (screen.z <= 0)
                {
                    screen.x = Screen.width - screen.x;
                    screen.y = Screen.height - screen.y;
                }

                screen.x = math.clamp(screen.x, 115, Screen.width - 115);
                screen.y = math.clamp(screen.y, 75, Screen.height - 75);
                var canvas = NightHud.RewardSpace;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(nightMarkers, screen, null, out var local);
                ((RectTransform)button.transform).anchoredPosition = local;
                itemView.Background.color = new Color(color.r * .25f, color.g * .25f, color.b * .25f, .95f);
                var textLabel = itemView.Label;
                textLabel.text = (outside ? "画面外 · " : "") + label;
                textLabel.color = color;
                button.interactable = s.Paused == 0 && (!responding || outside);
                button.onClick.RemoveAllListeners();
                ulong key = id.Id;
                button.onClick.AddListener(() =>
                {
                    if (outside)
                        navigation.LocateGarrison(key);
                    else
                        commandsController.Send(CommandKind.PickUp, key);
                });
            }

            foreach (var key in new List<ulong>(nightMarkerButtons.Keys))
                if (!seenMarkers.Contains(key) && !nightMarkerButtons[key].Interaction.IsPinned)
                { Destroy(nightMarkerButtons[key].gameObject); nightMarkerButtons.Remove(key); }
        }

        public void ShowMessage(string text)
        {
            Message.text = text;
            sessionController.nextRefresh = 0;
        }

        internal void ResetSession()
        {
            foreach (var flight in rewardFlights)
                if (flight.Text != null)
                    Destroy(flight.Text.gameObject);
            rewardFlights.Clear();
            priorNightTime = 0;
            foreach (var item in heroButtons.Values)
                if (item != null)
                    Destroy(item.gameObject);
            heroButtons.Clear();
            heroIds.Clear();
            foreach (var item in nightMarkerButtons.Values)
                if (item != null)
                    Destroy(item.gameObject);
            nightMarkerButtons.Clear();
            Message.text = "";
        }
    }
}
