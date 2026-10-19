using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Cardboard
{
    public enum IntroPhase { Dormant,Escort,Push,Tutorial,PrinterDelay,PrinterFocus,Complete }
    [DefaultExecutionOrder(35)]
    public class FactoryIntro : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public Material glow;
        public Text hint;
        public IntroPhase Phase {get;private set;}
        public bool Active=>Phase!=IntroPhase.Dormant && Phase!=IntroPhase.Complete;
        public bool Angry=>Phase==IntroPhase.Escort || Phase==IntroPhase.Push;
        public bool BlocksControls=>Angry || Phase==IntroPhase.PrinterFocus;
        public bool AllowPrinter=>!Active || Phase==IntroPhase.PrinterFocus;
        public Vector3 CameraPosition {get;private set;}
        public Quaternion CameraRotation {get;private set;}
        public FactoryGear TutorialTarget {get;private set;}
        public FactoryPhysicsProp FloorGear {get;private set;}
        CardboardRunner[] guards;Vector3[] homes;Quaternion[] rotations;
        LineRenderer floorOutline;float elapsed;Vector3 landing,focusHome,focusShow;Quaternion focusRotation;
        public bool ControlsWorker(CardboardRunner worker)=>Angry && guards!=null && System.Array.IndexOf(guards,worker)>=0;
        static void Place(CardboardRunner actor,Vector3 position,Quaternion rotation)
        {
            var controller=actor.GetComponent<CharacterController>();bool enabled=controller && controller.enabled;
            if(enabled)controller.enabled=false;actor.transform.SetPositionAndRotation(position,rotation);if(enabled)controller.enabled=true;
        }
        public void Begin()
        {
            Cancel();Phase=IntroPhase.Escort;elapsed=0;
            guards=game.crowd.runners.Where(w=>!w.isPlayer).Take(4).ToArray();homes=guards.Select(w=>w.transform.position).ToArray();rotations=guards.Select(w=>w.transform.rotation).ToArray();
            TutorialTarget=game.gears.Where(g=>g.transform.position.y<1.9f).OrderBy(g=>Mathf.Abs(g.transform.position.x-(CardboardFactoryWorld.WorkshopEast-3))).First();
            game.PrepareTutorial(TutorialTarget);game.exitDoor.SetOpen(true,true);
            Place(CardboardRunner.Player,new Vector3(CardboardFactoryWorld.WorkshopEast+11.5f,0,-2),Quaternion.Euler(0,270,0));
            CameraPosition=CardboardRunner.Player.transform.position+Vector3.up*1.62f;CameraRotation=Quaternion.Euler(0,270,0);
            Camera.main.fieldOfView=CardboardCamera.GameplayFov;Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            Tick(0);
        }
        void LateUpdate(){if(Angry && guards!=null)for(int i=0;i<guards.Length;i++)guards[i].Pose(elapsed*6+i,1);}
        void Update(){if(game.CaptureActive && Active){Cancel();return;}if(Active && !game.MenuActive)Tick(Time.deltaTime);else if(hint && !Active)hint.text="";}
        public void Tick(float dt)
        {
            elapsed+=Mathf.Max(0,dt);
            if(Angry)
            {
                float t=Phase==IntroPhase.Escort?Mathf.Clamp01(elapsed/7):Mathf.Clamp01(elapsed/1.05f);
                float x=Phase==IntroPhase.Escort?Mathf.Lerp(CardboardFactoryWorld.WorkshopEast+11.5f,CardboardFactoryWorld.WorkshopEast+.7f,t):Mathf.Lerp(CardboardFactoryWorld.WorkshopEast+.7f,landing.x,1-Mathf.Pow(1-t,2));
                var player=new Vector3(x,0,Phase==IntroPhase.Push?Mathf.Lerp(-2,landing.z,t):-2);Place(CardboardRunner.Player,player,Quaternion.Euler(0,270,0));
                for(int i=0;i<guards.Length;i++)
                {
                    var position=player+new Vector3(1.15f+(i/2)*1.15f,0,(i%2==0?-.7f:.7f));
                    Place(guards[i],position,Quaternion.Euler(0,270,0));guards[i].Pose(elapsed*6+i,1);
                }
                CameraPosition=player+Vector3.up*(1.62f+Mathf.Sin(elapsed*10)*.018f);
                float turn=Phase==IntroPhase.Escort?Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-1)/1.3f))*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-4.8f)/1.7f))):0;
                CameraRotation=Quaternion.Slerp(Quaternion.Euler(0,270,0),Quaternion.Euler(0,90,0),turn)*Quaternion.Euler(Phase==IntroPhase.Push?Mathf.Sin(t*Mathf.PI)*8:0,0,Mathf.Sin(elapsed*8)*(Phase==IntroPhase.Push?2:.3f));
                if(hint)hint.text=Phase==IntroPhase.Escort?"KEEP MOVING.":"GET TO WORK.";
                if(Phase==IntroPhase.Escort && t>=1){landing=ChooseLanding();Phase=IntroPhase.Push;elapsed=0;}
                else if(Phase==IntroPhase.Push && t>=1)
                {
                    RestoreGuards();game.exitDoor.SetOpen(false);SpawnGear();Phase=IntroPhase.Tutorial;elapsed=0;
                    Camera.main.transform.SetPositionAndRotation(CameraPosition,Quaternion.LookRotation(FloorGear.transform.position-CameraPosition));Camera.main.GetComponent<CardboardCamera>().AdoptCurrentView();
                }
            }
            else if(Phase==IntroPhase.Tutorial)
            {
                if(hint)hint.text=game.HasPart?"INSTALL THE GEAR IN THE DAMAGED MECHANISM.  [E]":"PICK UP THE HIGHLIGHTED GEAR.  [E]  THEN PUT IT BACK IN THE MECHANISM.";
                if(floorOutline)floorOutline.enabled=FloorGear && FloorGear.gameObject.activeInHierarchy;
                if(!game.HasPart && !FloorGear)
                {
                    var dropped=game.GetComponentsInChildren<FactoryPhysicsProp>().FirstOrDefault(p=>p.workbenchPart && p.recipe.Equals(TutorialTarget.recipe));
                    if(dropped)FloorGear=dropped;else SpawnGear();
                }
            }
            else if(Phase==IntroPhase.PrinterDelay && elapsed>=2)
            {
                Phase=IntroPhase.PrinterFocus;elapsed=0;focusHome=Camera.main.transform.position;focusRotation=Camera.main.transform.rotation;
                Vector3 printer=game.station.orderCard.parent.position+Vector3.up*.15f;var away=focusHome-printer;away.y=0;if(away.sqrMagnitude<.01f)away=-game.station.transform.forward;focusShow=printer+away.normalized*3+Vector3.up*1.2f;
                game.StartShiftAfterTutorial(TutorialTarget);if(hint)hint.text="YOUR NEXT ORDER IS PRINTING AT YOUR WORKSTATION.";
            }
            else if(Phase==IntroPhase.PrinterFocus)
            {
                float turn=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.9f));float shake=Mathf.Sin(Mathf.PI*Mathf.Clamp01(elapsed/3.2f));
                Vector3 target=game.station.orderCard.parent.position+Vector3.up*.15f;
                float dolly=turn*(1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-2.5f)/.7f)));
                CameraPosition=Vector3.Lerp(focusHome,focusShow,dolly)+new Vector3(Mathf.Sin(elapsed*43),Mathf.Sin(elapsed*37),0)*(.008f*shake);
                CameraRotation=Quaternion.Slerp(focusRotation,Quaternion.LookRotation(target-CameraPosition),turn)*Quaternion.Euler(Mathf.Sin(elapsed*39)*.15f*shake,0,Mathf.Sin(elapsed*31)*.3f*shake);
                float zoom=elapsed<2.5f?Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/1.2f)):1-Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-2.5f)/.7f));
                Camera.main.fieldOfView=Mathf.Lerp(CardboardCamera.GameplayFov,34,zoom);
                if(elapsed>=3.2f){Phase=IntroPhase.Complete;Camera.main.transform.SetPositionAndRotation(CameraPosition,CameraRotation);Camera.main.GetComponent<CardboardCamera>().AdoptCurrentView();Camera.main.fieldOfView=CardboardCamera.GameplayFov;if(hint)hint.text="";}
            }
        }
        public bool OnRepair(FactoryGear gear)
        {
            if(Phase!=IntroPhase.Tutorial || gear!=TutorialTarget)return false;
            if(FloorGear){FloorGear.gameObject.SetActive(false);Destroy(FloorGear.gameObject);}FloorGear=null;Phase=IntroPhase.PrinterDelay;elapsed=0;if(hint)hint.text="GOOD. WAIT FOR YOUR NEXT ORDER.";return true;
        }
        Vector3 ChooseLanding()
        {
            Physics.SyncTransforms();
            foreach(float x in new[]{3.2f,4.2f,2.2f,5.2f})foreach(float z in new[]{-2f,-1f,-3f})
            {
                var point=new Vector3(CardboardFactoryWorld.WorkshopEast-x,0,z);
                var overlaps=Physics.OverlapCapsule(point+Vector3.up*.5f,point+Vector3.up*1.85f,.43f,~0,QueryTriggerInteraction.Ignore);
                if(!overlaps.Any(c=>c.GetComponentInParent<CardboardRunner>()!=CardboardRunner.Player) && (homes==null || !homes.Any(h=>(h-point).sqrMagnitude<1.3f*1.3f)))return point;
            }
            return new Vector3(CardboardFactoryWorld.WorkshopEast-4.2f,0,-1);
        }
        void SpawnGear()
        {
            if(FloorGear)return;
            var root=new GameObject("Tutorial gear • pick up and install");root.transform.SetParent(game.transform,false);root.transform.localScale=Vector3.one*.30f;
            root.transform.rotation=Quaternion.Euler(-90,0,0);root.transform.position=landing+new Vector3(-1.05f,.15f,.1f);
            FactoryGeometry.GearVisual(root.transform,TutorialTarget.recipe,game.gearMeshes,game.hubMeshes,game.gearPaper,game.gearEdge,game.gearInk);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){var shape=filter.gameObject.AddComponent<MeshCollider>();shape.sharedMesh=filter.sharedMesh;shape.convex=true;}
            root.layer=8;var body=root.AddComponent<Rigidbody>();body.mass=.3f;
            FloorGear=root.AddComponent<FactoryPhysicsProp>();FloorGear.workbenchPart=true;FloorGear.recipe=TutorialTarget.recipe;FloorGear.itemName="gear";
            var interaction=root.AddComponent<FactoryInteractable>();interaction.action=FactoryAction.PhysicsProp;interaction.prompt="E - PICK UP GEAR";
            var points=TutorialTarget.placementOutline.GetComponent<LineRenderer>();var outline=new GameObject("Tutorial gear glow");outline.transform.SetParent(root.transform,false);outline.layer=9;
            floorOutline=outline.AddComponent<LineRenderer>();floorOutline.useWorldSpace=false;floorOutline.loop=true;floorOutline.widthMultiplier=.02f;floorOutline.sharedMaterial=glow;
            floorOutline.positionCount=points.positionCount;var vertices=new Vector3[points.positionCount];points.GetPositions(vertices);floorOutline.SetPositions(vertices);
        }
        void RestoreGuards(){if(guards==null || homes==null)return;for(int i=0;i<guards.Length;i++)if(guards[i])Place(guards[i],homes[i],rotations[i]);guards=null;homes=null;}
        public void Cancel()
        {
            RestoreGuards();if(FloorGear){FloorGear.gameObject.SetActive(false);Destroy(FloorGear.gameObject);}FloorGear=null;floorOutline=null;Phase=IntroPhase.Dormant;elapsed=0;
            if(hint)hint.text="";if(Camera.main)Camera.main.fieldOfView=CardboardCamera.GameplayFov;
        }
    }
}
