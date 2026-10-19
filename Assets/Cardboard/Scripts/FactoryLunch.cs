using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    public enum LunchState { Waiting, Ready, Carrying, Served, Spilled }
    [DefaultExecutionOrder(-30)]
    public class FactoryLunch : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public Transform tray,pickup,destination;
        public Transform[] food;
        public GameObject pickupHit,destinationHit;
        public TextMesh counterSign,tableSign;
        public float lunchAfter=60;
        public FactoryLunchCrowd diners;
        public FactoryTrayPhysics physicsTray;
        public LunchState State {get;private set;}
        public bool Carrying=>State==LunchState.Carrying;
        public float Tilt {get;private set;}
        public float PitchTilt {get;private set;}
        public float AngularSpeed {get;private set;}
        public float Elapsed {get;private set;}
        public float CarryTime {get;private set;}
        public float ResultUntil {get;private set;}
        public bool CanServe=>Carrying && CardboardRunner.Player && Vector3.Distance(CardboardRunner.Player.transform.position,destination.position)<2.5f && Mathf.Abs(Tilt)<=.4f && Mathf.Abs(PitchTilt)<=.45f;
        bool Gameplay=>game && !game.MenuActive && !game.Ended && !game.CaptureActive;
        public void ResetLunch()
        {
            State=LunchState.Waiting;Tilt=PitchTilt=AngularSpeed=Elapsed=CarryTime=ResultUntil=0;
            if(physicsTray)physicsTray.ResetContents();
            if(diners)diners.ResetLunch();
            tray.SetParent(transform,false);tray.SetPositionAndRotation(pickup.position,pickup.rotation);tray.gameObject.SetActive(true);
            if(physicsTray){physicsTray.body.position=tray.position;physicsTray.body.rotation=tray.rotation;}
            RefreshWorld();
        }
        public void TickSchedule(float dt)
        {
            if(!Gameplay || State!=LunchState.Waiting)return;
            Elapsed+=Mathf.Max(0,dt);
            if(Elapsed>=lunchAfter)
            {
                State=LunchState.Ready;RefreshWorld();if(diners)diners.Begin();
                game.Toast("Обед! Возьмите поднос на раздаче в столовой.",8);game.PlayCaptureSound("good");
            }
        }
        public bool TryTake()
        {
            if(!Gameplay || State!=LunchState.Ready || game.HasPart || game.HandsBusy || game.PlayerBlocked || !CardboardRunner.Player ||
                CardboardRunner.Player.JumpHeight>.01f || Vector3.Distance(CardboardRunner.Player.transform.position,pickup.position)>3.5f || !physicsTray)return false;
            if(game.station)game.station.EndGrip(false);
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false);
            State=LunchState.Carrying;Tilt=.06f;AngularSpeed=0;PitchTilt=0;CarryTime=0;
            tray.SetParent(transform,true);
            HoldingPose(out var position,out var rotation);physicsTray.Begin(position,rotation);RefreshWorld();
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            game.Toast("Донесите обед до стола. Двигайтесь и поворачивайтесь плавно: посуда может соскользнуть в любую сторону.",7);return true;
        }
        void Update()
        {
            if(!Gameplay || !Carrying || !Application.isFocused)return;
            var mouse=Mouse.current;
            TickBalance(Time.deltaTime,mouse!=null && mouse.leftButton.wasPressedThisFrame,mouse!=null && mouse.rightButton.wasPressedThisFrame);
        }
        public void TickBalance(float dt,bool leftClick,bool rightClick)
        {
            if(!Gameplay || !Carrying || dt<=0)return;
            CarryTime+=Mathf.Min(dt,.25f);
            AngularSpeed+=((rightClick?1:0)-(leftClick?1:0))*.42f;
            AngularSpeed+=(.09f*Mathf.Sin(CarryTime*1.7f)-Tilt*.8f-AngularSpeed*1.1f)*dt;
            Tilt=Mathf.Clamp(Tilt+AngularSpeed*dt,-1.6f,1.6f);
            // Pitch follows looking up/down, so the near and far edges can spill too.
            float pitch=Camera.main?Mathf.DeltaAngle(0,Camera.main.transform.eulerAngles.x):0;
            PitchTilt=Mathf.Clamp((pitch*.45f+3)/38,-1.7f,1.7f);PoseTray();
        }
        void HoldingPose(out Vector3 position,out Quaternion rotation)
        {
            var camera=Camera.main.transform;
            position=camera.TransformPoint(new Vector3(Mathf.Sin(CarryTime*2)*.009f,-.39f,.72f));
            float pitch=Mathf.DeltaAngle(0,camera.eulerAngles.x)*.45f;
            rotation=Quaternion.Euler(pitch+3+Mathf.Sin(CarryTime*2.6f)*.8f,camera.eulerAngles.y,-Tilt*38);
        }
        void PoseTray(){if(!physicsTray || !Camera.main)return;HoldingPose(out var position,out var rotation);physicsTray.Aim(position,rotation);}
        void LateUpdate(){if(Carrying)PoseTray();}
        public bool TryServe()
        {
            if(!Gameplay || !CanServe)return false;
            physicsTray.Park(destination);State=LunchState.Served;Tilt=AngularSpeed=PitchTilt=0;ResultUntil=Time.time+6;
            RefreshWorld();game.Toast("Обед доставлен. Возвращайтесь к работе.",7);game.PlayCaptureSound("good");return true;
        }
        public void Spill()
        {
            if(!Carrying)return;
            State=LunchState.Spilled;ResultUntil=Time.time+6;if(physicsTray)physicsTray.ReleaseTray();
            RefreshWorld();game.RegisterMistake("Уронил еду с подноса во время обеда.");game.PlayCaptureSound("impact");
        }
        public void TickSpill(float dt){} // Unity physics owns the flight, impacts and resting pose.
        public void CancelCarry()
        {
            if(diners)diners.Cancel();
            if(Carrying){State=LunchState.Spilled;if(physicsTray)physicsTray.ReleaseTray();}
            RefreshWorld();
        }
        void RefreshWorld()
        {
            if(pickupHit)pickupHit.SetActive(State==LunchState.Waiting || State==LunchState.Ready);
            if(destinationHit)destinationHit.SetActive(Carrying);
            if(counterSign)counterSign.text=State==LunchState.Waiting?"РАЗДАЧА\nОБЕД ПО ЗВОНКУ":State==LunchState.Ready?"ОБЕД\nВОЗЬМИТЕ ПОДНОС":"РАЗДАЧА\nОБЕД ВЫДАН";
            if(tableSign)tableSign.text=State==LunchState.Served?"ПРИЯТНОГО\nАППЕТИТА!":"ОБЕДЕННЫЙ СТОЛ\nПОДНОС СЮДА";
        }
    }
}
