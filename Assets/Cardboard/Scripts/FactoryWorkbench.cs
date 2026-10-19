using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(-40)]
    public class FactoryWorkbench : MonoBehaviour
    {
        public Transform toothDial,hubDial,feedLever,clampLever,guard,flywheel,cutter,needle,output;
        public Transform feedRollerA,feedRollerB,clampPlaten,cuttingHead,driveGearA,driveGearB,rejectHandle,gripHand;
        public Transform[] scraps;
        public Transform[] sheets;
        public TextMesh status;
        public Renderer readyLamp;
        public Material lampOff,lampOn;
        public static float LeverStrokePixels => Mathf.Clamp(Screen.height*.08f,40,85);
        public static float CutterStrokePixels => Mathf.Clamp(Screen.height*.12f,60,125);
        public int Teeth {get;private set;}=8;
        public FactoryHub Hub {get;private set;}
        public int Layers {get;private set;}
        public bool FeedRaised {get;private set;}
        public bool Clamped {get;private set;}
        public bool GuardClosed {get;private set;}
        public bool Jammed {get;private set;}
        public bool Finished {get;private set;}
        public float Winding {get;private set;}
        public float CutTravel {get;private set;}
        public bool Manipulating {get;private set;}
        public WorkbenchPart GrippedPart {get;private set;}
        public float GripTravel {get;private set;}
        public bool ConsumesMouse => Manipulating || Time.frameCount==releasedFrame;
        public FactoryRecipe FinishedRecipe {get;private set;}
        public bool Nearby => Camera.main && Vector3.Distance(Camera.main.transform.position,transform.position+new Vector3(0,1.5f,1))<4.2f;
        CardboardFactoryGame Game => CardboardFactoryGame.Instance;
        float lastAngle,startY,lastTick=-10,wheelAngle,selectorDrag,strokeDirection,gripFromState;
        float feedAngle,scrapAt=-10,cutAt=-10;
        readonly float[] fedAt={-10,-10,-10};
        int releasedFrame=-1;
        bool hasAngle,strokeCommitted,returnToLook;
        Vector2 gripPointer,gripStart;
        float pointerTravel;
        public Vector3 guardHome;
        GameObject product;

        void Start() { ResetMachine(); }
        public void ResetMachine()
        {
            EndGrip(false);Teeth=8;Hub=FactoryHub.Square;Layers=0;FeedRaised=false;
            Clamped=GuardClosed=Jammed=Finished=false;Winding=CutTravel=wheelAngle=0;
            GripTravel=selectorDrag=feedAngle=0;scrapAt=cutAt=-10;for(int i=0;i<fedAt.Length;i++) fedAt[i]=-10;
            if(product) Destroy(product);Status("NO SHEET LOADED");Refresh();
        }
        void Update()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;
            if(!Game || Game.Ended || Game.HandsBusy || Game.State==FactoryState.Chased || !Application.isFocused)
            { if(Manipulating) EndGrip(false);return; }
            if(Manipulating)
            {
                if(keyboard!=null && keyboard.escapeKey.wasPressedThisFrame) { EndGrip(false);return; }
                if(mouse==null || !mouse.leftButton.isPressed) { EndGrip();return; }
                gripPointer+=mouse.delta.ReadValue();DragTo(gripPointer);return;
            }
            if(mouse!=null && mouse.leftButton.wasPressedThisFrame && !CardboardMarker.DrawingMode)
            {
                Ray ray=Cursor.lockState==CursorLockMode.Locked?Camera.main.ViewportPointToRay(new Vector3(.5f,.5f)):Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                TryGrip(ray,mouse.position.ReadValue());
            }
        }
        public bool TryGrip(Ray ray,Vector2 pointer)
        {
            if(!Game || Game.Ended || Game.HandsBusy || Game.State==FactoryState.Chased || Manipulating) return false;
            var hit=Game.TargetForRay(ray);
            var control=hit?hit.GetComponent<WorkbenchControl>():null;
            if(!control || control.machine!=this) return false;
            GrippedPart=control.part;Manipulating=true;hasAngle=false;startY=pointer.y;
            gripPointer=gripStart=pointer;pointerTravel=0;
            GripTravel=selectorDrag=0;strokeCommitted=false;returnToLook=Cursor.lockState==CursorLockMode.Locked;
            bool state=GrippedPart==WorkbenchPart.Feed?FeedRaised:GrippedPart==WorkbenchPart.Clamp?Clamped:GuardClosed;
            gripFromState=state?1:0;
            strokeDirection=GrippedPart==WorkbenchPart.Feed || GrippedPart==WorkbenchPart.Guard?(state?-1:1):GrippedPart==WorkbenchPart.Clamp?(state?1:-1):-1;
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            if(GrippedPart==WorkbenchPart.Feed || GrippedPart==WorkbenchPart.Clamp || GrippedPart==WorkbenchPart.Guard)
                Status("CLICK - SWITCH / DRAG "+(strokeDirection>0?"UP":"DOWN"));
            else if(GrippedPart==WorkbenchPart.Cutter || GrippedPart==WorkbenchPart.Reject || GrippedPart==WorkbenchPart.Output)
                Status("CLICK - OPERATE / DRAG DOWN");
            DragTo(pointer);return true;
        }
        public void DragTo(Vector2 pointer)
        {
            if(!Manipulating) return;
            pointerTravel=Mathf.Max(pointerTravel,Vector2.Distance(pointer,gripStart));
            bool selector=GrippedPart==WorkbenchPart.Teeth || GrippedPart==WorkbenchPart.Hub;
            if(GrippedPart==WorkbenchPart.Crank || selector)
            {
                var pivot=GrippedPart==WorkbenchPart.Crank?flywheel:GrippedPart==WorkbenchPart.Teeth?toothDial:hubDial;
                Vector2 center=Camera.main.WorldToScreenPoint(pivot.position);
                Vector2 offset=pointer-center;
                float radius=Screen.height*(selector?.012f:.025f);
                if(offset.sqrMagnitude<radius*radius) { hasAngle=false;return; }
                float angle=Mathf.Atan2(offset.y,offset.x)*Mathf.Rad2Deg;
                if(hasAngle)
                {
                    float step=-Mathf.DeltaAngle(lastAngle,angle);
                    // A teleport across the wheel is not a revolution. The hand must follow its rim.
                    if(Mathf.Abs(step)<=65)
                    {
                        if(selector)
                        {
                            selectorDrag+=step;
                            if(SelectorsLocked)
                            { selectorDrag=Mathf.Clamp(selectorDrag,-7,7);Status("SELECTOR LOCKED BY CLAMP");Tick(true); }
                            else while(Mathf.Abs(selectorDrag)>=120)
                            {
                                int direction=selectorDrag>0?1:-1;
                                AdvanceSelector(GrippedPart,direction);selectorDrag-=direction*120;
                            }
                            Refresh();
                        }
                        else TurnWheel(step);
                    }
                }
                lastAngle=angle;hasAngle=true;
            }
            else if(GrippedPart==WorkbenchPart.Cutter) PullCutter(Mathf.Clamp01((startY-pointer.y)/CutterStrokePixels));
            else if(strokeCommitted && GrippedPart==WorkbenchPart.Feed && (pointer.y-startY)*strokeDirection<LeverStrokePixels-6)
            {
                // Keep the feed in the same hand for the return stroke.
                strokeCommitted=false;gripFromState=FeedRaised?1:0;
                startY+=strokeDirection*LeverStrokePixels;strokeDirection=-strokeDirection;
                DragTo(pointer);
            }
            else if(!strokeCommitted)
            {
                GripTravel=Mathf.Clamp01((pointer.y-startY)*strokeDirection/LeverStrokePixels);
                if(!CanStroke(GrippedPart))
                {
                    if(GripTravel>.08f) { Status("MECHANICAL STOP / STROKE LOCKED");Tick(true); }
                    GripTravel=Mathf.Min(.08f,GripTravel);Refresh();return;
                }
                if(GripTravel>=.98f)
                {
                    // Use the same interlocks as the E interaction, once per complete hand stroke.
                    Manipulating=false;bool accepted=Operate(GrippedPart);Manipulating=true;
                    strokeCommitted=true;if(!accepted) GripTravel=.08f;
                }
                Refresh();
            }
        }
        void TurnWheel(float degrees)
        {
            wheelAngle+=degrees;
            if(!Clamped || !GuardClosed || Layers==0 || Finished || Jammed)
            { Status(Jammed?"FEED JAMMED":!Clamped?"RATCHET: IDLE":!GuardClosed?"CLUTCH DISENGAGED":"SHEET ALREADY CUT");Tick(false);Refresh();return; }
            Winding=Mathf.Clamp(Winding+degrees,0,1080);
            Status(Winding>=1080?"SPRING LOADED":degrees<0?"REVERSE - RESET":"RATCHET ENGAGED");
            Tick(false);Refresh();
        }
        void PullCutter(float amount)
        {
            if(Finished) return;
            bool armed=Clamped && GuardClosed && !Jammed && Layers>0 && Winding>=1079.9f;
            CutTravel=armed?amount:Mathf.Min(.14f,amount);
            if(!armed && amount>.15f) { Status("CUTTER AT STOP");Tick(true); }
            if(armed && amount>=.98f)
            {
                Finished=true;FinishedRecipe=new FactoryRecipe(Teeth,Hub,Layers);
                product=new GameObject("Physical finished gear inside the cutting chamber");product.transform.SetParent(output,false);
                product.transform.localScale=Vector3.one*.32f;
                FactoryGeometry.GearVisual(product.transform,FinishedRecipe,Game.gearMeshes,Game.hubMeshes,Game.gearPaper,Game.gearEdge,Game.gearInk);
                Winding=0;cutAt=Time.time;Status("CUT COMPLETE / SHEET CLAMPED");Tick(true);
            }
            Refresh();
        }
        public void EndGrip(bool capture=true)
        {
            bool held=Manipulating;Manipulating=false;hasAngle=false;
            if(held && capture && pointerTravel<=6 && !strokeCommitted && GrippedPart!=WorkbenchPart.Teeth && GrippedPart!=WorkbenchPart.Hub && GrippedPart!=WorkbenchPart.Crank)
                Operate(GrippedPart);
            CutTravel=GripTravel=selectorDrag=0;
            if(held)
            {
                releasedFrame=Time.frameCount;Refresh();bool lockLook=capture && returnToLook;
                Cursor.lockState=lockLook?CursorLockMode.Locked:CursorLockMode.None;FactoryVolumeCursor.SetSystemVisible(!lockLook);
                if(!lockLook && Mouse.current!=null)
                    Mouse.current.WarpCursorPosition(new Vector2(Mathf.Clamp(gripStart.x,0,Screen.width-1),Mathf.Clamp(gripStart.y,0,Screen.height-1)));
            }
        }
        bool SelectorsLocked => Clamped || GuardClosed || Finished || Jammed;
        bool CanStroke(WorkbenchPart part)
        {
            switch(part)
            {
                case WorkbenchPart.Feed: return !Clamped && !GuardClosed && !Finished && !Jammed;
                case WorkbenchPart.Clamp: return !GuardClosed && !Jammed && Layers>0;
                case WorkbenchPart.Guard: return GuardClosed || (Clamped && !Jammed);
                case WorkbenchPart.Reject: return !Clamped && !GuardClosed;
                case WorkbenchPart.Output: return Finished && !GuardClosed && !Clamped && !Game.HasPart;
                default: return true;
            }
        }
        bool AdvanceSelector(WorkbenchPart part,int direction)
        {
            if(SelectorsLocked) { Status("SELECTOR LOCKED BY CLAMP");return false; }
            if(part==WorkbenchPart.Teeth)
            {
                int index=Teeth==8?0:Teeth==12?1:2;
                index=(index+direction+3)%3;Teeth=index==0?8:index==1?12:10;
            }
            else Hub=(FactoryHub)(((int)Hub+direction+3)%3);
            Status("CAM ADJUSTED");Tick(false);return true;
        }
        public bool Operate(WorkbenchPart part)
        {
            if(!Game || Game.Ended || Game.HandsBusy || Game.State==FactoryState.Chased || Manipulating) return false;
            bool done=false;
            switch(part)
            {
                case WorkbenchPart.Teeth:
                case WorkbenchPart.Hub:
                    done=AdvanceSelector(part,part==WorkbenchPart.Teeth?1:-1);break;
                case WorkbenchPart.Feed:
                    if(Clamped || GuardClosed || Finished || Jammed) { Status(Jammed?"FEED JAMMED":"FEED LOCKED");break; }
                    FeedRaised=!FeedRaised;
                    feedAngle+=180;
                    if(!FeedRaised)
                    {
                        if(Layers==3) { Jammed=true;Game.AddSuspicion(6);Status("DOUBLE FEED / DISCARD");break; }
                        fedAt[Layers]=Time.time;Layers++;Status("SHEET PASSED THROUGH ROLLERS");
                    }
                    else Status("FEED: RETURN STROKE");
                    done=true;break;
                case WorkbenchPart.Clamp:
                    if(GuardClosed) { Status("GUARD BLOCKS CLAMP");break; }
                    if(Jammed || Layers==0) { Status(Jammed?"SHEET MISALIGNED":"CLAMP CANNOT REACH SHEET");break; }
                    Clamped=!Clamped;Status(Clamped?"SHEET CLAMPED":"CLAMP RELEASED");done=true;break;
                case WorkbenchPart.Guard:
                    if(!GuardClosed && (!Clamped || Jammed)) { Status("GUARD HIT THE CLAMP");break; }
                    GuardClosed=!GuardClosed;
                    if(!GuardClosed) Winding=0;
                    Status(GuardClosed?"CLUTCH ENGAGED":"GUARD OPEN / SPRING RESET");done=true;break;
                case WorkbenchPart.Reject:
                    if(Clamped || GuardClosed) { Status("OUTPUT LOCKED BY CLAMP");break; }
                    if(Layers>0 || Finished) scrapAt=Time.time;
                    Layers=0;Jammed=Finished=FeedRaised=false;Winding=0;if(product) Destroy(product);
                    Status("SHEET DISCARDED");done=true;break;
                case WorkbenchPart.Output:
                    if(!Finished) { Status("EMPTY SLOT");break; }
                    if(GuardClosed || Clamped) { Status("PART HELD BY CLAMP");break; }
                    if(Game.HasPart) { Status("LEFT HAND OCCUPIED");break; }
                    Game.ReceiveWorkbenchPart(FinishedRecipe);if(product) Destroy(product);
                    Layers=0;Finished=FeedRaised=false;Status("PART COLLECTED");done=true;break;
                case WorkbenchPart.Cutter:
                    PullCutter(1);done=Finished;break;
                default: Status("HANDLE: HOLD AND PULL");break;
            }
            Tick(!done);Refresh();return done;
        }
        void Status(string text) { if(status) status.text=text; }
        void Tick(bool heavy)
        {
            if(Time.unscaledTime-lastTick<.13f) return;lastTick=Time.unscaledTime;
            var sound=GetComponent<FactoryWorkbenchSound>();if(sound) sound.Clunk(heavy);
        }
        void Refresh()
        {
            if(!toothDial) return;
            toothDial.localRotation=Quaternion.Euler(0,0,Teeth==8?0:Teeth==12?120:240);
            hubDial.localRotation=Quaternion.Euler(0,0,(int)Hub*120);
            feedLever.localRotation=Quaternion.Euler(0,0,FeedRaised?20:110);
            clampLever.localRotation=Quaternion.Euler(0,0,Clamped?-70:20);
            guard.localPosition=guardHome+Vector3.down*(GuardClosed?0:.72f);
            flywheel.localRotation=Quaternion.Euler(0,0,wheelAngle);
            cutter.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-32,105,CutTravel));
            needle.localRotation=Quaternion.Euler(0,0,65-Winding/1080*130);
            float clamp=Clamped?1:0,guardAmount=GuardClosed?1:0,feedPreview=0;
            if(Manipulating)
            {
                if(GrippedPart==WorkbenchPart.Teeth) toothDial.Rotate(0,0,selectorDrag,Space.Self);
                if(GrippedPart==WorkbenchPart.Hub) hubDial.Rotate(0,0,selectorDrag,Space.Self);
                if(!strokeCommitted)
                {
                    float amount=Mathf.Lerp(gripFromState,1-gripFromState,GripTravel);
                    if(GrippedPart==WorkbenchPart.Feed) { feedLever.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(110,20,amount));feedPreview=GripTravel*180; }
                    if(GrippedPart==WorkbenchPart.Clamp) { clamp=amount;clampLever.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(20,-70,amount)); }
                    if(GrippedPart==WorkbenchPart.Guard) guardAmount=amount;
                }
            }
            guard.localPosition=guardHome+Vector3.down*((1-guardAmount)*.72f);
            if(feedRollerA) feedRollerA.localRotation=Quaternion.Euler(feedAngle+feedPreview,0,0);
            if(feedRollerB) feedRollerB.localRotation=Quaternion.Euler(-feedAngle-feedPreview,0,0);
            if(clampPlaten) clampPlaten.localPosition=new Vector3(.25f,Mathf.Lerp(1.72f,1.20f+Layers*.075f,clamp),.60f);
            if(cuttingHead) cuttingHead.localPosition=new Vector3(.25f,1.84f-Mathf.Max(CutTravel,Mathf.Clamp01(1-(Time.time-cutAt)*3))*.52f,.68f);
            if(driveGearA) driveGearA.localRotation=Quaternion.Euler(0,0,wheelAngle*2);
            if(driveGearB) driveGearB.localRotation=Quaternion.Euler(0,0,-wheelAngle*2);
            if(rejectHandle) rejectHandle.localPosition=new Vector3(-1.23f,.73f,1+(Manipulating && GrippedPart==WorkbenchPart.Reject?GripTravel*.18f:0));
            if(product) product.transform.localPosition=Vector3.forward*(Manipulating && GrippedPart==WorkbenchPart.Output?GripTravel*.4f:0);
            for(int i=0;i<sheets.Length;i++)
            {
                sheets[i].gameObject.SetActive(!Finished && i<Layers);
                float feed=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time-fedAt[i])/.5f));
                sheets[i].localPosition=new Vector3(Mathf.Lerp(-1.1f,.25f,feed),1.12f+i*.075f,.58f);
                sheets[i].localRotation=Quaternion.Euler(0,0,Jammed && i==2?13:0);
            }
            if(scraps!=null) for(int i=0;i<scraps.Length;i++)
            {
                float t=Time.time-scrapAt;scraps[i].gameObject.SetActive(t>=0 && t<1.1f);
                scraps[i].localPosition=new Vector3(.25f+(i-1)*t*.4f,1.15f-t*t*1.1f,.68f+t*.7f);
                scraps[i].localRotation=Quaternion.Euler(t*120,i*35,t*90);
            }
            readyLamp.sharedMaterial=Winding>=1079.9f?lampOn:lampOff;
            if(gripHand)
            {
                gripHand.gameObject.SetActive(Manipulating);
                if(Manipulating)
                {
                    Transform grip=GrippedPart==WorkbenchPart.Teeth?toothDial:GrippedPart==WorkbenchPart.Hub?hubDial:GrippedPart==WorkbenchPart.Feed?feedLever:GrippedPart==WorkbenchPart.Clamp?clampLever:GrippedPart==WorkbenchPart.Crank?flywheel:GrippedPart==WorkbenchPart.Cutter?cutter:GrippedPart==WorkbenchPart.Reject?rejectHandle:output;
                    Vector3 local=GrippedPart==WorkbenchPart.Crank?new Vector3(.38f,0,.31f):GrippedPart==WorkbenchPart.Cutter?new Vector3(0,.64f,.15f):GrippedPart==WorkbenchPart.Feed?new Vector3(0,.36f,.15f):GrippedPart==WorkbenchPart.Clamp?new Vector3(0,.35f,.15f):new Vector3(0,0,.22f);
                    gripHand.position=GrippedPart==WorkbenchPart.Guard?transform.TransformPoint(new Vector3(1.03f,1.44f,.99f)):grip.TransformPoint(local);
                    gripHand.rotation=Quaternion.LookRotation(gripHand.position-Camera.main.transform.position);
                }
            }
        }
        void LateUpdate() { Refresh(); }
        void OnApplicationFocus(bool focused) { if(!focused) EndGrip(false); }
        void OnDisable() { EndGrip(false); }
    }
}
