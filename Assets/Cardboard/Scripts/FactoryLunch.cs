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
        public float attendanceGrace=45;
        public bool Attended {get;private set;}
        public bool Missed {get;private set;}
        public bool FinalLunch {get;private set;}
        public float AttendanceElapsed {get;private set;}
        public string ArrivalCountdown
        {
            get{int seconds=Mathf.CeilToInt(Mathf.Max(0,attendanceGrace-AttendanceElapsed));return (seconds/60).ToString("00")+":"+(seconds%60).ToString("00");}
        }
        public FactoryLunchCrowd diners;
        public FactoryTrayPhysics physicsTray;
        public Light traySpotlight;
        public Light deliverySpotlight;
        public Renderer deliveryBulb;
        public LunchState State {get;private set;}
        public bool Carrying=>State==LunchState.Carrying;
        public float Tilt {get;private set;}
        public float PitchTilt {get;private set;}
        public float AngularSpeed {get;private set;}
        public float Elapsed {get;private set;}
        public float CarryTime {get;private set;}
        public float ResultUntil {get;private set;}
        public float SwayBlend=>Carrying?Mathf.SmoothStep(0,1,CarryTime/.7f):0;
        public float SwayRoll=>SwayBlend*(7*Mathf.Sin(CarryTime*1.75f)+1.3f*Mathf.Sin(CarryTime*2.9f));
        public float SwayOffset=>SwayBlend*(.075f*Mathf.Sin(CarryTime*1.75f)+.015f*Mathf.Sin(CarryTime*2.9f));
        public float StaggerVelocity=>SwayBlend*(.26f*Mathf.Sin(CarryTime*1.75f)+.07f*Mathf.Sin(CarryTime*2.9f));
        public bool NearDestination=>Carrying && CardboardRunner.Player && Vector3.Distance(CardboardRunner.Player.transform.position,destination.position)<2.75f;
        public string DeliveryPrompt=>CanServe?"E - SET DOWN TRAY":"E - SET DOWN TRAY  |  HOLD IT LEVEL";
        MaterialPropertyBlock bulbProperties;
        bool previousLeft,previousRight;
        public bool CanServe=>NearDestination && Mathf.Abs(Tilt)<=.4f && Mathf.Abs(PitchTilt)<=.45f;
        bool Gameplay=>game && !game.MenuActive && !game.Ended && !game.CaptureActive && !game.IntroBusy && !game.EndingBusy;
        public void ResetLunch()
        {
            Attended=Missed=FinalLunch=previousLeft=previousRight=false;AttendanceElapsed=0;
            State=LunchState.Waiting;Tilt=PitchTilt=AngularSpeed=Elapsed=CarryTime=ResultUntil=0;
            if(diners)diners.ResetLunch();
            RestoreTray();RefreshWorld();
        }
        void RestoreTray()
        {
            if(physicsTray)physicsTray.ResetContents();
            tray.SetParent(transform,false);tray.SetPositionAndRotation(pickup.position,pickup.rotation);tray.gameObject.SetActive(true);
            if(physicsTray){physicsTray.body.position=tray.position;physicsTray.body.rotation=tray.rotation;}
        }
        void BeginOrdinaryLunch()
        {
            Attended=Missed=previousLeft=previousRight=false;AttendanceElapsed=0;
            Tilt=PitchTilt=AngularSpeed=CarryTime=ResultUntil=Elapsed=0;
            RestoreTray();State=LunchState.Ready;RefreshWorld();if(diners)diners.Begin();
            game.Toast("Lunch time! Reach the canteen in "+ArrivalCountdown+".",8);game.PlayCaptureSound("good");TickAttendance(0);
        }
        public void TickSchedule(float dt)
        {
            if(!Gameplay || game.IntroActive || FinalLunch)return;
            Elapsed+=Mathf.Max(0,dt);
            if(State==LunchState.Waiting)
            {
                if(Elapsed>=lunchAfter && game.Repairs>0)BeginOrdinaryLunch();
                return;
            }
            TickAttendance(dt);

        }
        void TickAttendance(float dt)
        {
            if(FinalLunch || Attended || Missed || !CardboardRunner.Player)return;
            Vector3 p=CardboardRunner.Player.transform.position;
            if(p.x>CardboardFactoryWorld.CanteenWest && p.x<CardboardFactoryWorld.CanteenEast &&
                p.z>CardboardFactoryWorld.CanteenSouth && p.z<CardboardFactoryWorld.CanteenNorth)
            {Attended=true;return;}
            AttendanceElapsed+=Mathf.Max(0,dt);
            if(AttendanceElapsed<attendanceGrace)return;
            Missed=true;game.RegisterMistake("Missed the lunch break.",1.5f,emotion:FactoryWorkerEmotion.None);
        }
        public void BeginFinalLunch()
        {
            FinalLunch=true;
            if(Carrying && physicsTray)physicsTray.ReleaseTray();
            State=LunchState.Ready;RefreshWorld();
            if(diners)diners.BeginFinal();
            game.PlayCaptureSound("good");
        }
        public bool TryTake()
        {
            if(!Gameplay || State!=LunchState.Ready || game.HasPart || game.HandsBusy || game.PlayerBlocked || !CardboardRunner.Player ||
                CardboardRunner.Player.JumpHeight>.01f || Vector3.Distance(CardboardRunner.Player.transform.position,pickup.position)>3.5f || !physicsTray)return false;
            if(game.station)game.station.EndGrip(false);
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false);
            Attended=true;previousLeft=previousRight=false;State=LunchState.Carrying;Tilt=0;AngularSpeed=0;PitchTilt=0;CarryTime=0;
            tray.SetParent(transform,true);
            HoldingPose(out var position,out var rotation);physicsTray.Begin(position,rotation);RefreshWorld();
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            return true;
        }
        void Update()
        {
            if(!Gameplay || !Carrying || !Application.isFocused){previousLeft=previousRight=false;if(physicsTray)physicsTray.SetBalanceInput(0);return;}
            var mouse=Mouse.current;
            TickBalance(Time.deltaTime,mouse!=null && mouse.leftButton.isPressed,mouse!=null && mouse.rightButton.isPressed,
                mouse!=null && mouse.leftButton.wasPressedThisFrame,mouse!=null && mouse.rightButton.wasPressedThisFrame);
        }
        public void TickBalance(float dt,bool leftHeld,bool rightHeld,bool leftPressed=false,bool rightPressed=false)
        {
            if(!Gameplay || !Carrying || dt<=0)return;
            dt=Mathf.Min(dt,.1f);CarryTime+=dt;
            bool left=leftPressed || (leftHeld && !previousLeft),right=rightPressed || (rightHeld && !previousRight);
            if(left!=right)physicsTray.PressBalance(right?1:-1);
            previousLeft=leftHeld;previousRight=rightHeld;
            physicsTray.SetBalanceInput((rightHeld?1:0)-(leftHeld?1:0));
            ReadPhysicalBalance();PoseTray();
        }
        void HoldingPose(out Vector3 position,out Quaternion rotation)
        {
            var camera=Camera.main.transform;
            // Looking down must not drive the tray through the counter or floor.
            position=camera.position+Quaternion.Euler(0,camera.eulerAngles.y,0)*new Vector3(SwayOffset*.35f,-.30f,.72f);
            float pitch=Mathf.Clamp(Mathf.DeltaAngle(0,camera.eulerAngles.x)*.15f,-10,10);
            rotation=Quaternion.Euler(pitch+2+Mathf.Sin(CarryTime*2.6f)*.4f,camera.eulerAngles.y,0);
        }
        void PoseTray(){if(!physicsTray || !Camera.main)return;HoldingPose(out var position,out var rotation);physicsTray.Aim(position,rotation);}
        void ReadPhysicalBalance()
        {
            if(!physicsTray || !physicsTray.body)return;
            Tilt=-physicsTray.RollDegrees/42;PitchTilt=physicsTray.PitchDegrees/42;AngularSpeed=-physicsTray.RollSpeed/42;
        }
        void LateUpdate(){if(Carrying){PoseTray();ReadPhysicalBalance();}RefreshTrayLight();RefreshDeliveryLight();}
        public void RefreshDeliveryLight()
        {
            bool bright=!Carrying || Mathf.Repeat(CarryTime,1.25f)<.8f;
            if(deliverySpotlight)deliverySpotlight.intensity=bright?24:2;
            if(deliveryBulb)
            {
                if(bulbProperties==null)bulbProperties=new MaterialPropertyBlock();
                bulbProperties.SetColor("_EmissionColor",new Color(1,.91f,.68f)*(bright?6:.12f));
                deliveryBulb.SetPropertyBlock(bulbProperties);
            }
        }
        public void EnsureTraySpotlight()
        {
            if(traySpotlight)return;
            var node=new GameObject("Lunch tray spotlight");node.transform.SetParent(transform,false);
            traySpotlight=node.AddComponent<Light>();traySpotlight.type=LightType.Spot;
            traySpotlight.color=new Color(1,.95f,.82f);traySpotlight.intensity=8;traySpotlight.range=3.5f;
            traySpotlight.spotAngle=75;traySpotlight.innerSpotAngle=48;traySpotlight.shadows=LightShadows.Soft;
            traySpotlight.shadowBias=.015f;traySpotlight.shadowNormalBias=.01f;traySpotlight.shadowNearPlane=.05f;
            traySpotlight.cullingMask=~((1<<10)|(1<<FactoryInventorySlot.Layer)|(1<<FactoryDeathPresentation.Layer));
            traySpotlight.enabled=false;
        }
        public void RefreshTrayLight()
        {
            if(!tray)return;EnsureTraySpotlight();
            traySpotlight.enabled=game && !game.MenuActive && !game.Ended && !FinalLunch &&
                (State==LunchState.Ready || Carrying || State==LunchState.Served);
            Vector3 forward=Camera.main?Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0)*Vector3.forward:Vector3.forward;
            Vector3 center=physicsTray && physicsTray.body?physicsTray.body.position:tray.position;
            Vector3 position=center+Vector3.up*1.2f-forward*.25f;
            traySpotlight.transform.SetPositionAndRotation(position,Quaternion.LookRotation(center+Vector3.up*.06f-position));
        }
        public bool TryServe()
        {
            if(!Gameplay || !CanServe)return false;
            physicsTray.Park(destination);State=LunchState.Served;Tilt=AngularSpeed=PitchTilt=0;ResultUntil=Time.time+6;
            RefreshWorld();game.PlayCaptureSound("good");return true;
        }
        public void Spill()
        {
            if(!Carrying)return;
            State=LunchState.Spilled;ResultUntil=Time.time+6;if(physicsTray)physicsTray.ReleaseTray();
            RefreshWorld();game.RegisterMistake("Lunch spilled in front of the workers.",emotion:FactoryWorkerEmotion.None);game.PlayCaptureSound("impact");
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
            if(diners)diners.RefreshDeliveryAccess();
            RefreshDeliveryLight();
            if(pickupHit)pickupHit.SetActive(State==LunchState.Waiting || State==LunchState.Ready);
            if(destinationHit)destinationHit.SetActive(Carrying);
            if(counterSign)counterSign.gameObject.SetActive(false);
            if(tableSign)tableSign.gameObject.SetActive(false);
        }
    }
}
