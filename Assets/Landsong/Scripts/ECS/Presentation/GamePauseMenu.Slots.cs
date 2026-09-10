using System;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class GamePauseMenu
    {
        TMP_InputField slotName;string slotSignature;
        void InitializeSlotManager()
        {
            slotName=InterfaceWidgets.Input("存档名称（新建 / 重命名）",SavesPage.transform,Status.font);slotName.transform.SetAsFirstSibling();
            var card=MainPage.transform.parent as RectTransform;if(card!=null){card.anchorMin=new Vector2(.19f,.06f);card.anchorMax=new Vector2(.81f,.94f);}
        }
        void RefreshSlotManager(RunArchiveStore store,string run,Session session,CheckpointSystem system)
        {
            var slots=store.Slots(run);var current=store.ActiveSlot(run);bool ready=session.Phase==Phase.Day&&session.CheckpointPending==0;
            string signature=run+":"+current+":"+ready+":"+string.Join("|",slots.Select(key=>key+File.GetLastWriteTimeUtc(store.SlotPath(run,key)).Ticks+File.GetLastWriteTimeUtc(store.SlotPath(run,key)+".bak").Ticks+File.GetLastWriteTimeUtc(store.SlotPath(run,key)+".name").Ticks));
            if(slotSignature==signature)return;slotSignature=signature;
            var scroll=SlotRows.GetComponentInParent<ScrollRect>();var position=scroll!=null?scroll.verticalNormalizedPosition:1;
            foreach(Transform child in SlotRows)if(child.gameObject!=SlotTemplate.gameObject){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            void ActionSafe(Action action){try{action();slotSignature=null;}catch(Exception error){View.Message.text="存档操作未完成："+error.Message;}Page(SavesPage);}
            foreach(var key in slots)
            {
                var info=store.Describe(run,key);var label=(key==current?"当前 · ":"")+info.Name+" · "+key.Substring(0,6)+"\n"+(info.Valid?info.Dynasty+" · "+info.Map+" · 回合 "+info.Turn+" · "+info.Stage+(info.Backup?" · 备份可恢复":""):"不可载入："+info.Error)+"\n"+info.Saved.ToString("yyyy-MM-dd HH:mm:ss");
                var button=Instantiate(SlotTemplate,SlotRows);button.gameObject.SetActive(true);button.GetComponentInChildren<TextMeshProUGUI>().text=label;button.GetComponent<LayoutElement>().preferredHeight=105;button.interactable=ready&&info.Valid;
                button.onClick.AddListener(()=>Confirm("载入“"+info.Name+"”？当前未保存进度将丢失。载入后此槽成为快速保存目标。"+(info.Backup?"正式记录损坏，将读取备份，原件保留。":""),()=>{View.Send(CommandKind.Load,text:key);Page(MainPage);}));
                InterfaceWidgets.Button("重命名（使用上方输入）",SlotRows,Status.font,ready?()=>Confirm("把此槽重命名为“"+RunArchiveStore.DisplayName(slotName.text)+"”？",()=>ActionSafe(()=>system.ManageSlot(Sim.Root(World.DefaultGameObjectInjectionWorld.EntityManager),key,slotName.text,info.Stamp,false))):null,38);
                InterfaceWidgets.Button("覆盖此槽",SlotRows,Status.font,ready?()=>Confirm("覆盖“"+info.Name+"”？原版本成为备份；自动白天/黄昏节点及其他槽不受影响。",()=>{View.Send(CommandKind.Save,other:info.Stamp,argument:2,text:key);slotSignature=null;Page(SavesPage);}):null,38);
                if(File.Exists(store.SlotPath(run,key)+".bak"))InterfaceWidgets.Button("从此槽备份载入",SlotRows,Status.font,ready?()=>Confirm("读取此槽的上一版备份？不会删除损坏原件。",()=>{View.Send(CommandKind.Load,text:key,argument:1);Page(MainPage);}):null,38);
                InterfaceWidgets.Button("删除此独立槽及其备份",SlotRows,Status.font,ready?()=>Confirm("永久删除“"+info.Name+"”及其槽备份/缩略图？无法撤销。王朝、自动节点和其他槽均保留。",()=>ActionSafe(()=>system.ManageSlot(Sim.Root(World.DefaultGameObjectInjectionWorld.EntityManager),key,null,info.Stamp,true))):null,38);
            }
            if(slots.Length==0)InterfaceWidgets.Button("尚无独立存档；可以使用上方按钮创建。",SlotRows,Status.font,null,65);
            Canvas.ForceUpdateCanvases();if(scroll!=null)scroll.verticalNormalizedPosition=position;
        }
    }
}
