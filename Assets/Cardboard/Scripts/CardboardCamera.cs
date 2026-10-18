using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(50)]
    public class CardboardCamera : MonoBehaviour
    {
        public Transform player;
        public float sensitivity=.10f;
        float yaw=180,pitch;
        CardboardRunner controller;
        void Start()
        {
            yaw=transform.eulerAngles.y;pitch=Mathf.DeltaAngle(0,transform.eulerAngles.x);
            if (player) controller=player.GetComponent<CardboardRunner>();
            var lens=GetComponent<Camera>(); lens.orthographic=false; lens.fieldOfView=72; lens.nearClipPlane=.05f;
            Cursor.lockState=CursorLockMode.None; Cursor.visible=true;
        }
        void Update()
        {
            var factory=CardboardFactoryGame.Instance;
            if(factory && factory.CameraOverride)
            { transform.SetPositionAndRotation(factory.CameraPosition,factory.CameraRotation);return; }
            if(factory && factory.PlayerBlocked) return;
            var mouse=Mouse.current; var keyboard=Keyboard.current;
            if(CardboardMarker.DrawingMode) return;
            if (keyboard!=null && keyboard.escapeKey.wasPressedThisFrame) Release();
            else if (mouse!=null && mouse.leftButton.wasPressedThisFrame && Application.isFocused)
            { Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false; }
            if (mouse!=null && Cursor.lockState==CursorLockMode.Locked)
            {
                Vector2 delta=mouse.delta.ReadValue(); yaw+=delta.x*sensitivity;
                pitch=Mathf.Clamp(pitch-delta.y*sensitivity,-80,80);
            }
            transform.rotation=Quaternion.Euler(pitch,yaw,0);
        }
        void LateUpdate()
        {
            var factory=CardboardFactoryGame.Instance;
            if(factory && factory.CameraOverride)
            { transform.SetPositionAndRotation(factory.CameraPosition,factory.CameraRotation);return; }
            if (player) transform.position=player.position+Vector3.up*(1.62f+(controller?controller.JumpHeight:0));
        }
        public void ResetView(float direction)
        {yaw=direction;pitch=0;transform.rotation=Quaternion.Euler(0,yaw,0);}
        void OnDisable() { Release(); }
        void OnApplicationFocus(bool focused) { if(!focused) Release(); }
        static void Release() { Cursor.lockState=CursorLockMode.None; Cursor.visible=true; }
    }
}
