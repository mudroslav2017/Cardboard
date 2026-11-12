using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cardboard
{
    [DefaultExecutionOrder(-60),DisallowMultipleComponent]
    public class FactoryCircleDance : MonoBehaviour
    {
        [Range(60,200)] public float beatsPerMinute=128;
        public float hopHeight=.45f,turnAngle=55,circleRadius=3.2f,orbitDegreesPerSecond=30;
        public float approachSpeed=3.4f;
        public bool Active {get;private set;}
        public bool SessionOnly {get;private set;}
        public bool InCircle {get;private set;}
        public bool ColumnReady {get;private set;}
        public float ColumnSpacing {get;private set;}
        public float DancingTime=>ColumnReady?Elapsed-danceStarted:0;
        public float Elapsed {get;private set;}
        public Vector3 Center {get;private set;}
        public float Radius {get;private set;}
        public string LastFailure {get;private set;} = "";
        static string obstacle;
        public int GatheredCount=>dancers.Count(d=>Mathf.Abs(Vector3.Distance(d.actor.transform.position,Center)-Radius)<.4f &&
            Vector3.Distance(d.actor.transform.position,MarchPoint(marchDistance-d.index*ColumnSpacing))<.4f);
        public IReadOnlyList<CardboardRunner> Workers=>performers;
        sealed class Dancer
        {
            public CardboardRunner actor;
            public CharacterController body;
            public Vector3 position,puppetPosition,puppetScale,velocity;
            public Quaternion rotation,puppetRotation;
            public float speed,stalled,lastDistance;
            public List<Vector3> path;
            public int point,index;
        }
        readonly List<Dancer> dancers=new List<Dancer>();
        readonly List<Vector3> marchRoute=new List<Vector3>();
        readonly List<float> marchLengths=new List<float>();
        readonly Dictionary<Behaviour,bool> paused=new Dictionary<Behaviour,bool>();
        readonly Dictionary<Canvas,bool> canvases=new Dictionary<Canvas,bool>();
        readonly Dictionary<Renderer,bool> hidden=new Dictionary<Renderer,bool>();
        readonly Dictionary<GameObject,bool> decorations=new Dictionary<GameObject,bool>();
        readonly Dictionary<FactoryWorkerAppearance,FactoryFaceState> faces=new Dictionary<FactoryWorkerAppearance,FactoryFaceState>();
        static readonly Collider[] shapes=new Collider[96];
        CardboardRunner[] performers=Array.Empty<CardboardRunner>();
        CardboardFactoryGame game;
        FactoryCaptureNavigation navigation;
        Camera lens;
        CardboardCamera rig;
        Transform oldCameraPlayer;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        float cameraFov,timeScale,danceStarted,approachLength,marchDistance,marchSpeed,entryAngle;
        bool audioPaused,beganInMenu,playerBodyEnabled,drawing;
        CharacterController playerBody;
        void Awake(){game=GetComponent<CardboardFactoryGame>();}
        void Update()
        {
            if(SessionOnly)return;
            var keyboard=Keyboard.current;
            if(keyboard!=null && keyboard.f8Key.wasPressedThisFrame){if(Active)Stop();else Begin();}
            else if(keyboard!=null && keyboard.f9Key.wasPressedThisFrame && Active)Restart();
            if(!Active || game.MenuActive)return;
            Tick(Time.unscaledDeltaTime);
            if(keyboard!=null)
            {
                Vector3 direction=lens.transform.right*((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0))+
                    lens.transform.forward*((keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0))+
                    Vector3.up*((keyboard.eKey.isPressed?1:0)-(keyboard.qKey.isPressed?1:0));
                lens.transform.position+=Vector3.ClampMagnitude(direction,1)*(keyboard.leftShiftKey.isPressed?7:3)*Time.unscaledDeltaTime;
            }
        }
        public bool Restart()
        {
            bool menu=beganInMenu;Stop(false);
            bool started=Begin();if(started)beganInMenu=menu;return started;
        }
        public bool Begin()=>Begin(false);
        public bool BeginSession()=>Begin(true);
        bool Begin(bool sessionOnly)
        {
            if(Active || !game || !CardboardRunner.Player || !Camera.main || game.Ended || game.CaptureActive ||
                (game.ending && game.ending.Phase!=FactoryEndingPhase.None))return false;
            performers=game.crowd.runners.Where(w=>w && !w.isPlayer && w.gameObject.activeInHierarchy).ToArray();
            Physics.SyncTransforms();LastFailure="";
            if(performers.Length<2){LastFailure="Not enough workers";return false;}
            if(!sessionOnly)
            {
                if(!ChooseCircle())return false;
                navigation=new FactoryCaptureNavigation(.35f);
                if(!BuildMarchRoute())return false;
            }
            beganInMenu=game.MenuActive;
            if(beganInMenu)game.mainMenu.CloseForFilming();
            lens=Camera.main;rig=lens.GetComponent<CardboardCamera>();
            cameraPosition=lens.transform.position;cameraRotation=lens.transform.rotation;cameraFov=lens.fieldOfView;
            oldCameraPlayer=rig.player;timeScale=Time.timeScale;audioPaused=AudioListener.pause;drawing=CardboardMarker.DrawingMode;
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(false);
            Time.timeScale=0;AudioListener.pause=true;Active=true;SessionOnly=sessionOnly;InCircle=ColumnReady=false;Elapsed=danceStarted=marchSpeed=0;
            playerBody=CardboardRunner.Player.GetComponent<CharacterController>();playerBodyEnabled=playerBody && playerBody.enabled;
            if(playerBody)playerBody.enabled=false;
            Pause(CardboardRunner.Player);Pause(game.intro);Pause(game.expressions);
            if(game.lunch){Pause(game.lunch);Pause(game.lunch.diners);}
            Pause(game.workbench);
            foreach(var station in game.stations)Pause(station);
            foreach(var canvas in game.GetComponentsInChildren<Canvas>(true)){canvases[canvas]=canvas.enabled;canvas.enabled=false;}
            foreach(var hud in game.GetComponentsInChildren<FactoryHUD>(true))Pause(hud);
            foreach(var inventory in lens.GetComponents<FactoryInventorySlot>())Pause(inventory);
            foreach(var renderer in lens.GetComponentsInChildren<Renderer>(true))Hide(renderer);
            if(game.objectCarry && game.objectCarry.Held)
                foreach(var renderer in game.objectCarry.Held.GetComponentsInChildren<Renderer>(true))Hide(renderer);
            if(game.lunch && game.lunch.Carrying)
                foreach(var renderer in game.lunch.tray.GetComponentsInChildren<Renderer>(true))Hide(renderer);
            foreach(var line in game.GetComponentsInChildren<LineRenderer>(true))Hide(line);
            foreach(var appearance in game.expressions.people)
            {if(!appearance)continue;faces[appearance]=appearance.State;appearance.SetState(FactoryFaceState.Normal);}
            foreach(var impact in game.GetComponentsInChildren<FactoryWorkerImpact>(true))
                if(impact.message){decorations[impact.message.gameObject]=impact.message.gameObject.activeSelf;impact.message.gameObject.SetActive(false);}
            // Assign the nearest people along the common route to successive places in the column.
            if(!sessionOnly)performers=performers.OrderByDescending(w=>NearestMarchDistance(w.transform.position)).ToArray();
            marchDistance=approachLength;
            foreach(var actor in performers)
            {
                int i=dancers.Count;
                var dancer=new Dancer{actor=actor,body=actor.GetComponent<CharacterController>(),position=actor.transform.position,
                    rotation=actor.transform.rotation,speed=actor.speed,index=i,puppetPosition=actor.puppet.localPosition,
                    puppetRotation=actor.puppet.localRotation,puppetScale=actor.puppet.localScale,lastDistance=float.PositiveInfinity};
                if(!sessionOnly)dancer.path=navigation.Path(actor.transform.position,ColumnSlot(i));dancers.Add(dancer);Pause(actor);
            }
            rig.player=null;
            if(!sessionOnly)
            {
            var shot=game.transform.InverseTransformPoint(Center)+new Vector3(0,4.5f,-6.5f);
            shot.z=Mathf.Max(CardboardFactoryWorld.WorkshopSouth+.9f,shot.z);
            Vector3 eye=game.transform.TransformPoint(shot);
            lens.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(Center+Vector3.up*.9f-eye));
            float actorRadius=.6f,actorHeight=1.8f;
            foreach(var actor in performers)
            foreach(var renderer in actor.puppet.GetComponentsInChildren<Renderer>())
            {
                var relative=renderer.bounds.center-actor.transform.position;
                actorRadius=Mathf.Max(actorRadius,new Vector2(relative.x,relative.z).magnitude+
                    new Vector2(renderer.bounds.extents.x,renderer.bounds.extents.z).magnitude);
                actorHeight=Mathf.Max(actorHeight,renderer.bounds.max.y-actor.transform.position.y);
            }
            float portraitAspect=Mathf.Min(lens.aspect,9f/16),needed=.5f;
            for(int sample=0;sample<64;sample++)
            foreach(float height in new[]{0,actorHeight+hopHeight+.2f})
            {
                float angle=sample*Mathf.PI*2/64;
                var world=Center+new Vector3(Mathf.Sin(angle)*(Radius+actorRadius+.1f),height,Mathf.Cos(angle)*(Radius+actorRadius+.1f));
                var local=lens.transform.InverseTransformPoint(world);float depth=Mathf.Max(.1f,local.z);
                needed=Mathf.Max(needed,Mathf.Abs(local.x)/(depth*portraitAspect*.9f),Mathf.Abs(local.y)/(depth*.9f));
            }
            lens.fieldOfView=Mathf.Clamp(2*Mathf.Atan(needed)*Mathf.Rad2Deg,60,110);rig.AdoptCurrentView();
            }
            Cursor.lockState=CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(false);
            Physics.SyncTransforms();return true;
        }
        bool ChooseCircle()
        {
            var middle=new Vector3(CardboardFactoryWorld.WorkshopCenterX,0,0);
            var offsets=new List<Vector3>();
            for(int z=-2;z<=2;z++)for(int x=-2;x<=2;x++)offsets.Add(new Vector3(x,0,z));
            foreach(var offset in offsets.OrderBy(p=>p.sqrMagnitude))
            foreach(float radius in new[]{circleRadius,circleRadius-.35f,circleRadius-.7f,2.4f,circleRadius+.35f})
            {
                Center=game.transform.TransformPoint(middle+offset);Radius=Mathf.Max(2.4f,radius);
                bool clear=true;
                for(int i=0;i<48 && clear;i++)clear=Clear(Slot(i*Mathf.PI*2/48));
                if(clear)return true;LastFailure+=Center+" r="+Radius+" "+obstacle+"; ";
            }
            return false;
        }
        bool BuildMarchRoute()
        {
            marchRoute.Clear();marchLengths.Clear();ColumnSpacing=Radius*Mathf.PI*2/performers.Length;
            float required=(performers.Length-1)*ColumnSpacing,best=float.PositiveInfinity;
            // Keep the incoming file outside the ring so it never crosses the people already entering it.
            var excluded=new List<Vector3>();
            for(float z=-Radius-.4f;z<=Radius+.4f;z+=.6f)
            for(float x=-Radius-.4f;x<=Radius+.4f;x+=.6f)
                if(x*x+z*z<(Radius+.35f)*(Radius+.35f))excluded.Add(Center+new Vector3(x,0,z));
            var outside=navigation.Avoiding(excluded.ToArray(),.35f);
            for(int entry=0;entry<12;entry++)
            {
                float angle=entry*Mathf.PI*2/12;var radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                var gate=Slot(angle);var outer=gate+radial*1.6f;bool clear=true;
                for(int sample=0;sample<=8 && clear;sample++)clear=Clear(Vector3.Lerp(gate,outer,sample/8f));
                if(!clear)continue;
                foreach(float x in new[]{CardboardFactoryWorld.WorkshopWest+2,CardboardFactoryWorld.WorkshopEast-2})
                foreach(float z in new[]{CardboardFactoryWorld.WorkshopSouth+2,0,CardboardFactoryWorld.WorkshopNorth-4})
                {
                    var from=game.transform.TransformPoint(new Vector3(x,0,z));if(!Clear(from))continue;
                    var points=outside.Path(from,outer);
                    if(points.Count==0 || Vector3.Distance(points[points.Count-1],outer)>.3f)continue;
                    points.Insert(0,from);
                    var heading=-radial;
                    for(int p=points.Count-1;p>=0;p--)if(Vector3.Distance(points[p],outer)>.1f){heading=(outer-points[p]).normalized;break;}
                    points.Add(outer);
                    var control1=outer+heading*.45f;var control2=gate-Vector3.Cross(Vector3.up,radial)*.8f;
                    bool smoothEntry=true;
                    for(int sample=1;sample<=20;sample++)
                    {
                        float t=sample/20f,u=1-t;
                        var point=u*u*u*outer+3*u*u*t*control1+3*u*t*t*control2+t*t*t*gate;
                        if(!Clear(point)){smoothEntry=false;break;}points.Add(point);
                    }
                    if(!smoothEntry)continue;
                    var lengths=new List<float>{0};
                    for(int i=1;i<points.Count;i++)lengths.Add(lengths[i-1]+Vector3.Distance(points[i-1],points[i]));
                    float length=lengths[lengths.Count-1];if(length<required+.4f || length>=best)continue;
                    // A bend must leave physical room between successive places, including the tail.
                    bool spaced=true;
                    for(int i=0;i<performers.Length && spaced;i++)for(int j=i+1;j<performers.Length;j++)
                        if(Vector3.Distance(PointOnRoute(points,lengths,length-i*ColumnSpacing),PointOnRoute(points,lengths,length-j*ColumnSpacing))<1.05f){spaced=false;break;}
                    if(!spaced)continue;
                    best=length;approachLength=length;entryAngle=angle;
                    marchRoute.Clear();marchRoute.AddRange(points);marchLengths.Clear();marchLengths.AddRange(lengths);
                }
            }
            if(marchRoute.Count==0){LastFailure="No clear route for the marching column";return false;}
            // The shared distance continues around the ring, without stopping or changing position targets.
            return true;
        }
        static Vector3 PointOnRoute(List<Vector3> points,List<float> lengths,float distance)
        {
            distance=Mathf.Clamp(distance,0,lengths[lengths.Count-1]);
            for(int i=1;i<points.Count;i++)if(distance<=lengths[i])
                return Vector3.Lerp(points[i-1],points[i],Mathf.InverseLerp(lengths[i-1],lengths[i],distance));
            return points[points.Count-1];
        }
        Vector3 MarchPoint(float distance)=>distance>=approachLength?Slot(entryAngle+(distance-approachLength)/Radius):
            PointOnRoute(marchRoute,marchLengths,distance);
        Vector3 ColumnSlot(int index)=>MarchPoint(approachLength-index*ColumnSpacing);
        float NearestMarchDistance(Vector3 position)
        {
            float nearest=float.PositiveInfinity,along=0;
            for(int i=1;i<marchRoute.Count && marchLengths[i-1]<approachLength;i++)
            {
                var delta=marchRoute[i]-marchRoute[i-1];float t=Mathf.Clamp01(Vector3.Dot(position-marchRoute[i-1],delta)/Mathf.Max(.0001f,delta.sqrMagnitude));
                float distance=(position-Vector3.Lerp(marchRoute[i-1],marchRoute[i],t)).sqrMagnitude;
                if(distance<nearest){nearest=distance;along=Mathf.Lerp(marchLengths[i-1],marchLengths[i],t);}
            }
            return along;
        }
        static bool Clear(Vector3 position)
        {
            if(!Physics.Raycast(position+Vector3.up*.3f,Vector3.down,.8f,1<<8,QueryTriggerInteraction.Ignore)){obstacle="no floor "+position;return false;}
            int count=Physics.OverlapCapsuleNonAlloc(position+Vector3.up*.48f,position+Vector3.up*1.65f,.35f,shapes,(1<<8)|1,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
                if(!shapes[i].GetComponentInParent<CardboardRunner>() && !FactoryDoor.IsLeaf(shapes[i])){obstacle=shapes[i].name+" at "+position+" "+shapes[i].bounds;return false;}
            return true;
        }
        Vector3 Slot(float angle)=>Center+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*Radius;
        void Pause(Behaviour component)
        {
            if(component && !paused.ContainsKey(component)){paused[component]=component.enabled;component.enabled=false;}
        }
        void Hide(Renderer renderer)
        {
            if(renderer && !hidden.ContainsKey(renderer)){hidden[renderer]=renderer.enabled;renderer.enabled=false;}
        }
        public void Tick(float dt)
        {
            if(!Active || SessionOnly || game.MenuActive || dt<=0)return;
            dt=Mathf.Min(dt,.25f);int steps=Mathf.Max(1,Mathf.CeilToInt(dt/.02f));float step=dt/steps;
            for(int i=0;i<steps;i++)Step(step);
        }
        void Step(float dt)
        {
            Elapsed+=dt;
            if(!ColumnReady && dancers.All(d=>Vector3.Distance(d.actor.transform.position,ColumnSlot(d.index))<.23f))
            {ColumnReady=true;danceStarted=Elapsed;}
            float closingDistance=approachLength+(dancers.Count-1)*ColumnSpacing;
            if(ColumnReady)
            {
                float error=dancers.Max(d=>Vector3.Distance(d.actor.transform.position,MarchPoint(marchDistance-d.index*ColumnSpacing)));
                float circleBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(closingDistance-ColumnSpacing*2,closingDistance,marchDistance));
                float catchup=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.85f,.5f,error));
                float wantedSpeed=Mathf.Lerp(approachSpeed,Radius*orbitDegreesPerSecond*Mathf.Deg2Rad,circleBlend)*catchup;
                marchSpeed=Mathf.Lerp(marchSpeed,wantedSpeed,1-Mathf.Exp(-5*dt));
                marchDistance+=marchSpeed*dt;
                if(!InCircle && marchDistance>=closingDistance && GatheredCount==dancers.Count)InCircle=true;
            }
            // One beat runs from the first hop in the column through every subsequent lap.
            float beat=DancingTime*Mathf.Clamp(beatsPerMinute,60,200)/60;
            float phase=beat-Mathf.Floor(beat);
            float jump=4*hopHeight*phase*(1-phase);
            float twist=turnAngle*Mathf.Sin(Mathf.PI*(beat-.5f));
            float danceBlend=ColumnReady?Mathf.SmoothStep(0,1,DancingTime/.18f):0;
            foreach(var dancer in dancers)
            {
                float along=marchDistance-dancer.index*ColumnSpacing;
                Vector3 goal=ColumnReady?MarchPoint(along):ColumnSlot(dancer.index);
                Vector3 target=goal;
                if(!ColumnReady && dancer.path!=null && dancer.path.Count>0)
                {
                    while(dancer.point<dancer.path.Count-1 && Vector3.Distance(dancer.actor.transform.position,dancer.path[dancer.point])<.28f)dancer.point++;
                    target=dancer.path[dancer.point];
                    if(dancer.point==dancer.path.Count-1 && Vector3.Distance(dancer.actor.transform.position,target)<.4f)target=goal;
                }
                float distance=Vector3.Distance(dancer.actor.transform.position,goal);
                dancer.stalled=distance>dancer.lastDistance-.005f && distance>.4f?dancer.stalled+dt:0;dancer.lastDistance=distance;
                if(!ColumnReady && dancer.stalled>2)
                {
                    var others=dancers.Where(d=>d!=dancer).Select(d=>d.actor.transform.position).ToArray();
                    var path=navigation.Avoiding(others).Path(dancer.actor.transform.position,goal);
                    if(path.Count>0){dancer.path=path;dancer.point=0;}
                    dancer.stalled=0;
                }
                Vector3 wish=Vector3.ClampMagnitude((target-dancer.actor.transform.position)*(ColumnReady?10:6),approachSpeed);
                if(!ColumnReady)
                    foreach(var other in dancers)
                    {
                        if(other==dancer)continue;var away=dancer.actor.transform.position-other.actor.transform.position;away.y=0;
                        float length=away.magnitude;if(length>.001f && length<.8f)wish+=away/length*(.8f-length)*4;
                    }
                dancer.velocity=Vector3.Lerp(dancer.velocity,Vector3.ClampMagnitude(wish,approachSpeed),1-Mathf.Exp(-12*dt));
                var movement=dancer.velocity*dt;
                if(dancer.body && dancer.body.enabled)dancer.body.Move(movement);else dancer.actor.transform.position+=movement;
                var position=dancer.actor.transform.position;position.y=dancer.position.y;dancer.actor.transform.position=position;
                Vector3 facing=dancer.velocity;facing.y=0;
                if(facing.sqrMagnitude>.001f || ColumnReady)
                {
                    if(ColumnReady){facing=MarchPoint(along+.15f)-MarchPoint(along-.15f);facing.y=0;}
                    var heading=facing.sqrMagnitude>.001f?Quaternion.LookRotation(facing):dancer.actor.transform.rotation;
                    if(ColumnReady)
                    {
                        var inward=Center-position;inward.y=0;
                        float ringBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(approachLength-.2f,approachLength+ColumnSpacing,along));
                        if(inward.sqrMagnitude>.001f)heading=Quaternion.Slerp(heading,Quaternion.LookRotation(inward),ringBlend);
                    }
                    var rotation=heading*Quaternion.Euler(0,twist*danceBlend,0);
                    dancer.actor.transform.rotation=Quaternion.Slerp(dancer.actor.transform.rotation,rotation,1-Mathf.Exp(-16*dt));
                }
                float walking=Mathf.Clamp01(dancer.velocity.magnitude)*Mathf.Sin(Elapsed*9);
                dancer.actor.puppet.localPosition=dancer.puppetPosition+Vector3.up*Mathf.Lerp(Mathf.Abs(walking)*.025f,jump,danceBlend);
                dancer.actor.puppet.localRotation=dancer.puppetRotation*Quaternion.Slerp(Quaternion.Euler(0,0,walking*3),
                    Quaternion.Euler(-5*Mathf.Sin(Mathf.PI*phase),0,6*Mathf.Sin(Mathf.PI*beat)),danceBlend);
                dancer.actor.speed=dancer.velocity.magnitude;
            }
            Physics.SyncTransforms();
        }
        public string Describe(CardboardRunner actor)
        {
            var dancer=dancers.FirstOrDefault(d=>d.actor==actor);
            return dancer==null?"not dancing":"position="+actor.transform.position+" column="+ColumnReady+" march="+marchDistance+" goal="+(ColumnReady?MarchPoint(marchDistance-dancer.index*ColumnSpacing):ColumnSlot(dancer.index))+" path="+dancer.point+"/"+(dancer.path==null?0:dancer.path.Count);
        }
        public void Stop(bool restoreMenu=true)
        {
            if(!Active)return;
            bool menu=restoreMenu && (beganInMenu || game.MenuActive);
            if(game.MenuActive)game.mainMenu.CloseForFilming();
            Active=false;SessionOnly=false;
            foreach(var dancer in dancers)
            {
                if(!dancer.actor)continue;
                bool enabled=dancer.body && dancer.body.enabled;if(dancer.body)dancer.body.enabled=false;
                dancer.actor.transform.SetPositionAndRotation(dancer.position,dancer.rotation);dancer.actor.speed=dancer.speed;
                dancer.actor.puppet.localPosition=dancer.puppetPosition;dancer.actor.puppet.localRotation=dancer.puppetRotation;dancer.actor.puppet.localScale=dancer.puppetScale;
                if(dancer.body)dancer.body.enabled=enabled;
            }
            if(playerBody)playerBody.enabled=playerBodyEnabled;
            if(rig){rig.player=oldCameraPlayer;lens.transform.SetPositionAndRotation(cameraPosition,cameraRotation);lens.fieldOfView=cameraFov;rig.AdoptCurrentView();}
            foreach(var entry in paused)if(entry.Key)entry.Key.enabled=entry.Value;
            foreach(var entry in canvases)if(entry.Key)entry.Key.enabled=entry.Value;
            foreach(var entry in hidden)if(entry.Key)entry.Key.enabled=entry.Value;
            foreach(var entry in decorations)if(entry.Key)entry.Key.SetActive(entry.Value);
            foreach(var entry in faces)if(entry.Key)entry.Key.SetState(entry.Value);
            paused.Clear();canvases.Clear();hidden.Clear();decorations.Clear();faces.Clear();dancers.Clear();performers=Array.Empty<CardboardRunner>();
            Time.timeScale=timeScale;AudioListener.pause=audioPaused;
            if(CardboardMarker.Instance)CardboardMarker.Instance.SetDrawingMode(drawing);
            Cursor.lockState=drawing?CursorLockMode.None:CursorLockMode.Locked;FactoryVolumeCursor.SetSystemVisible(drawing);
            if(menu)game.mainMenu.Show();
            Physics.SyncTransforms();
        }
        void OnDisable(){Stop(false);}
    }
}