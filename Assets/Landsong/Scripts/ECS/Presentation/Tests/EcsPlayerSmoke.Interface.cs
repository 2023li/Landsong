#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator InterfaceUi(EcsGameView view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);var checkpoint=World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>();var archive=checkpoint.Export(root);var priorStore=checkpoint.Store;
            var preferences=InterfaceSettings.Current.Copy();var cameraPosition=view.Camera.transform.position;var cameraRotation=view.Camera.transform.rotation;float zoom=view.Camera.orthographicSize;
            var priorPersistence=InterfaceSettings.PersistenceOverride;string savedPreferences=null;InterfaceSettings.PersistenceOverride=json=>savedPreferences=json;
            GameObject browserModal=null;var background=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();var touch=InputSystem.AddDevice<Touchscreen>();
            string directory=Path.Combine(Path.GetTempPath(),"Landsong-14-UI-"+Guid.NewGuid().ToString("N"));var store=new RunArchiveStore(directory);checkpoint.Store=store;
            IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));for(int frame=0;frame<4;frame++){keyboard.MakeCurrent();yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
            Button Find(Transform parent,string text)=>parent.GetComponentsInChildren<Button>().First(b=>b.GetComponentInChildren<TMP_Text>()?.text.Contains(text)==true);
            try
            {
                InterfaceSettings.Apply(new InterfacePreferences(),false,false);view.OpenPanel("王室");yield return new WaitForSecondsRealtime(.35f);
                var first=view.PrimaryRows.GetComponentsInChildren<Button>().First();int identity=first.GetInstanceID();var scroll=view.PrimaryRows.GetComponentInParent<ScrollRect>();scroll.verticalNormalizedPosition=.4f;
                yield return new WaitForSecondsRealtime(.6f);Require(view.PrimaryRows.GetComponentsInChildren<Button>().First().GetInstanceID()==identity&&Mathf.Abs(scroll.verticalNormalizedPosition-.4f)<.05f,"14 row identity and scroll survive timed refresh");
                view.OpenPanel("历史");Sim.Emit(em,root,EventKind.Message,"可定位验收消息");yield return new WaitForSecondsRealtime(.35f);
                Require(view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("可定位验收消息")),"14 authoritative message history is browseable");
                var filter=view.GetComponentInParent<Canvas>().GetComponentsInChildren<TMP_InputField>().First(i=>i.gameObject.name=="筛选来源 / 内容");filter.SetTextWithoutNotify("历史焦点");filter.ActivateInputField();yield return null;
                var before=view.Camera.transform.position;yield return KeyPress(Key.W);Require(view.Camera.transform.position==before,"14 focused TMP input owns keyboard and camera");filter.DeactivateInputField();UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                view.BackPanel();yield return new WaitForSecondsRealtime(.3f);Require(view.Panel=="王室","14 panel return stack restores previous panel");
                view.OpenPanel("建筑");yield return null;before=view.Camera.transform.position;yield return KeyPress(Key.W);Require(view.Camera.transform.position!=before,"14 default camera movement remains usable");
                var rotation=view.Camera.transform.rotation;yield return KeyPress(Key.E);Require(view.Camera.transform.rotation!=rotation,"14 keyboard camera rotation works");
                // Start away from the map clamp and choose an actually unoccluded UI ray, not an assumed screen coordinate.
                var mapGrid=em.GetComponentData<GridData>(root);var center=mapGrid.Origin+new Unity.Mathematics.float3((mapGrid.Value.Value.Min.x+mapGrid.Value.Value.Size.x*.5f)*mapGrid.CellSize,0,(mapGrid.Value.Value.Min.y+mapGrid.Value.Value.Size.y*.5f)*mapGrid.CellSize);
                view.Camera.transform.position=(Vector3)center-view.Camera.transform.forward*45;
                var position=new Vector2(Screen.width*.52f,Screen.height*.52f);var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=position},hits);
                Require(hits.Count==0,"14 drag fixture starts outside UI ownership");InputSystem.QueueStateEvent(mouse,new MouseState {position=position});yield return null;
                var blocked=new Vector2(Screen.width*.78f,Screen.height*.52f);before=view.Camera.transform.position;
                InputSystem.QueueStateEvent(mouse,new MouseState {position=blocked,buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position,buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position});yield return null;
                Require(view.Camera.transform.position==before,"14 drag starting over task UI cannot leak onto map");
                before=view.Camera.transform.position;InputSystem.QueueStateEvent(mouse,new MouseState {position=position,buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position+new Vector2(90,0),buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position+new Vector2(90,0)});yield return null;
                Require(view.Camera.transform.position!=before,"14 middle-button drag pans without world clicks");
                float size=view.Camera.orthographicSize;InputSystem.QueueStateEvent(touch,new TouchState {touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=position});InputSystem.QueueStateEvent(touch,new TouchState {touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Began,position=position+new Vector2(90,0)});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=position+new Vector2(180,30)});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=position});InputSystem.QueueStateEvent(touch,new TouchState {touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=position+new Vector2(180,30)});yield return null;
                Require(!Mathf.Approximately(view.Camera.orthographicSize,size),"14 two-finger pinch changes camera zoom");
                view.PauseMenu.Open();yield return new WaitForSecondsRealtime(.2f);view.PauseMenu.SettingsButton.onClick.Invoke();yield return null;
                Require(view.PauseMenu.SettingsPage.GetComponentsInChildren<Slider>().Length>=8,"14 shared settings includes audio, display, camera and scale");
                var master=view.PauseMenu.SettingsPage.GetComponentsInChildren<Slider>().First();master.value=.42f;Find(view.PauseMenu.SettingsPage.transform,"应用 / 保留画面").onClick.Invoke();
                Require(savedPreferences!=null&&Mathf.Approximately(InterfaceSettings.Decode(savedPreferences).Master,.42f)&&Mathf.Approximately(AudioListener.volume,.42f),"14 actual settings apply persists to owned sink and changes audio");
                Find(view.PauseMenu.SettingsPage.transform,"镜头前移：").onClick.Invoke();yield return KeyPress(Key.UpArrow);Find(view.PauseMenu.SettingsPage.transform,"应用 / 保留画面").onClick.Invoke();
                Require(InterfaceSettings.Decode(savedPreferences).Forward==Key.UpArrow,"14 actual key rebind applies through shared settings");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/interface-settings-Map_Test2.png");yield return new WaitForEndOfFrame();}
                view.PauseMenu.Escape();view.PauseMenu.SaveButton.onClick.Invoke();view.PauseMenu.CreateSlotButton.onClick.Invoke();yield return null;yield return null;
                var run=em.GetComponentData<RunPersistence>(root).RunId.ToString();yield return WaitFor(()=>store.Slots(run).Length==1,"14 isolated save created");string slot=store.Slots(run)[0];
                yield return WaitFor(()=>File.Exists(store.PreviewPath(run,slot)),"14 map thumbnail attached to saved slot");
                yield return new WaitForSecondsRealtime(1.1f);var name=view.PauseMenu.SavesPage.GetComponentsInChildren<TMP_InputField>().First();name.SetTextWithoutNotify("验收独立槽");Find(view.PauseMenu.SlotRows,"重命名").onClick.Invoke();Require(view.PauseMenu.ConfirmPage.activeSelf,"14 rename requests confirmation");view.PauseMenu.ConfirmButton.onClick.Invoke();
                Require(store.Describe(run,slot).Name=="验收独立槽","14 confirmed rename uses slot metadata only");yield return new WaitForSecondsRealtime(1.1f);
                Find(view.PauseMenu.SlotRows,"覆盖此槽").onClick.Invoke();view.PauseMenu.CancelButton.onClick.Invoke();Require(!File.Exists(store.SlotPath(run,slot)+".bak"),"14 cancelling overwrite preserves original and creates no backup");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/interface-slots-Map_Test2.png");yield return new WaitForEndOfFrame();}
                browserModal=InterfaceWidgets.Modal("Owned archive browser",view.GetComponentInParent<Canvas>().transform,600,out var card);var browser=card.gameObject.AddComponent<ArchiveBrowser>();string chosen=null;
                browser.Store=store;browser.Load=(r,s,b)=>chosen=r+":"+s+":"+b;browser.Initialize(view.Message.font,()=>browserModal.SetActive(false));browser.Begin();yield return null;
                Find(card,run.Substring(0,8)).onClick.Invoke();Find(card,"验收独立槽").onClick.Invoke();yield return null;
                Require(card.GetComponentsInChildren<RawImage>().Any(i=>i.texture!=null&&i.texture.width==320),"14 browser displays saved map thumbnail");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/interface-browser-Map_Test2.png");yield return new WaitForEndOfFrame();}
                Find(card,"载入此记录").onClick.Invoke();Find(card,"取消").onClick.Invoke();Require(chosen==null,"14 archive load cancellation never dispatches scene transition");
                Find(card,"验收独立槽").onClick.Invoke();Find(card,"载入此记录").onClick.Invoke();Find(card,"确认").onClick.Invoke();Require(chosen==run+":"+slot+":False","14 browser dispatches exactly selected owned run and slot");
                browserModal.SetActive(false);Destroy(browserModal);browserModal=null;yield return null;
                Find(view.PauseMenu.SlotRows,"删除此独立槽").onClick.Invoke();view.PauseMenu.CancelButton.onClick.Invoke();Require(store.Slots(run).Length==1,"14 cancelling delete preserves slot");Find(view.PauseMenu.SlotRows,"删除此独立槽").onClick.Invoke();view.PauseMenu.ConfirmButton.onClick.Invoke();Require(store.Slots(run).Length==0&&File.Exists(store.RunPath(run)),"14 confirmed delete keeps automatic dynasty nodes");
                view.PauseMenu.Close();yield return WaitFor(()=>!view.PauseMenu.IsOpen,"14 modal closes and releases input");
            }
            finally
            {
                if(browserModal!=null)Destroy(browserModal);
                InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;
                InterfaceSettings.Apply(preferences,false,false);view.Camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);view.Camera.orthographicSize=zoom;
                InterfaceSettings.PersistenceOverride=priorPersistence;
                checkpoint.Store=priorStore;checkpoint.Import(root,archive,false);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));
                if(Directory.Exists(directory))Directory.Delete(directory,true);view.OpenPanel("建筑");
            }
        }
    }
}
#endif
