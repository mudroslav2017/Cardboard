using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Cardboard
{
    [DefaultExecutionOrder(-50)]
    public class CardboardFactoryGame : MonoBehaviour
    {
        public static CardboardFactoryGame Instance {get;private set;}
        public FactoryGear[] gears;
        public CardboardWorld crowd;
        public FactoryWorkerExpressions expressions;
        public FactoryIntro intro;
        public FactoryEnding ending;
        public bool EndingBusy=>ending && ending.Active;
        public bool IntroBusy=>intro && intro.BlocksControls;
        public bool IntroActive=>intro && intro.Active;
        public Mesh[] gearMeshes,hubMeshes;
        public Material gearPaper,gearEdge,gearInk;
        public Transform victim,buttons,pins,pressHead,exitGate,captureStage;
        public Vector3[] patrol,workerPosts;
        public FactoryWorkbench workbench;
        public FactoryWorkstation station;
        public FactoryCaptureSequence captureSequence;
        public FactoryMainMenu mainMenu;
        public FactoryLunch lunch;
        public FactoryInstruction instruction;
        public FactoryObjectCarry objectCarry;
        public FactoryDoor exitDoor;
        public bool HandsBusy => (lunch && lunch.Carrying) || (objectCarry && objectCarry.Held);
        public bool MenuActive => mainMenu && mainMenu.BlocksGameplay;
        public bool CaptureActive => captureSequence && captureSequence.Active;
        public FactoryWorkstation[] stations;
        public int CurrentOrder {get;private set;}
        public Material[] finishMaterials;
        public bool DippingPart {get;private set;}
        public FactoryState State {get;private set;}
        public FactoryDeath Death {get;private set;}
        public float Suspicion {get;private set;}
        public float Disguise {get;private set;}
        public int Repairs {get;private set;}
        public const int MistakeLimit=3;
        public const int RequiredRepairs=6;
        public int RepairGoal=>Mathf.Min(RequiredRepairs,gears.Length);
        public int OrderedType=>Memorized?Memorized.recipe.Type:0;
        readonly HashSet<int> faultHistory=new HashSet<int>();
        float nextFault;
        public float Mistakes {get;private set;}
        public IReadOnlyList<string> MistakeReasons => mistakeReasons;
        public string LastMistakeReason {get;private set;} = "";
        readonly List<string> mistakeReasons=new List<string>();
        public float ShiftRemaining {get;private set;}
        public bool HasPart {get;private set;}
        public FactoryRecipe HeldRecipe {get;private set;}
        public FactoryGear Memorized {get;private set;}
        public FactoryInteractable Focus {get;private set;}
        public string Message {get;private set;}
        public float MessageUntil {get;private set;}
        public bool Pretending {get;private set;}
        public bool Ended => State==FactoryState.Captured || State==FactoryState.Dead || State==FactoryState.Escaped;
        public bool PlayerBlocked => MenuActive || Ended || IntroBusy || EndingBusy || (!CaptureActive && (DippingPart || (workbench && workbench.ConsumesMouse) || (station && station.ConsumesMouse)));
        public bool CameraOverride => (mainMenu && mainMenu.OwnsCamera) || (captureSequence && captureSequence.CameraControlled) || IntroBusy || (ending && ending.CameraControlled);
        public Vector3 CameraPosition => mainMenu && mainMenu.OwnsCamera?mainMenu.CameraPosition:IntroBusy?intro.CameraPosition:ending && ending.CameraControlled?ending.CameraPosition:captureSequence.CameraPosition;
        public Quaternion CameraRotation => mainMenu && mainMenu.OwnsCamera?mainMenu.CameraRotation:IntroBusy?intro.CameraRotation:ending && ending.CameraControlled?ending.CameraRotation:captureSequence.CameraRotation;
        public bool ReadyToLeave => RepairGoal>0 && Repairs>=RepairGoal && WorkingGears==gears.Length;
        public int WorkingGears {get { int count=0;foreach(var gear in gears) if(!gear.broken) count++;return count; } }
        float jumpWatch,runSeen,runGrace;
        bool runCharged;
        GameObject carried;
        FactorySound sound;
        Vector3 pressHome,gateHome;

        void Awake() { Instance=this; }
        void Start()
        {
            sound=gameObject.AddComponent<FactorySound>();
            pressHome=pressHead.localPosition;gateHome=exitGate.localPosition;
            ResetShift();
        }
        public void ResetShift()
        {
            if(ending)ending.Cancel();
            if(intro)intro.Cancel();
            if(captureSequence) captureSequence.Cancel();
            State=FactoryState.Working;Suspicion=0;Disguise=100;Repairs=0;Mistakes=0;ShiftRemaining=240;
            mistakeReasons.Clear();LastMistakeReason="";runSeen=runGrace=0;runCharged=false;
            HasPart=false;Pretending=false;Memorized=null;
            DippingPart=false;
            if(lunch) lunch.ResetLunch();
            if(objectCarry)objectCarry.Clear();
            foreach(var prop in GetComponentsInChildren<FactoryPhysicsProp>(true))
            {if(prop.workbenchPart)Destroy(prop.gameObject);else prop.ResetHome();}
            foreach(var reaction in GetComponentsInChildren<FactoryWorkerImpact>())reaction.ResetReaction();
            if(instruction) instruction.ResetFloor();
            if(workbench) workbench.ResetMachine();
            if(station) station.ResetMachine();
            jumpWatch=0;
            if(carried){carried.SetActive(false);Destroy(carried);carried=null;}
            foreach(var gear in gears) gear.SetBroken(false);
            faultHistory.Clear();nextFault=0;SetOrder(0);BreakRandomGear();
            victim.gameObject.SetActive(false);buttons.gameObject.SetActive(false);pins.gameObject.SetActive(false);
            pressHead.localPosition=pressHome;exitGate.localPosition=gateHome;
            foreach(var door in GetComponentsInChildren<FactoryDoor>(true))door.SetOpen(false,true);
            if(expressions)expressions.ResetShift();
            Toast("Your work order is printing at your workstation.",9);
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(MenuActive || CaptureActive || IntroBusy || EndingBusy) return;
            if(Ended)
            {
                if((State==FactoryState.Dead || State==FactoryState.Escaped) && keyboard!=null && keyboard.enterKey.wasPressedThisFrame) Restart();
                return;
            }
            if(!CardboardRunner.Player) return;
            if(keyboard!=null && keyboard.tKey.isPressed && keyboard.uKey.isPressed && keyboard.oKey.isPressed &&
                (keyboard.tKey.wasPressedThisFrame || keyboard.uKey.wasPressedThisFrame || keyboard.oKey.wasPressedThisFrame) && CompleteRepairsShortcut())return;
            float dt=Time.deltaTime;if(IntroActive){UpdateFocus();if(keyboard!=null && keyboard.eKey.wasPressedThisFrame && !CardboardMarker.DrawingMode)Interact();return;}TickShiftClock(dt);if(Ended || CaptureActive) return;
            TickFaults(dt);if(lunch) lunch.TickSchedule(dt);
            Pretending=keyboard!=null && keyboard.cKey.isPressed && !CardboardMarker.DrawingMode && !PlayerBlocked;
            UpdateFocus();
            if(keyboard!=null)
            {
                if(keyboard.eKey.wasPressedThisFrame && !PlayerBlocked && !CardboardMarker.DrawingMode) Interact();
                // Mouse placement and fixed-strength throws are routed through FactoryObjectCarry.
            }
            if(ReadyToLeave && !exitDoor) exitGate.localPosition=Vector3.Lerp(exitGate.localPosition,gateHome+Vector3.right*2.5f,dt*2);
            bool watched=IsWatched();
            ObserveConduct(watched,CardboardRunner.Player.speed,CardboardRunner.Player.JumpHeight,station && station.IsPumpingPlayer,dt);
        }
        public bool CompleteRepairsShortcut()
        {
            if(MenuActive || Ended || CaptureActive || IntroActive || EndingBusy || !CardboardRunner.Player)return false;
            if(station)station.EndGrip(false);
            if(workbench)workbench.EndGrip(false);
            if(objectCarry)objectCarry.Release(false);
            if(lunch && lunch.Carrying)lunch.CancelCarry();
            if(DippingPart)CancelDip();
            if(HasPart)ReleasePart(false);
            foreach(var gear in gears)gear.SetBroken(false);
            Repairs=RepairGoal;SetOrder(0);nextFault=float.PositiveInfinity;Focus=null;
            if(ending)ending.Begin();
            return true;
        }
        public bool IsWatched()=>FindWitness()!=null;
        public CardboardRunner FindWitness()
        {
            if(!CardboardRunner.Player || !crowd || crowd.runners==null)return null;
            Vector3 eyes=CardboardRunner.Player.transform.position+Vector3.up*1.5f;
            CardboardRunner chosen=null;float nearest=5.5f;
            foreach(var worker in crowd.runners)
            {
                if(!worker || worker.isPlayer || !worker.gameObject.activeInHierarchy)continue;
                float distance=Vector3.Distance(worker.transform.position,eyes);
                if(distance>=nearest || Physics.Linecast(worker.transform.position+Vector3.up*1.7f,eyes,1<<8,QueryTriggerInteraction.Ignore))continue;
                chosen=worker;nearest=distance;
            }
            return chosen;
        }
        public CardboardRunner NearestWorker()
        {
            if(!crowd || crowd.runners==null || !CardboardRunner.Player)return null;
            CardboardRunner chosen=null;float nearest=float.PositiveInfinity;
            foreach(var worker in crowd.runners)
            {
                if(!worker || worker.isPlayer || !worker.gameObject.activeInHierarchy)continue;
                float distance=(worker.transform.position-CardboardRunner.Player.transform.position).sqrMagnitude;
                if(distance<nearest){nearest=distance;chosen=worker;}
            }
            return chosen;
        }
        public void TickFaults(float dt)
        {
            if(MenuActive || Ended || CaptureActive || IntroActive || EndingBusy || CurrentOrder!=0 || Repairs>=RepairGoal)return;
            nextFault-=Mathf.Max(0,dt);if(nextFault<=0)BreakRandomGear();
        }
        public bool BreakRandomGear()
        {
            if(CurrentOrder!=0 || Repairs>=RepairGoal)return false;
            var choices=new List<int>();
            for(int i=0;i<gears.Length;i++)if(gears[i] && !gears[i].broken && !faultHistory.Contains(i))choices.Add(i);
            if(choices.Count==0)return false;
            int selected=choices[Random.Range(0,choices.Count)];faultHistory.Add(selected);
            gears[selected].SetBroken(true);SetOrder(selected+1);return true;
        }
        public Vector3 WorkerTarget(CardboardRunner worker,out float speed)
        {
            if(State==FactoryState.Chased)
            { speed=6.6f;return captureSequence?captureSequence.WorkerWaypoint(worker):CardboardRunner.Player.transform.position; }
            if(lunch && lunch.diners && lunch.diners.TryTarget(worker,out var lunchTarget,out speed)) return lunchTarget;
            if(worker.personality>=2 && worker.personality<=4 && workerPosts!=null && workerPosts.Length==3)
            { speed=1.2f;return workerPosts[worker.personality-2]; }
            speed=1.15f+(worker.personality%3)*.18f;
            int point=(Mathf.FloorToInt(Time.time/(7+worker.personality%3))+worker.personality)%patrol.Length;
            return patrol[point];
        }
        public bool RegisterMistake(string reason,float stars=1f,CardboardRunner witness=null,FactoryWorkerEmotion emotion=FactoryWorkerEmotion.Machine)
        {
            if(MenuActive || Ended || CaptureActive || EndingBusy || Mistakes>=MistakeLimit || stars<=0 || float.IsNaN(stars) || float.IsInfinity(stars)) return false;
            LastMistakeReason=string.IsNullOrWhiteSpace(reason)?"Shift rules violation.":reason;
            mistakeReasons.Add(LastMistakeReason);Mistakes=Mathf.Min(MistakeLimit,Mistakes+stars);
            Suspicion=100f*Mistakes/MistakeLimit;Disguise=100-Mistakes*25;
            Toast(LastMistakeReason,7);
            if(sound) sound.Play("wrong");
            var observer=witness?witness:NearestWorker();
            if(observer){var reaction=observer.GetComponent<FactoryWorkerImpact>();if(reaction)reaction.Notice(2.7f,emotion);}
            if(Mistakes>=MistakeLimit) Capture(FactoryDeath.Press);
            return true;
        }
        public void ObserveConduct(bool watched,float speed,float jump,bool pumping,float dt)
        {
            if(Ended || EndingBusy) return;
            if(watched && speed>4.1f)
            {
                runSeen+=Mathf.Max(0,dt);runGrace=0;
                if(!runCharged && runSeen>=.75f)
                { runCharged=true;RegisterMistake("Workers noticed you running.",.5f,FindWitness(),FactoryWorkerEmotion.Run); }
            }
            else
            {
                runGrace+=Mathf.Max(0,dt);
                if(runGrace>=.5f) {runSeen=0;runCharged=false;}
            }
            if(watched && jump>.3f && jumpWatch<=.3f && !pumping)
                RegisterMistake("Workers noticed you jumping away from the pump.",.5f,FindWitness(),FactoryWorkerEmotion.Jump);
            jumpWatch=jump;
        }
        public void TickShiftClock(float dt)
        {
            if(MenuActive || Ended || IntroActive || EndingBusy) return;
            ShiftRemaining=Mathf.Max(0,ShiftRemaining-Mathf.Max(0,dt));
        }
        // Compatibility for the preserved legacy machine's discrete jam event.
        public void AddSuspicion(float amount)
        { if(amount>0) RegisterMistake("Incorrect machine operation."); }
        readonly RaycastHit[] focusHits=new RaycastHit[128];
        static readonly IComparer<RaycastHit> hitOrder=Comparer<RaycastHit>.Create((a,b)=>a.distance.CompareTo(b.distance));
        void UpdateFocus()
        {
            Focus=null;var cam=Camera.main;if(!cam || PlayerBlocked || CardboardMarker.DrawingMode) return;
            Focus=TargetForRay(cam.ViewportPointToRay(new Vector3(.5f,.5f)));
        }
        public FactoryInteractable TargetForRay(Ray ray)
        {
            var hits=focusHits;int count=Physics.RaycastNonAlloc(ray,hits,3.5f,~((1<<9)|(1<<CardboardMarkerHand.HandLayer)),QueryTriggerInteraction.Collide);
            System.Array.Sort(hits,0,count,hitOrder);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                // The liquid, rim and outside of a vessel all select the same bath.
                // Resolve the first visible vessel only, so walls still block interaction.
                var owner=hit.collider.GetComponentInParent<FactoryWorkstation>();
                if(owner && owner.isPlayerStation && !HandsBusy)
                    foreach(var bath in owner.baths)
                        if(bath && hit.collider.transform.IsChildOf(bath))
                        {
                            var control=bath.GetComponentInChildren<FactoryStationControl>();
                            if(control)return control.GetComponent<FactoryInteractable>();
                        }
                if(hit.collider.isTrigger && !hit.collider.GetComponentInParent<FactoryInteractable>())continue;
                var station=hit.collider.GetComponentInParent<FactoryWorkstation>();
                if(station && !hit.collider.GetComponentInParent<FactoryInteractable>())
                {
                    for(int j=i+1;j<count;j++)
                    {
                        var control=hits[j].collider.GetComponent<FactoryStationControl>();
                        if(control && control.station==station && hits[j].distance-hit.distance<=.45f)
                            return !HandsBusy?control.GetComponent<FactoryInteractable>():null;
                    }
                }
                var target=hit.collider.GetComponentInParent<FactoryInteractable>();
                return HandsBusy && target && target.action!=FactoryAction.Lunch && target.action!=FactoryAction.Door?null:target;
            }
            return null;
        }

        public string FocusPrompt
        {
            get
            {
                if(objectCarry && objectCarry.Held)return objectCarry.Prompt;
                if(!Focus) return HasPart && objectCarry?objectCarry.Prompt:"";
                if(Focus.action==FactoryAction.Lunch)
                {
                    var lunchControl=Focus.GetComponent<FactoryLunchControl>();
                    return lunchControl?lunchControl.Prompt:"";
                }
                if(Focus.action==FactoryAction.Door) return Focus.GetComponentInParent<FactoryDoor>().Prompt;
                if(Focus.action==FactoryAction.Bench) return "";
                if(Focus.action==FactoryAction.Gear) return Focus.gear.broken && HasPart?"E - INSTALL GEAR":"";
                if(Focus.action==FactoryAction.Exit) return ReadyToLeave?"E - FINISH SHIFT AND LEAVE":"Exit unlocks after six repairs";
                return Focus.prompt;
            }
        }
        public void Interact()
        {
            if(Ended)return;
            if(instruction && instruction.Held){instruction.Flip();return;}
            if(!Focus)return;
            if(HandsBusy && Focus.action!=FactoryAction.Lunch && Focus.action!=FactoryAction.Door) return;
            switch(Focus.action)
            {
                case FactoryAction.Door:
                    var door=Focus.GetComponentInParent<FactoryDoor>();if(door)door.Toggle();break;
                case FactoryAction.Instruction:
                    if(instruction) instruction.Take();break;
                case FactoryAction.PhysicsProp:
                    var prop=Focus.GetComponentInParent<FactoryPhysicsProp>();if(prop)prop.Take(this);break;
                case FactoryAction.Gear:
                    if(HasPart && Focus.gear.broken) TryInstall(Focus.gear);else ReadBlueprint(Focus.gear);break;
                case FactoryAction.Blueprint: ReadBlueprint(Focus.gear);break;
                case FactoryAction.Bench:
                    var control=Focus.GetComponent<WorkbenchControl>();if(control) control.Use();break;
                case FactoryAction.Station:
                    var stationControl=Focus.GetComponent<FactoryStationControl>();if(stationControl) stationControl.Use();break;
                case FactoryAction.Exit:
                    TryExit();break;
                case FactoryAction.Lunch:
                    var lunchControl=Focus.GetComponent<FactoryLunchControl>();if(lunchControl) lunchControl.Use();break;
            }
        }
        public bool TryExit(bool atCorridorEnd=false)
        {
            if(ending && ending.Active){if(atCorridorEnd)ending.EnterGlow();return true;}
            if(CaptureActive || HandsBusy) return false;
            if(ending && ReadyToLeave)return ending.Begin();
            if(!ReadyToLeave || Ended) { Toast("Repair six faults in the main mechanism first.");return false; }
            if(intro && !atCorridorEnd){exitDoor.SetOpen(true);Toast("The way out is open. Follow the corridor.",6);return true;}
            State=FactoryState.Escaped;Cursor.lockState=CursorLockMode.None;FactoryVolumeCursor.SetSystemVisible(true);
            if(sound) sound.Play("good");return true;
        }
        public void ReadBlueprint(FactoryGear gear)
        {
            if(Ended || CurrentOrder==0) return;
            if(station) station.PrintOrder(CurrentOrder);
        }
        public void PrepareTutorial(FactoryGear target)
        {
            foreach(var gear in gears)gear.SetBroken(gear==target);
            Repairs=0;faultHistory.Clear();nextFault=float.PositiveInfinity;SetOrder(0);
            CurrentOrder=target.index+1;Memorized=target;
        }
        public void StartShiftAfterTutorial(FactoryGear repaired)
        {
            Repairs=0;faultHistory.Clear();faultHistory.Add(repaired.index);nextFault=0;SetOrder(0);BreakRandomGear();
        }
        void SetOrder(int number)
        {
            CurrentOrder=number;
            Memorized=number>=1 && number<=gears.Length?gears[number-1]:null;
            if(station) station.PrintOrder(number);
        }
        public void ReceiveWorkbenchPart(FactoryRecipe recipe)
        {
            HeldRecipe=recipe;HasPart=true;if(objectCarry)objectCarry.ItemReceived();
            if(carried){carried.SetActive(false);Destroy(carried);carried=null;}
            carried=new GameObject("Assembled cardboard gear in left hand");
            carried.transform.SetParent(Camera.main.transform,false);
            carried.transform.localPosition=new Vector3(-.28f,-.28f,.64f);
            carried.transform.localRotation=Quaternion.Euler(12,10,0);carried.transform.localScale=Vector3.one*.24f;
            FactoryGeometry.GearVisual(carried.transform,HeldRecipe,gearMeshes,hubMeshes,gearPaper,gearEdge,gearInk);
            carried.AddComponent<FactoryHeldGearMotion>().Initialize(this,Camera.main);
            if(recipe.finish>0 && finishMaterials!=null && recipe.finish<=finishMaterials.Length)
                foreach(var renderer in carried.GetComponentsInChildren<Renderer>())
                {var materials=renderer.sharedMaterials;for(int i=0;i<materials.Length;i++)if(materials[i]==gearPaper)materials[i]=finishMaterials[recipe.finish-1];renderer.sharedMaterials=materials;}
            Toast("Part collected: "+HeldRecipe+". Find the matching socket.",6);if(sound) sound.Play("good");
        }
        public bool BeginDip()
        {
            if(!HasPart || HeldRecipe.finish!=0 || DippingPart || Ended || HandsBusy) return false;
            DippingPart=true;if(carried) carried.SetActive(false);return true;
        }
        public void FinishDip(int bath)
        {
            if(!DippingPart || !HasPart || Ended) return;
            var recipe=HeldRecipe;recipe.finish=bath;HeldRecipe=recipe;DippingPart=false;
            if(carried)
            {
                carried.SetActive(true);
                foreach(var renderer in carried.GetComponentsInChildren<Renderer>())
                {
                    var materials=renderer.sharedMaterials;
                    for(int i=0;i<materials.Length;i++) if(materials[i]==gearPaper) materials[i]=finishMaterials[bath-1];
                    renderer.sharedMaterials=materials;
                }
            }
            if(sound) sound.Play("good");
        }
        void CancelDip() { DippingPart=false;if(carried) carried.SetActive(true); }
        public bool TryInstall(FactoryGear gear)
        {
            if(!HasPart || !gear || !gear.broken || Ended || HandsBusy) return false;
            if(gear.index+1!=CurrentOrder || !HeldRecipe.Equals(gear.recipe))
            {
                string reason=gear.index+1!=CurrentOrder?"Tried to install a gear in the wrong socket.":
                    HeldRecipe.Type!=gear.recipe.Type?"Installed the wrong gear type.":
                    HeldRecipe.finish==0?"Tried to install an untreated gear.":
                    "Wrong finishing liquid: "+HeldRecipe.finish+" instead of "+gear.recipe.finish+".";
                RegisterMistake(reason);return false;
            }
            gear.SetBroken(false);HasPart=false;if(carried){carried.SetActive(false);Destroy(carried);carried=null;}
            if(intro && intro.OnRepair(gear))return true;
            Repairs++;
            SetOrder(0);nextFault=Random.Range(4f,8f);
            if(ReadyToLeave && ending)ending.Begin();
            Toast(ReadyToLeave?"Six repairs complete. The lunch bell will ring in a moment.":"Socket repaired. Repairs: "+Repairs+" / "+RepairGoal+".",6);
            if(sound) sound.Play("good");return true;
        }
        public void DropPart(){ReleasePart(false);}
        public void ReleasePart(bool thrown)
        {
            if(!HasPart || !carried || DippingPart)return;
            HasPart=false;var loose=carried;carried=null;
            var position=loose.transform.position;var rotation=loose.transform.rotation;
            var motion=loose.GetComponent<FactoryHeldGearMotion>();if(motion){motion.enabled=false;Destroy(motion);}
            loose.transform.SetParent(transform,true);loose.transform.SetPositionAndRotation(position,rotation);
            Bounds bounds=default;bool first=true;
            foreach(var renderer in loose.GetComponentsInChildren<MeshRenderer>())
            {if(first){bounds=renderer.bounds;first=false;}else bounds.Encapsulate(renderer.bounds);}
            var collider=loose.AddComponent<BoxCollider>();
            collider.center=loose.transform.InverseTransformPoint(bounds.center);
            collider.size=new Vector3(bounds.size.x/loose.transform.lossyScale.x,bounds.size.y/loose.transform.lossyScale.y,Mathf.Max(.15f,bounds.size.z/loose.transform.lossyScale.z));
            var body=loose.AddComponent<Rigidbody>();body.mass=.65f;
            var prop=loose.AddComponent<FactoryPhysicsProp>();prop.itemName="gear";prop.workbenchPart=true;prop.recipe=HeldRecipe;prop.Initialize();
            var target=loose.AddComponent<FactoryInteractable>();target.action=FactoryAction.PhysicsProp;target.prompt="E - PICK UP GEAR";
            prop.BeginCarry();prop.Release(thrown?Camera.main.transform.forward*FactoryObjectCarry.ThrowSpeed(body.mass):Vector3.zero,thrown);
        }
        public void Toast(string message,float seconds=4) { Message=message;MessageUntil=Time.time+seconds; }
        public void Capture(FactoryDeath death)
        {
            if(Ended || CaptureActive) return;
            Death=FactoryDeath.Press;State=FactoryState.Chased;Pretending=false;
            if(lunch) lunch.CancelCarry();
            if(workbench) workbench.EndGrip(false);
            if(station) station.EndGrip(false);CancelDip();
            if(CardboardMarker.Instance) CardboardMarker.Instance.SetDrawingMode(false,false);
            if(carried) carried.SetActive(false);
            victim.gameObject.SetActive(false);pins.gameObject.SetActive(false);buttons.gameObject.SetActive(false);
            captureSequence.Begin();
            if(sound) sound.Play("alarm");
        }
        internal void SetCaptureState(FactoryState state) { State=state; }
        public void PlayCaptureSound(string clip) { if(sound) sound.Play(clip); }
        public void Restart() { FactoryMainMenu.SkipNextMenu=true;SceneManager.LoadScene(SceneManager.GetActiveScene().path); }
        void OnDestroy() { if(Instance==this) Instance=null; }
    }
}
