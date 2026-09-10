using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        RectTransform heroHud, heroCards;
        Text defenseStatus;
        Button defenseFocus;
        readonly List<Button> heroButtons = new List<Button>();
        readonly List<ulong> heroIds = new List<ulong>();
        void HeroHotkeys(Keyboard keyboard)
        {
            var s = em.GetComponentData<Session>(root); if (s.Phase != Phase.Night && s.Phase != Phase.Retreat) return;
            for (int i = 0; i < math.min(9, heroIds.Count); i++) if (keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) Send(CommandKind.SelectHero, heroIds[i]);
            if (keyboard.backquoteKey.wasPressedThisFrame) Send(CommandKind.SelectHero);
            if (keyboard.hKey.wasPressedThisFrame && Sim.Alive(em, s.SelectedHero)) Send(CommandKind.MoveHero, position: Sim.Position(em, s.SelectedHero));
        }
        void RefreshHeroHud()
        {
            if (heroHud == null)
            {
                heroHud = TechnologyTreeView.Rect("Battle HUD", GetComponentInParent<Canvas>().transform, new Vector2(.32f, .14f), new Vector2(.75f, .34f));
                heroHud.gameObject.AddComponent<Image>().color = new Color(.055f, .075f, .10f, .96f);
                var status = TechnologyTreeView.Rect("Defense summary", heroHud, new Vector2(0, .61f), Vector2.one);
                defenseStatus = status.gameObject.AddComponent<Text>(); defenseStatus.font = Status.font; defenseStatus.fontSize = 15; defenseStatus.color = Color.white; defenseStatus.alignment = TMPro.TextAlignmentOptions.TopLeft;
                defenseFocus = status.gameObject.AddComponent<Button>(); defenseFocus.targetGraphic = defenseStatus;
                var viewport = TechnologyTreeView.Rect("Hero viewport", heroHud, new Vector2(0, .14f), new Vector2(1, .61f)); viewport.gameObject.AddComponent<RectMask2D>();
                heroCards = TechnologyTreeView.Rect("Hero portraits", viewport, Vector2.zero, Vector2.one); heroCards.pivot = new Vector2(0, .5f);
                var layout = heroCards.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.childControlWidth = true; layout.childForceExpandWidth = false; layout.spacing = 5;
                var fitter = heroCards.gameObject.AddComponent<ContentSizeFitter>(); fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = heroCards; scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
                var hint = TechnologyTreeView.Rect("Hero controls", heroHud, Vector2.zero, new Vector2(1, .14f)); var label = hint.gameObject.AddComponent<Text>(); label.font = Status.font; label.fontSize = 13; label.color = Color.white; label.text = "数字 1–9 / 点击头像选择　右键地面移动　H 驻守　` 取消选择　Esc 暂停菜单"; label.raycastTarget = false;
            }
            var s = em.GetComponentData<Session>(root); bool visible = s.Phase == Phase.Night || s.Phase == Phase.Retreat || s.Phase == Phase.Celebration;
            visible &= !intel && Panel != "科技" && Panel != "任务" && (BuildingConfirmPanel == null || !BuildingConfirmPanel.activeSelf);
            heroHud.gameObject.SetActive(visible); if (!visible) { heroIds.Clear(); return; }
            int deployed = 0, returning = 0, housed = 0, dead = 0, enemies = 0; bool danger = false;
            using (var actors = Sim.Entities<Combatant>(em)) foreach (var e in actors)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (a.Faction == 0) { if (!Sim.Alive(em, e)) { if (!em.HasComponent<Hero>(e) || em.GetComponentData<Hero>(e).DeathPending != 0) dead++; } else if (a.Deployed != 0) deployed++; if (em.HasComponent<Soldier>(e)) { var soldier = em.GetComponentData<Soldier>(e); if (soldier.RecallState == 1) returning++; if (soldier.RecallState == 2) housed++; } }
                else if (Sim.Alive(em, e) && a.Deployed != 0 && em.HasComponent<VisualState>(e) && em.GetComponentData<VisualState>(e).Visible != 0)
                { enemies++; if (em.Exists(a.Target) && em.HasComponent<BuildingStats>(a.Target) && em.GetComponentData<BuildingStats>(a.Target).IsCore != 0 && CombatOps.Distance(em, e, a.Target) <= a.Range + 1) danger = true; }
            }
            defenseStatus.text = (danger ? "核心遭到攻击！\n" : "") + $"出场 {deployed} · 召回中 {returning} · 归营 {housed} · 阵亡 {dead} · 已现身敌军 {enemies}\n" + (s.ActiveBell != 0 ? "警铃集结：" + EntityName(s.ActiveBell) : "警铃未激活") + (s.Phase == Phase.Retreat ? " · 敌军撤退中" : s.Phase == Phase.Celebration ? " · 战斗结束，十秒收尾" : "");
            defenseStatus.color = danger ? new Color(1, .4f, .35f) : Color.white;
            ulong attention = 0; int priority = int.MaxValue; string attentionText = "";
            using (var all = Sim.OrderedEntities<Identity>(em)) foreach (var e in all)
            {
                if (!em.HasComponent<VisualState>(e)) continue;
                int candidate = int.MaxValue; string label = "";
                if (em.HasComponent<Hero>(e) && em.GetComponentData<Hero>(e).DeathPending != 0) { candidate = 1; label = "英雄阵亡"; }
                else if (em.GetComponentData<VisualState>(e).Visible != 0 && em.HasComponent<Combatant>(e) && em.GetComponentData<Combatant>(e).Faction == 1 && Sim.Alive(em, e))
                { var actor = em.GetComponentData<Combatant>(e); candidate = danger && em.Exists(actor.Target) && em.HasComponent<BuildingStats>(actor.Target) && em.GetComponentData<BuildingStats>(actor.Target).IsCore != 0 ? 0 : 2; label = candidate == 0 ? "核心受袭" : "敌军"; }
                else if (em.GetComponentData<VisualState>(e).Visible != 0 && em.HasComponent<Opportunity>(e)) { candidate = 3; label = "可交互机会"; }
                if (candidate >= priority || Camera == null) continue;
                var screen = Camera.WorldToViewportPoint(Sim.Position(em, e)); if (screen.z > 0 && screen.x >= 0 && screen.x <= 1 && screen.y >= 0 && screen.y <= 1) continue;
                priority = candidate; attention = em.GetComponentData<Identity>(e).Id; attentionText = (screen.x < 0 ? "左" : screen.x > 1 ? "右" : screen.y > 1 ? "上" : "下") + "侧画面外 · " + label;
            }
            defenseFocus.onClick.RemoveAllListeners(); defenseFocus.interactable = attention != 0;
            if (attention != 0) { ulong focus = attention; defenseStatus.text += "\n" + attentionText + "（点击定位）"; defenseFocus.onClick.AddListener(() => LocateGarrison(focus)); }
            heroIds.Clear(); using var heroes = Sim.OrderedEntities<Hero>(em);
            int count = 0;
            foreach (var e in heroes)
            {
                var id = em.GetComponentData<Identity>(e); var h = em.GetComponentData<Hero>(e); var a = em.GetComponentData<Combatant>(e); var hp = em.GetComponentData<Health>(e); bool active = h.Recruited != 0 && hp.Current > 0 && a.Deployed != 0;
                heroIds.Add(id.Id); Button button;
                if (count == heroButtons.Count)
                {
                    var card = Instantiate(RowTemplate, heroCards); card.SetActive(true); button = card.GetComponent<Button>(); var layout = card.GetComponent<LayoutElement>() ?? card.AddComponent<LayoutElement>(); layout.preferredWidth = 225; layout.minWidth = 225; heroButtons.Add(button);
                    var portrait = TechnologyTreeView.Rect("Hero portrait", card.transform, new Vector2(0, .15f), new Vector2(.2f, .85f)); portrait.gameObject.AddComponent<Image>().preserveAspect = true;
                }
                button = heroButtons[count]; button.gameObject.SetActive(true); button.onClick.RemoveAllListeners(); ulong key = id.Id;
                button.interactable = s.Paused == 0 && s.Phase != Phase.Celebration; ulong sanctum = h.Sanctum;
                button.onClick.AddListener(() => { if (active) Send(CommandKind.SelectHero, key); else { LocateGarrison(sanctum); showBuildingDetails = true; OpenPanel("建筑"); } });
                var label = button.GetComponentInChildren<Text>(); label.fontSize = 14; label.enableAutoSizing = false;
                var portraitImage = button.transform.Find("Hero portrait").GetComponent<Image>(); portraitImage.sprite = BuildingSource(id.Definition)?.Icon; portraitImage.color = portraitImage.sprite != null ? Color.white : new Color(.3f, .48f, .65f); portraitImage.raycastTarget = false;PortraitImageBinding.Bind(portraitImage,em,root,id.Id);
                label.rectTransform.offsetMin = new Vector2(48, 2);
                string state = h.DeathPending != 0 ? "阵亡 · 黎明开始冷却" : h.Recruited == 0 ? "冷却 " + math.max(0, h.CooldownUntil - s.Turn) + " 回合" : !active ? "待神殿唤醒" : $"生命 {hp.Current:0}/{hp.Maximum:0}";
                label.text = (s.SelectedHero == e ? "▶ " : "") + (count < 9 ? (count + 1) + " " : "") + id.Name + " Lv." + MilitaryOps.HeroLevel(em, root, e) + "\n" + state + "\n经验 " + h.Experience + " · 本夜 +" + MilitaryOps.HeroBattleExperience(em, root, e);
                count++;
            }
            for (int i = count; i < heroButtons.Count; i++) heroButtons[i].gameObject.SetActive(false);
        }
    }
}
