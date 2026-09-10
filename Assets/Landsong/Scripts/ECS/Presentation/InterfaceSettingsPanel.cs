using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong.ECS.Presentation
{
    public sealed class InterfaceSettingsPanel : MonoBehaviour
    {
        TMP_FontAsset font; Action close; RectTransform rows; InterfacePreferences draft, rollback;
        TextMeshProUGUI status; float deadline; int rebinding = -1; UnityEngine.UI.Button rebindButton;
        public bool AwaitingDisplayConfirmation => rollback != null;
        public void Initialize(TMP_FontAsset value, Action back)
        {
            font = value; close = back;
            rows = InterfaceWidgets.Scroll(transform,"Settings list",new Vector2(0,.15f),new Vector2(1,.94f));
            status = InterfaceWidgets.Text("",InterfaceWidgets.Rect("Settings status",transform,new Vector2(0,.94f),Vector2.one),font,16);
            var buttons = InterfaceWidgets.Rect("Settings actions",transform,Vector2.zero,new Vector2(1,.13f)); var layout = buttons.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>(); layout.spacing = 8;
            InterfaceWidgets.Button("应用 / 保留画面",buttons,font,Apply);
            InterfaceWidgets.Button("恢复默认（待应用）",buttons,font,() => { RevertDisplay(); draft = new InterfacePreferences(); Rebuild(); });
            InterfaceWidgets.Button("返回（丢弃未应用）",buttons,font,() => { RevertDisplay(); close(); });
        }
        public void Begin() { RevertDisplay(); draft = InterfaceSettings.Current.Copy(); Rebuild(); }
        void OnDisable() { RevertDisplay(); rebinding = -1; }
        void Rebuild()
        {
            foreach (Transform child in rows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            void Slider(string name,float min,float max,float value,Action<float> set) => InterfaceWidgets.Slider(name,rows,font,min,max,value,set);
            void Toggle(string name,Func<bool> get,Action<bool> set)
            {
                UnityEngine.UI.Button b = null; b = InterfaceWidgets.Button(name + (get() ? "：开" : "：关"),rows,font,() => { set(!get()); b.GetComponentInChildren<TextMeshProUGUI>().text = name + (get() ? "：开" : "：关"); });
            }
            Slider("主音量",0,1,draft.Master,v=>draft.Master=v); Slider("音乐音量",0,1,draft.Music,v=>draft.Music=v); Slider("音效 / UI 音量",0,1,draft.Effects,v=>draft.Effects=v); Slider("环境音量",0,1,draft.Ambient,v=>draft.Ambient=v);
            Toggle("静音",()=>draft.Muted,v=>draft.Muted=v);
            InterfaceWidgets.Button("分类音量即时作用于表现音源；主音量只在唯一监听器上应用。",rows,font,null,58);
            var languages=new[]{"zh-Hans","en"}.Concat(PresentationText.Packs.Keys.OrderBy(k=>k)).ToArray();
            UnityEngine.UI.Button language=null;language=InterfaceWidgets.Button("语言 / Language："+draft.Language,rows,font,()=>{int index=Array.IndexOf(languages,draft.Language);draft.Language=languages[(index+1)%languages.Length];language.GetComponentInChildren<TextMeshProUGUI>().text="语言 / Language："+draft.Language;});
            InterfaceWidgets.Button("重新扫描外部语言包",rows,font,()=>{PresentationText.Discover(PresentationRuntime.LanguageDirectory);Rebuild();});
            if(!string.IsNullOrEmpty(PresentationText.Diagnostics))InterfaceWidgets.Button(PresentationText.Diagnostics,rows,font,null,100);
            Slider("界面缩放",.8f,1.4f,draft.UiScale,v=>draft.UiScale=v); Slider("镜头速度",5,60,draft.CameraSpeed,v=>draft.CameraSpeed=v); Slider("缩放灵敏度",.25f,3,draft.ZoomSpeed,v=>draft.ZoomSpeed=v); Slider("镜头缓动（0 为即时）",0,.3f,draft.Smoothing,v=>draft.Smoothing=v);
            Toggle("全屏",()=>draft.Fullscreen<0?Screen.fullScreen:draft.Fullscreen!=0,v=>draft.Fullscreen=v?1:0);
            Toggle("减少动态效果",()=>draft.ReducedMotion,v=>draft.ReducedMotion=v); Toggle("高对比度标记",()=>draft.HighContrast,v=>draft.HighContrast=v);
            Toggle("一般消息提示",()=>draft.GeneralMessages,v=>draft.GeneralMessages=v); Toggle("经济消息提示",()=>draft.EconomyMessages,v=>draft.EconomyMessages=v);
            InterfaceWidgets.Button("重要消息不静音；筛选不删除历史。情报仍为独立按钮。",rows,font,null,58);
            var resolutions = Screen.resolutions.Select(r=>(r.width,r.height)).Where(r=>r.width>=800&&r.height>=600).Distinct().ToList();
            if (!resolutions.Contains((Screen.width,Screen.height))) resolutions.Add((Screen.width,Screen.height));
            UnityEngine.UI.Button resolution = null; resolution = InterfaceWidgets.Button("分辨率："+(draft.Width>0?draft.Width:Screen.width)+" × "+(draft.Height>0?draft.Height:Screen.height)+"（点击切换）",rows,font,()=>
            { var index=resolutions.IndexOf((draft.Width>0?draft.Width:Screen.width,draft.Height>0?draft.Height:Screen.height)); var r=resolutions[(index+1)%resolutions.Count]; draft.Width=r.width; draft.Height=r.height; resolution.GetComponentInChildren<TextMeshProUGUI>().text="分辨率："+r.width+" × "+r.height; });
            UnityEngine.UI.Button quality=null; quality=InterfaceWidgets.Button("画质："+QualitySettings.names[draft.Quality<0?QualitySettings.GetQualityLevel():draft.Quality]+"（点击切换）",rows,font,()=> {draft.Quality=((draft.Quality<0?QualitySettings.GetQualityLevel():draft.Quality)+1)%QualitySettings.names.Length;quality.GetComponentInChildren<TextMeshProUGUI>().text="画质："+QualitySettings.names[draft.Quality];});
            var labels=new[]{"镜头前移","镜头后移","镜头左移","镜头右移","镜头左转","镜头右转","暂停"};
            for(int i=0;i<labels.Length;i++) {int index=i;UnityEngine.UI.Button b=null; b=InterfaceWidgets.Button(labels[i]+"："+Keys()[i],rows,font,()=> {rebinding=index; rebindButton=b; status.text="按下新按键。Esc 取消；Esc / R / H / ` / 数字键保留，不允许重复。";});}
            InterfaceWidgets.Button("中键拖图；Q/E 默认旋转；滚轮缩放。触摸：单指点选/拖图、双指缩放旋转、长按移动选中英雄。Esc 始终打开暂停菜单。",rows,font,null,90);
            status.text="修改后点击应用。显示变更需在 15 秒内确认，否则恢复。";
        }
        Key[] Keys()=>new[]{draft.Forward,draft.Back,draft.Left,draft.Right,draft.RotateLeft,draft.RotateRight,draft.Pause};
        void SetKeys(Key[] keys) {draft.Forward=keys[0];draft.Back=keys[1];draft.Left=keys[2];draft.Right=keys[3];draft.RotateLeft=keys[4];draft.RotateRight=keys[5];draft.Pause=keys[6];}
        public bool CancelRebind() {if(rebinding<0)return false;rebinding=-1;status.text="已取消按键修改。";return true;}
        void Update()
        {
            if(rollback!=null) {status.text="保留画面设置？"+Mathf.CeilToInt(deadline-Time.unscaledTime)+" 秒后恢复。点击“应用 / 保留画面”。";if(Time.unscaledTime>=deadline){RevertDisplay();draft=InterfaceSettings.Current.Copy();Rebuild();}}
            if(rebinding<0||Keyboard.current==null)return;
            foreach(var control in Keyboard.current.allKeys) if(control.wasPressedThisFrame)
            {if(control.keyCode==Key.Escape){CancelRebind();return;}var keys=Keys();if(!InterfacePreferences.AllowedKey(control.keyCode)||keys.Where((_,i)=>i!=rebinding).Contains(control.keyCode)){status.text="该按键被保留或已使用，请选择其他按键。";return;}keys[rebinding]=control.keyCode;SetKeys(keys);rebindButton.GetComponentInChildren<TextMeshProUGUI>().text="已绑定："+control.keyCode;rebinding=-1;status.text="按键已修改，应用后保存。";return;}
        }
        void Apply()
        {
            if(rollback!=null){rollback=null;InterfaceSettings.Apply(draft);status.text="设置已保存。";return;}
            draft.Validate();bool changed=(draft.Width>0&&(draft.Width!=Screen.width||draft.Height!=Screen.height))||(draft.Fullscreen>=0&&(draft.Fullscreen!=0)!=Screen.fullScreen);
            if(changed){rollback=InterfaceSettings.Current.Copy();rollback.Width=Screen.width;rollback.Height=Screen.height;rollback.Fullscreen=Screen.fullScreen?1:0;deadline=Time.unscaledTime+15;InterfaceSettings.Apply(draft,false);}
            else {InterfaceSettings.Apply(draft);status.text="设置已保存。";}
        }
        void RevertDisplay(){if(rollback==null)return;var prior=rollback;rollback=null;InterfaceSettings.Apply(prior,false);}
    }
}
