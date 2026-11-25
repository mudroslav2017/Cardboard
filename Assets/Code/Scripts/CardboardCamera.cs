using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(-20)]
    public class CardboardCamera : MonoBehaviour
    {
        public const float GameplayFov=80;
        public Transform player;
        public float sensitivity=.10f;
        public Vector3 WalkingOffset {get;private set;}
        public Quaternion WalkingRotation {get;private set;}=Quaternion.identity;
        float yaw=180,pitch,stepPhase,walkingAmount;
        float forkliftYaw;
        bool followingForklift;
        Vector3 previousPosition;
        CardboardRunner controller;
        void Start()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            // Let requestAnimationFrame follow the display refresh rate (including 120/144 Hz).
            Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
#endif
            yaw=transform.eulerAngles.y;pitch=Mathf.DeltaAngle(0,transform.eulerAngles.x);
            if(player){controller=player.GetComponent<CardboardRunner>();previousPosition=player.position;}
            var lens=GetComponent<Camera>();lens.orthographic=false;lens.fieldOfView=GameplayFov;lens.nearClipPlane=.05f;
            CardboardWebMouse.Configure(false,false);
            bool menu=CardboardFactoryGame.Instance && CardboardFactoryGame.Instance.MenuActive;
            Cursor.lockState=menu?CursorLockMode.None:CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(menu);
        }
        void Update()
        {
            var factory=CardboardFactoryGame.Instance;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            bool kitchenMouse=factory && factory.kitchen && mouse!=null && factory.kitchen.ConsumesMouse(mouse);
            bool capture=Cursor.lockState==CursorLockMode.Locked;
            bool hatchMotion=factory && factory.craneJob && factory.craneJob.HatchInMotion;
            CardboardWebMouse.Configure(capture,Application.isFocused && !CardboardMarker.DrawingMode && !hatchMotion &&
                (!factory || (!factory.CameraOverride && (!factory.PlayerBlocked || kitchenMouse))));
            if(factory && factory.CameraOverride)
            {transform.SetPositionAndRotation(factory.CameraPosition,factory.CameraRotation);return;}
            if(hatchMotion)return;
            if(kitchenMouse && Application.isFocused)
            {factory.kitchen.HandleMouse(mouse,CardboardWebMouse.ReadDelta());return;}
            if(factory && factory.PlayerBlocked)return;
            if(CardboardMarker.DrawingMode)return;
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)Release();
            else if(mouse!=null && mouse.leftButton.wasPressedThisFrame && Application.isFocused && Cursor.lockState!=CursorLockMode.Locked)
            {Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);}
            if(mouse!=null && Cursor.lockState==CursorLockMode.Locked)
            {
                ApplyLookDelta(CardboardWebMouse.ReadDelta());
            }
            transform.rotation=Quaternion.Euler(pitch,yaw,0);
        }
        public void ApplyLookDelta(Vector2 delta)
        {
            if(float.IsNaN(delta.x) || float.IsInfinity(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.y))return;
            yaw=Mathf.Repeat(yaw+delta.x*sensitivity,360);
            pitch=Mathf.Clamp(pitch-delta.y*sensitivity,-80,80);
        }
        internal void RotateWithSupport(float deltaYaw) => yaw = Mathf.Repeat(yaw + deltaYaw, 360f);

        public void TickWalkingMotion(float dt,float speed,bool grounded)
        {
            dt=Mathf.Max(0,dt);speed=Mathf.Max(0,speed);
            float run=Mathf.InverseLerp(3.2f,6,speed);
            float target=grounded && speed>.15f?Mathf.Clamp01(speed/3.2f)*Mathf.Lerp(1,2.6f,run):0;
            walkingAmount=Mathf.Lerp(walkingAmount,target,1-Mathf.Exp(-12*dt));
            if(target>0)stepPhase+=dt*Mathf.Lerp(7.5f,11.5f,run);
            WalkingOffset=new Vector3(Mathf.Sin(stepPhase)*.008f,Mathf.Sin(stepPhase*2)*.012f,0)*walkingAmount;
            WalkingRotation=Quaternion.Euler(Mathf.Sin(stepPhase*2)*.16f*walkingAmount,0,Mathf.Sin(stepPhase)*.32f*walkingAmount);
        }
        void ResetWalkingMotion()
        {
            stepPhase=walkingAmount=0;WalkingOffset=Vector3.zero;WalkingRotation=Quaternion.identity;
            if(player)previousPosition=player.position;
        }
        void LateUpdate(){RefreshPlayerPose(Time.deltaTime);}
        public void RefreshPlayerPose(float dt)
        {
            var factory=CardboardFactoryGame.Instance;
            if(factory && factory.CameraOverride)
            {ResetWalkingMotion();transform.SetPositionAndRotation(factory.CameraPosition,factory.CameraRotation);return;}
            if(factory && factory.craneJob && factory.craneJob.TryGetHatchView(out var hatchPosition,out var hatchRotation))
            {ResetWalkingMotion();transform.SetPositionAndRotation(hatchPosition,hatchRotation);return;}
            if(factory && factory.kitchen && factory.kitchen.Preparing)
            {ResetWalkingMotion();transform.SetPositionAndRotation(factory.kitchen.PrepCameraPosition,factory.kitchen.PrepCameraRotation);return;}
            if(!player)return;
            if(factory && factory.craneJob && factory.craneJob.TryGetForkliftSeat(player,out var seatPosition,out var supportYaw))
            {
                if(followingForklift)RotateWithSupport(Mathf.DeltaAngle(forkliftYaw,supportYaw));
                forkliftYaw=supportYaw;
                followingForklift=true;
                ResetWalkingMotion();
                transform.SetPositionAndRotation(seatPosition+Vector3.up*1.62f,Quaternion.Euler(pitch,yaw,0));
                return;
            }
            followingForklift=false;
            Vector3 movement=player.position-previousPosition;movement.y=0;previousPosition=player.position;
            bool active=(!factory || !factory.PlayerBlocked) && !CardboardMarker.DrawingMode;
            float speed=dt>.00001f?Mathf.Min(movement.magnitude/dt,controller?controller.speed:6):0;
            TickWalkingMotion(dt,active?speed:0,!controller || controller.JumpHeight<.01f);
            var lunch=factory?factory.lunch:null;
            var sway=active && lunch?new Vector3(lunch.SwayOffset,0,0):Vector3.zero;
            float roll=active && lunch?lunch.SwayRoll:0;
            transform.position=player.position+Vector3.up*(1.62f+(controller?controller.JumpHeight:0))+Quaternion.Euler(0,yaw,0)*(WalkingOffset+sway);
            transform.rotation=Quaternion.Euler(pitch,yaw,0)*WalkingRotation*Quaternion.Euler(0,0,roll);
        }
        public void AdoptCurrentView()
        {CardboardWebMouse.Configure(false,false);yaw=transform.eulerAngles.y;pitch=Mathf.DeltaAngle(0,transform.eulerAngles.x);followingForklift=false;ResetWalkingMotion();}
        public void ResetView(float direction)
        {CardboardWebMouse.Configure(false,false);yaw=direction;pitch=0;ResetWalkingMotion();transform.rotation=Quaternion.Euler(0,yaw,0);}
        void OnDisable(){Release();}
        void OnApplicationFocus(bool focused){if(!focused)Release();}
        static void Release(){CardboardWebMouse.Configure(false,false);Cursor.lockState=CursorLockMode.None;FactoryVolumeCursor.SetSystemVisible(true);}
    }
}
