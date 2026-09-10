using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        readonly Stack<string> panelHistory = new Stack<string>();
        bool cameraDragging, pointerClaimed, touchBlocked, touchMoved; Vector2 dragLast, touchStart; float touchStarted; int touchId = -1; float pinchDistance, pinchAngle;
        Vector3 cameraVelocity; int appliedPreferences = -1; Vector2 baseResolution;
        readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
        public bool TextFocused => EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponentInParent<TMP_InputField>() is TMP_InputField input && input.isFocused;
        bool PointerOnUi(Vector2 position)
        { if(EventSystem.current==null)return false;pointerHits.Clear();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},pointerHits);return pointerHits.Count>0; }
        public void BackPanel()
        {
            if(SoldierDetailsOpen){CloseSoldierDetails();return;}
            if(PortraitOpen){ClosePortrait();return;}
            if(PersonRequestsOpen){ClosePersonRequests();return;}
            if(MarriageOpen){CloseMarriage();return;}
            if(PauseMenu!=null&&PauseMenu.IsOpen){PauseMenu.Escape();return;}
            if(CancelBuildingInteraction())return;
            if(inventoryDragging){EndInventoryDrag();return;}
            if(panelHistory.Count>0){var previous=panelHistory.Pop();OpenPanelCore(previous,false);}else ClosePanel();
        }
        void ApplyInterfacePreferences()
        {
            if(appliedPreferences==InterfaceSettings.Revision)return;appliedPreferences=InterfaceSettings.Revision;
            var scaler=GetComponentInParent<Canvas>().GetComponent<CanvasScaler>();if(scaler!=null){if(baseResolution==Vector2.zero)baseResolution=scaler.referenceResolution;scaler.referenceResolution=baseResolution/InterfaceSettings.Current.UiScale;}
        }
        public static Vector3 ClampCameraPosition(Camera camera, GridData grid, Vector3 proposed)
        {
            // Clamp the camera's ground focus, not its elevated Transform, to the authored map rectangle.
            var direction=camera.transform.forward;float t=Mathf.Abs(direction.y)>.001f?(grid.Origin.y-proposed.y)/direction.y:0;
            var focus=proposed+direction*Mathf.Max(0,t);var min=grid.Origin.xz+(Unity.Mathematics.float2)grid.Value.Value.Min*grid.CellSize;var max=min+(Unity.Mathematics.float2)grid.Value.Value.Size*grid.CellSize;
            var clamped=new Vector3(Mathf.Clamp(focus.x,min.x,max.x),focus.y,Mathf.Clamp(focus.z,min.y,max.y));return proposed+clamped-focus;
        }
        void PanCamera(Vector3 delta)
        {var position=ClampCameraPosition(Camera,em.GetComponentData<GridData>(root),Camera.transform.position+delta);Camera.transform.position=position;}
        void RotateCamera(float degrees)
        {
            var ray=Camera.ViewportPointToRay(new Vector3(.5f,.5f));var plane=new Plane(Vector3.up,em.GetComponentData<GridData>(root).Origin.y*Vector3.up);if(!plane.Raycast(ray,out var distance))return;
            Camera.transform.RotateAround(ray.GetPoint(distance),Vector3.up,degrees);Camera.transform.position=ClampCameraPosition(Camera,em.GetComponentData<GridData>(root),Camera.transform.position);
        }
        void DragCamera(Vector2 delta)
        {var right=Camera.transform.right;right.y=0;right.Normalize();var forward=Vector3.ProjectOnPlane(Camera.transform.up,Vector3.up).normalized;PanCamera((-right*delta.x-forward*delta.y)*(Camera.orthographicSize*2/Mathf.Max(1,Screen.height)));Send(CommandKind.CameraMoved);}
        void ZoomCamera(float amount)
        {var size=Mathf.Clamp(Camera.orthographicSize-amount*InterfaceSettings.Current.ZoomSpeed,5,100);if(!Mathf.Approximately(size,Camera.orthographicSize)){Camera.orthographicSize=size;Send(CommandKind.CameraZoomed);}}
        bool CameraInput(Mouse mouse,Keyboard keyboard)
        {
            ApplyInterfacePreferences();if(Camera==null)return true;
            var preferences=InterfaceSettings.Current;
            if(keyboard!=null&&!TextFocused)
            {
                var x=(keyboard[preferences.Right].isPressed?1:0)-(keyboard[preferences.Left].isPressed?1:0);var y=(keyboard[preferences.Forward].isPressed?1:0)-(keyboard[preferences.Back].isPressed?1:0);
                var right=Vector3.ProjectOnPlane(Camera.transform.right,Vector3.up).normalized;var forward=Vector3.ProjectOnPlane(Camera.transform.forward,Vector3.up).normalized;
                var wanted=Vector3.ClampMagnitude(right*x+forward*y,1)*preferences.CameraSpeed;
                var smooth=preferences.ReducedMotion?0:preferences.Smoothing;cameraVelocity=smooth<=0?wanted:Vector3.Lerp(cameraVelocity,wanted,1-Mathf.Exp(-Time.unscaledDeltaTime/smooth));
                if(cameraVelocity.sqrMagnitude>.001f){PanCamera(cameraVelocity*Time.unscaledDeltaTime);if(x!=0||y!=0)Send(CommandKind.CameraMoved);}
                var rotation=(keyboard[preferences.RotateRight].isPressed?1:0)-(keyboard[preferences.RotateLeft].isPressed?1:0);if(rotation!=0){RotateCamera(rotation*60*Time.unscaledDeltaTime);Send(CommandKind.CameraMoved);}
            }else cameraVelocity=Vector3.zero;
            if(TouchInput())return true;
            if(mouse==null)return true;var position=mouse.position.ReadValue();bool over=PointerOnUi(position);
            if(mouse.middleButton.wasPressedThisFrame){cameraDragging=!over&&!TextFocused;dragLast=position;}
            if(cameraDragging&&mouse.middleButton.isPressed){DragCamera(position-dragLast);dragLast=position;return true;}if(mouse.middleButton.wasReleasedThisFrame){cameraDragging=false;return true;}
            if(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame)pointerClaimed=over;
            if(over||pointerClaimed){if(!mouse.leftButton.isPressed&&!mouse.rightButton.isPressed&&!mouse.leftButton.wasReleasedThisFrame&&!mouse.rightButton.wasReleasedThisFrame)pointerClaimed=false;return true;}
            if(TextFocused)return true;var scroll=mouse.scroll.ReadValue().y;if(Mathf.Abs(scroll)>.01f)ZoomCamera(scroll*.03f);return false;
        }
        bool TouchInput()
        {
            var screen=Touchscreen.current;if(screen==null)return false;
            var active=new List<UnityEngine.InputSystem.Controls.TouchControl>();foreach(var finger in screen.touches)if(finger.press.isPressed)active.Add(finger);
            if(active.Count>=2)
            {
                var a=active[0].position.ReadValue();var b=active[1].position.ReadValue();var vector=b-a;
                if(touchId!=-2){touchBlocked=(touchId>=0&&touchBlocked)||PointerOnUi(a)||PointerOnUi(b)||HasBuildingPlacement||TextFocused;pinchDistance=vector.magnitude;pinchAngle=Mathf.Atan2(vector.y,vector.x)*Mathf.Rad2Deg;}
                if(!touchBlocked){ZoomCamera((vector.magnitude-pinchDistance)*Camera.orthographicSize/Mathf.Max(1,Screen.height));var angle=Mathf.Atan2(vector.y,vector.x)*Mathf.Rad2Deg;RotateCamera(-Mathf.DeltaAngle(pinchAngle,angle));pinchAngle=angle;}
                pinchDistance=vector.magnitude;touchId=-2;touchMoved=true;return true;
            }
            if(active.Count==1)
            {
                var finger=active[0];var position=finger.position.ReadValue();
                if(touchId==-2){touchBlocked=true;return true;}
                if(touchId<0){touchId=finger.touchId.ReadValue();touchStart=dragLast=position;touchStarted=Time.unscaledTime;touchMoved=false;touchBlocked=PointerOnUi(position)||TextFocused;}
                if((position-touchStart).sqrMagnitude>144)touchMoved=true;
                if(!touchBlocked&&touchMoved&&!HasBuildingPlacement)DragCamera(position-dragLast);dragLast=position;return true;
            }
            if(touchId!=-1)
            {
                if(!touchBlocked&&!touchMoved&&!PointerOnUi(dragLast)&&GroundPoint(Camera.ScreenPointToRay(dragLast),out var point)&&!intel)
                {if(Time.unscaledTime-touchStarted>.55f&&!HasBuildingPlacement)Send(CommandKind.MoveHero,position:point);else if(!BuildingPointer(point,true,true,false))WorldClick(point);}
                touchId=-1;touchBlocked=touchMoved=false;return true;
            }
            return false;
        }
        void WorldClick(Vector3 point)
        {
            var entity=Hit(point,false);if(entity==Entity.Null){selected=0;nextRefresh=0;return;}var identity=em.GetComponentData<Identity>(entity);
            if(em.HasComponent<Loot>(entity)||em.HasComponent<Opportunity>(entity)){Send(CommandKind.PickUp,identity.Id);return;}
            if(em.HasComponent<Hero>(entity)){Send(CommandKind.SelectHero,identity.Id);selected=identity.Id;nextRefresh=0;return;}
            if(!em.HasComponent<Building>(entity))return;
            if(selected==identity.Id&&Time.unscaledTime-clickTime<.35f&&em.GetComponentData<Session>(root).Phase==Phase.Day)Send(CommandKind.Harvest,identity.Id);
            SelectBuildingDetails(identity.Id);clickTime=Time.unscaledTime;NameInput.SetTextWithoutNotify(identity.Name.ToString());nextRefresh=0;
            if(em.GetComponentData<Session>(root).Phase==Phase.Night&&em.GetComponentData<BuildingStats>(entity).BellRadius>0)Send(CommandKind.Bell,identity.Id);
        }
    }
}
