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
        public FactoryWorkstation[] stations;
        public int CurrentOrder {get;private set;}
        public Material[] finishMaterials;
        public bool DippingPart {get;private set;}
        public FactoryState State {get;private set;}
        public FactoryDeath Death {get;private set;}
        public float Suspicion {get;private set;}
        public float Disguise {get;private set;}
        public int Repairs {get;private set;}
        public int Mistakes {get;private set;}
        public float ShiftRemaining {get;private set;}
        public bool HasPart {get;private set;}
        public FactoryRecipe HeldRecipe {get;private set;}
        public FactoryGear Memorized {get;private set;}
        public FactoryInteractable Focus {get;private set;}
        public string Message {get;private set;}
        public float MessageUntil {get;private set;}
        public bool Inspection {get;private set;}
        public float InspectionRemaining {get;private set;}
        public float InspectionProgress {get;private set;}
        public bool Pretending {get;private set;}
        public bool Ended => State==FactoryState.Captured || State==FactoryState.Dead || State==FactoryState.Escaped;
        public bool PlayerBlocked => Ended || DippingPart || (workbench && workbench.ConsumesMouse) || (station && station.ConsumesMouse);
        public bool CameraOverride => State==FactoryState.Captured || State==FactoryState.Dead;
        public Vector3 CameraPosition {get;private set;}
        public Quaternion CameraRotation {get;private set;}
        public bool ReadyToLeave => gears.Length>0 && WorkingGears==gears.Length;
        public int WorkingGears {get { int count=0;foreach(var gear in gears) if(!gear.broken) count++;return count; } }
        float inspectionAt,capturedAt,lastAlarm,jumpWatch;
        int inspectionWorker=1;
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
            State=FactoryState.Working;Suspicion=0;Disguise=100;Repairs=Mistakes=0;ShiftRemaining=240;
            HasPart=false;Inspection=false;Pretending=false;Memorized=null;
            DippingPart=false;
            if(workbench) workbench.ResetMachine();
            if(station) station.ResetMachine();
            inspectionAt=Time.time+35;jumpWatch=0;
            if(carried) Destroy(carried);
            foreach(var gear in gears) gear.SetBroken(true);
            SetOrder(1);
            victim.gameObject.SetActive(false);buttons.gameObject.SetActive(false);pins.gameObject.SetActive(false);
            pressHead.localPosition=pressHome;exitGate.localPosition=gateHome;
            Toast("Заказ на перфокарте у вашего рабочего места.",9);
        }
        void Update()
        {
            var keyboard=Keyboard.current;
            if(Ended)
            {
                if(State==FactoryState.Captured) AnimateCapture();
                if((State==FactoryState.Dead || State==FactoryState.Escaped) && keyboard!=null && keyboard.enterKey.wasPressedThisFrame) Restart();
                return;
            }
            if(!CardboardRunner.Player) return;
            float dt=Time.deltaTime;ShiftRemaining=Mathf.Max(0,ShiftRemaining-dt);
            Pretending=keyboard!=null && keyboard.cKey.isPressed && !CardboardMarker.DrawingMode && !PlayerBlocked;
            UpdateFocus();
            if(keyboard!=null)
            {
                if(keyboard.eKey.wasPressedThisFrame && !PlayerBlocked && !CardboardMarker.DrawingMode) Interact();
                if(keyboard.qKey.wasPressedThisFrame && HasPart && !PlayerBlocked) DropPart();
            }
            if(ReadyToLeave) exitGate.localPosition=Vector3.Lerp(exitGate.localPosition,gateHome+Vector3.right*2.5f,dt*2);
            if(State==FactoryState.Chased)
            {
                if(Time.time>lastAlarm+2) { lastAlarm=Time.time;if(sound) sound.Play("alarm"); }
                foreach(var worker in crowd.runners)
                {
                    if(worker.isPlayer) continue;
                    if(Vector3.Distance(worker.transform.position,CardboardRunner.Player.transform.position)<.85f)
                    { Capture((FactoryDeath)(worker.personality%3));break; }
                }
                return;
            }
            bool watched=IsWatched();
            if(watched && CardboardRunner.Player.speed>4.1f)
            { Disguise=Mathf.Max(0,Disguise-dt*5);AddSuspicion(dt*2.3f); }
            float jump=CardboardRunner.Player.JumpHeight;
            if(watched && jump>.3f && jumpWatch<=.3f && !(station && station.IsPumpingPlayer)) { Disguise=Mathf.Max(0,Disguise-4);AddSuspicion(5);Toast("Рабочие заметили слишком живой прыжок."); }
            jumpWatch=jump;
            if(watched && Disguise<30) AddSuspicion(dt*5);
            if(Pretending && CardboardRunner.Player.speed<1.8f && !Inspection) Suspicion=Mathf.Max(0,Suspicion-dt*.8f);
            if(!watched && !Inspection) Suspicion=Mathf.Max(0,Suspicion-dt*.35f);
            if(!Inspection && Time.time>=inspectionAt && watched) StartInspection();
            if(Inspection)
            {
                InspectionRemaining-=dt;
                if(Pretending && CardboardRunner.Player.speed<.25f && Disguise>=35) InspectionProgress+=dt;
                else InspectionProgress=Mathf.Max(0,InspectionProgress-dt*2);
                if(InspectionProgress>=2)
                {
                    Inspection=false;Suspicion=Mathf.Max(0,Suspicion-15);inspectionAt=Time.time+38;
                    Toast("Проверка пройдена. «Нормальная картонка. Работай». ",5);if(sound) sound.Play("good");
                }
                else if(InspectionRemaining<=0)
                { Inspection=false;inspectionAt=Time.time+30;AddSuspicion(32);Disguise=Mathf.Max(0,Disguise-12);Toast("Проверяющий вам не поверил."); }
            }
            if(ShiftRemaining<=0 && !ReadyToLeave) { Toast("Смена провалена. Аварийная проверка: ЧУЖОЙ В ЦЕХУ!",6);AddSuspicion(100); }
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
        public void StartInspection()
        {
            if(DippingPart) { inspectionAt=Time.time+2;return; }
            Inspection=true;InspectionRemaining=7;InspectionProgress=0;if(workbench) workbench.EndGrip();if(station) station.EndGrip();
            if(CardboardMarker.Instance) CardboardMarker.Instance.SetDrawingMode(false);
            Toast("Проверка! Зажмите C и замрите на 2 секунды. Не показывайте, что вы человек.",7);
            if(sound) sound.Play("alarm");
        }
        public Vector3 WorkerTarget(CardboardRunner worker,out float speed)
        {
            if(State==FactoryState.Chased)
            { speed=4.6f;return CardboardRunner.Player.transform.position; }
            if(Inspection && worker.personality==inspectionWorker)
            { speed=2;return CardboardRunner.Player.transform.position+Vector3.forward*1.65f; }
            if(worker.personality>=2 && worker.personality<=4 && workerPosts!=null && workerPosts.Length==3)
            { speed=1.2f;return workerPosts[worker.personality-2]; }
            speed=1.15f+(worker.personality%3)*.18f;
            int point=(Mathf.FloorToInt(Time.time/(7+worker.personality%3))+worker.personality)%patrol.Length;
            return patrol[point];
        }
        public void AddSuspicion(float amount)
        {
            if(Ended) return;
            Suspicion=Mathf.Clamp(Suspicion+amount,0,100);
            if(Suspicion>=100 && State!=FactoryState.Chased)
            {
                State=FactoryState.Chased;Inspection=false;if(workbench) workbench.EndGrip();
                if(station) station.EndGrip();
                CancelDip();
                if(CardboardMarker.Instance) CardboardMarker.Instance.SetDrawingMode(false);
                Toast("ВАС РАСКРЫЛИ. Картонные рабочие идут за вами!",8);
                if(sound) sound.Play("alarm");
            }
        }
        void UpdateFocus()
        {
            Focus=null;var cam=Camera.main;if(!cam || PlayerBlocked || CardboardMarker.DrawingMode) return;
            Physics.SyncTransforms();
            Focus=TargetForRay(cam.ViewportPointToRay(new Vector3(.5f,.5f)));
        }
        public FactoryInteractable TargetForRay(Ray ray)
        {
            if(Physics.Raycast(ray,out var hit,3.5f,~(1<<9),QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<FactoryInteractable>();
            return null;
        }
        public string FocusPrompt
        {
            get
            {
                if(!Focus) return "";
                if(Focus.action==FactoryAction.Bench) return "";
                if(Focus.action==FactoryAction.Gear) return Focus.gear.broken && HasPart?"E — установить деталь":"";
                if(Focus.action==FactoryAction.Exit) return ReadyToLeave?"E — закончить смену и уйти":"Выход после починки всех 3 шестерёнок";
                return Focus.prompt;
            }
        }
        public void Interact()
        {
            if(!Focus || Ended) return;
            switch(Focus.action)
            {
                case FactoryAction.Gear:
                    if(HasPart && Focus.gear.broken) TryInstall(Focus.gear);else ReadBlueprint(Focus.gear);break;
                case FactoryAction.Blueprint: ReadBlueprint(Focus.gear);break;
                case FactoryAction.Bench:
                    var control=Focus.GetComponent<WorkbenchControl>();if(control) control.Use();break;
                case FactoryAction.Station:
                    var stationControl=Focus.GetComponent<FactoryStationControl>();if(stationControl) stationControl.Use();break;
                case FactoryAction.Exit:
                    TryExit();break;
            }
        }
        public bool TryExit()
        {
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
            if(carried) Destroy(carried);
            carried=new GameObject("Assembled cardboard gear in left hand");
            carried.transform.SetParent(Camera.main.transform,false);
            carried.transform.localPosition=new Vector3(-.28f,-.28f,.64f);
            carried.transform.localRotation=Quaternion.Euler(12,10,0);carried.transform.localScale=Vector3.one*.24f;
            FactoryGeometry.GearVisual(carried.transform,HeldRecipe,gearMeshes,hubMeshes,gearPaper,gearEdge,gearInk);
            Toast("Деталь снята со станка: "+HeldRecipe+". Найдите подходящий узел.",6);if(sound) sound.Play("good");
        }
        public bool BeginDip()
        {
            if(!HasPart || HeldRecipe.finish!=0 || DippingPart || Ended) return false;
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
            if(!HasPart || !gear || !gear.broken || Ended) return false;
            if(gear.index+1!=CurrentOrder || !HeldRecipe.Equals(gear.recipe))
            {
                Mistakes++;Disguise=Mathf.Max(0,Disguise-8);AddSuspicion(28);
                Toast("ОШИБКА. Узлу "+(char)('A'+gear.index)+" нужна другая деталь: "+gear.recipe,7);
                if(sound) sound.Play("wrong");return false;
            }
            gear.SetBroken(false);HasPart=false;if(carried) Destroy(carried);
            Repairs++;Suspicion=Mathf.Max(0,Suspicion-9);Disguise=Mathf.Min(100,Disguise+3);
            SetOrder(Repairs<gears.Length?Repairs+1:0);
            Toast(ReadyToLeave?"Наряд выполнен. Все механизмы работают. Идите к выходу!":"Узел "+(char)('A'+gear.index)+" запущен. Ремонтов: "+Repairs+" / "+gears.Length+".",6);
            if(sound) sound.Play("good");return true;
        }
        public void DropPart() { HasPart=false;if(carried) Destroy(carried);Toast("Неподходящая деталь отправлена в обрезки."); }
        public void Toast(string message,float seconds=4) { Message=message;MessageUntil=Time.time+seconds; }
        public void Capture(FactoryDeath death)
        {
            if(Ended) return;
            State=FactoryState.Captured;Death=death;capturedAt=Time.time;Inspection=false;if(workbench) workbench.EndGrip(false);
            if(station) station.EndGrip(false);CancelDip();
            if(CardboardMarker.Instance) CardboardMarker.Instance.SetDrawingMode(false,false);
            if(carried) carried.SetActive(false);
            victim.gameObject.SetActive(true);buttons.gameObject.SetActive(death==FactoryDeath.Buttons);pins.gameObject.SetActive(death==FactoryDeath.Pins);
            victim.localScale=Vector3.one;
            Vector3 stageOffset=captureStage?captureStage.position:Vector3.zero;
            if(death==FactoryDeath.Press)
            {
                victim.position=stageOffset+new Vector3(9,.42f,-2.4f);victim.rotation=Quaternion.Euler(-90,0,0);
                CameraPosition=stageOffset+new Vector3(9,2.5f,.6f);CameraRotation=Quaternion.LookRotation(stageOffset+new Vector3(9,1,-3.5f)-CameraPosition);
            }
            else
            {
                victim.position=stageOffset+new Vector3(9,.15f,3.3f);victim.rotation=Quaternion.identity;
                CameraPosition=stageOffset+new Vector3(9,1.5f,6.4f);CameraRotation=Quaternion.LookRotation(stageOffset+new Vector3(9,1.2f,3.3f)-CameraPosition);
            }
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;if(sound) sound.Play("impact");
        }
        void AnimateCapture()
        {
            float t=Time.time-capturedAt;
            if(Death==FactoryDeath.Press)
            {
                pressHead.localPosition=Vector3.Lerp(pressHome,new Vector3(pressHome.x,.61f,pressHome.z),Mathf.SmoothStep(0,1,t/2.8f));
                victim.localScale=new Vector3(1,1,Mathf.Lerp(1,.10f,Mathf.Clamp01((t-2.4f)*3)));
            }
            else
            {
                Transform group=Death==FactoryDeath.Pins?pins:buttons;
                for(int i=0;i<group.childCount;i++)
                {
                    var point=group.GetChild(i);Vector3 p=point.localPosition;
                    float progress=Mathf.SmoothStep(0,1,Mathf.Clamp01((t-i*.19f)*3));
                    p.z=Mathf.Lerp(1.9f,-.08f,progress);point.localPosition=p;
                }
            }
            if(t>4.5f) State=FactoryState.Dead;
        }
        public void Restart() { SceneManager.LoadScene(SceneManager.GetActiveScene().path); }
        void OnDestroy() { if(Instance==this) Instance=null; }
    }
}
