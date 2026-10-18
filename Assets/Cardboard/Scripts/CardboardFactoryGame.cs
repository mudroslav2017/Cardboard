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
        public FactoryDoor exitDoor;
        public bool HandsBusy => (lunch && lunch.Carrying) || (instruction && instruction.Held);
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
        public bool PlayerBlocked => MenuActive || Ended || (!CaptureActive && (DippingPart || (workbench && workbench.ConsumesMouse) || (station && station.ConsumesMouse)));
        public bool CameraOverride => (mainMenu && mainMenu.OwnsCamera) || (captureSequence && captureSequence.CameraControlled);
        public Vector3 CameraPosition => mainMenu && mainMenu.OwnsCamera?mainMenu.CameraPosition:captureSequence.CameraPosition;
        public Quaternion CameraRotation => mainMenu && mainMenu.OwnsCamera?mainMenu.CameraRotation:captureSequence.CameraRotation;
        public bool ReadyToLeave => gears.Length>0 && WorkingGears==gears.Length;
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
            if(captureSequence) captureSequence.Cancel();
            State=FactoryState.Working;Suspicion=0;Disguise=100;Repairs=0;Mistakes=0;ShiftRemaining=240;
            mistakeReasons.Clear();LastMistakeReason="";runSeen=runGrace=0;runCharged=false;
            HasPart=false;Pretending=false;Memorized=null;
            DippingPart=false;
            if(lunch) lunch.ResetLunch();
            if(instruction) instruction.ResetFloor();
            if(workbench) workbench.ResetMachine();
            if(station) station.ResetMachine();
            jumpWatch=0;
            if(carried){carried.SetActive(false);Destroy(carried);carried=null;}
            foreach(var gear in gears) gear.SetBroken(true);
            SetOrder(1);
            victim.gameObject.SetActive(false);buttons.gameObject.SetActive(false);pins.gameObject.SetActive(false);
            pressHead.localPosition=pressHome;exitGate.localPosition=gateHome;
            foreach(var door in GetComponentsInChildren<FactoryDoor>(true))door.SetOpen(false,true);
            Toast("Заказ на перфокарте у вашего рабочего места.",9);
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(MenuActive || CaptureActive) return;
            if(Ended)
            {
                if((State==FactoryState.Dead || State==FactoryState.Escaped) && keyboard!=null && keyboard.enterKey.wasPressedThisFrame) Restart();
                return;
            }
            if(!CardboardRunner.Player) return;
            float dt=Time.deltaTime;TickShiftClock(dt);if(Ended || CaptureActive) return;
            if(lunch) lunch.TickSchedule(dt);
            Pretending=keyboard!=null && keyboard.cKey.isPressed && !CardboardMarker.DrawingMode && !PlayerBlocked;
            UpdateFocus();
            if(keyboard!=null)
            {
                if(keyboard.eKey.wasPressedThisFrame && !PlayerBlocked && !CardboardMarker.DrawingMode) Interact();
                if(keyboard.qKey.wasPressedThisFrame && !PlayerBlocked)
                {if(instruction && instruction.Held) instruction.PutDown();else if(HasPart) DropPart();}
            }
            if(ReadyToLeave && !exitDoor) exitGate.localPosition=Vector3.Lerp(exitGate.localPosition,gateHome+Vector3.right*2.5f,dt*2);
            bool watched=IsWatched();
            ObserveConduct(watched,CardboardRunner.Player.speed,CardboardRunner.Player.JumpHeight,station && station.IsPumpingPlayer,dt);
        }
        public bool IsWatched()
        {
            if(!CardboardRunner.Player) return false;
            Vector3 eyes=CardboardRunner.Player.transform.position+Vector3.up*1.5f;
            foreach(var worker in crowd.runners)
            {
                if(worker.isPlayer || Vector3.Distance(worker.transform.position,eyes)>5.5f) continue;
                if(!Physics.Linecast(worker.transform.position+Vector3.up*1.7f,eyes,1<<8,QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
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
        public bool RegisterMistake(string reason,float stars=1f)
        {
            if(MenuActive || Ended || CaptureActive || Mistakes>=MistakeLimit || stars<=0 || float.IsNaN(stars) || float.IsInfinity(stars)) return false;
            LastMistakeReason=string.IsNullOrWhiteSpace(reason)?"Нарушение правил смены.":reason;
            mistakeReasons.Add(LastMistakeReason);Mistakes=Mathf.Min(MistakeLimit,Mistakes+stars);
            Suspicion=100f*Mistakes/MistakeLimit;Disguise=100-Mistakes*25;
            Toast(LastMistakeReason,7);
            if(sound) sound.Play("wrong");
            if(Mistakes>=MistakeLimit) Capture(FactoryDeath.Press);
            return true;
        }
        public void ObserveConduct(bool watched,float speed,float jump,bool pumping,float dt)
        {
            if(Ended) return;
            if(watched && speed>4.1f)
            {
                runSeen+=Mathf.Max(0,dt);runGrace=0;
                if(!runCharged && runSeen>=.75f)
                { runCharged=true;RegisterMistake("Рабочие заметили бег.",.5f); }
            }
            else
            {
                runGrace+=Mathf.Max(0,dt);
                if(runGrace>=.5f) {runSeen=0;runCharged=false;}
            }
            if(watched && jump>.3f && jumpWatch<=.3f && !pumping)
                RegisterMistake("Рабочие заметили прыжок вне панели насоса.",.5f);
            jumpWatch=jump;
        }
        public void TickShiftClock(float dt)
        {
            if(MenuActive || Ended) return;
            ShiftRemaining=Mathf.Max(0,ShiftRemaining-Mathf.Max(0,dt));
        }
        // Compatibility for the preserved legacy machine's discrete jam event.
        public void AddSuspicion(float amount)
        { if(amount>0) RegisterMistake("Нарушение работы станка."); }
        void UpdateFocus()
        {
            Focus=null;var cam=Camera.main;if(!cam || PlayerBlocked || CardboardMarker.DrawingMode) return;
            Physics.SyncTransforms();
            Focus=TargetForRay(cam.ViewportPointToRay(new Vector3(.5f,.5f)));
        }
        public FactoryInteractable TargetForRay(Ray ray)
        {
            if(Physics.Raycast(ray,out var hit,3.5f,~(1<<9),QueryTriggerInteraction.Ignore))
            {
                var target=hit.collider.GetComponentInParent<FactoryInteractable>();
                return HandsBusy && target && target.action!=FactoryAction.Lunch && target.action!=FactoryAction.Door?null:target;
            }
            return null;
        }
        public string FocusPrompt
        {
            get
            {
                if(!Focus) return "";
                if(Focus.action==FactoryAction.Lunch)
                {
                    var lunchControl=Focus.GetComponent<FactoryLunchControl>();
                    return lunchControl?lunchControl.Prompt:"";
                }
                if(Focus.action==FactoryAction.Door) return Focus.GetComponentInParent<FactoryDoor>().Prompt;
                if(Focus.action==FactoryAction.Bench) return "";
                if(Focus.action==FactoryAction.Gear) return Focus.gear.broken && HasPart?"E — установить деталь":"";
                if(Focus.action==FactoryAction.Exit) return ReadyToLeave?"E — закончить смену и уйти":"Выход после починки всех 3 шестерёнок";
                return Focus.prompt;
            }
        }
        public void Interact()
        {
            if(instruction && instruction.Held){instruction.Flip();return;}
            if(!Focus || Ended) return;
            if(HandsBusy && Focus.action!=FactoryAction.Lunch && Focus.action!=FactoryAction.Door) return;
            switch(Focus.action)
            {
                case FactoryAction.Door:
                    var door=Focus.GetComponentInParent<FactoryDoor>();if(door)door.Toggle();break;
                case FactoryAction.Instruction:
                    if(instruction) instruction.Take();break;
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
        public bool TryExit()
        {
            if(CaptureActive || HandsBusy) return false;
            if(!ReadyToLeave || Ended) { Toast("Сначала почините все 3 шестерёнки главного механизма.");return false; }
            State=FactoryState.Escaped;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(sound) sound.Play("good");return true;
        }
        public void ReadBlueprint(FactoryGear gear)
        {
            if(Ended || CurrentOrder==0) return;
            if(station) station.PrintOrder(CurrentOrder);
        }
        void SetOrder(int number)
        {
            CurrentOrder=number;
            Memorized=number>=1 && number<=gears.Length?gears[number-1]:null;
            if(station) station.PrintOrder(number);
        }
        public void ReceiveWorkbenchPart(FactoryRecipe recipe)
        {
            HeldRecipe=recipe;HasPart=true;
            if(carried){carried.SetActive(false);Destroy(carried);carried=null;}
            carried=new GameObject("Assembled cardboard gear in left hand");
            carried.transform.SetParent(Camera.main.transform,false);
            carried.transform.localPosition=new Vector3(-.28f,-.28f,.64f);
            carried.transform.localRotation=Quaternion.Euler(12,10,0);carried.transform.localScale=Vector3.one*.24f;
            FactoryGeometry.GearVisual(carried.transform,HeldRecipe,gearMeshes,hubMeshes,gearPaper,gearEdge,gearInk);
            Toast("Деталь снята со станка: "+HeldRecipe+". Найдите подходящий узел.",6);if(sound) sound.Play("good");
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
                string reason=gear.index+1!=CurrentOrder?"Попытка установить шестерёнку не в узел текущего заказа.":
                    HeldRecipe.Type!=gear.recipe.Type?"Установлен неверный тип шестерёнки.":
                    HeldRecipe.finish==0?"Попытка установить шестерёнку без обработки в жидкости.":
                    "Шестерёнка обработана в неверной жидкости: "+HeldRecipe.finish+" вместо "+gear.recipe.finish+".";
                RegisterMistake(reason);return false;
            }
            gear.SetBroken(false);HasPart=false;if(carried){carried.SetActive(false);Destroy(carried);carried=null;}
            Repairs++;
            SetOrder(Repairs<gears.Length?Repairs+1:0);
            Toast(ReadyToLeave?"Наряд выполнен. Все механизмы работают. Идите к выходу!":"Узел "+(char)('A'+gear.index)+" запущен. Ремонтов: "+Repairs+" / "+gears.Length+".",6);
            if(sound) sound.Play("good");return true;
        }
        public void DropPart() { HasPart=false;if(carried){carried.SetActive(false);Destroy(carried);carried=null;}Toast("Неподходящая деталь отправлена в обрезки."); }
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
