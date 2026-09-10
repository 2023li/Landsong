using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        RectTransform nightMarkers;
        readonly List<Button> nightMarkerButtons = new List<Button>();
        int historyTurn;
        sealed class Flight { public Text Text; public Vector2 From; public float Age; }
        readonly List<Flight> rewardFlights = new List<Flight>();
        float priorNightTime;
        void RewardFlight(GameEvent reward)
        {
            if (Camera == null || reward.Amount <= 0) return;
            if (rewardFlights.Count >= 8) { Destroy(rewardFlights[0].Text.gameObject); rewardFlights.RemoveAt(0); }
            var canvas = GetComponentInParent<Canvas>(); var rect = TechnologyTreeView.Rect("Special reward to HUD", canvas.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f)); rect.sizeDelta = new Vector2(240, 38);
            var text = rect.gameObject.AddComponent<Text>(); text.font = Status.font; text.fontSize = 16; text.textWrappingMode = TMPro.TextWrappingModes.Normal; text.alignment = TMPro.TextAlignmentOptions.Center; text.raycastTarget = false; text.color = Color.yellow; text.text = "+ " + Name(reward.Definition) + " × " + reward.Amount + "（待结算）";
            var screen = Camera.WorldToScreenPoint(reward.Position); screen.x = math.clamp(screen.x, 120, Screen.width - 120); screen.y = math.clamp(screen.y, 70, Screen.height - 70);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            rect.anchoredPosition = local; rewardFlights.Add(new Flight { Text = text, From = local });
        }
        void TickRewardFlights(Session s)
        {
            bool clear = s.Phase == Phase.GameOver || s.Phase == Phase.Ended || s.Time < priorNightTime; priorNightTime = s.Time;
            for (int i = rewardFlights.Count - 1; i >= 0; i--)
            {
                var f = rewardFlights[i]; if (clear || f.Text == null || f.Age >= 1.2f) { if (f.Text != null) Destroy(f.Text.gameObject); rewardFlights.RemoveAt(i); continue; }
                f.Text.gameObject.SetActive(!intel && (PauseMenu == null || !PauseMenu.IsOpen)); if (s.Paused == 0) f.Age += Time.unscaledDeltaTime;
                var canvas = (RectTransform)GetComponentInParent<Canvas>().transform; f.Text.rectTransform.anchoredPosition = InterfaceSettings.Current.ReducedMotion ? new Vector2(0,canvas.rect.height*.42f) : Vector2.Lerp(f.From, new Vector2(0, canvas.rect.height * .42f), Mathf.SmoothStep(0, 1, f.Age / 1.2f));
                f.Text.color = new Color(1, .85f, .2f, 1 - math.saturate((f.Age - .8f) / .4f));
            }
        }
        void ReportHistoryRows()
        {
            if (!em.HasBuffer<BattleHistoryEntry>(root)) return;
            var turns = new List<int>(); foreach (var h in em.GetBuffer<BattleHistoryEntry>(root)) if (!turns.Contains(h.Turn)) turns.Add(h.Turn);
            turns.Reverse(); foreach (int turn in turns) Row("查看第 " + turn + " 夜已结算战报", () => { historyTurn = historyTurn == turn ? 0 : turn; nextRefresh = 0; });
            if (historyTurn == 0) return; var entries = new List<BattleReportEntry>(); foreach (var h in em.GetBuffer<BattleHistoryEntry>(root)) if (h.Turn == historyTurn) entries.Add(h.Entry);
            Row("第 " + historyTurn + " 夜 · 已提交记录"); foreach (var line in NightReportOps.Lines(em, root, entries)) Row(line);
        }
        void DrawNightResults(Action<float3, Vector3, Color> draw)
        {
            var s = em.GetComponentData<Session>(root);
            TickRewardFlights(s);
            if (nightMarkers == null) nightMarkers = TechnologyTreeView.Rect("Night interaction markers", GetComponentInParent<Canvas>().transform, Vector2.zero, Vector2.one);
            bool visible = !intel && (s.Phase == Phase.Night || s.Phase == Phase.Retreat || s.Phase == Phase.Celebration) && (PauseMenu == null || !PauseMenu.IsOpen) && (BuildingConfirmPanel == null || !BuildingConfirmPanel.activeSelf);
            nightMarkers.gameObject.SetActive(visible); if (!visible) return;
            int count = 0; using var all = Sim.OrderedEntities<Identity>(em);
            foreach (var e in all)
            {
                if (!em.HasComponent<VisualState>(e) || em.GetComponentData<VisualState>(e).Visible == 0) continue;
                var position = Sim.Position(em, e); var visual = em.GetComponentData<VisualState>(e);
                if (visual.Celebrating != 0 && em.HasComponent<Combatant>(e))
                {
                    // Semantic placeholder cues, keyed to simulation time, so pause freezes them as well.
                    float motion = InterfaceSettings.Current.ReducedMotion?0:math.sin(s.Time * 5 + (float)em.GetComponentData<Identity>(e).Id);
                    if (visual.Celebrating == (byte)NightEndPose.Celebrate)
                    { draw(position + new float3(-.4f, 1 + motion * .18f, 0), new Vector3(.14f, .55f, .14f), Color.yellow); draw(position + new float3(.4f, 1 + motion * .18f, 0), new Vector3(.14f, .55f, .14f), Color.yellow); }
                    else if (visual.Celebrating == (byte)NightEndPose.Aid)
                    { draw(position + new float3(0, 1.2f, 0), new Vector3(.6f, .15f, .15f), Color.cyan); draw(position + new float3(0, 1.2f, 0), new Vector3(.15f, .6f, .15f), Color.cyan); }
                    else draw(position + new float3(0, 1.2f, 0), new Vector3(.15f, .5f, .15f), new Color(1, .65f, .2f));
                }
                bool loot = em.HasComponent<Loot>(e), visitor = em.HasComponent<Opportunity>(e); if (!loot && !visitor) continue;
                Color color; string label; var id = em.GetComponentData<Identity>(e); bool responding = false;
                if (loot)
                {
                    var drop = em.GetComponentData<Loot>(e); color = drop.Rarity >= 3 ? new Color(1, .65f, .15f) : drop.Rarity == 2 ? new Color(.7f, .4f, 1) : Color.cyan;
                    label = "特殊战利品 · " + Name(drop.Item) + " × " + drop.Count;
                    draw(position + new float3(0, 1.4f, 0), new Vector3(.2f, 2.8f, .2f), color);
                }
                else
                {
                    var o = em.GetComponentData<Opportunity>(e); responding = em.Exists(o.Responder);
                    color = o.Thief != 0 ? new Color(1, .4f, .2f) : new Color(.3f, 1, .7f);
                    label = (o.Thief != 0 ? "小偷" : "小精灵") + " · " + math.max(0, o.Expires - s.Time).ToString("0.0") + "秒";
                    label += responding ? "\n" + em.GetComponentData<Identity>(o.Responder).Name + "接近中" : "\n点击派人拦截";
                    if (responding) draw(Sim.Position(em, o.Responder) + new float3(0, 1, 0), new Vector3(.2f, .6f, .2f), color);
                    draw(position + new float3(0, .65f + math.sin(s.Time * 3) * .15f, 0), new Vector3(.3f, .4f, .3f), color);
                }
                if (Camera == null) continue;
                if (count == nightMarkerButtons.Count)
                {
                    var go = new GameObject("Night marker", typeof(RectTransform), typeof(Image), typeof(Button)); var rect = (RectTransform)go.transform; rect.SetParent(nightMarkers, false); rect.sizeDelta = new Vector2(215, 48);
                    var textRect = TechnologyTreeView.Rect("TMP label", rect, Vector2.zero, Vector2.one); var text = textRect.gameObject.AddComponent<Text>(); text.font = Status.font; text.fontSize = 14; text.textWrappingMode = TMPro.TextWrappingModes.Normal; text.alignment = TMPro.TextAlignmentOptions.Center; text.raycastTarget = false;
                    nightMarkerButtons.Add(go.GetComponent<Button>());
                }
                var button = nightMarkerButtons[count++]; button.gameObject.SetActive(true); var screen = Camera.WorldToScreenPoint((Vector3)position + Vector3.up * 1.6f);
                bool outside = screen.z <= 0 || screen.x < 110 || screen.x > Screen.width - 110 || screen.y < 75 || screen.y > Screen.height - 75;
                if (screen.z <= 0) { screen.x = Screen.width - screen.x; screen.y = Screen.height - screen.y; }
                screen.x = math.clamp(screen.x, 115, Screen.width - 115); screen.y = math.clamp(screen.y, 75, Screen.height - 75);
                var canvas = GetComponentInParent<Canvas>(); RectTransformUtility.ScreenPointToLocalPointInRectangle(nightMarkers, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
                ((RectTransform)button.transform).anchoredPosition = local;
                button.GetComponent<Image>().color = new Color(color.r * .25f, color.g * .25f, color.b * .25f, .95f); var textLabel = button.GetComponentInChildren<Text>(); textLabel.text = (outside ? "画面外 · " : "") + label; textLabel.color = color;
                button.interactable = s.Paused == 0 && (!responding || outside); button.onClick.RemoveAllListeners(); ulong key = id.Id;
                button.onClick.AddListener(() => { if (outside) LocateGarrison(key); else Send(CommandKind.PickUp, key); });
            }
            for (int i = count; i < nightMarkerButtons.Count; i++) nightMarkerButtons[i].gameObject.SetActive(false);
        }
    }
}
