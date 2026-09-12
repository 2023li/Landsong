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
        IEnumerator InterfaceUi(UI_GamePanel view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);var checkpoint=World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>();var archive=checkpoint.Export(root);var priorStore=checkpoint.Store;
            var preferences=InterfaceSettings.Current.Copy();var cameraPosition=view.WorldInteraction.Camera.transform.position;var cameraRotation=view.WorldInteraction.Camera.transform.rotation;float zoom=view.WorldInteraction.Camera.orthographicSize;
            var priorPersistence=InterfaceSettings.PersistenceOverride;string savedPreferences=null;InterfaceSettings.PersistenceOverride=json=>savedPreferences=json;
            var background=InputSystem.settings.backgroundBehavior;var editorInputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();var touch=InputSystem.AddDevice<Touchscreen>();
            string directory=Path.Combine(Path.GetTempPath(),"Landsong-14-UI-"+Guid.NewGuid().ToString("N"));var store=new RunArchiveStore(directory);checkpoint.Store=store;
            IEnumerator KeyPress(Key key){InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));for(int frame=0;frame<4;frame++){keyboard.MakeCurrent();yield return null;}InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;}
            try
            {
                InterfaceSettings.Apply(new InterfacePreferences(),false,false);view.OpenPanel(GamePanelId.Royal);yield return new WaitForSecondsRealtime(.35f);
                var graph=FindFirstObjectByType<UI_GamePanel_CourtGraph>();var scroll=graph.Scroll;
                var first=scroll.content.GetComponentsInChildren<Button>().First();int identity=first.GetInstanceID();scroll.verticalNormalizedPosition=.4f;
                yield return null;float normalized=scroll.verticalNormalizedPosition;
                yield return new WaitForSecondsRealtime(.6f);Require(scroll.content.GetComponentsInChildren<Button>().First().GetInstanceID()==identity&&Mathf.Abs(scroll.verticalNormalizedPosition-normalized)<.05f,"14 family node identity and scroll survive timed refresh");
                view.OpenPanel(GamePanelId.History);Sim.Emit(em,root,EventKind.Message,"可定位验收消息");yield return new WaitForSecondsRealtime(.35f);
                Require(view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("可定位验收消息")),"14 authoritative message history is browseable");
                var filter=view.GetComponentInParent<Canvas>().GetComponentsInChildren<TMP_InputField>().First(i=>i.gameObject.name=="筛选来源 / 内容");filter.SetTextWithoutNotify("历史焦点");filter.ActivateInputField();yield return null;
                var before=view.WorldInteraction.Camera.transform.position;yield return KeyPress(Key.W);Require(view.WorldInteraction.Camera.transform.position==before,"14 focused TMP input owns keyboard and camera");filter.DeactivateInputField();UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                view.BackPanel();yield return new WaitForSecondsRealtime(.3f);Require(view.Panel==GamePanelId.Royal,"14 panel return stack restores previous panel");
                view.OpenPanel(GamePanelId.Building);yield return null;before=view.WorldInteraction.Camera.transform.position;yield return KeyPress(Key.W);Require(view.WorldInteraction.Camera.transform.position!=before,"14 default camera movement remains usable");
                var rotation=view.WorldInteraction.Camera.transform.rotation;yield return KeyPress(Key.E);Require(view.WorldInteraction.Camera.transform.rotation!=rotation,"14 keyboard camera rotation works");
                // Start away from the map clamp and choose an actually unoccluded UI ray, not an assumed screen coordinate.
                var mapGrid=em.GetComponentData<GridData>(root);var center=mapGrid.Origin+new Unity.Mathematics.float3((mapGrid.Value.Value.Min.x+mapGrid.Value.Value.Size.x*.5f)*mapGrid.CellSize,0,(mapGrid.Value.Value.Min.y+mapGrid.Value.Value.Size.y*.5f)*mapGrid.CellSize);
                view.WorldInteraction.Camera.transform.position=(Vector3)center-view.WorldInteraction.Camera.transform.forward*45;
                var position=Vector2.zero;var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                bool worldPointFound=false;
                foreach(float x in new[]{.52f,.4f,.6f,.3f})
                {
                    foreach(float y in new[]{.52f,.4f,.6f})
                    {
                        var candidate=new Vector2(Screen.width*x,Screen.height*y);hits.Clear();
                        UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=candidate},hits);
                        if(hits.Count!=0)continue;
                        position=candidate;worldPointFound=true;break;
                    }
                    if(worldPointFound)break;
                }
                Require(worldPointFound,"14 drag fixture starts outside UI ownership");InputSystem.QueueStateEvent(mouse,new MouseState {position=position});yield return null;
                var questRect=(RectTransform)view.Quests.QuestTracking.transform;
                var blocked=RectTransformUtility.WorldToScreenPoint(null,questRect.TransformPoint(questRect.rect.center));hits.Clear();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=blocked},hits);
                Require(hits.Any(hit=>hit.gameObject.transform==questRect||hit.gameObject.transform.IsChildOf(questRect)),"14 blocked drag fixture hits the configured task HUD");
                before=view.WorldInteraction.Camera.transform.position;
                InputSystem.QueueStateEvent(mouse,new MouseState {position=blocked,buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position,buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position});yield return null;
                Require(view.WorldInteraction.Camera.transform.position==before,"14 drag starting over task UI cannot leak onto map");
                before=view.WorldInteraction.Camera.transform.position;InputSystem.QueueStateEvent(mouse,new MouseState {position=position,buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position+new Vector2(90,0),buttons=4});yield return null;InputSystem.QueueStateEvent(mouse,new MouseState {position=position+new Vector2(90,0)});yield return null;
                Require(view.WorldInteraction.Camera.transform.position!=before,"14 middle-button drag pans without world clicks");
                float size=view.WorldInteraction.Camera.orthographicSize;InputSystem.QueueStateEvent(touch,new TouchState {touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Began,position=position});InputSystem.QueueStateEvent(touch,new TouchState {touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Began,position=position+new Vector2(90,0)});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Moved,position=position+new Vector2(180,30)});yield return null;
                InputSystem.QueueStateEvent(touch,new TouchState {touchId=1,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=position});InputSystem.QueueStateEvent(touch,new TouchState {touchId=2,phase=UnityEngine.InputSystem.TouchPhase.Ended,position=position+new Vector2(180,30)});yield return null;
                Require(!Mathf.Approximately(view.WorldInteraction.Camera.orthographicSize,size),"14 two-finger pinch changes camera zoom");
                view.PauseMenu.Open(); yield return new WaitForSecondsRealtime(.2f); view.PauseMenu.SettingsButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_SettingPanel>() != null, "14 pause opens shared settings");
                var settingsPanel = Shared<UI_SettingPanel>();
                Require(settingsPanel.GetComponentsInChildren<Slider>().Length >= 8, "14 shared settings includes audio, display, camera and scale");
                settingsPanel.Master.value = .42f; settingsPanel.ApplyButton.onClick.Invoke();
                Require(savedPreferences != null && Mathf.Approximately(InterfaceSettings.Decode(savedPreferences).Master, .42f) && Mathf.Approximately(AudioListener.volume, .42f), "14 actual settings apply persists to owned sink and changes audio");
                settingsPanel.KeyButtons[0].onClick.Invoke(); yield return KeyPress(Key.UpArrow); settingsPanel.ApplyButton.onClick.Invoke();
                Require(InterfaceSettings.Decode(savedPreferences).Forward == Key.UpArrow, "14 actual key rebind applies through shared settings");
                if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/interface-settings-Map_Test2.png"); yield return new WaitForEndOfFrame(); }
                yield return CloseShared<UI_SettingPanel>(); view.PauseMenu.SaveButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_SavePanel>() != null, "14 shared save panel opens");
                var browser = Shared<UI_SavePanel>(); browser.RenameInput.SetTextWithoutNotify("界面验收原始槽"); ClickArchive(browser, "创建独立存档");
                var run = em.GetComponentData<RunPersistence>(root).RunId.ToString(); yield return WaitFor(() => store.Slots(run).Length == 1, "14 isolated save created"); string slot = store.Slots(run)[0];
                yield return WaitFor(() => File.Exists(store.PreviewPath(run, slot)), "14 map thumbnail attached to saved slot");
                browser.Refresh(); ClickArchive(browser, store.Describe(run, slot).Name);
                browser.RenameInput.SetTextWithoutNotify("验收独立槽"); ClickArchive(browser, "重命名");
                yield return WaitFor(() => Shared<UI_ConfirmPanel>() != null, "14 rename requests shared confirmation"); yield return ConfirmShared(true);
                Require(store.Describe(run, slot).Name == "验收独立槽", "14 confirmed rename uses slot metadata only");
                ClickArchive(browser, "验收独立槽"); ClickArchive(browser, "覆盖此槽"); yield return ConfirmShared(false);
                Require(!File.Exists(store.SlotPath(run, slot) + ".bak"), "14 cancelling overwrite preserves original and creates no backup");
                if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/interface-slots-Map_Test2.png"); yield return new WaitForEndOfFrame(); }
                ClickArchive(browser, "永久删除此槽及其备份"); yield return ConfirmShared(false);
                Require(store.Slots(run).Length == 1, "14 cancelling delete preserves slot");
                ClickArchive(browser, "永久删除此槽及其备份"); yield return ConfirmShared(true);
                Require(store.Slots(run).Length == 0 && File.Exists(store.RunPath(run)), "14 confirmed delete keeps automatic dynasty nodes");
                yield return CloseShared<UI_SavePanel>();
                view.PauseMenu.Close();yield return WaitFor(()=>!view.PauseMenu.IsOpen,"14 modal closes and releases input");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(touch);InputSystem.settings.backgroundBehavior=background;InputSystem.settings.editorInputBehaviorInPlayMode=editorInputBehavior;
                InterfaceSettings.Apply(preferences,false,false);view.WorldInteraction.Camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);view.WorldInteraction.Camera.orthographicSize=zoom;
                InterfaceSettings.PersistenceOverride=priorPersistence;
                checkpoint.Store=priorStore;checkpoint.Import(root,archive,false);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));
                if(Directory.Exists(directory))Directory.Delete(directory,true);view.OpenPanel(GamePanelId.Building);
            }
        }
    }
}
#endif
