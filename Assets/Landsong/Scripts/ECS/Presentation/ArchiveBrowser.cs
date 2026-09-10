using System;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    // Menu-only disk browser. Loading still passes through LoadingTransition and full snapshot validation.
    public sealed class ArchiveBrowser : MonoBehaviour
    {
        public RunArchiveStore Store; public Action<string,string,bool> Load;
        RectTransform rows;TMP_FontAsset font;Action close;string run;int page;bool ended,confirming;Texture2D preview;
        public void Initialize(TMP_FontAsset value,Action back)
        {font=value;close=back;rows=InterfaceWidgets.Scroll(transform,"Archive list",new Vector2(.02f,.04f),new Vector2(.98f,.94f));}
        public void Begin(){run=null;page=0;ended=false;Refresh();}
        public void Back(){if(confirming){Refresh();return;}if(run!=null){run=null;page=0;Refresh();return;}close();}
        void Clear(){if(preview!=null)Destroy(preview);preview=null;foreach(Transform child in rows){child.gameObject.SetActive(false);Destroy(child.gameObject);}confirming=false;}
        void OnDestroy(){if(preview!=null)Destroy(preview);}
        void Button(string label,Action action=null,float height=48)=>InterfaceWidgets.Button(label,rows,font,action,height);
        void Safe(Action action){try{action();}catch(Exception e){Button("未完成："+e.Message,null,90);}}
        void Confirm(string text,Action action){Clear();confirming=true;Button(text,null,100);Button("确认",()=>Safe(action));Button("取消",Refresh);}
        public void Refresh()
        {
            Clear();var store=Store??CheckpointSystem.DefaultStore;
            Button("存档管理 · 自动节点与独立槽分开，删槽不会结束王朝",null,62);Button("关闭 / 返回",Back);Button(ended?"查看在世王朝":"查看覆灭记录",()=>{ended=!ended;run=null;page=0;Refresh();});
            if(ended){var history=store.Histories();foreach(var id in history.Skip(page*15).Take(15))Button(store.History(id),null,85);Pages(history.Length);return;}
            if(run==null)
            {
                var runs=store.Runs();foreach(var id in runs.Skip(page*15).Take(15))
                {var info=store.Describe(id);Button((info.Valid?info.Dynasty+" · "+info.Map+" · 回合 "+info.Turn+(info.Backup?"（备份）":""):"王朝记录不可读")+"\n"+id.Substring(0,8),()=>{run=id;page=0;Refresh();},75);}
                if(runs.Length==0)Button("暂无在世王朝存档。");Pages(runs.Length);return;
            }
            Button("返回王朝列表",()=>{run=null;page=0;Refresh();});var current=store.Describe(run);AddLoad(current,null);
            var slots=store.Slots(run);foreach(var slot in slots.Skip(page*15).Take(15))AddLoad(store.Describe(run,slot),slot);Pages(slots.Length);
            void AddLoad(ArchiveListing info,string slot)
            {
                Button(info.Name+" · "+(info.Valid?info.Dynasty+" / "+info.Map+" / 回合 "+info.Turn+" / "+info.Stage+(info.Backup?" / 备份可恢复":""):info.Error)+"\n"+info.Saved.ToString("yyyy-MM-dd HH:mm:ss"),()=>Details(info),92);
            }
        }
        void Pages(int count){if(page>0)Button("上一页",()=>{page--;Refresh();});if((page+1)*15<count)Button("下一页",()=>{page++;Refresh();});}
        void Details(ArchiveListing info)
        {
            Clear();var store=Store??CheckpointSystem.DefaultStore;Button(info.Name+" · "+info.Dynasty+" · "+info.Map+" · 回合 "+info.Turn,null,65);
            if(info.Valid)Button(info.Backup?"载入（恢复有效备份）":"载入此记录",()=>Confirm("载入“"+info.Name+"”？将经加载场景检查完整节点，原文件不删除。",()=>Load(info.Run,info.Slot,false)));
            else Button("无法载入："+info.Error,null,90);
            if(File.Exists((info.Slot==null?store.RunPath(info.Run):store.SlotPath(info.Run,info.Slot))+".bak"))Button("载入上一版备份",()=>Confirm("从上一版备份重新开始？文件原件保留。",()=>Load(info.Run,info.Slot,true)));
            if(info.Slot!=null)
            {
                var input=InterfaceWidgets.Input("存档名称",rows,font);input.SetTextWithoutNotify(info.Name);
                Button("重命名",()=>{var name=input.text;Confirm("将此槽重命名为“"+RunArchiveStore.DisplayName(name)+"”？",()=>{store.RenameSlot(info.Run,info.Slot,name,info.Stamp);Refresh();});});
                Button("永久删除此槽及其备份",()=>Confirm("只删除“"+info.Name+"”及其槽备份/缩略图，不删除王朝自动节点或其他槽。无法撤销。",()=>{store.DeleteSlot(info.Run,info.Slot,info.Stamp);Refresh();}));
                var path=store.PreviewPath(info.Run,info.Slot);if(File.Exists(path)&&new FileInfo(path).Length<4*1024*1024)
                {
                    preview=new Texture2D(2,2);if(preview.LoadImage(File.ReadAllBytes(path))){var rect=InterfaceWidgets.Rect("Saved preview",rows,Vector2.zero,Vector2.one);rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=180;var child=InterfaceWidgets.Rect("Image",rect,Vector2.zero,Vector2.one);var image=child.gameObject.AddComponent<UnityEngine.UI.RawImage>();image.texture=preview;image.raycastTarget=false;var aspect=child.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();aspect.aspectMode=UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;aspect.aspectRatio=(float)preview.width/preview.height;}
                }
            }
            Button("返回列表",Refresh);
        }
    }
}
