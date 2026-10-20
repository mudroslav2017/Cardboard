using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cardboard
{
    public enum CapturePhase { None, Approaching, Lifting, Carrying, Placing, Lever, Crushing, Grade, Restarting }

    [DefaultExecutionOrder(40)]
    public class FactoryCaptureSequence : MonoBehaviour
    {
        public CardboardFactoryGame game;
        public FactoryDeathGrade grade;
        public CardboardRunner[] workers;
        public bool AutomaticRestart=true;
        public CapturePhase Phase {get;private set;}
        public float PhaseTime {get;private set;}
        public bool RestartRequested {get;private set;}
        public bool Active => Phase!=CapturePhase.None;
        public bool ControlsWorkers => Phase>=CapturePhase.Lifting;
        public bool CameraControlled => ControlsWorkers;
        public Vector3 CameraPosition {get;private set;}
        public Quaternion CameraRotation {get;private set;}
        public Vector3 BedPosition => game.station.transform.TransformPoint(new Vector3(-.08f,1.05f,.12f));
        public int CarrierLeft {get;private set;}
        public int CarrierRight {get;private set;}
        public int LeverWorker {get;private set;}
        readonly Dictionary<int,Route> routes=new Dictionary<int,Route>();
        FactoryCaptureNavigation navigation;
        List<Vector3> carryRoute;
        Vector3[] homes;Quaternion[] rotations;bool[] controllers;
        Vector3 playerHome,grabPoint,carryPoint,carryForward,headStart;
        Quaternion playerRotation,headRotation;
        int routeIndex;
        float gait;
        bool leverSound,pinSound;
        sealed class Route { public List<Vector3> points;public int index;public Vector3 goal;public float until; }
        public static FactoryCaptureSequence Create(Transform root,CardboardFactoryGame game,FactoryDeathGrade grade)
        {
            var sequence=CardboardArt.Group("First-person capture • workers carry player to station",root,Vector3.zero).gameObject.AddComponent<FactoryCaptureSequence>();
            sequence.game=game;sequence.grade=grade;
            var workers=new List<CardboardRunner>();foreach(var worker in game.crowd.runners)if(!worker.isPlayer)workers.Add(worker);
            sequence.workers=workers.ToArray();
            return sequence;
        }
        public void Begin()
        {
            if(Active || !CardboardRunner.Player) return;
            homes=new Vector3[workers.Length];rotations=new Quaternion[workers.Length];controllers=new bool[workers.Length];
            for(int i=0;i<workers.Length;i++)
            {
                homes[i]=workers[i].transform.position;rotations[i]=workers[i].transform.rotation;
                controllers[i]=workers[i].GetComponent<CharacterController>().enabled;
            }
            playerHome=CardboardRunner.Player.transform.position;playerRotation=CardboardRunner.Player.transform.rotation;
            headStart=Camera.main.transform.position;headRotation=Camera.main.transform.rotation;
            CameraPosition=headStart;CameraRotation=headRotation;
            navigation=new FactoryCaptureNavigation();routes.Clear();gait=0;RestartRequested=leverSound=pinSound=false;
            grade.Hide();Change(CapturePhase.Approaching);
        }
        public Vector3 WorkerWaypoint(CardboardRunner worker) => Waypoint(worker,CardboardRunner.Player.transform.position);
        Vector3 Waypoint(CardboardRunner worker,Vector3 target)
        {
            if(navigation==null) return target;
            if(!routes.TryGetValue(worker.personality,out var route))
            {route=new Route();routes[worker.personality]=route;}
            if(route.points==null || (Time.time>=route.until && ((route.goal-target).sqrMagnitude>.2f || route.index>=route.points.Count)))
            {
                route.points=navigation.Path(worker.transform.position,target);route.index=0;route.goal=target;
                route.until=Time.time+.45f+worker.personality*.012f;
            }
            if(route.points.Count==0)return target;
            while(route.index<route.points.Count-1 && FlatDistance(worker.transform.position,route.points[route.index])<.36f)route.index++;
            return route.points[Mathf.Min(route.index,route.points.Count-1)];
        }
        void LateUpdate() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            if(!Active || game.MenuActive) return;
            dt=Mathf.Max(0,dt);PhaseTime+=dt;gait+=dt;
            if(Phase==CapturePhase.Approaching)
            {
                foreach(var worker in workers)
                {
                    if(FlatDistance(worker.transform.position,CardboardRunner.Player.transform.position)>1.12f)continue;
                    if(FactoryCaptureNavigation.LineBlocked(worker.transform.position+Vector3.up*1.3f,CardboardRunner.Player.transform.position+Vector3.up*1.3f))continue;
                    Grab();break;
                }
                return;
            }
            if(Phase==CapturePhase.Lifting)
            {
                float t=Mathf.SmoothStep(0,1,PhaseTime/.9f);
                CameraPosition=Vector3.Lerp(headStart,grabPoint+Vector3.up*1.4f,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.12f;
                CameraRotation=Quaternion.Slerp(headRotation,CarryRotation(carryForward),t);
                carryPoint=grabPoint;PoseCrowd(dt);
                if(PhaseTime>=.9f && CarriersReady())
                {
                    carryRoute=new FactoryCaptureNavigation(.78f).Path(grabPoint,game.station.transform.TransformPoint(new Vector3(0,0,-1.8f)));
                    routeIndex=0;Change(CapturePhase.Carrying);
                }
            }
            else if(Phase==CapturePhase.Carrying)
            {
                if(carryRoute.Count>0 && routeIndex<carryRoute.Count)
                {
                    Vector3 target=carryRoute[routeIndex],delta=target-carryPoint;delta.y=0;
                    if(delta.sqrMagnitude>.01f)carryForward=Vector3.Slerp(carryForward,delta.normalized,Mathf.Clamp01(dt*5)).normalized;
                    carryPoint=Vector3.MoveTowards(carryPoint,target,dt*2.5f);
                    if(FlatDistance(carryPoint,target)<.08f)routeIndex++;
                }
                CameraPosition=carryPoint+Vector3.up*(1.4f+Mathf.Sin(gait*10)*.045f)+Vector3.Cross(Vector3.up,carryForward)*Mathf.Sin(gait*5)*.035f;
                CameraRotation=CarryRotation(carryForward)*Quaternion.Euler(Mathf.Sin(gait*7)*3,Mathf.Sin(gait*5)*2,Mathf.Sin(gait*8)*6);
                CardboardRunner.Player.transform.position=carryPoint;PoseCrowd(dt);
                if((carryRoute.Count==0 || routeIndex>=carryRoute.Count) && PhaseTime>=1.2f)
                {headStart=CameraPosition;headRotation=CameraRotation;Change(CapturePhase.Placing);}
            }
            else if(Phase==CapturePhase.Placing)
            {
                float t=Mathf.SmoothStep(0,1,PhaseTime/1.3f);
                CameraPosition=Vector3.Lerp(headStart,BedPosition,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.12f;
                CameraRotation=Quaternion.Slerp(headRotation,CeilingRotation(game.station.transform.forward),t);
                PoseCrowd(dt);
                if(PhaseTime>=1.3f)Change(CapturePhase.Lever);
            }
            else if(Phase==CapturePhase.Lever)
            {
                CameraPosition=BedPosition;
                Vector3 grip=game.station.pressGrip.position;
                Vector3 operatorFace=game.station.transform.TransformPoint(new Vector3(.72f,1.9f,-1.35f));
                Quaternion look=Quaternion.LookRotation(Vector3.Lerp(grip+Vector3.up*.1f,operatorFace,.65f)-BedPosition,Vector3.up);
                float glance=Mathf.Sin(Mathf.Clamp01(PhaseTime/1.65f)*Mathf.PI)*.7f;
                CameraRotation=Quaternion.Slerp(CeilingRotation(game.station.transform.forward),look,glance);
                float pull=Mathf.SmoothStep(0,1,(PhaseTime-.55f)/.65f);
                game.station.SetExecutionPose(pull,0);
                PoseCrowd(dt);PoseLeverWorker();
                if(!leverSound && PhaseTime>=1.0f){leverSound=true;game.PlayCaptureSound("impact");}
                if(PhaseTime>=1.65f)Change(CapturePhase.Crushing);
            }
            else if(Phase==CapturePhase.Crushing)
            {
                CameraPosition=BedPosition;
                CameraRotation=CeilingRotation(game.station.transform.forward)*Quaternion.Euler(Mathf.Sin(PhaseTime*47)*.35f,0,0);
                float crush=Mathf.SmoothStep(0,1,PhaseTime/.95f);
                game.station.SetExecutionPose(1-Mathf.Clamp01(PhaseTime/.7f),crush);
                PoseCrowd(dt);PoseLeverWorker();
                // Cut before the lower face crosses the camera near plane.
                if(crush>=.83f)
                {game.station.SetExecutionPose(0,1);game.SetCaptureState(FactoryState.Dead);game.PlayCaptureSound("impact");Change(CapturePhase.Grade);grade.Show(0);}
            }
            else if(Phase==CapturePhase.Grade)
            {
                grade.Show(PhaseTime);
                if(!pinSound && PhaseTime>=FactoryDeathGrade.PinImpactAt){pinSound=true;game.PlayCaptureSound("impact");}
                if(PhaseTime>=FactoryDeathGrade.RestartAt)
                {RestartRequested=true;Change(CapturePhase.Restarting);if(AutomaticRestart)game.Restart();}
            }
        }
        void Grab()
        {
            grabPoint=CardboardRunner.Player.transform.position;grabPoint.y=0;carryPoint=grabPoint;
            headStart=Camera.main.transform.position;headRotation=Camera.main.transform.rotation;
            CameraPosition=headStart;CameraRotation=headRotation;
            carryForward=game.station.transform.position-grabPoint;carryForward.y=0;
            if(carryForward.sqrMagnitude<.01f)carryForward=Vector3.forward;carryForward.Normalize();
            var nearest=new List<int>();for(int i=0;i<workers.Length;i++)nearest.Add(i);
            nearest.Sort((a,b)=>FlatDistance(workers[a].transform.position,grabPoint).CompareTo(FlatDistance(workers[b].transform.position,grabPoint)));
            CarrierLeft=nearest[0];CarrierRight=nearest[1];LeverWorker=nearest[2];
            foreach(var worker in workers)worker.GetComponent<CharacterController>().enabled=false;
            CardboardRunner.Player.GetComponent<CharacterController>().enabled=false;
            game.SetCaptureState(FactoryState.Captured);game.station.BeginExecution();
            game.PlayCaptureSound("impact");Change(CapturePhase.Lifting);
        }
        bool CarriersReady()
        {
            Vector3 right=Vector3.Cross(Vector3.up,carryForward).normalized;
            return FlatDistance(workers[CarrierLeft].transform.position,carryPoint-right*.68f+carryForward*.85f)<.12f &&
                FlatDistance(workers[CarrierRight].transform.position,carryPoint+right*.68f+carryForward*.85f)<.12f;
        }
        void PoseCrowd(float dt)
        {
            Vector3 right=Vector3.Cross(Vector3.up,carryForward).normalized;
            for(int i=0;i<workers.Length;i++)
            {
                if(i==CarrierLeft || i==CarrierRight)
                {
                    int side=i==CarrierLeft?-1:1;
                    Vector3 spot=carryPoint+right*side*.68f+carryForward*.85f;
                    if(Phase>=CapturePhase.Placing)
                        spot=game.station.transform.TransformPoint(new Vector3(side*.65f,0,-.18f));
                    MoveWorker(i,spot,dt,Phase==CapturePhase.Lifting?5:9,false);
                    // Look at the reclining player's head, rather than the old carrying point.
                    FacePlayer(workers[i],Phase>=CapturePhase.Placing?BedPosition:carryPoint,Phase>=CapturePhase.Lever?1:dt);
                }
                else
                {
                    float angle=i*2.39996f;
                    Vector3 spot=Phase<=CapturePhase.Carrying?
                        carryPoint+new Vector3(Mathf.Cos(angle)*1.8f,0,Mathf.Sin(angle)*1.8f):
                        game.station.transform.TransformPoint(new Vector3(Mathf.Cos(angle)*2.6f,0,-2.4f-Mathf.Abs(Mathf.Sin(angle))*1.4f));
                    if(i==LeverWorker && Phase>=CapturePhase.Placing) spot=game.station.transform.TransformPoint(new Vector3(.72f,0,-1.35f));
                    MoveWorker(i,spot,dt,5.5f,true);
                    if(Phase>=CapturePhase.Placing)FacePlayer(workers[i],BedPosition,Phase>=CapturePhase.Lever?1:dt);
                }
            }
        }
        static void FacePlayer(CardboardRunner worker,Vector3 target,float dt)
        {
            Vector3 direction=target-worker.transform.position;direction.y=0;
            if(direction.sqrMagnitude>.0001f)
                worker.transform.rotation=Quaternion.Slerp(worker.transform.rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-14*dt));
        }
        void PoseLeverWorker()
        {
            var worker=workers[LeverWorker];var station=game.station;
            Vector3 spot=station.transform.TransformPoint(new Vector3(.72f,0,-1.35f));
            worker.transform.position=spot;FacePlayer(worker,BedPosition,1);worker.Pose(gait*5,0);
        }
        void MoveWorker(int index,Vector3 target,float dt,float speed,bool navigate)
        {
            var worker=workers[index];Vector3 before=worker.transform.position;
            Vector3 next=navigate?Waypoint(worker,target):target;
            FactoryDoor.OpenForWorker(before,next-before);
            worker.transform.position=Vector3.MoveTowards(before,next,dt*speed);
            Vector3 direction=worker.transform.position-before;
            if(direction.sqrMagnitude>.0001f)worker.transform.rotation=Quaternion.Slerp(worker.transform.rotation,Quaternion.LookRotation(direction),Mathf.Clamp01(dt*8));
            worker.Pose(gait*9+index,(direction.sqrMagnitude>.00001f && Phase<=CapturePhase.Carrying)?1:0);
        }
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        static Quaternion CarryRotation(Vector3 forward) => Quaternion.LookRotation((forward+Vector3.up).normalized,Vector3.up);
        static Quaternion CeilingRotation(Vector3 forward) => Quaternion.LookRotation(Vector3.up,forward.sqrMagnitude>.01f?forward:Vector3.forward);
        void Change(CapturePhase phase){Phase=phase;PhaseTime=0;}
        public void Cancel()
        {
            if(Active && homes!=null)
            {
                for(int i=0;i<workers.Length;i++)
                {
                    workers[i].transform.SetPositionAndRotation(homes[i],rotations[i]);
                    workers[i].GetComponent<CharacterController>().enabled=controllers[i];
                }
                if(CardboardRunner.Player)
                {
                    var body=CardboardRunner.Player.GetComponent<CharacterController>();body.enabled=false;
                    CardboardRunner.Player.transform.SetPositionAndRotation(playerHome,playerRotation);body.enabled=true;
                }
                if(Camera.main)Camera.main.transform.SetPositionAndRotation(headStart,headRotation);
            }
            Phase=CapturePhase.None;PhaseTime=0;RestartRequested=false;routes.Clear();
            if(grade)grade.Hide();
        }
    }
}
