using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        int historyCategory=-1,historyPage,historyFilterTurn;string historySearch="";TMP_InputField historyFilter;RectTransform historyTools;
        CheckpointSystem previewCheckpoint;
        WorldPresentationView worldPresentation;
        void ConsumeInterfaceEvents(Session s)
        {
            var events=em.GetBuffer<GameEvent>(root);
            for(int i=0;i<events.Length;i++)
            {
                var e=events[i];if(worldPresentation!=null)worldPresentation.Consume(em,root,e);if(e.Kind==EventKind.Reward&&s.Phase!=Phase.GameOver&&s.Phase!=Phase.Ended)RewardFlight(e);
                if(e.Kind==EventKind.Message||e.Kind==EventKind.Ruin)
                {
                    BindInvitationMessage(e);var category=HistoryOps.Category(e.Kind,e.Message.ToString());
                    if(category==HistoryCategory.Important||category==HistoryCategory.Economy&&InterfaceSettings.Current.EconomyMessages||category==HistoryCategory.General&&InterfaceSettings.Current.GeneralMessages)
                        Message.text=e.Message.ToString()=="研究完成"?"研究完成："+Name(e.Definition)+"（奖励见科技详情）":e.Message.ToString();
                }
                else if(e.Kind==EventKind.CommandResult&&e.Result!=ResultCode.Success){Message.text=ResultName(e.Result);if(e.Result==ResultCode.ConfirmationRequired)OpenPanel("入夜确认");if(e.Result==ResultCode.QuestOverflow)OpenPanel("任务");}
            }
            // IO requests remain owned by CheckpointSystem; all presentation events are consumed even while a text field is focused.
            for(int i=events.Length-1;i>=0;i--){var kind=events[i].Kind;if(kind!=EventKind.Save&&kind!=EventKind.Load&&kind!=EventKind.Retry&&kind!=EventKind.EndDynasty&&kind!=EventKind.DayCheckpoint&&kind!=EventKind.DuskCheckpoint)events.RemoveAt(i);}
        }
        void InitializeInterface()
        {
            worldPresentation=gameObject.AddComponent<WorldPresentationView>();
            var canvas=GetComponentInParent<Canvas>().transform;
            var navigation=InterfaceWidgets.Rect("Interface navigation",canvas,new Vector2(.35f,.805f),new Vector2(.63f,.845f));var layout=navigation.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=5;
            InterfaceWidgets.Button("返回面板",navigation,Status.font,BackPanel,32);InterfaceWidgets.Button("消息 / 历史",navigation,Status.font,()=>OpenPanel("历史"),32);
            previewCheckpoint=World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CheckpointSystem>();if(previewCheckpoint!=null)previewCheckpoint.SlotSaved+=CaptureSlotPreview;
        }
        void CaptureSlotPreview(RunArchiveStore store,string run,string slot)
        {
            if(Camera==null)return;var texture=RenderTexture.GetTemporary(320,180,16,RenderTextureFormat.ARGB32);var previous=Camera.targetTexture;var active=RenderTexture.active;Texture2D image=null;
            try {Camera.targetTexture=texture;Camera.Render();RenderTexture.active=texture;image=new Texture2D(320,180,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,320,180),0,0);image.Apply();store.AtomicWrite(store.PreviewPath(run,slot),image.EncodeToPNG());}
            finally{Camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(texture);if(image!=null)Destroy(image);}
        }
        void OnDestroy(){if(previewCheckpoint!=null)previewCheckpoint.SlotSaved-=CaptureSlotPreview;}
        void HistoryRows()
        {
            Row("王朝历史 · 按来源快照记录；筛选不删除记录，也不重新结算收益。");
            Row("分类："+(historyCategory<0?"全部":new[]{"一般消息","经济收支","已结算夜战","重要消息"}[historyCategory]),()=>{historyCategory=historyCategory==3?-1:historyCategory+1;historyPage=0;nextRefresh=0;});
            Row(historyFilterTurn==0?"回合：全部（点击仅看当前回合）":"回合："+historyFilterTurn+"（点击查看全部）",()=>{historyFilterTurn=historyFilterTurn==0?em.GetComponentData<Session>(root).Turn:0;historyPage=0;nextRefresh=0;});
            if(historyTools==null)
            {
                historyTools=InterfaceWidgets.Rect("History filter",GetComponentInParent<Canvas>().transform,new Vector2(.01f,.805f),new Vector2(.29f,.845f));
                historyFilter=InterfaceWidgets.Input("筛选来源 / 内容",historyTools,Status.font,32);historyFilter.onValueChanged.AddListener(value=>{historySearch=value;historyPage=0;nextRefresh=0;});
            }
            historyTools.gameObject.SetActive(true);
            var entries=new List<(int turn,string text,Action locate)>();
            var totals=new SortedDictionary<int,(long income,long expense)>();
            if(em.HasBuffer<HistoryEntry>(root))foreach(var h in em.GetBuffer<HistoryEntry>(root))
            {
                if(historyCategory>=0&&(int)h.Category!=historyCategory||historyFilterTurn!=0&&h.Turn!=historyFilterTurn)continue;
                var line=$"回合 {h.Turn} · {h.SourceName} · {h.Text}";
                if(h.Item>=0)line+=$" · {Name(h.Item)} {(h.Delta>0?"+":"")}{h.Delta}（{(h.Pending!=0?"待存放":"库存")}）";
                if(h.Count>1)line+=" × "+h.Count;
                if(!string.IsNullOrWhiteSpace(historySearch)&&line.IndexOf(historySearch,StringComparison.OrdinalIgnoreCase)<0)continue;
                if(h.Item>=0&&h.Transfer==0){totals.TryGetValue(h.Item,out var total);if(h.Delta>0)total.income+=h.Delta;else total.expense-=(long)h.Delta;totals[h.Item]=total;}
                var position=(Vector3)h.Position;var source=h.Source;bool locate=h.HasPosition!=0;entries.Add((h.Turn,line,locate?()=>LocateHistory(source,position):null));
            }
            if((historyCategory==-1||historyCategory==2)&&em.HasBuffer<BattleHistoryEntry>(root))
            {
                var reports=new SortedDictionary<int,List<BattleReportEntry>>();foreach(var h in em.GetBuffer<BattleHistoryEntry>(root)){if(historyFilterTurn!=0&&h.Turn!=historyFilterTurn)continue;if(!reports.TryGetValue(h.Turn,out var list))reports.Add(h.Turn,list=new List<BattleReportEntry>());list.Add(h.Entry);}
                foreach(var pair in reports)foreach(var line in NightReportOps.Lines(em,root,pair.Value)){var label="夜晚 "+pair.Key+" · "+line;if(string.IsNullOrWhiteSpace(historySearch)||label.IndexOf(historySearch,StringComparison.OrdinalIgnoreCase)>=0)entries.Add((pair.Key,label,null));}
            }
            entries=entries.OrderByDescending(e=>e.turn).ToList();int pages=Math.Max(1,(entries.Count+39)/40);historyPage=Mathf.Clamp(historyPage,0,pages-1);
            if(totals.Count>0){Row("当前筛选的经济历史汇总（转库不作收入/支出；夜战另按完整战报显示）");foreach(var t in totals)Row(Name(t.Key)+" · 收入 +"+t.Value.income+" / 支出 -"+t.Value.expense+" / 净额 "+Signed(t.Value.income-t.Value.expense));}
            Row($"第 {historyPage+1}/{pages} 页 · {entries.Count} 条；一般/经济历史保留最近 {HistoryOps.Limit} 条，夜战按完整回合保留。");
            if(historyPage>0)Row("上一页",()=>{historyPage--;nextRefresh=0;});if(historyPage+1<pages)Row("下一页",()=>{historyPage++;nextRefresh=0;});
            foreach(var entry in entries.Skip(historyPage*40).Take(40))Row(entry.text,entry.locate);if(entries.Count==0)Row("没有匹配记录。");
        }
        void LocateHistory(ulong source,Vector3 oldPosition)
        {
            if(PauseMenu!=null&&PauseMenu.IsOpen)return;var entity=Sim.Find(em,source);var point=entity!=Entity.Null&&em.HasComponent<Unity.Transforms.LocalTransform>(entity)?(Vector3)Sim.Position(em,entity):oldPosition;
            var direction=Camera.transform.forward;float distance=Mathf.Abs(direction.y)>.001f?(point.y-Camera.transform.position.y)/direction.y:0;
            Camera.transform.position=ClampCameraPosition(Camera,em.GetComponentData<GridData>(root),point-direction*Mathf.Max(0,distance));
            if(entity==Entity.Null)Message.text="来源已不存在，已定位当时的位置。";
        }
        void RefreshInterfaceBarrier()
        {
            ApplyInterfacePreferences();if(historyTools!=null)historyTools.gameObject.SetActive(Panel=="历史"&&(PauseMenu==null||!PauseMenu.IsOpen)&&!intel);
            Selection.gameObject.SetActive(Panel!="历史");
            var canvas=GetComponentInParent<Canvas>();var group=canvas.GetComponent<CanvasGroup>();if(group==null)group=canvas.gameObject.AddComponent<CanvasGroup>();
            bool confirm=BuildingConfirmPanel!=null&&BuildingConfirmPanel.activeSelf;group.interactable=!(PauseMenu!=null&&PauseMenu.IsOpen)&&!confirm;
            if(!group.interactable){cameraVelocity=Vector3.zero;cameraDragging=false;}
        }
    }
}
