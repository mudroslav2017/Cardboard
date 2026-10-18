using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(50)]
    public class CardboardCamera : MonoBehaviour
    {
        public const float GameplayFov=80;
        public Transform player;
        public float sensitivity=.10f;
        public Vector3 WalkingOffset {get;private set;}
        public Quaternion WalkingRotation {get;private set;}=Quaternion.identity;
        float yaw=180,pitch,stepPhase,walkingAmount;
        Vector3 previousPosition;
        CardboardRunner controller;
        void Start()
        {
            yaw=transform.eulerAngles.y;pitch=Mathf.DeltaAngle(0,transform.eulerAngles.x);
            if(player){controller=player.GetComponent<CardboardRunner>();previousPosition=player.position;}
            var lens=GetComponent<Camera>();lens.orthographic=false;lens.fieldOfView=GameplayFov;lens.nearClipPlane=.05f;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void Update()
        {
            var factory=CardboardFactoryGame.Instance;
            if(factory && factory.CameraOverride)
            {transform.SetPositionAndRotation(factory.CameraPosition,factory.CameraRotation);return;}
            if(factory && factory.PlayerBlocked)return;
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            if(CardboardMarker.DrawingMode)return;
            if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)Release();
            else if(mouse!=null && mouse.leftButton.wasPressedThisFrame && Application.isFocused)
            {Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            if(mouse!=null && Cursor.lockState==CursorLockMode.Locked)
            {
                Vector2 delta=mouse.delta.ReadValue();yaw+=delta.x*sensitivity;
                pitch=Mathf.Clamp(pitch-delta.y*sensitivity,-80,80);
            }
            transform.rotation=Quaternion.Euler(pitch,yaw,0);
        }
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
        void LateUpdate()
        {
            var factory=CardboardFactoryGame.Instance;
            if(factory && factory.CameraOverride)
            {ResetWalkingMotion();transform.SetPositionAndRotation(factory.CameraPosition,factory.CameraRotation);return;}
            if(!player)return;
            float dt=Time.deltaTime;
            Vector3 movement=player.position-previousPosition;movement.y=0;previousPosition=player.position;
            bool active=(!factory || !factory.PlayerBlocked) && !CardboardMarker.DrawingMode;
            float speed=dt>.00001f?Mathf.Min(movement.magnitude/dt,controller?controller.speed:6):0;
            TickWalkingMotion(dt,active?speed:0,!controller || controller.JumpHeight<.01f);
            transform.position=player.position+Vector3.up*(1.62f+(controller?controller.JumpHeight:0))+Quaternion.Euler(0,yaw,0)*WalkingOffset;
            transform.rotation=Quaternion.Euler(pitch,yaw,0)*WalkingRotation;
        }
        public void ResetView(float direction)
        {yaw=direction;pitch=0;ResetWalkingMotion();transform.rotation=Quaternion.Euler(0,yaw,0);}
        void OnDisable(){Release();}
        void OnApplicationFocus(bool focused){if(!focused)Release();}
        static void Release(){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
    }
}
