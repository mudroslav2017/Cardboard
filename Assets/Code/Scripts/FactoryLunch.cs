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
        internal bool LocalCarrying => Carrying && (!game.MultiplayerPrototype || game.CoopLocalTray);
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
        bool previousLeft,previousRight,ordinaryStarted,cookedLunchStarted;
        public bool CanServe=>NearDestination && Mathf.Abs(Tilt)<=.4f && Mathf.Abs(PitchTilt)<=.45f;
        bool Gameplay=>game && (!game.MenuActive || game.MultiplayerPrototype) && !game.Ended &&
            !game.CaptureActive && !game.IntroBusy && !game.EndingBusy;
        bool coopAttendance;
        public void ResetLunch()
        {
            Attended=Missed=FinalLunch=ordinaryStarted=cookedLunchStarted=previousLeft=previousRight=coopAttendance=false;AttendanceElapsed=0;
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
            ordinaryStarted=true;
            Elapsed=0;
            Attended=Missed=false;AttendanceElapsed=0;
            previousLeft=previousRight=false;
            Tilt=PitchTilt=AngularSpeed=CarryTime=ResultUntil=0;
            RestoreTray();State=LunchState.Ready;
            RefreshWorld();if(diners)diners.Begin();
            game.Toast("Lunch time! Reach the canteen in "+ArrivalCountdown+".",8);game.PlayCaptureSound("good");TickAttendance(0);
        }
        public void TickSchedule(float dt)
        {
            if(!Gameplay || game.IntroActive || FinalLunch)return;
            Elapsed+=Mathf.Max(0,dt);
            if(!ordinaryStarted)
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
            if(game.MultiplayerPrototype ? coopAttendance : InCanteen(p))
            {Attended=true;return;}
            AttendanceElapsed+=Mathf.Max(0,dt);
            if(AttendanceElapsed<attendanceGrace)return;
            Missed=true;game.RegisterMistake("Missed the lunch break.",1.5f,emotion:FactoryWorkerEmotion.None);
        }
        static bool InCanteen(Vector3 p) => p.x > CardboardFactoryWorld.CanteenWest && p.x < CardboardFactoryWorld.CanteenEast &&
            p.z > CardboardFactoryWorld.CanteenSouth && p.z < CardboardFactoryWorld.CanteenNorth;

        internal void TickCoopSchedule(float dt, FactoryCoopPlayer[] players)
        {
            coopAttendance = System.Array.TrueForAll(players, actor => !actor.Active || InCanteen(actor.Position));
            TickSchedule(dt);
        }
        public void BeginFinalLunch()
        {
            FinalLunch=true;
            if(Carrying && physicsTray)physicsTray.ReleaseTray();
            if(cookedLunchStarted)RestoreTray();
            State=LunchState.Ready;RefreshWorld();
            if(diners)diners.BeginFinal();
            game.PlayCaptureSound("good");
        }
        internal void BeginKitchenService(FactoryKitchenService service)
        {
            cookedLunchStarted=ordinaryStarted=Attended=true;
            State=LunchState.Waiting;
            ResultUntil=0;
            tray.gameObject.SetActive(false);
            RefreshWorld();
            if(diners)diners.BeginKitchenService(service);
            game.PlayCaptureSound("good");
            game.Toast("Soup's on. The workers are going to the canteen.",6f);
        }
        public bool TryTake()
        {
            if(!Gameplay || State!=LunchState.Ready || game.HasPart || game.HandsBusy || game.PlayerBlocked || !CardboardRunner.Player ||
                CardboardRunner.Player.JumpHeight>.01f || Vector3.Distance(CardboardRunner.Player.transform.position,pickup.position)>3.5f || !physicsTray)return false;
            if(game.station)game.station.EndGrip(false);
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false);
            var camera=Camera.main.transform;
            BeginCarry(camera.position, camera.rotation);
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            return true;
        }
        internal bool TryTakeCoop(FactoryCoopPlayer actor)
        {
            if (!Gameplay || State != LunchState.Ready || actor.Jump > .015f ||
                (actor.Position - pickup.position).sqrMagnitude > 12.25f || !physicsTray) return false;
            BeginCarry(actor.Position + Vector3.up * 1.62f, actor.Look);
            return true;
        }
        void BeginCarry(Vector3 eyes, Quaternion look)
        {
            Attended=true;previousLeft=previousRight=false;State=LunchState.Carrying;Tilt=0;AngularSpeed=0;PitchTilt=0;CarryTime=0;
            tray.SetParent(transform,true);
            HoldingPose(eyes, look, out var position,out var rotation);physicsTray.Begin(position,rotation);RefreshWorld();
        }
        void Update()
        {
            if (game && game.MultiplayerPrototype) return;
            if(!Gameplay || !Carrying || !Application.isFocused){previousLeft=previousRight=false;if(physicsTray)physicsTray.SetBalanceInput(0);return;}
            var mouse=Mouse.current;
            TickBalance(Time.deltaTime,mouse!=null && mouse.leftButton.isPressed,mouse!=null && mouse.rightButton.isPressed,
                mouse!=null && mouse.leftButton.wasPressedThisFrame,mouse!=null && mouse.rightButton.wasPressedThisFrame);
        }
        public void TickBalance(float dt,bool leftHeld,bool rightHeld,bool leftPressed=false,bool rightPressed=false)
        {
            if (!Camera.main) return;
            var camera = Camera.main.transform;
            TickBalance(dt, leftHeld, rightHeld, camera.position, camera.rotation, leftPressed, rightPressed);
        }
        internal void TickCoopBalance(float dt, FactoryCoopPlayer actor)
        {
            TickBalance(dt, actor.Left, actor.Right, actor.Position + Vector3.up * (1.62f + actor.Jump), actor.Look);
        }
        void TickBalance(float dt, bool leftHeld, bool rightHeld, Vector3 eyes, Quaternion look,
            bool leftPressed = false, bool rightPressed = false)
        {
            if(!Gameplay || !Carrying || dt<=0)return;
            dt=Mathf.Min(dt,.1f);CarryTime+=dt;
            bool left=leftPressed || (leftHeld && !previousLeft),right=rightPressed || (rightHeld && !previousRight);
            if(left!=right)physicsTray.PressBalance(right?1:-1);
            previousLeft=leftHeld;previousRight=rightHeld;
            physicsTray.SetBalanceInput((rightHeld?1:0)-(leftHeld?1:0));
            ReadPhysicalBalance();PoseTray(eyes, look);
        }
        void HoldingPose(Vector3 eyes, Quaternion look, out Vector3 position,out Quaternion rotation)
        {
            var angles = look.eulerAngles;
            // Looking down must not drive the tray through the counter or floor.
            position=eyes+Quaternion.Euler(0,angles.y,0)*new Vector3(SwayOffset*.35f,-.30f,.72f);
            float pitch=Mathf.Clamp(Mathf.DeltaAngle(0,angles.x)*.15f,-10,10);
            rotation=Quaternion.Euler(pitch+2+Mathf.Sin(CarryTime*2.6f)*.4f,angles.y,0);
        }
        void PoseTray(Vector3 eyes, Quaternion look)
        {if(!physicsTray)return;HoldingPose(eyes,look,out var position,out var rotation);physicsTray.Aim(position,rotation);}
        void ReadPhysicalBalance()
        {
            if(!physicsTray || !physicsTray.body)return;
            Tilt=-physicsTray.RollDegrees/42;PitchTilt=physicsTray.PitchDegrees/42;AngularSpeed=-physicsTray.RollSpeed/42;
        }
        void LateUpdate()
        {
            if(Carrying && !game.MultiplayerPrototype && Camera.main)
            {var camera=Camera.main.transform;PoseTray(camera.position,camera.rotation);ReadPhysicalBalance();}
            RefreshTrayLight();RefreshDeliveryLight();
        }
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
            traySpotlight.enabled=game && (!game.MenuActive || game.MultiplayerPrototype) && !game.Ended && !FinalLunch &&
                (State==LunchState.Ready || Carrying || State==LunchState.Served);
            Vector3 forward=Camera.main?Quaternion.Euler(0,Camera.main.transform.eulerAngles.y,0)*Vector3.forward:Vector3.forward;
            Vector3 center=physicsTray && physicsTray.body?physicsTray.body.position:tray.position;
            Vector3 position=center+Vector3.up*1.2f-forward*.25f;
            traySpotlight.transform.SetPositionAndRotation(position,Quaternion.LookRotation(center+Vector3.up*.06f-position));
        }
        public bool TryServe()
        {
            if(!Gameplay || !CanServe)return false;
            return Serve();
        }
        internal bool CanReachCoopTable(Vector3 position) => Carrying && (position - destination.position).sqrMagnitude < 7.56f;
        internal bool TryServeCoop(Vector3 position) => Gameplay && CanReachCoopTable(position) &&
            Mathf.Abs(Tilt)<=.4f && Mathf.Abs(PitchTilt)<=.45f && Serve();
        bool Serve()
        {
            physicsTray.Park(destination);State=LunchState.Served;Tilt=AngularSpeed=PitchTilt=0;ResultUntil=Time.time+6;
            RefreshWorld();game.PlayCaptureSound("good");return true;
        }
        internal void ReadCoopState(FactoryCoopLunchState state)
        {
            state.State = State;
            state.CarryTime = CarryTime;
            state.Tilt = Tilt;
            state.PitchTilt = PitchTilt;
            state.Attendance = AttendanceElapsed;
            state.Attended = Attended;
            state.Missed = Missed;
            state.Final = FinalLunch;
            state.Visible = tray.gameObject.activeSelf;
            state.CookedService = cookedLunchStarted;
            state.Tray = new FactoryCoopPose { Position = tray.position, Rotation = tray.rotation };
            if (state.Food == null) state.Food = new FactoryCoopPose[food.Length];
            for (int i = 0; i < food.Length; i++)
                state.Food[i] = new FactoryCoopPose { Position = food[i].position, Rotation = food[i].rotation };
        }
        internal bool ApplyCoopState(FactoryCoopLunchState state)
        {
            if (state == null || state.Food?.Length != food.Length || state.State < LunchState.Waiting ||
                state.State > LunchState.Spilled) return false;
            bool changed = State != state.State || cookedLunchStarted != state.CookedService;
            State = state.State;
            CarryTime = state.CarryTime;
            Tilt = state.Tilt;
            PitchTilt = state.PitchTilt;
            AttendanceElapsed = state.Attendance;
            Attended = state.Attended;
            Missed = state.Missed;
            FinalLunch = state.Final;
            cookedLunchStarted = state.CookedService;
            tray.gameObject.SetActive(state.Visible);
            tray.SetPositionAndRotation(state.Tray.Position, state.Tray.Rotation);
            for (int i = 0; i < food.Length; i++)
                food[i].SetPositionAndRotation(state.Food[i].Position, state.Food[i].Rotation);
            if (changed) RefreshWorld();
            return true;
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
            if(pickupHit)pickupHit.SetActive(!cookedLunchStarted && (State==LunchState.Waiting || State==LunchState.Ready));
            if(destinationHit)destinationHit.SetActive(Carrying);
            if(counterSign)counterSign.gameObject.SetActive(false);
            if(tableSign)tableSign.gameObject.SetActive(false);
        }
    }
}
